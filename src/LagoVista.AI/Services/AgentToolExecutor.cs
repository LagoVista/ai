using System;
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

        public async Task<InvokeResult<AgentToolCall>> ExecuteServerToolAsync(
            AgentToolCall call,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (call == null)
            {
                throw new ArgumentNullException(nameof(call));
            }

            // Default: assume not a server tool until proven otherwise.
            call.WasExecuted = false;

            if (string.IsNullOrWhiteSpace(call.Name))
            {
                call.ErrorMessage = "Tool call name is empty.";
                _logger.AddError("[AgentToolExecutor_ExecuteServerToolAsync__EmptyName]", call.ErrorMessage);

                return InvokeResult<AgentToolCall>.FromError($"[AgentToolExecutor_ExecuteServerToolAsync__EmptyName] {call.ErrorMessage}");
            }

            // If the registry does not know this tool, it's a client-only tool.
            if (!_toolRegistry.HasTool(call.Name))
            {
                _logger.Trace(
                    $"[AgentToolExecutor_ExecuteServerToolAsync] Tool '{call.Name}' not registered as a server tool. " +
                    "Leaving for client execution.");

                return InvokeResult<AgentToolCall>.FromError($"[AgentToolExecutor_ExecuteServerToolAsync] Tool '{call.Name}' not registered as a server tool. " +
                    "Leaving for client execution.");
            }


            var toolResult = _toolFactory.GetTool(call.Name);
            if (!toolResult.Successful)
            {
                call.ErrorMessage = toolResult.ErrorMessage ?? "Failed to resolve server tool.";
                _logger.AddError(
                    "[AgentToolExecutor_ExecuteServerToolAsync__ResolveFailed]",
                    $"Tool '{call.Name}' resolve failed: {call.ErrorMessage}");

                return InvokeResult<AgentToolCall>.FromInvokeResult(toolResult.ToInvokeResult());
            }

            var tool = toolResult.Result;
            call.IsServerTool = tool.IsToolFullyExecutedOnServer;
            if (tool == null)
            {
                call.ErrorMessage = $"Tool '{call.Name}' resolved to null instance.";
                _logger.AddError("[AgentToolExecutor_ExecuteServerToolAsync__NullInstance]", call.ErrorMessage);

                return InvokeResult<AgentToolCall>.FromError($"[AgentToolExecutor_ExecuteServerToolAsync__NullInstance] {call.ErrorMessage}");
            }

            try
            {
                var execResult = await tool.ExecuteAsync(call.ArgumentsJson, context, cancellationToken);

                call.WasExecuted = execResult.Successful;
                call.ResultJson = execResult.Successful ? execResult.Result : null;
                call.ErrorMessage = execResult.Successful ? null : execResult.ErrorMessage;

                if (!execResult.Successful)
                {
                    _logger.AddError(
                        "[AgentToolExecutor_ExecuteServerToolAsync__ToolFailed]",
                        $"Tool '{call.Name}' execution failed: {call.ErrorMessage}");

                    return InvokeResult<AgentToolCall>.FromInvokeResult(execResult.ToInvokeResult());
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                call.WasExecuted = false;
                call.ErrorMessage = $"Tool '{call.Name}' execution was cancelled.";

                _logger.AddError(
                    "[AgentToolExecutor_ExecuteServerToolAsync__Cancelled]",
                    call.ErrorMessage);
            }
            catch (Exception ex)
            {
                call.WasExecuted = false;
                call.ErrorMessage = $"Tool '{call.Name}' threw an exception: {ex.Message}";

                _logger.AddException("[AgentToolExecutor_ExecuteServerToolAsync__Exception]", ex);
            }

            return InvokeResult<AgentToolCall>.Create(call);
        }
    }
}
