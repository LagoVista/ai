// Interfaces to pull boring chunks out of OpenAIResponsesClientPipelineStap.
// Keep these tiny, single-purpose, and mock-friendly.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
    /// Handles low-level HTTP invocation of OpenAI /v1/responses.
    /// </summary>
    public interface IOpenAIResponsesInvoker
    {
        /// <summary>
        /// Sends the request JSON to /v1/responses and returns the raw HttpResponseMessage.
        /// Caller is responsible for disposing the returned HttpResponseMessage.
        /// </summary>
        Task<HttpResponseMessage> InvokeAsync(string baseUrl, string apiKey, string requestJson, CancellationToken cancellationToken = default);
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

    /// <summary>
    /// Emits workflow/partial updates to whatever streaming UI context you maintain.
    /// This keeps OpenAIResponsesClientPipelineStap from knowing how the UX is implemented.
    /// </summary>
    public interface IAgentStreamingNotifier
    {
        Task AddWorkflowAsync(string message, CancellationToken cancellationToken = default);
        Task AddPartialAsync(string deltaText, CancellationToken cancellationToken = default);
    }
}
