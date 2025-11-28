using System;
using LagoVista.AI.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.Extensions.DependencyInjection;

namespace LagoVista.AI.Services
{
    public class AgentToolFactory : IAgentToolFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAgentToolRegistry _toolRegistry;
        private readonly IAdminLogger _logger;

        public AgentToolFactory(IServiceProvider serviceProvider, IAgentToolRegistry toolRegistry, IAdminLogger adminLogger)
        {
            _logger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        }

        public InvokeResult<IAgentTool> GetTool(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                const string msg = "Tool name is required.";
                _logger.AddError("[AgentToolRegistry_GetTool__EmptyName]", msg);

                return InvokeResult<IAgentTool>.FromError(msg, "AGENT_TOOL_EMPTY_NAME");
            }

            if (!_toolRegistry.HasTool(toolName))
            {
                var msg = $"Tool '{toolName}' is not registered in AgentToolRegistry.";
                _logger.AddError("[AgentToolRegistry_GetTool__NotFound]", msg);

                return InvokeResult<IAgentTool>.FromError(msg, "AGENT_TOOL_NOT_FOUND");
            }

            var toolType = _toolRegistry.GetToolType(toolName);

            try
            {
                var instance = _serviceProvider.GetService(toolType) as IAgentTool;

                if (instance == null)
                {
                    var msg = $"Failed to create instance of tool '{toolName}' from type '{toolType.FullName}'.";
                    _logger.AddError("[AgentToolRegistry_GetTool__NullInstance]", msg);

                    return InvokeResult<IAgentTool>.FromError(msg, "AGENT_TOOL_CREATE_FAILED");
                }

                return InvokeResult<IAgentTool>.Create(instance);
            }
            catch (Exception ex)
            {
                var msg = $"Exception while creating tool '{toolName}' from type '{toolType.FullName}'.";
                _logger.AddException("[AgentToolRegistry_GetTool__Exception]", ex);

                return InvokeResult<IAgentTool>.FromError($"{msg} {ex.Message}", "AGENT_TOOL_CREATE_EXCEPTION");
            }
        }
    }
}
