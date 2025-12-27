using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Tool wrapper over IContentHashService that computes a normalized hash for
    /// arbitrary text content. Normalization and hashing behavior are fully
    /// delegated to IContentHashService.
    /// </summary>
    public sealed class CodeHashNormalizedTool : IAgentTool
    {
        public const string ToolName = "code_hash_normalized";
        public string Name => ToolName;

        public const string ToolSummary = "normalize text and then calculate a hash";
        public bool IsToolFullyExecutedOnServer => true;

        private readonly IContentHashService _contentHashService;
        private readonly IAdminLogger _logger;
        public CodeHashNormalizedTool(IContentHashService contentHashService, IAdminLogger logger)
        {
            _contentHashService = contentHashService ?? throw new ArgumentNullException(nameof(contentHashService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// JSON schema used when registering this tool with the LLM.
        /// </summary>
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Compute a normalized content hash (SHA-256) for arbitrary text using the canonical LagoVista normalization rules.", p =>
            {
                p.String("content", "Raw text to hash. Normalization (line endings, whitespace, etc.) is applied before hashing.", required: true);
                p.String("docPath", "Optional canonical document path used only for logging and correlation.");
                p.String("label", "Optional label for this hash (e.g., 'pre-patch', 'post-patch', 'index-chunk').");
            });
        }

        /// <summary>
        /// Human-readable usage guidance that can be injected into system prompts.
        /// </summary>
        public const string ToolUsageMetadata = @"code_hash_normalized usage

Purpose:
- Compute a normalized hash for text content using the canonical LagoVista normalization rules.
- Use this when you need a stable fingerprint for files, RAG chunks, or buffers that may differ only by whitespace or formatting.

Rules:
- Always send the exact content you wish to compare; do not pre-normalize.
- The returned hash is a lowercase hex SHA-256 string.
- docPath and label are optional and used only for logging/correlation.

When to use:
- Before or after workspace_write_patch to compare pre/post content.
- When validating that a RAG chunk or DDR section matches the current repo content.
- When comparing Active File content with cloud or indexed copies.
";
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                var cancelledPayload = JsonConvert.SerializeObject(new CodeHashNormalizedResponse { Success = false, ErrorCode = "CANCELLED", ErrorMessage = "code_hash_normalized execution was cancelled before processing.", DocPath = null, Label = null, Hash = null, ContentLength = 0 });
                return InvokeResult<string>.Create(cancelledPayload);
            }

            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                var missingArgsPayload = JsonConvert.SerializeObject(new CodeHashNormalizedResponse { Success = false, ErrorCode = "MISSING_ARGUMENTS", ErrorMessage = "code_hash_normalized requires a non-empty JSON arguments payload.", DocPath = null, Label = null, Hash = null, ContentLength = 0 });
                return InvokeResult<string>.Create(missingArgsPayload);
            }

            CodeHashNormalizedArgs args;
            try
            {
                args = JsonConvert.DeserializeObject<CodeHashNormalizedArgs>(argumentsJson) ?? new CodeHashNormalizedArgs();
            }
            catch (Exception ex)
            {
                _logger.AddException("[code_hash_normalized_Deserialize]", ex);
                var errorPayload = JsonConvert.SerializeObject(new CodeHashNormalizedResponse { Success = false, ErrorCode = "DESERIALIZATION_ERROR", ErrorMessage = "Unable to deserialize code_hash_normalized arguments.", DocPath = null, Label = null, Hash = null, ContentLength = 0 });
                return InvokeResult<string>.Create(errorPayload);
            }

            if (args.Content == null)
            {
                var validationPayload = JsonConvert.SerializeObject(new CodeHashNormalizedResponse { Success = false, ErrorCode = "MISSING_CONTENT", ErrorMessage = "code_hash_normalized requires a 'content' field. The field may be an empty string but it must be present.", DocPath = args.DocPath, Label = args.Label, Hash = null, ContentLength = 0 });
                return InvokeResult<string>.Create(validationPayload);
            }

            try
            {
                // Compute byte length of the original (pre-normalization) content
                var contentByteLength = Encoding.UTF8.GetByteCount(args.Content);
                // Delegate hashing to the canonical content hash service
                var hash = _contentHashService.ComputeTextHash(args.Content);
                var response = new CodeHashNormalizedResponse
                {
                    Success = true,
                    ErrorCode = null,
                    ErrorMessage = null,
                    Hash = hash,
                    ContentLength = contentByteLength,
                    DocPath = args.DocPath,
                    Label = args.Label
                };
                var json = JsonConvert.SerializeObject(response);
                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException("[code_hash_normalized_Execute]", ex);
                var errorPayload = JsonConvert.SerializeObject(new CodeHashNormalizedResponse { Success = false, ErrorCode = "UNEXPECTED_ERROR", ErrorMessage = "code_hash_normalized failed to process arguments.", DocPath = args.DocPath, Label = args.Label, Hash = null, ContentLength = 0 });
                return InvokeResult<string>.Create(errorPayload);
            }
        }
    }

    /// <summary>
    /// Arguments payload for code_hash_normalized.
    /// </summary>
    public sealed class CodeHashNormalizedArgs
    {
        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("docPath")]
        public string DocPath { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }
    }

    /// <summary>
    /// Response payload for code_hash_normalized.
    /// </summary>
    public sealed class CodeHashNormalizedResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("contentLength")]
        public int ContentLength { get; set; }

        [JsonProperty("docPath")]
        public string DocPath { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("errorCode")]
        public string ErrorCode { get; set; }

        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; set; }
    }
}