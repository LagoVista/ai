using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Tool: Returns the current session's code file activity list.
    ///
    /// Notes:
    /// - Takes no parameters (session id is inferred from context.Session.Id).
    /// - Returns a JSON payload containing SessionId and Activities.
    /// </summary>
    public sealed class GetSessionCodeFileActivitiesTool : IAgentTool
    {
        /* --------------------------------------------------------------
         * REQUIRED CONSTANTS
         * -------------------------------------------------------------- */
        public const string ToolName = "get_code_session_files";
        public const string ToolUsageMetadata =
            "Returns the current session's code file activity list. " +
            "Use when the user asks what code files changed or wants recent code editing activity for this session.";
        public const string ToolSummary = "returns a list of session code file activities for the current session";

        /* --------------------------------------------------------------
         * DI CONSTRUCTOR
         * -------------------------------------------------------------- */
        private readonly IAdminLogger _logger;
        private readonly ISessionCodeFilesRepo _repo;

        public GetSessionCodeFileActivitiesTool(
            IAdminLogger logger,
            ISessionCodeFilesRepo repo)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        /* --------------------------------------------------------------
         * TOOL IDENTITY
         * -------------------------------------------------------------- */
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        /* --------------------------------------------------------------
         * INPUT/OUTPUT CONTRACT
         * -------------------------------------------------------------- */
        private sealed class GetSessionCodeFileActivitiesResult
        {
            public string SessionId { get; set; }
            public List<SessionCodeFileActivity> Activities { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            // Kept for interface compatibility.
            throw new NotImplementedException();
        }

        /* --------------------------------------------------------------
         * EXECUTION LOGIC
         * -------------------------------------------------------------- */
        public async Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            IAgentPipelineContext context)
        {
            if (context?.Session == null || string.IsNullOrWhiteSpace(context.Session.Id))
            {
                return InvokeResult<string>.FromError("GetSessionCodeFileActivitiesTool requires a valid session context.");
            }

            try
            {
                var sessionId = context.Session.Id;

                var activities = await _repo
                    .GetSessionCodeFileActivitiesAsync(sessionId)
                    .ConfigureAwait(false);

                var result = new GetSessionCodeFileActivitiesResult
                {
                    SessionId = sessionId,
                    Activities = activities ?? new List<SessionCodeFileActivity>()
                };

                var json = JsonConvert.SerializeObject(result);
                return InvokeResult<string>.Create(json);
            }
            catch (Exception ex)
            {
                _logger.AddException(this.Tag(), ex);

                return InvokeResult<string>.FromError("GetSessionCodeFileActivitiesTool failed to fetch session code file activities.");
            }
        }

        /* --------------------------------------------------------------
         * SCHEMA DEFINITION
         * -------------------------------------------------------------- */
        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Returns the current session's code file activity list.", _ =>{ });
        }
    }
}