using System;
using System.Collections.Generic;
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

        public async Task<InvokeResult<AgentExecuteResponse>> ExecuteNewSessionTurnAsync(AgentContext agentContext, AgentSession session, AgentSessionTurn turn, AgentExecuteRequest execRequest, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            if (agentContext == null) throw new ArgumentNullException(nameof(agentContext));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (turn == null) throw new ArgumentNullException(nameof(turn));
            if (execRequest == null) throw new ArgumentNullException(nameof(execRequest));

            var execResult = await _agentExecutionService.ExecuteAsync(execRequest, org, user, cancellationToken);
            if (!execResult.Successful)
            {
                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(execResult.ToInvokeResult());
            }

            var execResponse = execResult.Result;

            var responseEnvelope = new
            {
                OrgId = org?.Id,
                SessionId = session.Id,
                TurnId = turn.Id,
                Response = execResponse
            };

            var responseJson = JsonConvert.SerializeObject(responseEnvelope);
            var responseBlobResult = await _transcriptStore.SaveTurnResponseAsync(org.Id, session.Id, turn.Id, responseJson, cancellationToken);

            if (!responseBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteNewSessionTurnAsync__Transcript]", "Failed to store turn response transcript.");
                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(responseBlobResult.ToInvokeResult());
            }

            execResult.Result.FullResponseUrl = responseBlobResult.Result.ToString();

            return execResult;
        }

        public async Task<InvokeResult<AgentExecuteResponse>> ExecuteFollowupTurnAsync(AgentContext agentContext, AgentSession session, AgentSessionTurn turn, AgentExecuteRequest execRequest, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            if (agentContext == null) throw new ArgumentNullException(nameof(agentContext));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (turn == null) throw new ArgumentNullException(nameof(turn));
            if (execRequest == null) throw new ArgumentNullException(nameof(execRequest));

            var execResult = await _agentExecutionService.ExecuteAsync(execRequest, org, user, cancellationToken);
            if (!execResult.Successful)
            {
                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(execResult.ToInvokeResult());
            }

            var execResponse = execResult.Result;

            var responseEnvelope = new
            {
                OrgId = org?.Id,
                SessionId = session.Id,
                ResponseId = execRequest.ResponseContinuationId,
                TurnId = turn.Id,
                Response = execResponse
            };

            var responseJson = JsonConvert.SerializeObject(responseEnvelope);
            var responseBlobResult = await _transcriptStore.SaveTurnResponseAsync(org.Id, session.Id, turn.Id, responseJson, cancellationToken);

            if (!responseBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteFollowupTurnAsync__Transcript]", "Failed to store turn response transcript.");
                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(responseBlobResult.ToInvokeResult());
            }

            execResult.Result.FullResponseUrl = responseBlobResult.Result.ToString();
            return execResult;
        }
    }
}
