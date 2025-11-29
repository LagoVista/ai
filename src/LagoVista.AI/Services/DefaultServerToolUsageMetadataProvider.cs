using System;
using System.Reflection;
using System.Text;
using LagoVista.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Default implementation that inspects all registered IAgentTool types and
    /// extracts their public const string ToolUsageMetadata.
    ///
    /// The result is a single, delimited text block that can be injected into
    /// the system role when constructing the LLM /responses request.
    /// </summary>
    public class DefaultServerToolUsageMetadataProvider : IServerToolUsageMetadataProvider
    {
        private readonly IAgentToolRegistry _toolRegistry;
        private readonly IAdminLogger _logger;

        public DefaultServerToolUsageMetadataProvider(IAgentToolRegistry toolRegistry, IAdminLogger logger)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string GetToolUsageMetadata()
        {
            var registered = _toolRegistry.GetRegisteredTools();

            var sb = new StringBuilder();

            sb.AppendLine("<<<APTIX_SERVER_TOOL_USAGE_METADATA_BEGIN>>>");

            foreach (var kvp in registered)
            {
                var toolName = kvp.Key;
                var toolType = kvp.Value;

                try
                {
                    var usageField = toolType.GetField(
                        "ToolUsageMetadata",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                    // This should already be enforced by AgentToolRegistry.RegisterTool,
                    // but we keep a defensive check and log if it is not.
                    if (usageField == null ||
                        usageField.FieldType != typeof(string) ||
                        usageField.IsLiteral == false)
                    {
                        _logger.AddError(
                            "[DefaultServerToolUsageMetadataProvider_GetToolUsageMetadata__MissingUsageField]",
                            "Tool '" + toolType.FullName + "' registered as '" + toolName +
                            "' does not expose a valid public const string ToolUsageMetadata.");
                        continue;
                    }

                    var usageMetadata = usageField.GetRawConstantValue() as string;

                    if (string.IsNullOrWhiteSpace(usageMetadata))
                    {
                        _logger.AddError(
                            "[DefaultServerToolUsageMetadataProvider_GetToolUsageMetadata__EmptyUsageMetadata]",
                            "Tool '" + toolType.FullName + "' registered as '" + toolName +
                            "' exposes an empty ToolUsageMetadata constant.");
                        continue;
                    }

                    // Delimited block for this specific tool.
                    sb.AppendLine("<<<APTIX_TOOL_USAGE_BEGIN name='" + toolName + "'>>>");
                    sb.AppendLine(usageMetadata.Trim());
                    sb.AppendLine("<<<APTIX_TOOL_USAGE_END name='" + toolName + "'>>>");
                    sb.AppendLine();
                }
                catch (Exception ex)
                {
                    _logger.AddException(
                        "[DefaultServerToolUsageMetadataProvider_GetToolUsageMetadata__Exception]",
                        ex);
                }
            }

            sb.AppendLine("<<<APTIX_SERVER_TOOL_USAGE_METADATA_END>>>");

            return sb.ToString();
        }
    }
}
