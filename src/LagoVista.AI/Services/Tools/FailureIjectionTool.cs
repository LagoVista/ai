using System;
using System.Collections.Generic;
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
    /// Failure injection tool to exercise error handling paths.
    ///
    /// The LLM can intentionally request success or failure and see how
    /// the orchestrator/reasoner surface the result.
    /// </summary>
    public sealed class FailureInjectionTool : IAgentTool
    {
        private readonly IAdminLogger _logger;

        public string Name => ToolName;

        public FailureInjectionTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public const string ToolUsageMetadata = "This tool is used for testing the system only and should not be used unless explicitly asked for.   Not much to do other than expect an error.";


        public bool IsToolFullyExecutedOnServer => true;

        private sealed class FailureArgs
        {
            public bool? ShouldFail { get; set; }
            public string FailureMessage { get; set; }
            public string Payload { get; set; }
        }

        private sealed class FailureResult
        {
            public bool RequestedFailure { get; set; }
            public string Payload { get; set; }
            public string SessionId { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

        public Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return Task.FromResult(
                    InvokeResult<string>.FromError("FailureInjectionTool requires a non-empty arguments object."));
            }

            try
            {
                var args = JsonConvert.DeserializeObject<FailureArgs>(argumentsJson) ?? new FailureArgs();

                var shouldFail = args.ShouldFail.GetValueOrDefault(false);

                if (shouldFail)
                {
                    var message = string.IsNullOrWhiteSpace(args.FailureMessage)
                        ? "FailureInjectionTool was asked to fail."
                        : args.FailureMessage;

                    // Log for observability.
                    _logger.AddCustomEvent(
                        LagoVista.Core.PlatformSupport.LogLevel.Error,
                        "FailureInjectionTool.ExecuteAsync",
                        "Intentional failure requested.",
                        new[]
                        {
                            new KeyValuePair<string, string>("SessionId", context?.Request?.SessionId ?? string.Empty),
                            new KeyValuePair<string, string>("SessionId", context?.SessionId ?? string.Empty),
                            new KeyValuePair<string, string>("Payload", args.Payload ?? string.Empty)
                        });


                    return Task.FromResult(InvokeResult<string>.FromError(message));
                }

                var result = new FailureResult
                {
                    RequestedFailure = false,
                    Payload = args.Payload,
                    SessionId = context?.SessionId
                };

                var json = JsonConvert.SerializeObject(result);

                return Task.FromResult(InvokeResult<string>.Create(json));
            }
            catch (Exception ex)
            {
                _logger.AddException("[FailureInjectionTool_ExecuteAsync__Exception]", ex);

                return Task.FromResult(
                    InvokeResult<string>.FromError("FailureInjectionTool failed to process arguments."));
            }
        }

        public const string ToolName = "testing_failure_injection";

        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Failure injection tool to exercise error handling. Can intentionally fail with a custom message, or succeed and echo a payload.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        shouldFail = new
                        {
                            type = "boolean",
                            description = "If true, the tool will return an error instead of a normal result."
                        },
                        failureMessage = new
                        {
                            type = "string",
                            description = "Optional custom error message when shouldFail is true."
                        },
                        payload = new
                        {
                            type = "string",
                            description = "Optional arbitrary payload that will be echoed back on success."
                        }
                    },
                    required = Array.Empty<string>()
                }
            };

            return schema;
        }
    }
}
