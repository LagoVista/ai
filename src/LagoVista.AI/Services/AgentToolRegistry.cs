using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.RegularExpressions;
using LagoVista.AI.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    public class AgentToolRegistry : IAgentToolRegistry
    {
        private readonly Dictionary<string, Type> _toolsByName;
        private readonly IAdminLogger _logger;

        public AgentToolRegistry(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _toolsByName = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        }

        public void RegisterTool<T>() where T : IAgentTool
        {
            var toolType = typeof(T);

            //
            // CONTRACT #1:
            //   The tool MUST define: public const string ToolName
            //
            var toolNameField = toolType.GetField(
                "ToolName",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            if (toolNameField == null ||
                toolNameField.FieldType != typeof(string) ||
                toolNameField.IsLiteral == false)
            {
                var msg =
                    $"Tool '{toolType.FullName}' must declare: public const string ToolName.";

                _logger.AddError("[AgentToolRegistry_RegisterTool__MissingToolNameConst]", msg);
                throw new InvalidOperationException(msg);
            }

            var toolName = toolNameField.GetRawConstantValue() as string;

            if (string.IsNullOrWhiteSpace(toolName))
            {
                var msg =
                    $"Tool '{toolType.FullName}' declares an empty ToolName constant.";

                _logger.AddError("[AgentToolRegistry_RegisterTool__EmptyToolNameConst]", msg);
                throw new InvalidOperationException(msg);
            }

            //
            // NEW CONTRACT: ToolName must be OpenAI-compliant:
            //   ^[a-zA-Z0-9_-]+$
            //
            var namePattern = new Regex("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

            if (!namePattern.IsMatch(toolName))
            {
                var msg =
                    $"Tool '{toolType.FullName}' declares ToolName='{toolName}', which " +
                    $"does not match the required pattern '^[a-zA-Z0-9_-]+$' " +
                    $"for OpenAI tool names.";

                _logger.AddError("[AgentToolRegistry_RegisterTool__InvalidToolNamePattern]", msg);
                throw new InvalidOperationException(msg);
            }

            //
            // CONTRACT #2:
            //   The tool MUST declare: public static object GetSchema()
            //
            var schemaMethod = toolType.GetMethod(
                "GetSchema",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            if (schemaMethod == null ||
                schemaMethod.ReturnType != typeof(object) ||
                schemaMethod.GetParameters().Length != 0)
            {
                var msg =
                    $"Tool '{toolType.FullName}' must declare: public static object GetSchema().";

                _logger.AddError("[AgentToolRegistry_RegisterTool__MissingSchemaMethod]", msg);
                throw new InvalidOperationException(msg);
            }

            //
            // CONTRACT #3:
            //   The tool MUST declare: public const string ToolUsageMetadata (non-empty)
            //
            var usageMetadataField = toolType.GetField(
                "ToolUsageMetadata",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            if (usageMetadataField == null ||
                usageMetadataField.FieldType != typeof(string) ||
                usageMetadataField.IsLiteral == false)
            {
                var msg =
                    $"Tool '{toolType.FullName}' must declare: public const string ToolUsageMetadata.";

                _logger.AddError("[AgentToolRegistry_RegisterTool__MissingToolUsageMetadataConst]", msg);
                throw new InvalidOperationException(msg);
            }

            var usageMetadata = usageMetadataField.GetRawConstantValue() as string;

            if (string.IsNullOrWhiteSpace(usageMetadata))
            {
                var msg =
                    $"Tool '{toolType.FullName}' declares an empty ToolUsageMetadata constant.";

                _logger.AddError("[AgentToolRegistry_RegisterTool__EmptyToolUsageMetadataConst]", msg);
                throw new InvalidOperationException(msg);
            }

            //
            // CONTRACT #4:
            //   Prevent duplicate tool names
            //
            if (_toolsByName.ContainsKey(toolName))
            {
                var existingType = _toolsByName[toolName];

                var msg =
                    $"Duplicate IAgentTool name '{toolName}'. " +
                    $"Existing type: '{existingType.FullName}', " +
                    $"Duplicate type: '{toolType.FullName}'.";

                _logger.AddError("[AgentToolRegistry_RegisterTool__DuplicateToolName]", msg);
                throw new InvalidOperationException(msg);
            }

            //
            // VALIDATION PASSED — REGISTER TOOL
            //
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

        public Type GetToolType(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                throw new ArgumentNullException(nameof(toolName));
            }

            if (!_toolsByName.TryGetValue(toolName, out var type))
            {
                throw new KeyNotFoundException($"Tool '{toolName}' is not registered.");
            }

            return type;
        }

        public IReadOnlyDictionary<string, Type> GetRegisteredTools()
        {
            // Return a read-only wrapper to avoid external mutation.
            return new ReadOnlyDictionary<string, Type>(_toolsByName);
        }
    }
}
