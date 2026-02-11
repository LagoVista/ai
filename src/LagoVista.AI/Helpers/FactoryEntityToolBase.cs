// Abstract base class for "Get {Entity} by id" tools.
// - Standardizes args validation, load, serialization, error handling, schema.
// - Keeps derived tools tiny (just wire manager + tool name + id field name if needed).
//
// Notes:
// - Uses the same patterns as your existing tools: IAgentTool, InvokeResult<string>, ToolSchema.Function
// - Supports BOTH ExecuteAsync signatures you have in the wild:
//   1) ExecuteAsync(string, IAgentPipelineContext)
//   2) ExecuteAsync(string, AgentToolExecutionContext, CancellationToken)
//
// You can put this in a shared location, e.g. LagoVista.AI.Tools (or Billing.Managers.AiTools.Shared)

using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Models.AIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RingCentral;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Helpers
{
    public abstract class FactoryEntityToolBase<TEntity> : IAgentTool where TEntity : EntityBase, new() 
    {
        public abstract string Name { get; }
        public virtual bool IsToolFullyExecutedOnServer => true;


        /// <summary>
        /// Override if you want a different required arg name (canonical would be "id").
        /// For legacy tools you may keep "personaId", "productId", etc.
        /// </summary>
        protected static string NameArgument => "descriptiveName";

        protected static string ToolDescription => $"create a new {typeof(TEntity).Name} by id.";
        protected virtual string MissingArgsMessage => $"{Name} requires a non-empty arguments object.";
        protected virtual string MissingNameMessage => $"{Name} requires '{NameArgument}'.";
        protected virtual string NotFoundMessage(string id) => $"{Name} could not find {typeof(TEntity).Name.ToLowerInvariant()} '{id}'.";

        private readonly IAdminLogger _adminlogger;

        public FactoryEntityToolBase(IAdminLogger adminlogger)
        {
            _adminlogger = adminlogger ?? throw new ArgumentNullException(nameof(adminlogger));
        }

        /// <summary>
        /// If you want to wrap the response differently, override BuildPayloadJson.
        /// Default payload: { id, entity, success, sessionId }
        /// </summary>
        protected virtual string BuildPayloadJson(string id, AiDetailResponse<TEntity> response, TEntity entity)
        {
            var payload = new
            {
                id,
                entity = response,
                success = true,
                isDraft = entity.IsDraft,
                errorMessage = entity.IsDraft ? "Entity is in draft state due to validation errors, please review errors in validationResult for more details." : null
            };

            return JsonConvert.SerializeObject(payload);
        }

        /// <summary>
        /// Implement: how to load the entity from your domain layer.
        /// Base class will validate Org/User presence.
        /// </summary>
        protected abstract Task<InvokeResult> AddEntityAsync(TEntity id, IAgentPipelineContext context);

        /// <summary>
        /// Override to log exceptions.
        /// </summary>
        protected virtual Task OnExceptionAsync(Exception ex, IAgentPipelineContext context)
        {
            _adminlogger.AddException($"[{nameof(GetEntityToolBase<TEntity>)}__{typeof(TEntity).Name}__GetAsync]", ex);
            return Task.CompletedTask;
        }
          
        public static OpenAiToolDefinition BuildSchema(string name)
        {
            return ToolSchema.Function(name, ToolDescription, p =>
            {
                p.String(NameArgument, $"name of the {typeof(TEntity).Name} to be created.", required: true);
            });
        }
        
        // Preferred signature for server-side tools
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return InvokeResult<string>.FromError(MissingArgsMessage);

            try
            {
                JObject obj;
                try
                {
                    obj = JObject.Parse(argumentsJson);
                }
                catch
                {
                    return InvokeResult<string>.FromError($"{Name} arguments must be valid JSON.");
                }

                if (obj == null)
                    return InvokeResult<string>.FromError(MissingArgsMessage);

                string name = null;

                // 1) Canonical: "id"
                if (obj.TryGetValue(NameArgument, StringComparison.OrdinalIgnoreCase, out var nameToken))
                {
                    name = nameToken.Type == JTokenType.Null ? null : nameToken.ToString();
                }

                if (string.IsNullOrWhiteSpace(name) &&
                    obj.TryGetValue(NameArgument, StringComparison.OrdinalIgnoreCase, out var legacyIdToken))
                {
                    name = legacyIdToken.Type == JTokenType.Null ? null : legacyIdToken.ToString();
                }

                if (string.IsNullOrWhiteSpace(name))
                    return InvokeResult<string>.FromError(MissingNameMessage);

                var now = DateTime.UtcNow.ToJSONString();

                var entity = new TEntity();
                entity.Id = Guid.NewGuid().ToId();
                entity.OwnerOrganization = context.Envelope.Org;
                entity.CreatedBy = context.Envelope.User;
                entity.LastUpdatedBy = context.Envelope.User;
                entity.Name = name;
                entity.CreationDate = now;
                entity.LastUpdatedDate = now;
                entity.Key = CreateKeyFromName(name);
                entity.AISessions.Add(context.Session.ToEntityHeader());

                if (entity is IValidateable validatable)
                {
                    var results = Validator.Validate(validatable, Actions.Update);
                    entity.IsDraft = results.Errors.Any();
                }

                var createResult = await AddEntityAsync(entity, context);
                if (createResult.Successful)
                {
                    var response = AiDetailResponse<TEntity>.Create(entity);
                    var json = BuildPayloadJson(entity.Id, response, entity);
                    return InvokeResult<string>.Create(json);
                }
                else
                {
                    var failedCreatedEntity = new
                    {
                        success = false,
                        errors = createResult.Errors
                    };

                    return InvokeResult<string>.Create(JsonConvert.SerializeObject(failedCreatedEntity));
                }
            }
            catch (Exception ex)
            {
                await OnExceptionAsync(ex, context);
                return InvokeResult<string>.FromError($"{Name} failed to process arguments.");
            }
        }

        private static readonly Regex _invalidChars = new Regex("[^a-z0-9]", RegexOptions.Compiled);

        public static string CreateKeyFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required to generate a key.", nameof(name));

            // 1. lowercase
            var key = name.Trim().ToLowerInvariant();

            // 2. remove invalid characters
            key = _invalidChars.Replace(key, string.Empty);

            // 3. ensure starts with a letter
            if (key.Length == 0 || !char.IsLetter(key[0]))
                key = $"a{key}";

            // 4. enforce min length (pad with 'a')
            if (key.Length < 3)
                key = key.PadRight(3, 'a');

            // 5. enforce max length
            if (key.Length > 32)
                key = key.Substring(0, 32);

            // final safety check
            if (!char.IsLetter(key[0]))
                throw new InvalidOperationException("Generated key does not start with a letter.");

            return key;
        }
    }
}
