using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services
{
    public class AgentTurnExecutor : IAgentTurnExecutor
    {
        private readonly IAgentExecutionService _agentExecutionService;
        private readonly IAgentTurnTranscriptStore _transcriptStore;
        private readonly IAdminLogger _adminLogger;

        public AgentTurnExecutor(IAgentExecutionService agentExecutionService, IAgentTurnTranscriptStore transcriptStore, IAdminLogger adminLogger)
        {
            _agentExecutionService = agentExecutionService ?? throw new ArgumentNullException(nameof(agentExecutionService));
            _transcriptStore = transcriptStore ?? throw new ArgumentNullException(nameof(transcriptStore));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(
            AgentPipelineContext ctx,
            CancellationToken cancellationToken = default)
        {
            if (ctx == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "AGENT_TURN_NULL_CONTEXT");
            }

            if (ctx.AgentContext == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError("AgentContext is required.", "AGENT_TURN_MISSING_AGENT_CONTEXT");
            }

            if (ctx.Session == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError("Session is required.", "AGENT_TURN_MISSING_SESSION");
            }

            if (ctx.Turn == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError("Turn is required.", "AGENT_TURN_MISSING_TURN");
            }

            if (ctx.Request == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError("AgentExecuteRequest is required.", "AGENT_TURN_MISSING_REQUEST");
            }

            if (ctx.Org == null || EntityHeader.IsNullOrEmpty(ctx.Org))
            {
                return InvokeResult<AgentPipelineContext>.FromError("Org is required.", "AGENT_TURN_MISSING_ORG");
            }

            if (ctx.User == null || EntityHeader.IsNullOrEmpty(ctx.User))
            {
                return InvokeResult<AgentPipelineContext>.FromError("User is required.", "AGENT_TURN_MISSING_USER");
            }

            var execResult = await _agentExecutionService.ExecuteAsync(ctx.Request, ctx.Org, ctx.User, cancellationToken);
            if (!execResult.Successful)
            {
                return InvokeResult<AgentPipelineContext>.FromInvokeResult(execResult.ToInvokeResult());
            }

            var execResponse = execResult.Result;

            var isNewSessionTurn = string.IsNullOrWhiteSpace(ctx.Request.ConversationId);
            object responseEnvelope;

            if (isNewSessionTurn)
            {
                responseEnvelope = new
                {
                    OrgId = ctx.Org?.Id,
                    SessionId = ctx.Session.Id,
                    ConversationId = ctx.Session.Id,
                    TurnId = ctx.Turn.Id,
                    Response = execResponse
                };
            }
            else
            {
                responseEnvelope = new
                {
                    OrgId = ctx.Org?.Id,
                    SessionId = ctx.Session.Id,
                    ConversationId = ctx.Session.Id,
                    ResponseId = ctx.Request.ResponseContinuationId,
                    TurnId = ctx.Turn.Id,
                    Response = execResponse
                };
            }

            var responseJson = JsonConvert.SerializeObject(responseEnvelope);
            var responseBlobResult = await _transcriptStore.SaveTurnResponseAsync(ctx.Org.Id, ctx.Session.Id, ctx.Turn.Id, responseJson, cancellationToken);

            if (!responseBlobResult.Successful)
            {
                _adminLogger.AddError(
                    isNewSessionTurn
                        ? "[AgentTurnExecutor_ExecuteNewSessionTurnAsync__Transcript]"
                        : "[AgentTurnExecutor_ExecuteFollowupTurnAsync__Transcript]",
                    "Failed to store turn response transcript.");

                return InvokeResult<AgentPipelineContext>.FromInvokeResult(responseBlobResult.ToInvokeResult());
            }

            execResponse.FullResponseUrl = responseBlobResult.Result.ToString();
            execResponse.ConversationId = ctx.Session.Id;
            execResponse.TurnId = ctx.Turn.Id;

            ctx.Response = execResponse;

            return InvokeResult<AgentPipelineContext>.Create(ctx);
        }
    }
}
