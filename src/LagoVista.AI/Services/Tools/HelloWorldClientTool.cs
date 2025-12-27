using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Minimal example tool that generates a greeting message.
    /// Serves as the canonical reference implementation for Aptix tools.
    /// </summary>
    public sealed class HelloWorldClientTool : IAgentTool
    {
        /* --------------------------------------------------------------
         * REQUIRED CONSTANTS (Contract §3.3)
         * -------------------------------------------------------------- */
        public const string ToolName = "agent_hello_world_client";
        public const string ToolUsageMetadata = "Generates a personalized greeting message. Use when the user " + "asks to be greeted, welcomed, or acknowledged.";
        public const string ToolSummary = "returns hello world from the initiating client (mostly used for testing)";
        /* --------------------------------------------------------------
         * DI CONSTRUCTOR (Contract §3.1)
         * -------------------------------------------------------------- */
        private readonly IAdminLogger _logger;
        public HelloWorldClientTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /* --------------------------------------------------------------
         * TOOL IDENTITY (IAgentTool)
         * -------------------------------------------------------------- */
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => false;

        /* --------------------------------------------------------------
         * INPUT/OUTPUT CONTRACT (Contract §4)
         * -------------------------------------------------------------- */
        private sealed class HelloWorldArgs
        {
            public string Name { get; set; }
        }

        private sealed class HelloWorldResult
        {
            public string Message { get; set; }
            public string SessionId { get; set; }
        }

        /* --------------------------------------------------------------
         * EXECUTION LOGIC (Contract §4)
         * -------------------------------------------------------------- */
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            // This is a client-side tool. On the server we never actually
            // obtain approval; instead we surface a normalized failure
            // that maps to the spec's UserApprovalUnavailable error.
            var envelope = new JObject
            {
                ["ok"] = false,
                ["error"] = new JObject
                {
                    ["code"] = "HelloWorldClientUnavailable",
                    ["message"] = "agent_hello_world_client is a client-side tool and must be executed by the hosting client."
                }
            };
            return Task.FromResult(InvokeResult<string>.Create(envelope.ToString()));
        }

        /* --------------------------------------------------------------
         * SCHEMA DEFINITION (Contract §5)
         * -------------------------------------------------------------- */
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Creates a friendly greeting message using the user's name.", p =>
            {
                p.String("name", "The user's name to include in the greeting.", required: true);
            });
        }
    }
}