using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Tool that changes the mode for the current agent session.
    /// </summary>
    public sealed class ModeChangeTool : IAgentTool
    {
        public const string ToolName = "agent_change_mode";

        public const string ToolUsageMetadata =
@"Changes the current agent session mode. 
Call immediately when the user explicitly requests a mode change; otherwise ask first
If already in the requested mode, do not call.
Provide a short reason. Set branch=true when starting a separate session.
";

        private readonly IAgentSessionManager _sessionManager;
        private readonly IAdminLogger _logger;

        public const string ToolSummary = "use this tool to change an agent mode that is used to customize the capabilities of the agent";

        public ModeChangeTool(IAgentSessionManager sessionManager, IAdminLogger logger)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => ToolName;

        public bool IsToolFullyExecutedOnServer => true;

        private sealed class ModeChangeArgs
        {
            [JsonProperty("mode")]
            public string Mode { get; set; }

            [JsonProperty("branch")]
            public bool? Branch { get; set; }

            [JsonProperty("reason")]
            public string Reason { get; set; }
        }

        private sealed class ModeChangeResult
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("errorMessage")]
            public string ErrorMessage { get; set; }


            [JsonProperty("canRetry")]
            public bool CanRetry { get; set; }

            [JsonProperty("mode")]
            public string Mode { get; set; }

            [JsonProperty("branch")]
            public bool Branch { get; set; }

            [JsonProperty("reason")]
            public string Reason { get; set; }
        }


        public Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext ctx)

        {
            Console.WriteLine($"----\r\n{argumentsJson}\r\n---");

            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return Task.FromResult(InvokeResult<string>.FromError("ModeChangeTool requires a non-empty arguments object."));
            }
          
            try
            {
                var args = JsonConvert.DeserializeObject<ModeChangeArgs>(argumentsJson) ?? new ModeChangeArgs();

                if (string.IsNullOrWhiteSpace(args.Mode))
                {
                    return Task.FromResult(InvokeResult<string>.FromError("ModeChangeTool requires a non-empty 'mode' string."));
                }

                if (!args.Branch.HasValue)
                {
                    return Task.FromResult(InvokeResult<string>.FromError("ModeChangeTool requires a 'branch' boolean flag."));
                }

                if (string.IsNullOrWhiteSpace(args.Reason))
                {
                    return Task.FromResult(InvokeResult<string>.FromError( "ModeChangeTool requires a non-empty 'reason' string explaining why the mode change is needed."));
                }

                var mode = ctx.AgentContext.AgentModes.SingleOrDefault(md => md.Key == args.Mode);
                if(mode == null)
                {
                    var failedResult = new ModeChangeResult
                    {
                        Success = false,
                        CanRetry = true,
                        ErrorMessage = $"Mode [{args.Mode}] not found, available modes are {String.Join(",", ctx.AgentContext.AgentModes.Select(md => md.Key)) }"
                    };

   
                    var modeChangeFailedJson = JsonConvert.SerializeObject(failedResult);
                    return Task.FromResult(InvokeResult<string>.Create(modeChangeFailedJson));
                }

                var previousMode = ctx.Session.Mode;

                ctx.Session.ModeHistory.Add(new ModeHistory()
                {
                    PreviousMode = previousMode,
                    NewMode = args.Mode,
                    Reason = args.Reason,
                    TimeStamp = ctx.TimeStamp,
                });

                ctx.Session.Mode = args.Mode;
                ctx.Session.AgentMode = mode.ToEntityHeader();
                ctx.Session.ModeReason = args.Reason;
                ctx.Session.ModeSetTimestamp = ctx.TimeStamp;
                ctx.Session.LastUpdatedDate = ctx.TimeStamp;
                ctx.AttachAgentContext(ctx.AgentContext, ctx.Role, mode);

                var result = new ModeChangeResult
                {
                    Success = true,
                    Mode = args.Mode,
                    Branch = args.Branch.Value,
                    Reason = args.Reason
                };

                _logger.Trace($"[ModeChangeTool_ExecuteAsync] - Changed mode via tool from {previousMode} to {args.Mode}"); 

                var json = JsonConvert.SerializeObject(result);
                return Task.FromResult(InvokeResult<string>.Create(json));
            }
            catch (Exception ex)
            {
                _logger.AddException("[ModeChangeTool_ExecuteAsync__Exception]", ex);

                return Task.FromResult(InvokeResult<string>.FromError( "ModeChangeTool failed to change the session mode." + ex.Message));
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Changes the mode for the current agent session to the specified mode string. " +
                "Call only after the user confirms a mode change, and provide a short 'reason' " +
                "describing why this mode fits the current request. Set branch=true when the user " +
                "wants the new work to start as a separate session.",
                p =>
                {
                    p.String(
                        "mode",
                        "Target mode name for the current session. Must be a non-empty string.",
                        required: true);

                    p.Boolean(
                        "branch",
                        "Whether to branch into a new session.",
                        required: true);

                    p.String(
                        "reason",
                        "Short explanation of why this mode is appropriate.",
                        required: true);
                });
        }

    }
}
