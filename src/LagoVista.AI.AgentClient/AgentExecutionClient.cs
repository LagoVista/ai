using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.Core;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.AgentClient
{
    public class AgentExecutionClient : IAgentExecutionClient
    {
        private readonly HttpClient _httpClient;
        private readonly IAdminLogger? _adminLogger;

        public AgentExecutionClient(HttpClient httpClient, IAdminLogger? adminLogger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _adminLogger = adminLogger; // can be null for simple callers like CLI
        }

        public async Task<AgentExecuteResponse> ExecuteAsync(AgentExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var correlationId = Guid.NewGuid().ToId();

            _adminLogger?.Trace("[AgentExecutionClient_ExecuteAsync] Sending execute request.",
                correlationId.ToKVP("correlationId"),
                (request.Mode ?? string.Empty).ToKVP("mode"),
                (request.AgentContext?.Id ?? string.Empty).ToKVP("agentContextId"));

            var response = await _httpClient.PostAsJsonAsync("/api/ai/agent/execute", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                _adminLogger?.AddError(
                    "[AgentExecutionClient_ExecuteAsync__HttpFailure]",
                    $"HTTP error calling /api/ai/agent/execute: {response.StatusCode}.",
                    response.StatusCode.ToString().ToKVP("statusCode"),
                    correlationId.ToKVP("correlationId"));

                if (!String.IsNullOrWhiteSpace(errorBody))
                {
                    _adminLogger?.AddError(
                        "[AgentExecutionClient_ExecuteAsync__HttpFailure]",
                        $"HTTP error body: {errorBody}",
                        correlationId.ToKVP("correlationId"));
                }

                return new AgentExecuteResponse
                {
                    Kind = "error",
                    ErrorCode = "HTTP_ERROR",
                    ErrorMessage = String.IsNullOrWhiteSpace(errorBody)
                        ? $"HTTP error calling /api/ai/agent/execute: {response.StatusCode}"
                        : $"HTTP error calling /api/ai/agent/execute: {response.StatusCode}. Body: {errorBody}"
                };
            }

            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if(json == null)
            {
                _adminLogger?.AddError(
        "[AgentExecutionClient_ExecuteAsync__DeserializeError]",
        $"Failed to deserialize InvokeResult<AgentExecuteResponse>: null string response",
        correlationId.ToKVP("correlationId"));

                return new AgentExecuteResponse
                {
                    Kind = "error",
                    ErrorCode = "DESERIALIZATION_ERROR",
                    ErrorMessage = "Unable to parse agent execution response JSON, NULL response String"
                };

            }

            var invokeResult = JsonConvert.DeserializeObject<InvokeResult<AgentExecuteResponse>>(json);    
            if (invokeResult == null)
            {
                _adminLogger?.AddError(
                    "[AgentExecutionClient_ExecuteAsync__NullInvokeResult]",
                    "Agent execution endpoint returned an empty or invalid payload.",
                    correlationId.ToKVP("correlationId"));

                return new AgentExecuteResponse
                {
                    Kind = "error",
                    ErrorCode = "EMPTY_RESPONSE",
                    ErrorMessage = "Empty or invalid response from agent execution endpoint."
                };
            }

            if (!invokeResult.Successful)
            {
                _adminLogger?.AddError(
                    "[AgentExecutionClient_ExecuteAsync__InvokeError]",
                    "Agent execution endpoint returned an unsuccessful InvokeResult.",
                    correlationId.ToKVP("correlationId"),
                    (invokeResult.ErrorMessage ?? string.Empty).ToKVP("errorMessage"));

                return new AgentExecuteResponse
                {
                    Kind = "error",
                    ErrorCode = "SERVER_ERROR",
                    ErrorMessage = invokeResult.ErrorMessage
                };
            }

            if (invokeResult.Result == null)
            {
                _adminLogger?.AddError(
                    "[AgentExecutionClient_ExecuteAsync__NullResult]",
                    "Agent execution endpoint reported success but Result was null.",
                    correlationId.ToKVP("correlationId"));

                return new AgentExecuteResponse
                {
                    Kind = "error",
                    ErrorCode = "MISSING_RESULT",
                    ErrorMessage = "Agent execution completed but did not return a result payload."
                };
            }

            _adminLogger?.Trace(
                "[AgentExecutionClient_ExecuteAsync] Execute request completed successfully.",
                correlationId.ToKVP("correlationId"),
                (invokeResult.Result.Kind ?? string.Empty).ToKVP("resultKind"));

            return invokeResult.Result;
        }

        public Task<AgentExecuteResponse> AskAsync(EntityHeader agentContext, EntityHeader conversationContext,
            string instruction, string? conversationId = null, string? workspaceId = null, string? repo = null,
            string? language = null, string? ragScope = null, IEnumerable<ActiveFile>? activeFiles = null,
            CancellationToken cancellationToken = default)
        {
            if (agentContext == null || EntityHeader.IsNullOrEmpty(agentContext))
            {
                throw new ArgumentNullException(nameof(agentContext));
            }

            if (String.IsNullOrWhiteSpace(instruction))
            {
                throw new ArgumentNullException(nameof(instruction));
            }

            var request = new AgentExecuteRequest
            {
                AgentContext = agentContext,
                ConversationContext = conversationContext,
                ConversationId = conversationId,
                Mode = "ask",
                Instruction = instruction,
                WorkspaceId = workspaceId,
                Repo = repo,
                Language = language,
                RagScope = ragScope,
                ActiveFiles = activeFiles != null
                    ? new List<ActiveFile>(activeFiles)
                    : new List<ActiveFile>()
            };

            return ExecuteAsync(request, cancellationToken);
        }

        public Task<AgentExecuteResponse> EditAsync(EntityHeader agentContext, EntityHeader conversationContext,
            string instruction, IEnumerable<ActiveFile> activeFiles, string? conversationId = null,
            string? workspaceId = null, string? repo = null, string? language = null, string? ragScope = null,
            CancellationToken cancellationToken = default)
        {
            if (agentContext == null || EntityHeader.IsNullOrEmpty(agentContext))
            {
                throw new ArgumentNullException(nameof(agentContext));
            }

            if (String.IsNullOrWhiteSpace(instruction))
            {
                throw new ArgumentNullException(nameof(instruction));
            }

            if (activeFiles == null)
            {
                throw new ArgumentNullException(nameof(activeFiles));
            }

            var request = new AgentExecuteRequest
            {
                AgentContext = agentContext,
                ConversationContext = conversationContext,
                ConversationId = conversationId,
                Mode = "edit",
                Instruction = instruction,
                WorkspaceId = workspaceId,
                Repo = repo,
                Language = language,
                RagScope = ragScope,
                ActiveFiles = new List<ActiveFile>(activeFiles)
            };

            return ExecuteAsync(request, cancellationToken);
        }
    }
}
