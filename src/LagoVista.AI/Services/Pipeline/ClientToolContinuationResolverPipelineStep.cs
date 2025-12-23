using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Pipeline
{
    /// <summary>
    /// AGN-032 Step: ClientToolContinuationResolver
    ///
    /// Expects:
    /// - ctx.Session and ctx.Turn are present.
    /// - ctx.Type == AgentPipelineContextTypes.ClientToolCallContinuation.
    /// - ctx.Request contains client tool results (ToolResults) and any continuation identifiers.
    ///
    /// Updates:
    /// - Restores/normalizes client tool continuation state into the shape expected by downstream
    ///   execution (AgentReasoner / LLM loop).
    ///
    /// Next:
    /// - AgentReasonerPipelineStep
    /// </summary>
    public sealed class ClientToolContinuationResolverPipelineStep : IClientToolContinuationResolverPipelineStep
    {
        private readonly IAgentReasonerPipelineStep _next;
        private readonly IAdminLogger _adminLogger;

        public ClientToolContinuationResolverPipelineStep(
            IAgentReasonerPipelineStep next,
            IAdminLogger adminLogger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentPipelineContext cannot be null.",
                    "CLIENT_TOOL_CONTINUATION_NULL_CONTEXT");
            }

            if (ctx.Type != AgentPipelineContextTypes.ClientToolCallContinuation)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Client tool continuation resolver requires ctx.Type == ClientToolCallContinuation.",
                    "CLIENT_TOOL_CONTINUATION_INVALID_CONTEXT_TYPE");
            }

            if (ctx.Request == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "AgentExecuteRequest is required.",
                    "CLIENT_TOOL_CONTINUATION_MISSING_REQUEST");
            }

            if (ctx.Session == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Session is required.",
                    "CLIENT_TOOL_CONTINUATION_MISSING_SESSION");
            }

            if (ctx.Turn == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError(
                    "Turn is required.",
                    "CLIENT_TOOL_CONTINUATION_MISSING_TURN");
            }

            _adminLogger.Trace("[ClientToolContinuationResolverPipelineStep__ExecuteAsync] - Preparing client tool continuation state.",
                (ctx.CorrelationId ?? string.Empty).ToKVP("CorrelationId"),
                (ctx.Session?.Id ?? string.Empty).ToKVP("SessionId"),
                (ctx.Turn?.Id ?? string.Empty).ToKVP("TurnId"));

            // TODO (meat later):
            // - Normalize ctx.Request.ToolResults / ToolResultsJson into the canonical shape used by the reasoner.
            // - Ensure continuation identifiers are present/consistent (PreviousTurnId, CurrentTurnId, ResponseContinuationId).
            // - Populate any turn-level "pending tool" state needed by downstream loop.

            return await _next.ExecuteAsync(ctx);
        }
    }
}
