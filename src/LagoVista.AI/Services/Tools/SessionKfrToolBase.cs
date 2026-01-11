using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    public abstract class SessionKfrToolBase : IAgentTool
    {
        protected readonly IAdminLogger Logger;
        protected readonly IAgentSessionManager Sessions;

        protected SessionKfrToolBase(IAdminLogger logger, IAgentSessionManager sessions)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        public abstract string Name { get; }
        public virtual bool IsToolFullyExecutedOnServer => true;

        public virtual Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
            => throw new NotImplementedException("Use ExecuteAsync(string, IAgentPipelineContext).");

        public abstract Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context);

        protected static Task<InvokeResult<string>> Fail(string message)
            => Task.FromResult(InvokeResult<string>.FromError(message));

        protected static void EnsureBranch(IAgentPipelineContext context)
        {
            if (context?.Session == null)
                return;

            if (string.IsNullOrWhiteSpace(context.Session.CurrentBranch))
                context.Session.CurrentBranch = AgentSession.DefaultBranch;

            context.Session.Kfrs ??= new Dictionary<string, List<AgentSessionKfrEntry>>(StringComparer.OrdinalIgnoreCase);

            if (!context.Session.Kfrs.TryGetValue(context.Session.CurrentBranch, out var branchList) || branchList == null)
            {
                branchList = new List<AgentSessionKfrEntry>();
                context.Session.Kfrs[context.Session.CurrentBranch] = branchList;
            }
        }

        protected static List<AgentSessionKfrEntry> GetBranchList(IAgentPipelineContext context)
        {
            EnsureBranch(context);
            return context.Session.Kfrs[context.Session.CurrentBranch];
        }

        protected static string UtcStamp() => DateTime.UtcNow.ToJSONString();

        protected sealed class Result
        {
            public string Operation { get; set; }
            public List<AgentSessionKfrEntry> Items { get; set; }
            public string SessionId { get; set; }
        }

        protected static InvokeResult<string> Ok(string op, IAgentPipelineContext context, List<AgentSessionKfrEntry> items)
        {
            var result = new Result
            {
                Operation = op,
                Items = items ?? new List<AgentSessionKfrEntry>(),
                SessionId = context?.Session?.Id
            };

            return InvokeResult<string>.Create(JsonConvert.SerializeObject(result));
        }
    }
}
