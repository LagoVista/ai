using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LagoVista.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    public class DefaultServerToolSchemaProvider : IServerToolSchemaProvider
    {
        private readonly IAgentToolRegistry _toolRegistry;
        private readonly IAdminLogger _logger;

        public DefaultServerToolSchemaProvider(IAgentToolRegistry toolRegistry, IAdminLogger logger)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        /// <summary>
        /// Returns schemas for the specified set of tool names. Unrecognized
        /// tool names are ignored.
        /// </summary>
        public IReadOnlyList<object> GetToolSchemas(List<string> toolNames)
        {
            var schemas = new List<object>();

            if (toolNames == null)
            {
                return schemas;
            }

            var requestedNames = new HashSet<string>(toolNames, StringComparer.OrdinalIgnoreCase);
            if (requestedNames.Count == 0)
            {
                return schemas;
            }

            var registered = _toolRegistry.GetRegisteredTools();

            foreach (var kvp in registered)
            {
                var toolName = kvp.Key;
                var toolType = kvp.Value;

                // Skip tools that were not requested.
                if (!requestedNames.Contains(toolName))
                {
                    continue;
                }

                TryAddSchemaForTool(schemas, toolName, toolType);
            }

            return schemas;
        }

        /// <summary>
        /// Returns a single schema for the specified tool name, or null if the
        /// tool is not registered or a schema cannot be obtained.
        /// </summary>
        public object GetToolSchema(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return null;
            }

            var registered = _toolRegistry.GetRegisteredTools();

            if (!registered.TryGetValue(toolName, out var toolType))
            {
                _logger.AddError(
                    "[DefaultServerToolSchemaProvider_GetToolSchema__UnknownTool]",
                    $"No server tool is registered with the name '{toolName}'.");
                return null;
            }

            var schemas = new List<object>();
            TryAddSchemaForTool(schemas, toolName, toolType);

            return schemas.FirstOrDefault();
        }

        /// <summary>
        /// Shared helper to reflect and add a schema for a single tool type.
        /// </summary>
        private void TryAddSchemaForTool(ICollection<object> target, string toolName, Type toolType)
        {
            try
            {
                var schemaMethod = toolType.GetMethod(
                    "GetSchema",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                // This *should* always be valid thanks to AgentToolRegistry.RegisterTool,
                // but we keep a defensive check and log if it isn’t.
                if (schemaMethod == null ||
                    schemaMethod.ReturnType != typeof(object) ||
                    schemaMethod.GetParameters().Length != 0)
                {
                    _logger.AddError(
                        "[DefaultServerToolSchemaProvider_GetToolSchemas__InvalidSchemaMethod]",
                        $"Tool '{toolType.FullName}' registered as '{toolName}' " +
                        "does not expose a valid public static object GetSchema().");
                    return;
                }

                var schema = schemaMethod.Invoke(null, null);
                if (schema != null)
                {
                    target.Add(schema);
                }
            }
            catch (Exception ex)
            {
                _logger.AddException(
                    "[DefaultServerToolSchemaProvider_GetToolSchemas__Exception]",
                    ex);
            }
        }
    }
}
