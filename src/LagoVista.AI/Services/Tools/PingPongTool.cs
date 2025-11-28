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
    /// Simple "ping-pong" test tool.
    ///
    /// Intended as a sanity-check server tool:
    /// - LLM calls the tool with { "message": "...", "count": 0 }
    /// - Tool responds with { "reply": "pong: ...", "count": 1, ... }
    ///
    /// This validates the full server-side tool pipeline:
    /// - LLM -> AgentReasoner -> AgentToolExecutor -> PingPongTool
    /// - Tool result flows back to LLM via ToolResultsJson.
    /// </summary>
    public sealed class PingPongTool : IAgentTool
    {
        private readonly IAdminLogger _logger;

        public string Name => PingPongTool.ToolName;

        public PingPongTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private sealed class PingPongArgs
        {
            public string Message { get; set; }
            public int? Count { get; set; }
        }

        private sealed class PingPongResult
        {
            public string Reply { get; set; }
            public int Count { get; set; }
            public string ConversationId { get; set; }
            public string SessionId { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return Task.FromResult(
                    InvokeResult<string>.FromError("PingPongTool requires a non-empty arguments object."));
            }

            try
            {
                var args = JsonConvert.DeserializeObject<PingPongArgs>(argumentsJson) ?? new PingPongArgs();

                var count = args.Count.GetValueOrDefault(0) + 1;
                var message = string.IsNullOrWhiteSpace(args.Message) ? "ping" : args.Message;

                var result = new PingPongResult
                {
                    Reply = $"pong: {message}",
                    Count = count,
                    ConversationId = context?.Request?.ConversationId,
                    SessionId = context?.SessionId
                };

                var resultJson = JsonConvert.SerializeObject(result);

                return Task.FromResult(InvokeResult<string>.Create(resultJson));
            }
            catch (Exception ex)
            {
                _logger.AddException("[PingPongTool_ExecuteAsync__Exception]", ex);

                return Task.FromResult(
                    InvokeResult<string>.FromError("PingPongTool failed to process arguments."));
            }
        }

        public const string ToolName = "testing_ping_pong";    

        public static object GetSchema()
        {
            var pingPongTool = new
            {
                type = "function",
                name = PingPongTool.ToolName,
                description = "Simple ping-pong test tool that echoes a message and increments a counter.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        message = new
                        {
                            type = "string",
                            description = "Optional message to echo back with a 'pong' prefix."
                        },
                        count = new
                        {
                            type = "integer",
                            description = "How many times this tool has been called in this chain; the tool will increment it."
                        }
                    },
                    required = Array.Empty<string>()
                }
            };

            return pingPongTool;
        }
    }
}
