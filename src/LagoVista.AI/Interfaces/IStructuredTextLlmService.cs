using System.Threading;
using System.Threading.Tasks;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Generic service for calling an LLM with a system prompt and a single text payload,
    /// returning a typed result wrapped in InvokeResult&lt;TResult&gt;.
    ///
    /// This abstraction is provider-agnostic and is the single entry point for
    /// "instructions + text → typed result" flows.
    /// </summary>
    public interface IStructuredTextLlmService
    {

        /// <summary>
        /// Execute a typed LLM operation with the given system prompt and text payload.
        /// </summary>
        /// <typeparam name="TResult">The result type that defines the expected output shape.</typeparam>
        /// <param name="systemPrompt">Instructions describing how the model should behave.</param>
        /// <param name="inputText">Single text payload to operate on.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>InvokeResult containing a populated TResult on success, or errors on failure.</returns>
        Task<InvokeResult<TResult>> ExecuteAsync<TResult>(
            string systemPrompt,
            string inputText,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute a typed LLM operation with explicit model and optional correlation/operation metadata.
        /// </summary>
        /// <typeparam name="TResult">The result type that defines the expected output shape.</typeparam>
        /// <param name="systemPrompt">Instructions describing how the model should behave.</param>
        /// <param name="inputText">Single text payload to operate on.</param>
        /// <param name="model">Optional model identifier; if null/empty, a default model is used.</param>
        /// <param name="operationName">Optional logical operation name for diagnostics.</param>
        /// <param name="correlationId">Optional correlation identifier for tracing.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>InvokeResult containing a populated TResult on success, or errors on failure.</returns>
        Task<InvokeResult<TResult>> ExecuteAsync<TResult>(
            string systemPrompt,
            string inputText,
            string model,
            string operationName,
            string correlationId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Non-generic convenience overload for simple string scenarios.
        /// Conceptually equivalent to ExecuteAsync&lt;string&gt;.
        /// </summary>
        /// <param name="systemPrompt">Instructions describing how the model should behave.</param>
        /// <param name="inputText">Single text payload to operate on.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>InvokeResult containing the resulting string on success, or errors on failure.</returns>
        Task<InvokeResult<string>> ExecuteAsync(
            string systemPrompt,
            string inputText,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Non-generic convenience overload for simple string scenarios.
        /// Conceptually equivalent to ExecuteAsync&lt;string&gt;.
        /// </summary>
        /// <param name="openAiSettings">Settings for OpenAI.</param>
        /// <param name="systemPrompt">Instructions describing how the model should behave.</param>
        /// <param name="inputText">Single text payload to operate on.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>InvokeResult containing the resulting string on success, or errors on failure.</returns>
        Task<InvokeResult<string>> ExecuteAsync(
            IOpenAISettings openAiSettings,
            string systemPrompt,
            string inputText,
            CancellationToken cancellationToken = default);

    }
}
