using System;
using System.Drawing;
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
            @"Use this tool to change the current agent session mode, if it was explicilty stated
             you can chnage modes immediately wihtout confirmation, however if the user wants
             to do something better supported by a different mode you should ask them first.
             after switching to the new mode, you should display the Welcome Message assocaited
             with the new mode.";

        private readonly IAgentSessionManager _sessionManager;
        private readonly IAdminLogger _logger;

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

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext ctx)

        {
            Console.WriteLine($"----\r\n{argumentsJson}\r\n---");

            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return InvokeResult<string>.FromError("ModeChangeTool requires a non-empty arguments object.");
            }
          
            try
            {
                var args = JsonConvert.DeserializeObject<ModeChangeArgs>(argumentsJson) ?? new ModeChangeArgs();

                if (string.IsNullOrWhiteSpace(args.Mode))
                {
                    return InvokeResult<string>.FromError(
                        "ModeChangeTool requires a non-empty 'mode' string.");
                }

                if (!args.Branch.HasValue)
                {
                    return InvokeResult<string>.FromError(
                        "ModeChangeTool requires a 'branch' boolean flag.");
                }

                if (string.IsNullOrWhiteSpace(args.Reason))
                {
                    return InvokeResult<string>.FromError(
                        "ModeChangeTool requires a non-empty 'reason' string explaining why the mode change is needed.");
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
                ctx.Session.ModeReason = args.Reason;
                ctx.Session.ModeSetTimestamp = ctx.TimeStamp;
                ctx.Session.LastUpdatedDate = ctx.TimeStamp;

                var result = new ModeChangeResult
                {
                    Success = true,
                    Mode = args.Mode,
                    Branch = args.Branch.Value,
                    Reason = args.Reason
                };

                _logger.Trace($"[ModeChangeTool_ExecuteAsync] - Changed mode via tool from {previousMode} to {args.Mode}"); 

                var json = JsonConvert.SerializeObject(result);
                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException("[ModeChangeTool_ExecuteAsync__Exception]", ex);

                return InvokeResult<string>.FromError(
                    "ModeChangeTool failed to change the session mode.");
            }
        }

        public static object GetSchema()
        {
            return new
            {
                type = "function",
                name = ToolName,
                description =
                    "Changes the mode for the current agent session to the specified mode string. " +
                    "Call only after the user confirms a mode change, and provide a short 'reason' " +
                    "describing why this mode fits the current request. Set branch=true when the user " +
                    "wants the new work to start as a separate session.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        mode = new
                        {
                            type = "string",
                            description = "Target mode name for the current session. Must be a non-empty string."
                        },
                        branch = new
                        {
                            type = "boolean",
                            description = "Whether the caller intends to branch into a new session for the new mode (true) or continue in this session (false)."
                        },
                        reason = new
                        {
                            type = "string",
                            description = "Short explanation of why this mode is appropriate for the current user request."
                        }
                    },
                    required = new[] { "mode", "branch", "reason" }
                }
            };
        }
    }
}
