using LagoVista.AI.Helpers;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Helper
{
    public abstract class UpdateEntityFieldToolBase<TEntity> : IAgentTool where TEntity : EntityBase
    {
        public abstract string Name { get; }
        public bool IsToolFullyExecutedOnServer => true;

        protected sealed class UpdateFieldArgs
        {
            public string Id { get; set; }
            public string Field { get; set; }
            public JToken Value { get; set; }

            // Optional list ops
            public string Op { get; set; }     // replace/add/update/remove
            public string ItemId { get; set; } // for update/remove
        }

        protected sealed class UpdateFieldResult
        {
            public string Id { get; set; }
            public string Field { get; set; }
            public bool Updated { get; set; }
            public string SessionId { get; set; }
            public string Op { get; set; }
            public string ItemId { get; set; }
        }


        private readonly IAdminLogger _adminlogger;

        public UpdateEntityFieldToolBase(IAdminLogger adminlogger)
        {
            _adminlogger = adminlogger ?? throw new ArgumentNullException(nameof(adminlogger));
        }

        /// <summary>
        /// Override to log exceptions.
        /// </summary>
        protected virtual Task OnExceptionAsync(Exception ex, IAgentPipelineContext context)
        {
            _adminlogger.AddException($"[{nameof(UpdateEntityFieldToolBase<TEntity>)}__{typeof(TEntity).Name}__GetAsync]", ex);
            return Task.CompletedTask;
        }
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException();


        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return InvokeResult<string>.FromError($"{Name} requires a non-empty arguments object.");

            UpdateFieldArgs args;
            try
            {
                args = JsonConvert.DeserializeObject<UpdateFieldArgs>(argumentsJson) ?? new UpdateFieldArgs();
                args.Value = NormalizeValueToken(args.Value);
            }
            catch
            {
                return InvokeResult<string>.FromError($"{Name} could not deserialize arguments.");
            }

            var validate = ValidateArgs(args, context);
            if (!validate.Successful)
                return InvokeResult<string>.FromError(validate.Errors?.FirstOrDefault()?.Message ?? $"{Name} invalid arguments.");

            var propName = "Unknown";

            try
            {
                var entity = await GetEntityAsync(args.Id, context);
                if (entity == null)
                    return InvokeResult<string>.FromError($"{Name} could not find entity '{args.Id}'.");

                var prop = ResolveProperty(args.Field);
                if (prop == null || !prop.CanWrite)
                    return InvokeResult<string>.FromError($"{Name} could not find a writable property '{args.Field}'.");

                propName = prop.Name;

                if (!IsFieldAllowed(prop))
                    return InvokeResult<string>.FromError($"{Name} does not allow updating field '{prop.Name}'.");

                ApplyUpdate(entity, prop, args);

                if(entity is IValidateable validatable)
                {
                    var results = Validator.Validate(validatable, Actions.Update);
                    entity.IsDraft = results.Errors.Any();
                }

                var existingSession = entity.AISessions.FirstOrDefault(session => session.Id == context.Session.Id);
                if (existingSession == null)
                {
                    entity.AISessions.Add(context.Session.ToEntityHeader());
                }

                var save = await SaveEntityAsync(entity, context);
                if (!save.Successful)
                    return InvokeResult<string>.FromError($"{Name} failed to persist the updated entity.");

                var payload = new UpdateFieldResult
                {
                    Id = args.Id,
                    Field = prop.Name,
                    Updated = true,
                    SessionId = context.Envelope.SessionId,
                    Op = args.Op,
                    ItemId = args.ItemId
                };

                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                await OnExceptionAsync(ex, context);
                _adminlogger.AddError($"[UpdateEntityFieldToolBase__{Name}__ExecuteAsync]", "failed to {args.Op} update {propName} to value {args.Value}.", args.Field.ToKVP("field"), args.Op.ToKVP("operation"), args.Value.ToString().ToKVP("value") );
                return InvokeResult<string>.FromError($"{Name} failed to {args.Op} update {propName} to value {args.Value}.");
            }
        }

        private static string NormalizeFieldName(string field)
        {
            if (string.IsNullOrWhiteSpace(field)) return field;

            // riskTolerance -> RiskTolerance
            return char.ToUpperInvariant(field[0]) + field.Substring(1);
        }

        protected virtual InvokeResult ValidateArgs(UpdateFieldArgs args, IAgentPipelineContext context)
        {
            if (args == null)
                return InvokeResult.FromError("Arguments are required.");

            if (string.IsNullOrWhiteSpace(args.Id))
                return InvokeResult.FromError("requires 'id'.");

            if (string.IsNullOrWhiteSpace(args.Field))
                return InvokeResult.FromError("requires 'field'.");

            if (args.Value == null)
                return InvokeResult.FromError("requires 'value' (may be empty, but not null).");

            return InvokeResult.Success;
        }

        protected virtual PropertyInfo ResolveProperty(string field)
        {
            return typeof(TEntity).GetProperty(NormalizeFieldName(field), BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        }

        /// <summary>
        /// Override to prevent updating sensitive fields (Id, OwnerOrganization, etc.)
        /// Default: allow everything writable.
        /// </summary>
        protected virtual bool IsFieldAllowed(PropertyInfo prop) => true;

        protected abstract Task<TEntity> GetEntityAsync(string id, IAgentPipelineContext context);
        protected abstract Task<InvokeResult> SaveEntityAsync(TEntity entity, IAgentPipelineContext context);

        private void ApplyUpdate(TEntity entity, PropertyInfo prop, UpdateFieldArgs args)
        {
            // List<T> path
            if (TryGetListItemType(prop.PropertyType, out var itemType))
            {
                ApplyListUpdate(entity, prop, itemType, args);
                return;
            }

            // Scalar path (includes EntityHeader<Enum> if your extension handles it)
            var typedValue = ConvertScalarValue(prop, args.Value);
            prop.SetValue(entity, typedValue);
        }

        protected virtual object ConvertScalarValue(PropertyInfo prop, JToken value)
        {
            var t = prop.PropertyType;

            if (t == typeof(string))
                return value.Type == JTokenType.Null ? null : value.ToString();
            if (t == typeof(bool))
                return value.Value<bool>();
            if (t == typeof(int))
                return value.Value<int>();
            if (t == typeof(double))
                return value.Value<double>();

            // Your extension
            if (t.IsEntityHeaderOfEnum(out _))
                return t.BuildEntityHeaderOfEnum(value, prop.DeclaringType);

            return value.ToObject(t);
        }

        private static JToken NormalizeValueToken(JToken token)
        {
            if (token == null) return null;

            // If it came in as a JSON-encoded string like "\"very-high\"" or "{\"a\":1}"
            if (token.Type == JTokenType.String)
            {
                var s = token.Value<string>()?.Trim();
                if (!string.IsNullOrEmpty(s) && (s.StartsWith("{") || s.StartsWith("[") || s.StartsWith("\"") || s == "true" || s == "false" || char.IsDigit(s[0]) || s == "null"))
                {
                    try { return JToken.Parse(s); } catch { /* leave as string */ }
                }
            }

            return token;
        }


        private void ApplyListUpdate(TEntity entity, PropertyInfo prop, Type itemType, UpdateFieldArgs args)
        {
            var op = (args.Op ?? "replace").Trim().ToLowerInvariant();

            // Get or create list
            var listObj = prop.GetValue(entity);
            if (listObj == null)
            {
                listObj = Activator.CreateInstance(prop.PropertyType);
                prop.SetValue(entity, listObj);
            }

            var list = (IList)listObj;

            if (op == "replace")
            {
                var newList = (IList)args.Value.ToObject(prop.PropertyType);
                prop.SetValue(entity, newList);
                return;
            }

            var idProp = itemType.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (idProp == null || idProp.PropertyType != typeof(string))
                throw new InvalidOperationException($"List item type '{itemType.Name}' must have a public string Id property for op '{op}'.");

            string GetId(object item) => (string)idProp.GetValue(item);

            if (op == "add")
            {
                var newItem = args.Value.ToObject(itemType);
                var id = GetId(newItem);
                if (string.IsNullOrWhiteSpace(id))
                    idProp.SetValue(newItem, Guid.NewGuid().ToString("N"));
                list.Add(newItem);
                return;
            }

            if (string.IsNullOrWhiteSpace(args.ItemId))
                throw new InvalidOperationException($"List op '{op}' requires itemId.");

            var existing = list.Cast<object>().FirstOrDefault(i => string.Equals(GetId(i), args.ItemId, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
                throw new InvalidOperationException($"Could not find item '{args.ItemId}' in '{prop.Name}'.");

            if (op == "remove")
            {
                list.Remove(existing);
                return;
            }

            if (op == "update")
            {
                // Replace whole item OR merge fields (we’ll do merge if object, replace if non-object)
                if (args.Value.Type != JTokenType.Object)
                    throw new InvalidOperationException("List op 'update' requires an object value.");

                var obj = (JObject)args.Value;

                foreach (var p in itemType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!p.CanWrite) continue;
                    if (string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase)) continue;

                    if (obj.TryGetValue(p.Name, StringComparison.OrdinalIgnoreCase, out var tok))
                    {
                        var newVal = tok.ToObject(p.PropertyType);
                        p.SetValue(existing, newVal);
                    }
                }

                return;
            }

            throw new InvalidOperationException($"Unknown list op '{args.Op}'.");
        }

        private static bool TryGetListItemType(Type t, out Type itemType)
        {
            itemType = null;
            if (t == null) return false;
            if (!t.IsGenericType) return false;
            if (t.GetGenericTypeDefinition() != typeof(System.Collections.Generic.List<>)) return false;

            itemType = t.GenericTypeArguments[0];
            return true;
        }

        public const string Op_Replace = "replace";
        public const string Op_Add = "add";
        public const string Op_Update = "update";
        public const string Op_Remove = "remove";

        public static OpenAiToolDefinition BuildSchema(string toolName)
        {
            return ToolSchema.Function(toolName, $"Update one writable field on a {typeof(TEntity).Name} using a camelCase field name. Supports list ops for List<T> fields.", p =>
            {
                p.String("id", $"{typeof(TEntity).Name} id to update.", required: true);

                p.String(
                    "field",
                    $"camelCase property name to set (e.g., name, description, notes, riskTolerance, commonObjections).",
                    required: true);

                // NOTE: ToolSchema currently exposes p.String in your sample.
                // If you have a richer "Any/Json" builder, swap to that. Otherwise keep as string and parse JSON.
                p.String(
                    "value",
                    "JSON value to assign. Pass a JSON string/number/bool/object/array as appropriate for the field.",
                    required: true);

                p.String(
                    "op",
                    $"Optional list operation for List<T> fields: '{Op_Replace}' (default), '{Op_Add}', '{Op_Update}', '{Op_Remove}'.",
                    required: false);

                p.String(
                    "itemId",
                    $"Required only when op is '{Op_Update}' or '{Op_Remove}' on a List<T> field. The child's id.",
                    required: false);
            });
        }

    }
}
