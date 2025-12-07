using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Takes a client-facing AgentRequestEnvelope (browser/CLI/thick client),
    /// performs light validation, maps it into NewAgentExecutionSession or
    /// AgentExecutionRequest, and delegates to the AgentOrchestrator.
    ///
    /// This class is intentionally thin so that:
    /// - Controllers have a single entry point regardless of client type.
    /// - The orchestrator only sees domain models, not transport DTOs.
    /// - Future client-specific response shaping can be added here without
    ///   impacting the orchestration pipeline.
    /// </summary>
    public class AgentRequestHandler : IAgentRequestHandler
    {
        private readonly IAgentOrchestrator _orchestrator;
        private readonly IAdminLogger _adminLogger;
        private readonly IServerToolSchemaProvider _serverToolSchemaProvider;
        private readonly IAgentModeCatalogService _agentModeCatalogService;

        public AgentRequestHandler(IAgentOrchestrator orchestrator, IAdminLogger adminLogger, IServerToolSchemaProvider serverToolSchemaProvider, IAgentModeCatalogService agentModeCatalogService)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _serverToolSchemaProvider = serverToolSchemaProvider ?? throw new ArgumentNullException(nameof(serverToolSchemaProvider));
            _agentModeCatalogService = agentModeCatalogService ?? throw new ArgumentNullException(nameof(agentModeCatalogService));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> HandleAsync(AgentExecuteRequest request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToId();

            _adminLogger.Trace("[AgentRequestHandler_HandleAsync] Handling agent request. " + $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}, sessionId={request?.ConversationId ?? "<null>"}");

            if (request == null)
            {
                const string msg = "AgentRequestEnvelope cannot be null.";
                _adminLogger.AddError("[AgentRequestHandler_HandleAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_REQ_NULL_REQUEST");
            }

            if (string.IsNullOrWhiteSpace(request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentRequestHandler_HandleAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_REQ_MISSING_INSTRUCTION");
            }

            var isNewSession = string.IsNullOrWhiteSpace(request.ConversationId);

            if (isNewSession)
            {
                return await HandleNewSessionAsync(request, org, user, correlationId, cancellationToken);
            }

            return await HandleFollowupTurnAsync(request, org, user, correlationId, cancellationToken);
        }

        private void MergeServerTools(AgentExecuteRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            //
            // 1) Normalize mode – defensive fallback to "general"
            //
            if (string.IsNullOrWhiteSpace(request.Mode))
            {
                request.Mode = "general";
            }

            //
            // 2) Start from any client-provided tools
            //
            var clientToolsJson = request.ToolsJson;

            var clientToolsArray = string.IsNullOrWhiteSpace(clientToolsJson)
                ? new Newtonsoft.Json.Linq.JArray()
                : Newtonsoft.Json.Linq.JArray.Parse(clientToolsJson);

            var merged = new Newtonsoft.Json.Linq.JArray(clientToolsArray);

            //
            // 3) Ask the mode catalog which server tools should be available
            //    for this mode / agent / workspace.
            //
            //    Interface is illustrative – adapt to your actual IAgentModeCatalogService.
            //
            var allowedServerToolNames = _agentModeCatalogService.GetToolsForMode(request.Mode);

            //
            // 4) Get full schemas for those tools from the schema provider.
            //    Again, adapt the helper method name/signature to your actual implementation.
            //
            var serverTools = _serverToolSchemaProvider.GetToolSchemas(allowedServerToolNames);

            foreach (var srv in serverTools)
            {
                merged.Add(Newtonsoft.Json.Linq.JObject.FromObject(srv));
            }

            //
            // 5) Ensure agent_change_mode is always present (TUL-007).
            //
            var hasChangeMode = false;

            foreach (var token in merged)
            {
                if (token is Newtonsoft.Json.Linq.JObject obj)
                {
                    var nameProp = obj["name"] ?? obj["function"]?["name"];
                    var toolName = nameProp?.ToString();

                    if (!string.IsNullOrWhiteSpace(toolName) &&
                        string.Equals(toolName, "agent_change_mode", StringComparison.OrdinalIgnoreCase))
                    {
                        hasChangeMode = true;
                        break;
                    }
                }
            }

            if (!hasChangeMode)
            {
                var changeModeSchema = _serverToolSchemaProvider.GetToolSchema("agent_change_mode");
                if (changeModeSchema != null)
                {
                    merged.Add(Newtonsoft.Json.Linq.JObject.FromObject(changeModeSchema));
                }
            }

            //
            // 6) Serialize back to a compact JSON string
            //
            request.ToolsJson = merged.ToString(Newtonsoft.Json.Formatting.None);
        }



        private async Task<InvokeResult<AgentExecuteResponse>> HandleNewSessionAsync(AgentExecuteRequest request, EntityHeader org, EntityHeader user, string correlationId, CancellationToken cancellationToken)
        {
            _adminLogger.Trace("[AgentRequestHandler_HandleNewSessionAsync] Normalizing new session request. " + $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}");

            if (request.AgentContext == null || EntityHeader.IsNullOrEmpty(request.AgentContext))
            {
                const string msg = "AgentContext is required for a new session.";
                _adminLogger.AddError("[AgentRequestHandler_HandleNewSessionAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_REQ_MISSING_AGENT_CONTEXT");
            }

            MergeServerTools(request);

            return await _orchestrator.BeginNewSessionAsync(request, org, user, cancellationToken);
        }

        private async Task<InvokeResult<AgentExecuteResponse>> HandleFollowupTurnAsync(AgentExecuteRequest request, EntityHeader org, EntityHeader user, string correlationId, CancellationToken cancellationToken)
        {
            _adminLogger.Trace("[AgentRequestHandler_HandleFollowupTurnAsync] Normalizing follow-up turn request. " + $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}, conversationId={request.ConversationId}");

            if (string.IsNullOrWhiteSpace(request.ConversationId))
            {
                const string msg = "ConversationId is required for a follow-up turn.";
                _adminLogger.AddError("[AgentRequestHandler_HandleFollowupTurnAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_REQ_MISSING_SESSION_ID");
            }

            MergeServerTools(request);

            return await _orchestrator.ExecuteTurnAsync(request, org, user, cancellationToken);
        }
    }
}
