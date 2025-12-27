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
        public const string ToolUsageMetadata = "Generates a personalized greeting message. Use when the user " + "asks to be greeted, welcomed, or acknowledged.";
        public const string ToolSummary = "returns hello world from the agent (mostly used for testing)";
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
            public string SessionId { get; set; }
        }

        /* --------------------------------------------------------------
         * EXECUTION LOGIC (Contract §4)
         * -------------------------------------------------------------- */
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return Task.FromResult(InvokeResult<string>.FromError("HelloWorldTool requires a non-empty arguments object."));
            }

            try
            {
                var args = JsonConvert.DeserializeObject<HelloWorldArgs>(argumentsJson) ?? new HelloWorldArgs();
                if (string.IsNullOrWhiteSpace(args.Name))
                {
                    return Task.FromResult(InvokeResult<string>.FromError("HelloWorldTool requires a non-empty 'name' string."));
                }

                var reply = new HelloWorldResult
                {
                    Message = $"Hello, {args.Name}!",
                    SessionId = context?.Request?.SessionId,
                };
                var json = JsonConvert.SerializeObject(reply);
                return Task.FromResult(InvokeResult<string>.Create(json));
            }
            catch (Exception ex)
            {
                _logger.AddException("[HelloWorldTool_ExecuteAsync__Exception]", ex);
                return Task.FromResult(InvokeResult<string>.FromError("HelloWorldTool failed to process arguments."));
            }
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