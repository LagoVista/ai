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
        private readonly IAgentContextManager _contextManager;

        public AgentTurnExecutor(IAgentContextManager contextManager, IAgentExecutionService agentExecutionService, IAgentTurnTranscriptStore transcriptStore, IAdminLogger adminLogger)
        {
            _agentExecutionService = agentExecutionService ?? throw new ArgumentNullException(nameof(agentExecutionService));
            _transcriptStore = transcriptStore ?? throw new ArgumentNullException(nameof(transcriptStore));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        }

        public async Task<InvokeResult<AgentExecutionResponse>> ExecuteNewSessionTurnAsync(AgentSession session, AgentSessionTurn turn, NewAgentExecutionSession request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (turn == null) throw new ArgumentNullException(nameof(turn));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var execRequest = BuildAgentExecuteRequestForNewSession(session, turn, request);

            var context = await _contextManager.GetAgentContextAsync(request.AgentContext.Id, org, user);

            var requestEnvelope = new
            {
                OrgId = org?.Id,
                SessionId = session.Id,
                TurnId = turn.Id,
                Request = execRequest,
                ActiveFiles = request.ActiveFiles,
                RagFilters = request.RagFilters
            };

            var requestJson = JsonConvert.SerializeObject(requestEnvelope);
            var requestBlobResult = await _transcriptStore.SaveTurnRequestAsync(context, org.Id, session.Id, turn.Id, requestJson, cancellationToken);
            if (!requestBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteNewSessionTurnAsync__SaveRequest]", "Failed to save turn request transcript.", requestBlobResult.ErrorsToKVPArray());
                return InvokeResult<AgentExecutionResponse>.FromInvokeResult(requestBlobResult.ToInvokeResult());
            }

            var execResult = await _agentExecutionService.ExecuteAsync(execRequest, org, user, cancellationToken);
            if (!execResult.Successful)
            {
                return InvokeResult<AgentExecutionResponse>.FromInvokeResult(execResult.ToInvokeResult());
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
            var responseBlobResult = await _transcriptStore.SaveTurnResponseAsync(context, org.Id, session.Id, turn.Id, responseJson, cancellationToken);
            if (!responseBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteNewSessionTurnAsync__SaveResponse]", "Failed to save turn response transcript.", responseBlobResult.ErrorsToKVPArray());
                return InvokeResult<AgentExecutionResponse>.FromInvokeResult(responseBlobResult.ToInvokeResult());
            }

            var response = BuildAgentExecutionResponseFromExecuteResponse(
                session,
                turn,
                execResponse,
                requestBlobResult.Result?.ToString(),
                responseBlobResult.Result?.ToString());

            return InvokeResult<AgentExecutionResponse>.Create(response);
        }

        public async Task<InvokeResult<AgentExecutionResponse>> ExecuteFollowupTurnAsync(AgentSession session, AgentSessionTurn turn, AgentExecutionRequest request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (turn == null) throw new ArgumentNullException(nameof(turn));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var execRequest = BuildAgentExecuteRequestForFollowup(session, turn, request);

            var context = await _contextManager.GetAgentContextAsync(session.AgentContext.Id, org, user);

            var requestEnvelope = new
            {
                OrgId = org?.Id,
                SessionId = session.Id,
                TurnId = turn.Id,
                Request = execRequest,
                ActiveFiles = request.ActiveFiles,
                RagFilters = request.RagFilters
            };

            var requestJson = JsonConvert.SerializeObject(requestEnvelope);
            var requestBlobResult = await _transcriptStore.SaveTurnRequestAsync(context, org.Id, session.Id, turn.Id, requestJson, cancellationToken);
            if (!requestBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteFollowupTurnAsync__SaveRequest]", "Failed to save turn request transcript.", requestBlobResult.ErrorsToKVPArray());
                return InvokeResult<AgentExecutionResponse>.FromInvokeResult(requestBlobResult.ToInvokeResult());
            }

            var execResult = await _agentExecutionService.ExecuteAsync(execRequest, org, user, cancellationToken);
            if (!execResult.Successful)
            {
                return InvokeResult<AgentExecutionResponse>.FromInvokeResult(execResult.ToInvokeResult());
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
            var responseBlobResult = await _transcriptStore.SaveTurnResponseAsync(context, org.Id, session.Id, turn.Id, responseJson, cancellationToken);
            if (!responseBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteFollowupTurnAsync__SaveResponse]", "Failed to save turn response transcript.", responseBlobResult.ErrorsToKVPArray());
                return InvokeResult<AgentExecutionResponse>.FromInvokeResult(responseBlobResult.ToInvokeResult());
            }

            var response = BuildAgentExecutionResponseFromExecuteResponse(
                session,
                turn,
                execResponse,
                requestBlobResult.Result?.ToString(),
                responseBlobResult.Result?.ToString());

            return InvokeResult<AgentExecutionResponse>.Create(response);
        }

        private static AgentExecuteRequest BuildAgentExecuteRequestForNewSession(AgentSession session, AgentSessionTurn turn, NewAgentExecutionSession request)
        {
            var execRequest = new AgentExecuteRequest
            {
                AgentContext = session.AgentContext,
                ConversationContext = session.ConversationContext,
                Mode = "ask",
                Instruction = request.Instruction,
                ConversationId = turn.ConversationId,
                Repo = request.Repo,
                Language = request.Language,
                WorkspaceId = request.WorkspaceId,
                ActiveFiles = BuildActiveFiles(request.ActiveFiles)
            };

            return execRequest;
        }

        private static AgentExecuteRequest BuildAgentExecuteRequestForFollowup(AgentSession session, AgentSessionTurn turn, AgentExecutionRequest request)
        {
            var execRequest = new AgentExecuteRequest
            {
                AgentContext = session.AgentContext,
                ConversationContext = session.ConversationContext,
                Mode = "ask",
                Instruction = request.Instruction,
                ConversationId = turn.ConversationId,
                Repo = session.Repo,
                Language = session.DefaultLanguage,
                WorkspaceId = session.WorkspaceId,
                ActiveFiles = BuildActiveFiles(request.ActiveFiles)
            };

            return execRequest;
        }

        private static List<ActiveFile> BuildActiveFiles(List<ActiveFileDescriptor> descriptors)
        {
            var files = new List<ActiveFile>();

            if (descriptors == null || descriptors.Count == 0)
            {
                return files;
            }

            foreach (var descriptor in descriptors)
            {
                files.Add(new ActiveFile
                {
                    Path = descriptor.Path,
                    Contents = descriptor.Content,
                    Language = descriptor.ContentType
                });
            }

            return files;
        }

        private static AgentExecutionResponse BuildAgentExecutionResponseFromExecuteResponse(AgentSession session, AgentSessionTurn turn, AgentExecuteResponse execResponse, string requestBlobUrl, string responseBlobUrl)
        {
            var response = new AgentExecutionResponse
            {
                SessionId = session.Id,
                TurnId = turn.Id,
                AgentAnswer = execResponse.Text,
                AgentAnswerFullText = execResponse.Text,
                OpenAIRequestBlobUrl = requestBlobUrl,
                OpenAIResponseBlobUrl = responseBlobUrl,
                OpenAIResponseId = null,
                PreviousOpenAIResponseId = turn.PreviousOpenAIResponseId,
                Warnings = new List<string>(),
                Errors = new List<string>(),
                ChunkRefs = new List<AgentSessionChunkRef>(),
                ActiveFileRefs = new List<AgentSessionActiveFileRef>()
            };

            if (execResponse.Sources != null)
            {
                foreach (var source in execResponse.Sources)
                {
                    response.ChunkRefs.Add(new AgentSessionChunkRef
                    {
                        ChunkId = source.Tag,
                        Path = source.Path,
                        StartLine = source.Start,
                        EndLine = source.End,
                        ContentHash = null
                    });
                }
            }

            return response;
        }
    }
}
