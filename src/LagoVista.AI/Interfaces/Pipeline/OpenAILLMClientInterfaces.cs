// Interfaces to pull boring chunks out of OpenAIResponsesClientPipelineStap.
// Keep these tiny, single-purpose, and mock-friendly.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Publishes orchestrator diagnostic events about LLM execution.
    /// </summary>
    public interface ILLMEventPublisher
    {
        Task PublishAsync(string sessionId, string stage, string status, string message, double? elapsedMs, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Handles low-level HTTP invocation of OpenAI /v1/responses, including
    /// logging and converting non-2xx HTTP responses into InvokeResult errors.
    /// </summary>
    public interface IOpenAIResponsesExecutor
    {
        /// <summary>
        /// On success: returns the open HttpResponseMessage (caller disposes).
        /// On failure: returns InvokeResult error (and disposes any response internally).
        /// </summary>
        Task<InvokeResult<string>> InvokeAsync(IAgentPipelineContext ctx, string requestJson);
    }

    /// <summary>
    /// Reads a non-streaming /responses HTTP response and returns the final response JSON to parse.
    /// </summary>
    public interface IOpenAINonStreamingResponseReader
    {
        Task<InvokeResult<string>> ReadAsync(HttpResponseMessage httpResponse, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Reads a streaming /responses HTTP response (SSE) and returns the final completed response JSON to parse.
    /// </summary>
    public interface IOpenAIStreamingResponseReader
    {
        Task<InvokeResult<string>> ReadAsync(HttpResponseMessage httpResponse, string sessionId, CancellationToken cancellationToken = default);
    }

    public interface ILLMWorkflowNarrator
    {
        Task ConnectingAsync(CancellationToken cancellationToken);
        Task ThinkingAsync(CancellationToken cancellationToken);
        Task SummarizingAsync(CancellationToken cancellationToken);
    }

    public interface IOpenAIErrorFormatter
    {
        Task<string> FormatAsync(HttpResponseMessage httpResponse);
    }
}
