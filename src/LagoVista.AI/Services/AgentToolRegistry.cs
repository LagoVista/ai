using System;
using System.Collections.Generic;
using LagoVista.AI.Interfaces;
using LagoVista.Core.IOC;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Default implementation of IAgentToolRegistry.
    ///
    /// Stores a mapping of tool name -> concrete IAgentTool Type and uses
    /// SLWIOC.CreateForType(...) to construct tool instances on demand.
    /// </summary>
    public class AgentToolRegistry : IAgentToolRegistry
    {
        private readonly Dictionary<string, Type> _toolsByName;
        private readonly IAdminLogger _logger;

        public AgentToolRegistry(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _toolsByName = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        }

        public void RegisterTool<T>(string toolName) where T : IAgentTool
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                _logger.AddError(
                    "[AgentToolRegistry_RegisterTool__EmptyName]",
                    $"Attempted to register tool '{typeof(T).FullName}' with an empty toolName.");
                return;
            }

            var toolType = typeof(T);

            if (_toolsByName.ContainsKey(toolName))
            {
                var existingType = _toolsByName[toolName];

                _logger.AddError(
                    "[AgentToolRegistry_RegisterTool__DuplicateToolName]",
                    $"Duplicate IAgentTool name '{toolName}'. " +
                    $"Existing type: '{existingType.FullName}', " +
                    $"Duplicate type: '{toolType.FullName}'. Ignoring duplicate.");
                return;
            }

            _toolsByName.Add(toolName, toolType);

            _logger.Trace(
                $"[AgentToolRegistry_RegisterTool] Registered tool '{toolName}' -> '{toolType.FullName}'.");
        }

        public bool HasTool(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return false;
            }

            return _toolsByName.ContainsKey(toolName);
        }

        public InvokeResult<IAgentTool> GetTool(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                const string msg = "Tool name is required.";
                _logger.AddError("[AgentToolRegistry_GetTool__EmptyName]", msg);

                return InvokeResult<IAgentTool>.FromError(msg, "AGENT_TOOL_EMPTY_NAME");
            }

            if (!_toolsByName.TryGetValue(toolName, out var toolType))
            {
                var msg = $"Tool '{toolName}' is not registered in AgentToolRegistry.";
                _logger.AddError("[AgentToolRegistry_GetTool__NotFound]", msg);

                return InvokeResult<IAgentTool>.FromError(msg, "AGENT_TOOL_NOT_FOUND");
            }

            try
            {
                var instance = SLWIOC.CreateForType(toolType) as IAgentTool;
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
