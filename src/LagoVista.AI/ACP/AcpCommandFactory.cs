using System;
using LagoVista.AI.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.Extensions.DependencyInjection;

namespace LagoVista.AI.ACP
{
    public class AcpCommandFactory : IAcpCommandFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAcpCommandRegistry _commandRegistry;
        private readonly IAdminLogger _logger;

        public AcpCommandFactory(IServiceProvider serviceProvider, IAcpCommandRegistry commandRegistry, IAdminLogger adminLogger)
        {
            _logger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        }

        public InvokeResult<IAcpCommand> GetCommand(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
            {
                const string msg = "ACP command id is required.";
                _logger.AddError("[AcpCommandFactory_GetCommand__EmptyId]", msg);

                return InvokeResult<IAcpCommand>.FromError(msg, "ACP_COMMAND_EMPTY_ID");
            }

            if (!_commandRegistry.HasCommand(commandId))
            {
                var msg = $"ACP command '{commandId}' is not registered in AcpCommandRegistry.";
                _logger.AddError("[AcpCommandFactory_GetCommand__NotFound]", msg);

                return InvokeResult<IAcpCommand>.FromError(msg, "ACP_COMMAND_NOT_FOUND");
            }

            var cmdType = _commandRegistry.GetCommandType(commandId);

            try
            {
                var instance = ActivatorUtilities.CreateInstance(_serviceProvider, cmdType) as IAcpCommand;

                if (instance == null)
                {
                    var msg = $"Failed to create instance of ACP command '{commandId}' from type '{cmdType.FullName}'.";
                    _logger.AddError("[AcpCommandFactory_GetCommand__NullInstance]", msg);

                    return InvokeResult<IAcpCommand>.FromError(msg, "ACP_COMMAND_CREATE_FAILED");
                }

                return InvokeResult<IAcpCommand>.Create(instance);
            }
            catch (Exception ex)
            {
                var msg = $"Exception while creating ACP command '{commandId}' from type '{cmdType.FullName}'.";
                _logger.AddException("[AcpCommandFactory_GetCommand__Exception]", ex);

                return InvokeResult<IAcpCommand>.FromError($"{msg} {ex.Message}", "ACP_COMMAND_CREATE_EXCEPTION");
            }
        }
    }
}