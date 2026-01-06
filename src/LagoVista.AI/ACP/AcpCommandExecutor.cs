using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.ACP
{
    public class AcpCommandExecutor : IAcpCommandExecutor
    {
        private readonly IAcpCommandFactory _commandFactory;
        private readonly IAcpCommandRegistry _commandRegistry;
        private readonly IAdminLogger _logger;

        public AcpCommandExecutor(
            IAcpCommandFactory commandFactory,
            IAcpCommandRegistry commandRegistry,
            IAdminLogger logger)
        {
            _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
            _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InvokeResult<IAgentPipelineContext>> ExecuteAsync(string commandId, IAgentPipelineContext context, string[] args)
        {
            if (String.IsNullOrWhiteSpace(commandId))
            {
                var msg = "ACP commandId is empty.";
                _logger.AddError("[AcpCommandExecutor_ExecuteAsync__EmptyCommandId]", msg);
                return InvokeResult<IAgentPipelineContext>.FromError(msg);
            }

            if (context == null) throw new ArgumentNullException(nameof(context));
            if (args == null) args = Array.Empty<string>();

            if (!_commandRegistry.HasCommand(commandId))
            {
                var msg = $"ACP command '{commandId}' is not registered.";
                _logger.AddError("[AcpCommandExecutor_ExecuteAsync__NotRegistered]", msg);
                return InvokeResult<IAgentPipelineContext>.FromError(msg);
            }

            _logger.Trace($"[AcpCommandExecutor_ExecuteAsync] ACP Command '{commandId}' matched. Executing with args: [{String.Join(", ", args)}]");

            var cmdResult = _commandFactory.GetCommand(commandId);
            if (!cmdResult.Successful)
            {
                var msg = cmdResult.ErrorMessage ?? $"Failed to resolve ACP command '{commandId}'.";
                _logger.AddError("[AcpCommandExecutor_ExecuteAsync__ResolveFailed]", msg);
                return InvokeResult<IAgentPipelineContext>.FromInvokeResult(cmdResult.ToInvokeResult());
            }

            var cmd = cmdResult.Result;
            if (cmd == null)
            {
                var msg = $"ACP command '{commandId}' resolved to null instance.";
                _logger.AddError("[AcpCommandExecutor_ExecuteAsync__NullInstance]", msg);
                return InvokeResult<IAgentPipelineContext>.FromError(msg);
            }

            try
            {
                var sw = Stopwatch.StartNew();
                var execResult = await cmd.ExecuteAsync(context, args);

                if (!execResult.Successful)
                {
                    var msg = execResult.ErrorMessage ?? $"ACP command '{commandId}' execution failed.";
                    _logger.AddError("[AcpCommandExecutor_ExecuteAsync__CommandFailed]", msg);
                    return InvokeResult<IAgentPipelineContext>.FromInvokeResult(execResult.ToInvokeResult());
                }

                _logger.Trace($"[AcpCommandExecutor_ExecuteAsync] ACP Command '{commandId}' executed successfully in {sw.Elapsed.TotalMilliseconds}ms.");
                return execResult;
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                var msg = $"ACP command '{commandId}' execution was cancelled.";
                _logger.AddError("[AcpCommandExecutor_ExecuteAsync__Cancelled]", msg);
                return InvokeResult<IAgentPipelineContext>.FromError(msg);
            }
            catch (Exception ex)
            {
                var msg = $"ACP command '{commandId}' threw an exception: {ex.Message}";
                _logger.AddException("[AcpCommandExecutor_ExecuteAsync__Exception]", ex);
                return InvokeResult<IAgentPipelineContext>.FromError(msg);
            }
        }
    }
}