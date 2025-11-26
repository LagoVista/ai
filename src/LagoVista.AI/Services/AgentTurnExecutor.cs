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

        public async Task<InvokeResult<AgentExecutionResponse>> ExecuteNewSessionTurnAsync(AgentContext agentContext, AgentSession session, AgentSessionTurn turn, NewAgentExecutionSession request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            if (agentContext == null) throw new ArgumentNullException(nameof(agentContext));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (turn == null) throw new ArgumentNullException(nameof(turn));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var execRequest = BuildAgentExecuteRequestForNewSession(session, turn, request);

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
            var requestBlobResult = await _transcriptStore.SaveTurnRequestAsync(agentContext, org.Id, session.Id, turn.Id, requestJson, cancellationToken);

            if (!requestBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteNewSessionTurnAsync__Transcript]", "Failed to store turn request transcript.");
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
            var responseBlobResult = await _transcriptStore.SaveTurnResponseAsync(agentContext, org.Id, session.Id, turn.Id, responseJson, cancellationToken);

            if (!responseBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteNewSessionTurnAsync__Transcript]", "Failed to store turn response transcript.");
                return InvokeResult<AgentExecutionResponse>.FromInvokeResult(responseBlobResult.ToInvokeResult());
            }

            var response = BuildAgentExecutionResponseFromExecuteResponse(session, turn, execResponse, requestBlobResult.Result.ToString(), responseBlobResult.Result.ToString());
            return InvokeResult<AgentExecutionResponse>.Create(response);
        }

        public async Task<InvokeResult<AgentExecutionResponse>> ExecuteFollowupTurnAsync(AgentContext agentContext, AgentSession session, AgentSessionTurn turn, AgentExecutionRequest request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            if (agentContext == null) throw new ArgumentNullException(nameof(agentContext));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (turn == null) throw new ArgumentNullException(nameof(turn));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var execRequest = BuildAgentExecuteRequestForFollowup(session, turn, request);

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
            var requestBlobResult = await _transcriptStore.SaveTurnRequestAsync(agentContext, org.Id, session.Id, turn.Id, requestJson, cancellationToken);

            if (!requestBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteFollowupTurnAsync__Transcript]", "Failed to store turn request transcript.");
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
            var responseBlobResult = await _transcriptStore.SaveTurnResponseAsync(agentContext, org.Id, session.Id, turn.Id, responseJson, cancellationToken);

            if (!responseBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteFollowupTurnAsync__Transcript]", "Failed to store turn response transcript.");
                return InvokeResult<AgentExecutionResponse>.FromInvokeResult(responseBlobResult.ToInvokeResult());
            }

            var response = BuildAgentExecutionResponseFromExecuteResponse(session, turn, execResponse, requestBlobResult.Result.ToString(), responseBlobResult.Result.ToString());
            return InvokeResult<AgentExecutionResponse>.Create(response);
        }

        private static AgentExecuteRequest BuildAgentExecuteRequestForNewSession(AgentSession session, AgentSessionTurn turn, NewAgentExecutionSession request)
        {
            return new AgentExecuteRequest
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
        }

        private static AgentExecuteRequest BuildAgentExecuteRequestForFollowup(AgentSession session, AgentSessionTurn turn, AgentExecutionRequest request)
        {
            return new AgentExecuteRequest
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
        }

        private static List<ActiveFile> BuildActiveFiles(List<ActiveFileDescriptor> descriptors)
        {
            var list = new List<ActiveFile>();
            if (descriptors == null || descriptors.Count == 0) return list;

            foreach (var d in descriptors)
            {
                list.Add(new ActiveFile
                {
                    Path = d.Path,
                    Contents = d.Content,
                    Language = d.ContentType
                });
            }

            return list;
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
                PreviousOpenAIResponseId = turn.PreviousOpenAIResponseId,
                Warnings = new List<string>(),
                Errors = new List<string>(),
                ChunkRefs = new List<AgentSessionChunkRef>(),
                ActiveFileRefs = new List<AgentSessionActiveFileRef>()
            };

            if (execResponse.Sources != null)
            {
                foreach (var src in execResponse.Sources)
                {
                    response.ChunkRefs.Add(new AgentSessionChunkRef
                    {
                        ChunkId = src.Tag,
                        Path = src.Path,
                        StartLine = src.Start,
                        EndLine = src.End,
                        ContentHash = null
                    });
                }
            }

            return response;
        }
    }
}
