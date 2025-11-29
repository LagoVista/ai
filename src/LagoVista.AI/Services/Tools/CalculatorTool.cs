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
    /// Simple calculator tool to exercise structured arguments and error paths.
    ///
    /// Supports: add, subtract, multiply, divide.
    /// </summary>
    public sealed class CalculatorTool : IAgentTool
    {
        private readonly IAdminLogger _logger;

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "This tool is used for testing the system only and should not be used unless explicitly asked for. Supply two numbers and an operator";

        public CalculatorTool(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private sealed class CalculatorArgs
        {
            public string Operation { get; set; }  // add | subtract | multiply | divide
            public double? Left { get; set; }
            public double? Right { get; set; }
        }

        private sealed class CalculatorResult
        {
            public string Operation { get; set; }
            public double Left { get; set; }
            public double Right { get; set; }
            public double Result { get; set; }
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
                    InvokeResult<string>.FromError("CalculatorTool requires a non-empty arguments object."));
            }

            try
            {
                var args = JsonConvert.DeserializeObject<CalculatorArgs>(argumentsJson) ?? new CalculatorArgs();

                if (string.IsNullOrWhiteSpace(args.Operation))
                {
                    return Task.FromResult(
                        InvokeResult<string>.FromError("CalculatorTool requires 'operation' (add|subtract|multiply|divide)."));
                }

                if (!args.Left.HasValue || !args.Right.HasValue)
                {
                    return Task.FromResult(
                        InvokeResult<string>.FromError("CalculatorTool requires both 'left' and 'right' numeric values."));
                }

                var op = args.Operation.Trim().ToLowerInvariant();
                var left = args.Left.Value;
                var right = args.Right.Value;

                double result;

                switch (op)
                {
                    case "add":
                        result = left + right;
                        break;

                    case "subtract":
                        result = left - right;
                        break;

                    case "multiply":
                        result = left * right;
                        break;

                    case "divide":
                        if (Math.Abs(right) < double.Epsilon)
                        {
                            return Task.FromResult(
                                InvokeResult<string>.FromError("CalculatorTool divide-by-zero is not allowed."));
                        }

                        result = left / right;
                        break;

                    default:
                        return Task.FromResult(
                            InvokeResult<string>.FromError($"CalculatorTool unsupported operation '{args.Operation}'."));
                }

                var payload = new CalculatorResult
                {
                    Operation = op,
                    Left = left,
                    Right = right,
                    Result = result,
                    ConversationId = context?.Request?.ConversationId,
                    SessionId = context?.SessionId
                };

                var json = JsonConvert.SerializeObject(payload);

                return Task.FromResult(InvokeResult<string>.Create(json));
            }
            catch (Exception ex)
            {
                _logger.AddException("[CalculatorTool_ExecuteAsync__Exception]", ex);

                return Task.FromResult(
                    InvokeResult<string>.FromError("CalculatorTool failed to process arguments."));
            }
        }

        public const string ToolName = "testing_calculator";

        public static object GetSchema()
        {
            var schema = new
            {
                type = "function",
                name = ToolName,
                description = "Simple calculator to exercise structured arguments and error paths. Supports add, subtract, multiply, and divide.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Operation to perform: add, subtract, multiply, divide."
                        },
                        left = new
                        {
                            type = "number",
                            description = "Left-hand operand."
                        },
                        right = new
                        {
                            type = "number",
                            description = "Right-hand operand."
                        }
                    },
                    required = new[] { "operation", "left", "right" }
                }
            };

            return schema;
        }
    }
}
