//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using LagoVista.AI.Interfaces;
//using LagoVista.AI.Models;
//using LagoVista.Core.Models;
//using LagoVista.Core.Validation;
//using LagoVista.IoT.Logging.Loggers;
//using Newtonsoft.Json;

//namespace LagoVista.AI.Services
//{
//    /// <summary>
//    /// Pipeline step that executes the agent (via the next execution step) and persists the turn response transcript.
//    /// Expects upstream steps to have populated:
//    /// - ctx.Session
//    /// - ctx.Turn
//    /// - ctx.AgentContext / ctx.ConversationContext (as needed by execution step)
//    /// - ctx.Request
//    /// </summary>
//    public sealed class AgentTurnExecutor : IAgentTurnExecutor
//    {
//        private readonly IAgentExecutionService _agentExecutionStep;
//        private readonly IAgentTurnTranscriptStore _transcriptStore;
//        private readonly IAdminLogger _adminLogger;

//        public AgentTurnExecutor(
//            IAgentExecutionService agentExecutionStep,
//            IAgentTurnTranscriptStore transcriptStore,
//            IAdminLogger adminLogger)
//        {
//            _agentExecutionStep = agentExecutionStep ?? throw new ArgumentNullException(nameof(agentExecutionStep));
//            _transcriptStore = transcriptStore ?? throw new ArgumentNullException(nameof(transcriptStore));
//            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
//        }

//        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
//        {
//            if (ctx == null)
//            {
//                return InvokeResult<AgentPipelineContext>.FromError(
//                    "AgentPipelineContext cannot be null.",
//                    "AGENT_TURN_NULL_CONTEXT");
//            }

//            if (ctx.Request == null)
//            {
//                return InvokeResult<AgentPipelineContext>.FromError(
//                    "AgentExecuteRequest is required.",
//                    "AGENT_TURN_MISSING_REQUEST");
//            }

//            if (ctx.Org == null || EntityHeader.IsNullOrEmpty(ctx.Org))
//            {
//                return InvokeResult<AgentPipelineContext>.FromError(
//                    "Org is required.",
//                    "AGENT_TURN_MISSING_ORG");
//            }

//            if (ctx.User == null || EntityHeader.IsNullOrEmpty(ctx.User))
//            {
//                return InvokeResult<AgentPipelineContext>.FromError(
//                    "User is required.",
//                    "AGENT_TURN_MISSING_USER");
//            }

//            if (ctx.Session == null)
//            {
//                return InvokeResult<AgentPipelineContext>.FromError(
//                    "Session is required.",
//                    "AGENT_TURN_MISSING_SESSION");
//            }

//            if (ctx.Turn == null)
//            {
//                return InvokeResult<AgentPipelineContext>.FromError(
//                    "Turn is required.",
//                    "AGENT_TURN_MISSING_TURN");
//            }

//            if (string.IsNullOrWhiteSpace(ctx.Session.Id))
//            {
//                return InvokeResult<AgentPipelineContext>.FromError(
//                    "Session.Id is required.",
//                    "AGENT_TURN_MISSING_SESSION_ID");
//            }

//            if (string.IsNullOrWhiteSpace(ctx.Turn.Id))
//            {
//                return InvokeResult<AgentPipelineContext>.FromError(
//                    "Turn.Id is required.",
//                    "AGENT_TURN_MISSING_TURN_ID");
//            }

//            // Execute the inner step that produces ctx.Response (e.g., AgentExecutionService as IAgentPipelineStep)
//            var execCtxResult = await _agentExecutionStep.ExecuteAsync(ctx);
//            if (!execCtxResult.Successful)
//            {
//                return InvokeResult<AgentPipelineContext>.FromInvokeResult(execCtxResult.ToInvokeResult());
//            }

//            var updatedCtx = execCtxResult.Result ?? ctx;

//            if (updatedCtx.Response == null)
//            {
//                return InvokeResult<AgentPipelineContext>.FromError(
//                    "Response was not set by execution step.",
//                    "AGENT_TURN_MISSING_RESPONSE");
//            }

//            var isNewSessionTurn = string.IsNullOrWhiteSpace(updatedCtx.Request.SessionId);

//            object responseEnvelope;
//            if (isNewSessionTurn)
//            {
//                responseEnvelope = new
//                {
//                    OrgId = updatedCtx.Org?.Id,
//                    SessionId = updatedCtx.Session.Id,
//                    TurnId = updatedCtx.Turn.Id,
//                    Response = updatedCtx.Response
//                };
//            }
//            else
//            {
//                responseEnvelope = new
//                {
//                    OrgId = updatedCtx.Org?.Id,
//                    SessionId = updatedCtx.Session.Id,
//                    ResponseId = updatedCtx.Request.ResponseContinuationId,
//                    TurnId = updatedCtx.Turn.Id,
//                    Response = updatedCtx.Response
//                };
//            }

//            var responseJson = JsonConvert.SerializeObject(responseEnvelope);

//            var responseBlobResult = await _transcriptStore.SaveTurnResponseAsync(
//                updatedCtx.Org.Id,
//                updatedCtx.Session.Id,
//                updatedCtx.Turn.Id,
//                responseJson,
//                ctx.CancellationToken);

//            if (!responseBlobResult.Successful)
//            {
//                _adminLogger.AddError(
//                    isNewSessionTurn
//                        ? "[AgentTurnExecutor_ExecuteNewSessionTurnAsync__Transcript]"
//                        : "[AgentTurnExecutor_ExecuteFollowupTurnAsync__Transcript]",
//                    "Failed to store turn response transcript.");

//                return InvokeResult<AgentPipelineContext>.FromInvokeResult(responseBlobResult.ToInvokeResult());
//            }

//            // Stamp response metadata
//            updatedCtx.Response.FullResponseUrl = responseBlobResult.Result.ToString();
//            updatedCtx.Response.SessionId = updatedCtx.Session.Id;
//            updatedCtx.Response.TurnId = updatedCtx.Turn.Id;

//            return InvokeResult<AgentPipelineContext>.Create(updatedCtx);
//        }
//    }
//}
