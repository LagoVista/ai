using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using RingCentral;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Default implementation of IAgentToolExecutor.
    ///
    /// Uses IAgentToolRegistry to resolve tools by name and delegates
    /// execution to the underlying IAgentTool.ExecuteAsync method.
    /// </summary>
    public class AgentToolExecutor : IAgentToolExecutor
    {
        private readonly IAgentToolFactory _toolFactory;
        private readonly IAdminLogger _logger;
        private readonly IAgentToolRegistry _toolRegistry;

        public AgentToolExecutor(IAgentToolFactory agentToolFactory, IAgentToolRegistry agentToolRegistry, IAdminLogger logger)
        {
            _toolFactory = agentToolFactory ?? throw new ArgumentNullException(nameof(agentToolFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _toolRegistry = agentToolRegistry ?? throw new ArgumentNullException(nameof(agentToolRegistry));
        }

        public async Task<InvokeResult<AgentToolCallResult>> ExecuteServerToolAsync(AgentToolCall call, IAgentPipelineContext context)
        {
            if (call == null)
            {
                throw new ArgumentNullException(nameof(call));
            }
    
            if (string.IsNullOrWhiteSpace(call.Name))
            {
                var errorMessage = "Tool call name is empty.";
                _logger.AddError("[AgentToolExecutor_ExecuteServerToolAsync__EmptyName]", errorMessage);

                return InvokeResult<AgentToolCallResult>.FromError($"[AgentToolExecutor_ExecuteServerToolAsync__EmptyName] {errorMessage}");
            }

            _logger.Trace($"[AgentToolExecutor_ExecuteServerToolAsync] Tool '{call.Name}' Was Called, Starting Execution with Arguments\r\n{call.ArgumentsJson}\r\n");
           
            var toolResult = _toolFactory.GetTool(call.Name);
            if (!toolResult.Successful)
            {
                var errorMessage = toolResult.ErrorMessage ?? "Failed to resolve server tool.";                
                _logger.AddError(
                    "[AgentToolExecutor_ExecuteServerToolAsync__ResolveFailed]",
                    $"Tool '{call.Name}' resolve failed: {errorMessage}");

                return InvokeResult<AgentToolCallResult>.FromInvokeResult(toolResult.ToInvokeResult());
            }

            var tool = toolResult.Result;
            if (tool == null)
            {
                var errorMessage = $"Tool '{call.Name}' resolved to null instance.";
                _logger.AddError("[AgentToolExecutor_ExecuteServerToolAsync__NullInstance]", errorMessage);

                return InvokeResult<AgentToolCallResult>.FromError($"[AgentToolExecutor_ExecuteServerToolAsync__NullInstance] {errorMessage}");
            }

            try
            {
                var sw = Stopwatch.StartNew();
                var execResult = await tool.ExecuteAsync(call.ArgumentsJson, context);

                var result = new AgentToolCallResult()
                {
                    ToolCallId = call.ToolCallId,
                    Name = call.Name,
                };

                if (!execResult.Successful)
                {
                    var errorMessage = execResult.ErrorMessage;

                    _logger.AddError(
                        "[AgentToolExecutor_ExecuteServerToolAsync__ToolFailed]",
                        $"Tool '{call.Name}' execution failed: {errorMessage}");


                    return InvokeResult<AgentToolCallResult>.FromInvokeResult(execResult.ToInvokeResult());
                }
                else
                {
                    result.ExecutionMs = Convert.ToInt32(sw.Elapsed.TotalMilliseconds);
                    result.ResultJson = execResult.Result;
                }

                result.RequiresClientExecution = !tool.IsToolFullyExecutedOnServer;
             
                _logger.Trace($"[AgentToolExecutor_ExecuteServerToolAsync] Tool '{call.Name}' Was Successfully Executed in {sw.Elapsed.TotalMilliseconds}ms\r\n");

                return InvokeResult<AgentToolCallResult>.Create(result);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                var errorMessage = $"Tool '{call.Name}' execution was cancelled.";

                _logger.AddError(
                    "[AgentToolExecutor_ExecuteServerToolAsync__Cancelled]",
                    errorMessage);

                return InvokeResult<AgentToolCallResult>.FromError($"[AgentToolExecutor_ExecuteServerToolAsync__Cancelled] {errorMessage}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Tool '{call.Name}' threw an exception: {ex.Message}";

                _logger.AddException("[AgentToolExecutor_ExecuteServerToolAsync__Exception]", ex);


                return InvokeResult<AgentToolCallResult>.FromError($"[AgentToolExecutor_ExecuteServerToolAsync__Cancelled] errorMessage");
            }
        }
    }
}
