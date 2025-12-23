using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Executes mode-entry bootstrap tools after a mode change has been applied.
    ///
    /// DDR: TUL-012 (Mode Change Tool Trigger)
    /// - Runs tools defined on AgentMode.BootStrapTool
    /// - Fail-fast: if any tool fails, stop and return failure
    /// - Bootstrap must NOT change modes (ModeChangeTool is disallowed)
    ///
    /// NOTE: This service is designed to be invoked by AgentReasoner.
    /// </summary>
    public class ModeEntryBootstrapService : IModeEntryBootstrapService
    {
        private readonly IAgentToolExecutor _toolExecutor;
        private readonly IAdminLogger _logger;

        public ModeEntryBootstrapService(IAgentToolExecutor toolExecutor, IAdminLogger logger)
        {
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InvokeResult<ModeEntryBootstrapDetails>> ExecuteAsync(
            ModeEntryBootstrapRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Mode == null) throw new ArgumentNullException(nameof(request.Mode));
            if (request.ToolContext == null) throw new ArgumentNullException(nameof(request.ToolContext));
            if (string.IsNullOrWhiteSpace(request.ModeKey)) throw new ArgumentNullException(nameof(request.ModeKey));

            var tag = "[ModeEntryBootstrapService__ExecuteAsync]";

            try
            {
                var tools = request.Mode.BootStrapTool ?? Array.Empty<BootStrapTool>();

                _logger.Trace(
                    $"{tag} Starting bootstrap. modeKey={request.ModeKey}, toolCount={tools.Length}");

                var details = new ModeEntryBootstrapDetails
                {
                    ModeKey = request.ModeKey,
                    ToolCount = tools.Length,
                    ExecutedTools = new List<ModeEntryBootstrapToolResult>()
                };

                foreach (var bootstrapTool in tools)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return InvokeResult<ModeEntryBootstrapDetails>.Abort();
                    }

                    if (bootstrapTool == null || string.IsNullOrWhiteSpace(bootstrapTool.ToolName))
                    {
                        var msg = "Bootstrap tool entry was null or missing ToolName.";
                        _logger.AddError("[ModeEntryBootstrapService__ExecuteAsync__InvalidBootstrapTool]", msg);
                        return InvokeResult<ModeEntryBootstrapDetails>.FromError(msg);
                    }

                    // Hard safety constraint per TUL-012: bootstrap cannot change modes.
                    // We block ModeChangeTool by name to avoid mode switching during bootstrap.
                    if (string.Equals(bootstrapTool.ToolName, "agent_change_mode", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(bootstrapTool.ToolName, "ModeChangeTool", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(bootstrapTool.ToolName, "TUL-007", StringComparison.OrdinalIgnoreCase))
                    {
                        var msg = $"Bootstrap attempted to call disallowed mode-change tool '{bootstrapTool.ToolName}'.";
                        _logger.AddError("[ModeEntryBootstrapService__ExecuteAsync__ModeChangeToolDisallowed]", msg);
                        return InvokeResult<ModeEntryBootstrapDetails>.FromError(msg);
                    }

                    var call = new AgentToolCall
                    {
                        Name = bootstrapTool.ToolName,
                        // ArgumentsJson is tool-specific. We avoid guessing shape.
                        // If arguments are provided as strings, we pass them through as a JSON array of strings.
                        ArgumentsJson = SerializeArgumentsAsJsonArray(bootstrapTool.Arguments)
                    };

                    _logger.Trace($"{tag} Executing bootstrap tool '{call.Name}'.");

                    //var result = await _toolExecutor.ExecuteServerToolAsync(call, request.ToolContext, cancellationToken);
                    //if (!result.Successful)
                    //{
                    //    _logger.LogInvokeResult("[ModeEntryBootstrapService__ExecuteAsync__ToolExecutorFailed]", result);
                    //    return InvokeResult<ModeEntryBootstrapDetails>.FromInvokeResult(result.ToInvokeResult());
                    //}

                    //var executed = result.Result;
                    //if (executed == null)
                    //{
                    //    var msg = $"Tool executor returned null AgentToolCall for tool '{call.Name}'.";
                    //    _logger.AddError("[ModeEntryBootstrapService__ExecuteAsync__NullToolResult]", msg);
                    //    return InvokeResult<ModeEntryBootstrapDetails>.FromError(msg);
                    //}

                    //// Per AgentReasoner semantics: WasExecuted=false indicates tool did not run logic.
                    //// For bootstrap, tools are expected to succeed; treat non-execution as failure.
                    //if (!executed.WasExecuted)
                    //{
                    //    var msg = $"Bootstrap tool '{executed.Name}' did not execute. err='{executed.ErrorMessage ?? ""}'.";
                    //    _logger.AddError("[ModeEntryBootstrapService__ExecuteAsync__ToolNotExecuted]", msg);
                    //    return InvokeResult<ModeEntryBootstrapDetails>.FromError(msg);
                    //}

                    //// If tool requires client execution, bootstrap cannot complete server-side.
                    //// Treat as failure because bootstrap tools are required to hydrate context.
                    //if (executed.RequiresClientExecution)
                    //{
                    //    var msg = $"Bootstrap tool '{executed.Name}' requires client execution, which is not allowed for bootstrap.";
                    //    _logger.AddError("[ModeEntryBootstrapService__ExecuteAsync__ClientExecutionNotAllowed]", msg);
                    //    return InvokeResult<ModeEntryBootstrapDetails>.FromError(msg);
                    //}

                    //details.ExecutedTools.Add(new ModeEntryBootstrapToolResult
                    //{
                    //    ToolName = executed.Name,
                    //    WasExecuted = executed.WasExecuted,
                    //    RequiresClientExecution = executed.RequiresClientExecution,
                    //    ResultJson = executed.ResultJson,
                    //    ErrorMessage = executed.ErrorMessage
                    //});
                }

                _logger.Trace($"{tag} Bootstrap complete. modeKey={request.ModeKey}, executed={details.ExecutedTools.Count}");
                return InvokeResult<ModeEntryBootstrapDetails>.Create(details);
            }
            catch (Exception ex)
            {
                return InvokeResult<ModeEntryBootstrapDetails>.FromException("ModeEntryBootstrapService.ExecuteAsync", ex);
            }
        }

        private static string SerializeArgumentsAsJsonArray(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return "[]";
            }

            // Minimal JSON array of strings without taking a dependency on JSON.NET here.
            // We only need to safely escape backslashes and quotes.
            var escaped = args.Select(a => a ?? string.Empty)
                              .Select(a => a.Replace("\\", "\\\\").Replace("\"", "\\\""))
                              .Select(a => $"\"{a}\"");

            return "[" + string.Join(",", escaped) + "]";
        }
    }

    public sealed class ModeEntryBootstrapRequest
    {
        public AgentMode Mode { get; set; }
        public string ModeKey { get; set; }
        public AgentToolExecutionContext ToolContext { get; set; }
    }

    public sealed class ModeEntryBootstrapDetails
    {
        public string ModeKey { get; set; }
        public int ToolCount { get; set; }
        public List<ModeEntryBootstrapToolResult> ExecutedTools { get; set; } = new List<ModeEntryBootstrapToolResult>();
    }

    public sealed class ModeEntryBootstrapToolResult
    {
        public string ToolName { get; set; }
        public bool WasExecuted { get; set; }
        public bool RequiresClientExecution { get; set; }
        public string ResultJson { get; set; }
        public string ErrorMessage { get; set; }
    }
}
