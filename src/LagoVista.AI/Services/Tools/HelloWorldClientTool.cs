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

        public const string ToolUsageMetadata =
            "Generates a personalized greeting message. Use when the user " +
            "asks to be greeted, welcomed, or acknowledged.";

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
            public string ConversationId { get; set; }
            public string SessionId { get; set; }
        }

        /* --------------------------------------------------------------
         * EXECUTION LOGIC (Contract §4)
         * -------------------------------------------------------------- */

        public Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
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

        public static object GetSchema()
        {
            return new
            {
                type = "function",
                name = ToolName,
                description =
                    "Creates a friendly greeting message using the user's name.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new
                        {
                            type = "string",
                            description = "The user's name to include in the greeting."
                        }
                    },
                    required = new[] { "name" }
                }
            };
        }
    }
}
