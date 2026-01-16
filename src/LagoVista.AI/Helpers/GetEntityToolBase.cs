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
using LagoVista.Core.Models;
using LagoVista.Core.Models.AIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Helpers
{
    public abstract class GetEntityToolBase<TEntity> : IAgentTool where TEntity : IEntityBase, new() 
    {
        public abstract string Name { get; }
        public virtual bool IsToolFullyExecutedOnServer => true;

        /// <summary>
        /// Override if you want a different required arg name (canonical would be "id").
        /// For legacy tools you may keep "personaId", "productId", etc.
        /// </summary>
        protected static string IdArgumentName => "id";

        protected static string ToolDescription => $"Fetch a {typeof(TEntity).Name} by id.";
        protected virtual string MissingArgsMessage => $"{Name} requires a non-empty arguments object.";
        protected virtual string MissingIdMessage => $"{Name} requires '{IdArgumentName}'.";
        protected virtual string NotFoundMessage(string id) => $"{Name} could not find {typeof(TEntity).Name.ToLowerInvariant()} '{id}'.";

        private readonly IAdminLogger _adminlogger;

        public GetEntityToolBase(IAdminLogger adminlogger)
        {
            _adminlogger = adminlogger ?? throw new ArgumentNullException(nameof(adminlogger));
        }


        /// <summary>
        /// If you want to wrap the response differently, override BuildPayloadJson.
        /// Default payload: { id, entity, success, sessionId }
        /// </summary>
        protected virtual string BuildPayloadJson(string id, AiDetailResponse<TEntity> response)
        {
            var payload = new
            {
                id,
                entity = response,
                success = true,
            };

            return JsonConvert.SerializeObject(payload);
        }

        /// <summary>
        /// Implement: how to load the entity from your domain layer.
        /// Base class will validate Org/User presence.
        /// </summary>
        protected abstract Task<TEntity> GetEntityAsync(string id, IAgentPipelineContext context);

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
                p.String(IdArgumentName, $"{typeof(TEntity).Name} id to fetch.", required: true);
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

                string id = null;

                // 1) Canonical: "id"
                if (obj.TryGetValue("id", StringComparison.OrdinalIgnoreCase, out var idToken))
                {
                    id = idToken.Type == JTokenType.Null ? null : idToken.ToString();
                }

                // 2) Tool-specific legacy id (e.g. "personaId")
                if (string.IsNullOrWhiteSpace(id) &&
                    obj.TryGetValue(IdArgumentName, StringComparison.OrdinalIgnoreCase, out var legacyIdToken))
                {
                    id = legacyIdToken.Type == JTokenType.Null ? null : legacyIdToken.ToString();
                }

                if (string.IsNullOrWhiteSpace(id))
                    return InvokeResult<string>.FromError(MissingIdMessage);

                var entity = await GetEntityAsync(id, context);
                if (entity == null)
                    return InvokeResult<string>.FromError(NotFoundMessage(id));

                var response = AiDetailResponse<TEntity>.Create(entity);

                var json = BuildPayloadJson(id, response);
                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                await OnExceptionAsync(ex, context);
                return InvokeResult<string>.FromError($"{Name} failed to process arguments.");
            }
        }
    }
}
