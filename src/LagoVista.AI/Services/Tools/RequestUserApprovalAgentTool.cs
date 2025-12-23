using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Client-side agent tool definition for request_user_approval.
    /// This is effectively a schema-only stub on the server: it advertises
    /// the tool surface to the LLM but does not actually obtain user input.
    /// </summary>
    public class RequestUserApprovalAgentTool : IAgentTool
    {
        public const string ToolName = "request_user_approval";

        private readonly IAdminLogger _adminLogger;

        public const string ToolUsageMetadata =
    "Client-side tool used to prompt the user for explicit approval before performing a server-side DDR modification. The server never executes this action directly.";

        public RequestUserApprovalAgentTool(IAdminLogger adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        /// <inheritdoc />
        public bool IsToolFullyExecutedOnServer => false;

        /// <inheritdoc />
        public string Name => ToolName;

        /// <summary>
        /// Returns the OpenAI tool schema definition for this tool.
        /// </summary>
        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Client-side tool that asks the user to approve a proposed DDR action before the LLM performs a server-side change.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        prompt = new
                        {
                            type = "string",
                            description = "Human-readable text explaining exactly what the user is being asked to approve."
                        },
                        context = new
                        {
                            type = "object",
                            description = "Optional metadata to correlate this approval to a DDR, chapter, or follow-up action.",
                            properties = new
                            {
                                ddr_identifier = new
                                {
                                    type = "string",
                                    description = "Optional DDR identifier in TLA-### format related to this approval."
                                },
                                chapter_id = new
                                {
                                    type = "string",
                                    description = "Optional chapter identifier related to this approval."
                                },
                                action = new
                                {
                                    type = "string",
                                    description = "Optional description of the concrete server-side action that will occur if Approved."
                                }
                            },
                            required = Array.Empty<string>()
                        }
                    },
                    required = new[] { "prompt" }
                }
            };

            return schema;
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        /// <inheritdoc />
        public Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            const string tag = "[RequestUserApprovalAgentTool__Execute]";

            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return Task.FromResult(
                    InvokeResult<string>.FromError("argumentsJson must not be empty for request_user_approval."));
            }

            try
            {
                var payload = JObject.Parse(argumentsJson);
                var prompt = payload.Value<string>("prompt")?.Trim();

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    return Task.FromResult(
                        InvokeResult<string>.FromError("prompt is required for request_user_approval."));
                }

                // This is a client-side tool. On the server we never actually
                // obtain approval; instead we surface a normalized failure
                // that maps to the spec's UserApprovalUnavailable error.
                var envelope = new JObject
                {
                    ["ok"] = false,
                    ["error"] = new JObject
                    {
                        ["code"] = "UserApprovalUnavailable",
                        ["message"] = "request_user_approval is a client-side tool and must be executed by the hosting client."
                    }
                };

                return Task.FromResult(InvokeResult<string>.Create(envelope.ToString()));
            }
            catch (Exception ex)
            {
                _adminLogger.AddException(tag, ex);
                return Task.FromResult(InvokeResult<string>.FromException(tag, ex));
            }
        }
    }
}
