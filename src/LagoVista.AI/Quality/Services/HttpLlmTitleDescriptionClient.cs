using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Quality.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Quality.Services
{
    /// <summary>
    /// Orchestrator implementation of <see cref="ITitleDescriptionLlmClient"/> that uses
    /// <see cref="IStructuredTextLlmService"/> to perform title/description refinement.
    ///
    /// This class is responsible for:
    /// - Building the system prompt and input payload from <see cref="TitleDescriptionReviewRequest"/>,
    /// - Calling the generic structured-text LLM service with <see cref="TitleDescriptionReviewResult"/>
    ///   as the target type,
    /// - Interpreting the <see cref="InvokeResult{T}"/> and surfacing success or failure.
    ///
    /// It no longer performs direct HTTP calls or low-level JSON parsing.
    /// </summary>
    public class HttpLlmTitleDescriptionClient : ITitleDescriptionLlmClient
    {
        private readonly IStructuredTextLlmService _structuredTextLlmService;
        private readonly IAdminLogger _logger;

        private const string OperationName = "TitleDescriptionReview";

        public HttpLlmTitleDescriptionClient(
            IStructuredTextLlmService structuredTextLlmService,
            IAdminLogger logger)
        {
            _structuredTextLlmService = structuredTextLlmService ?? throw new ArgumentNullException(nameof(structuredTextLlmService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<TitleDescriptionReviewResult> ReviewAsync(
            TitleDescriptionReviewRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var systemPrompt = BuildSystemPrompt();
            var inputText = BuildUserPayload(request);

            _logger.Trace(
                $"[HttpLlmTitleDescriptionClient_ReviewAsync] Sending structured review request for symbol '{request.SymbolName ?? string.Empty}'.");

            var invokeResult = await _structuredTextLlmService.ExecuteAsync<TitleDescriptionReviewResult>(
                    systemPrompt,
                    inputText,
                    request.Model,
                    OperationName,
                    request.SymbolName,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!invokeResult.Successful)
            {
                var errorSummary = string.Join(" | ", invokeResult.Errors.Select(err => err.Message));

                _logger.AddError(
                    "HttpLlmTitleDescriptionClient_ReviewAsync",
                    "Title/description review LLM call failed.",
                    new KeyValuePair<string, string>("Errors", errorSummary));

                // Preserve existing behavior pattern: surface as an exception so that
                // higher-level orchestrators can treat this as a warning/failure and
                // fall back to original values if desired.
                throw new InvalidOperationException(
                    "Title/description review LLM call failed; see logs for detailed errors.");
            }

            return invokeResult.Result;
        }

        /// <summary>
        /// System prompt used for all title/description refinement calls.
        /// This defines the behavior of the model and the expectations for how
        /// it should populate <see cref="TitleDescriptionReviewResult"/>.
        /// </summary>
        private static string BuildSystemPrompt()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("You are an expert technical editor for a large enterprise codebase.");
            sb.AppendLine("Your job is to refine domain and model titles/descriptions/help text used in UI metadata.");
            sb.AppendLine();
            sb.AppendLine("Requirements:");
            sb.AppendLine("- Preserve the original semantic meaning.");
            sb.AppendLine("- Improve clarity, grammar, and spelling.");
            sb.AppendLine("- Keep text concise and user-facing (no internal jargon).");
            sb.AppendLine("- Do NOT invent features or behavior that are not implied by the input.");
            sb.AppendLine();
            sb.AppendLine("You must respond with data that can be interpreted as a TitleDescriptionReviewResult instance.");
            sb.AppendLine("Each property of TitleDescriptionReviewResult must be populated according to its meaning.");

            return sb.ToString();
        }

        /// <summary>
        /// Build the single text payload that will be passed to the generic LLM service.
        /// This wraps the full <see cref="TitleDescriptionReviewRequest"/> so the model
        /// sees all relevant context without coupling the orchestrator to individual fields.
        /// </summary>
        private static string BuildUserPayload(TitleDescriptionReviewRequest request)
        {
            var wrapper = new
            {
                kind = request.Kind.ToString(),
                symbolName = request.SymbolName,
                request
            };

            return JsonConvert.SerializeObject(wrapper, Formatting.None);
        }
    }
}
