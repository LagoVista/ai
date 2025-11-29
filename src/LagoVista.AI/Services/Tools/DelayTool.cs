using System;
using System.Diagnostics;
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
    /// Delay tool to exercise async behavior and cancellation.
    ///
    /// The LLM can use this to test:
    /// - How long-running tools behave.
    /// - Whether cancellation/timeout flows are wired up correctly.
    /// </summary>
    public sealed class DelayTool : IAgentTool
    {
        private readonly IAdminLogger _logger;

        public string Name => ToolName;

        public const string ToolUsageMetadata = "This tool is used for testing the system only and should not be used unless explicitly asked for. Provide a deley in milliseconds and wait";


        public bool IsToolFullyExecutedOnServer => true;


        public DelayTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private sealed class DelayArgs
        {
            public int? Milliseconds { get; set; }
            public string Note { get; set; }
        }

        private sealed class DelayResult
        {
            public int RequestedMilliseconds { get; set; }
            public int ElapsedMilliseconds { get; set; }
            public bool WasCancelled { get; set; }
            public string Note { get; set; }
            public string ConversationId { get; set; }
            public string SessionId { get; set; }
        }

        public async Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return InvokeResult<string>.FromError("DelayTool requires a non-empty arguments object.");
            }

            try
            {
                var args = JsonConvert.DeserializeObject<DelayArgs>(argumentsJson) ?? new DelayArgs();

                var requestedMs = args.Milliseconds.GetValueOrDefault(1000);

                // Clamp to something reasonable.
                if (requestedMs < 0)
                {
                    requestedMs = 0;
                }

                if (requestedMs > 60_000)
                {
                    requestedMs = 60_000;
                }

                var stopwatch = Stopwatch.StartNew();

                try
                {
                    await Task.Delay(requestedMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    stopwatch.Stop();

                    var cancelledResult = new DelayResult
                    {
                        RequestedMilliseconds = requestedMs,
                        ElapsedMilliseconds = (int)stopwatch.ElapsedMilliseconds,
                        WasCancelled = true,
                        Note = args.Note,
                        ConversationId = context?.Request?.ConversationId,
                        SessionId = context?.SessionId
                    };

                    var cancelledJson = JsonConvert.SerializeObject(cancelledResult);

                    return InvokeResult<string>.Create(cancelledJson);
                }

                stopwatch.Stop();

                var result = new DelayResult
                {
                    RequestedMilliseconds = requestedMs,
                    ElapsedMilliseconds = (int)stopwatch.ElapsedMilliseconds,
                    WasCancelled = false,
                    Note = args.Note,
                    ConversationId = context?.Request?.ConversationId,
                    SessionId = context?.SessionId
                };

                var json = JsonConvert.SerializeObject(result);

                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException("[DelayTool_ExecuteAsync__Exception]", ex);

                return InvokeResult<string>.FromError("DelayTool failed to process arguments.");
            }
        }

        public const string ToolName = "testing_delay";

        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Delay tool to exercise async behavior and cancellation. Waits for a requested duration, then returns timing info.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        milliseconds = new
                        {
                            type = "integer",
                            description = "How long to delay in milliseconds (0–60000). Defaults to 1000 if omitted."
                        },
                        note = new
                        {
                            type = "string",
                            description = "Optional note to echo back."
                        }
                    },
                    required = Array.Empty<string>()
                }
            };

            return schema;
        }
    }
}
