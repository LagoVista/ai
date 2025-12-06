using System;
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
    /// Minimal example tool that generates a greeting message.
    /// Serves as the canonical reference implementation for Aptix tools.
    /// </summary>
    public sealed class HelloWorldTool : IAgentTool
    {
        /* --------------------------------------------------------------
         * REQUIRED CONSTANTS (Contract §3.3)
         * -------------------------------------------------------------- */

        public const string ToolName = "agent_hello_world";

        public const string ToolUsageMetadata =
            "Generates a personalized greeting message. Use when the user " +
            "asks to be greeted, welcomed, or acknowledged.";

        /* --------------------------------------------------------------
         * DI CONSTRUCTOR (Contract §3.1)
         * -------------------------------------------------------------- */

        private readonly IAdminLogger _logger;

        public HelloWorldTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /* --------------------------------------------------------------
         * TOOL IDENTITY (IAgentTool)
         * -------------------------------------------------------------- */

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

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
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return Task.FromResult(
                    InvokeResult<string>.FromError(
                        "HelloWorldTool requires a non-empty arguments object."));
            }

            try
            {
                var args = JsonConvert.DeserializeObject<HelloWorldArgs>(argumentsJson)
                           ?? new HelloWorldArgs();

                if (string.IsNullOrWhiteSpace(args.Name))
                {
                    return Task.FromResult(
                        InvokeResult<string>.FromError(
                            "HelloWorldTool requires a non-empty 'name' string."));
                }

                var reply = new HelloWorldResult
                {
                    Message = $"Hello, {args.Name}!",
                    ConversationId = context?.Request?.ConversationId,
                    SessionId = context?.SessionId
                };

                var json = JsonConvert.SerializeObject(reply);
                return Task.FromResult(InvokeResult<string>.Create(json));
            }
            catch (Exception ex)
            {
                _logger.AddException("[HelloWorldTool_ExecuteAsync__Exception]", ex);

                return Task.FromResult(
                    InvokeResult<string>.FromError(
                        "HelloWorldTool failed to process arguments."));
            }
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
