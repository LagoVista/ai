using System;
using System.Collections.Generic;
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
    /// Manage Known Facts Registry (KFR) entries for the current session.
    ///
    /// Supports:
    /// - list: return all active KFRs for human visibility
    /// - clear: truncate all KFRs (manual reset)
    /// - upsert: LLM-managed lifecycle updates (add/replace/remove)
    /// </summary>
    public sealed class SessionKfrTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IAgentSessionManager _sessions;
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolName = "session_kfr";
        public const string ToolSummary = "known facts registry - working memory";
        public const string ToolUsageMetadata = @"Tool Usage Instructions � Working Memory & Known Facts Registry (KFR)

Purpose:
Use the Working Memory update tool to maintain a small, authoritative Known Facts Registry (KFR) that defines what must remain correct for the next few turns.

When to Call the Tool:
Call the tool only when working state changes, including when:
- A new goal or plan is established or revised.
- A new Active Contract becomes relevant.
- A new constraint or invariant is identified.
- An open question is raised or resolved.
- Existing KFR content is no longer operationally required and should be removed (evict).
- The user signals importance using phrases such as �this is important�, �this is critical�, �this is a key insight�, or similar emphasis.

Do not call the tool if no working-state change occurred.

Active Contracts:
Active Contracts are binding rules, schemas, interfaces, identifiers, or process rules that must be followed exactly to avoid incorrect output or regressions.

An item is an Active Contract if violating it would:
- Cause a tool call to fail.
- Produce output that violates a required schema or format.
- Break a previously locked decision or invariant.
- Cause incompatibility with a consuming system, process, or policy.

If violating an item would not cause near-term incorrectness, it is not an Active Contract.

What Belongs in the KFR:
Add items to the KFR only if they are required for correctness in the next few turns:
- Goal: the current objective driving the work (single; replaces prior goal).
- Plan: the active steps being followed (single; replaces prior plan).
- Active Contracts: binding contracts that must be honored.
- Constraints & Invariants: rules that must not be violated.
- Open Questions: unresolved blockers (set requiresResolution=true).

What Must Never Go in the KFR:
Do not store:
- Rationale or reasoning history.
- Rejected alternatives.
- Examples, narrative, or explanatory text.
- Historical context that is no longer active.

Update Discipline:
- Use upsert to add/replace entries and evict to remove entries that are no longer required.
- Replace Goal and Plan when they materially change; do not keep multiple active Goal/Plan entries.
- Do not duplicate existing entries; if already present and still correct, do not re-add.
- Keep entries short, precise, and operational.

Replacement and Eviction:
- The KFR represents current working state and may change frequently.
- When working state changes, update the KFR so it reflects only the current state.
- Evict entries that are no longer operationally required.
- Do not evict entries with requiresResolution=true unless explicitly dismissing them (force=true).

Relationship to Durable Memory Notes:
- An item may exist in both the KFR and durable Memory Notes.
- The KFR copy governs current reasoning and execution.
- The Memory Note copy exists solely for long-term recall.

Conflict Handling:
- Only the system-injected KFR is authoritative.
- Ignore any prior conversational content not present in the injected KFR.
- If unsure whether an item qualifies for the KFR, omit it.
";
        public SessionKfrTool(IAdminLogger logger, IAgentSessionManager sessions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        private sealed class Args
        {
            public string Operation { get; set; } // list | clear | upsert | evict
            public KfrPayload Entry { get; set; }
            public List<string> KfrIds { get; set; }
            public bool? Force { get; set; }
        }

        private sealed class KfrPayload
        {
            public string KfrId { get; set; }
            public string Kind { get; set; }
            public string Value { get; set; }
            public bool RequiresResolution { get; set; }
        }

        private sealed class Result
        {
            public string Operation { get; set; }
            public List<AgentSessionKfrEntry> Items { get; set; }
            public string SessionId { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
                return InvokeResult<string>.FromError("session_kfr requires a valid execution context.");
            if (string.IsNullOrWhiteSpace(context.SessionId))
                return InvokeResult<string>.FromError("session_kfr requires a sessionId in the execution context.");
            try
            {
                var args = string.IsNullOrWhiteSpace(argumentsJson) ? new Args() : JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args();
                var op = args.Operation?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(op))
                    return InvokeResult<string>.FromError("session_kfr requires 'operation' (list|clear|upsert).");
                if (op == "list")
                {
                    var session = await _sessions.GetAgentSessionAsync(context.SessionId, context.Org, context.User);
                    var items = session.Kfrs[session.CurrentBranch].Where(k => k.IsActive).ToList() ?? new List<AgentSessionKfrEntry>();
                    return InvokeResult<string>.Create(JsonConvert.SerializeObject(new Result { Operation = "list", Items = items, SessionId = context.SessionId }));
                }

                if (op == "clear")
                {
                    var session = await _sessions.GetAgentSessionAsync(context.SessionId, context.Org, context.User);
                    session?.Kfrs[session.Mode]?.Clear();
                    await _sessions.UpdateKFRsAsync(session.Id, session.Mode, session.Kfrs[session.Mode], context.Org, context.User);
                    return InvokeResult<string>.Create(JsonConvert.SerializeObject(new Result { Operation = "clear", Items = new List<AgentSessionKfrEntry>(), SessionId = context.SessionId }));
                }

                if (op == "upsert")
                {
                    if (args.Entry == null)
                        return InvokeResult<string>.FromError("session_kfr upsert requires 'entry'.");
                    if (!Enum.TryParse<KfrKind>(args.Entry.Kind, true, out var kind))
                        return InvokeResult<string>.FromError($"Invalid KFR kind '{args.Entry.Kind}'.");
                    var session = await _sessions.GetAgentSessionAsync(context.SessionId, context.Org, context.User);
                    if (session == null)
                        return InvokeResult<string>.FromError("session_kfr could not load the current session.");
                    if (session.Kfrs == null)
                        return InvokeResult<string>.FromError("session_kfr session has no KFR store initialized.");
                    if (string.IsNullOrWhiteSpace(session.CurrentBranch))
                        session.CurrentBranch = AgentSession.DefaultBranch;
                    if (!session.Kfrs.TryGetValue(session.CurrentBranch, out var list) || list == null)
                    {
                        list = new List<AgentSessionKfrEntry>();
                        session.Kfrs[session.CurrentBranch] = list;
                    }

                    var timeStamp = DateTime.UtcNow.ToJSONString();
                    AgentSessionKfrEntry entry = null;
                    // 1) If KfrId is supplied, update existing entry (true upsert)
                    if (!string.IsNullOrWhiteSpace(args.Entry.KfrId))
                    {
                        var id = args.Entry.KfrId.Trim();
                        entry = list.SingleOrDefault(k => k.KfrId.Equals(id, StringComparison.OrdinalIgnoreCase));
                        if (entry != null)
                        {
                            // Update in place
                            entry.Kind = kind;
                            entry.Value = args.Entry.Value;
                            entry.RequiresResolution = args.Entry.RequiresResolution;
                            entry.IsActive = true;
                            entry.LastUpdatedDate = timeStamp;
                            // For single-cardinality kinds, deactivate others (but keep this one active)
                            if (kind == KfrKind.Goal || kind == KfrKind.Plan)
                            {
                                foreach (var existing in list.Where(k => k.Kind == kind && k.IsActive && !k.KfrId.Equals(entry.KfrId, StringComparison.OrdinalIgnoreCase)))
                                    existing.IsActive = false;
                            }
                        }
                    }

                    // 2) If no existing match was found, insert new
                    if (entry == null)
                    {
                        // For single-cardinality kinds, deactivate prior entries first
                        if (kind == KfrKind.Goal || kind == KfrKind.Plan)
                        {
                            foreach (var existing in list.Where(k => k.Kind == kind && k.IsActive))
                                existing.IsActive = false;
                        }

                        // Generate a per-kind sequence (based on existing ids in this branch)
                        var nextSeq = list.Where(k => k.Kind == kind && !string.IsNullOrWhiteSpace(k.KfrId)).Select(k =>
                        {
                            var parts = k.KfrId.Split('-');
                            if (parts.Length < 3)
                                return 0;
                            return int.TryParse(parts[^1], out var n) ? n : 0;
                        }).DefaultIfEmpty(0).Max() + 1;
                        var newId = string.IsNullOrWhiteSpace(args.Entry.KfrId) ? $"KFR-{kind.ToString().ToUpperInvariant()}-{nextSeq:000}" : args.Entry.KfrId.Trim();
                        entry = new AgentSessionKfrEntry
                        {
                            KfrId = newId,
                            Kind = kind,
                            Value = args.Entry.Value,
                            RequiresResolution = args.Entry.RequiresResolution,
                            IsActive = true,
                            CreationDate = timeStamp,
                            LastUpdatedDate = timeStamp
                        };
                        list.Add(entry);
                    }

                    // Persist just the current branch KFRs (per your pattern)
                    await _sessions.UpdateKFRsAsync(session.Id, session.Mode, session.Kfrs[session.CurrentBranch], context.Org, context.User);
                    return InvokeResult<string>.Create(JsonConvert.SerializeObject(new Result { Operation = "upsert", Items = new List<AgentSessionKfrEntry> { entry }, SessionId = context.SessionId }));
                }
                else if (op == "evict")
                {
                    // Validate input
                    var ids = args.KfrIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
                    if (ids.Count == 0)
                        return InvokeResult<string>.FromError("session_kfr evict requires 'kfrIds' (one or more KFR ids).");
                    var force = args.Force.HasValue && args.Force.Value;
                    var session = await _sessions.GetAgentSessionAsync(context.SessionId, context.Org, context.User);
                    if (session == null)
                        return InvokeResult<string>.FromError("session_kfr could not load the current session.");
                    const string branchKey = "main";
                    session.Kfrs ??= new Dictionary<string, List<AgentSessionKfrEntry>>(StringComparer.OrdinalIgnoreCase);
                    if (!session.Kfrs.TryGetValue(branchKey, out var branchList) || branchList == null)
                    {
                        // Nothing to evict, treat as successful no-op
                        return InvokeResult<string>.Create(JsonConvert.SerializeObject(new Result { Operation = "evict", Items = new List<AgentSessionKfrEntry>(), SessionId = context.SessionId }));
                    }

                    // Find matching active entries
                    var matches = branchList.Where(k => k.IsActive && ids.Contains(k.KfrId, StringComparer.OrdinalIgnoreCase)).ToList();
                    if (matches.Count == 0)
                    {
                        // No matches, treat as successful no-op
                        return InvokeResult<string>.Create(JsonConvert.SerializeObject(new Result { Operation = "evict", Items = new List<AgentSessionKfrEntry>(), SessionId = context.SessionId }));
                    }

                    // Enforce resolution gate unless forced
                    var blocked = matches.Where(m => m.RequiresResolution).ToList();
                    if (blocked.Count > 0 && !force)
                    {
                        var blockedIds = string.Join(", ", blocked.Select(b => b.KfrId));
                        return InvokeResult<string>.FromError($"session_kfr evict refused: the following KFR entries require resolution: {blockedIds}. " + "Set force=true to dismiss/evict anyway.");
                    }

                    // Soft-evict
                    foreach (var m in matches)
                    {
                        m.IsActive = false;
                        m.LastUpdatedDate = DateTime.UtcNow.ToString("o");
                    }

                    await _sessions.UpdateKFRsAsync(session.Id, session.Mode, session.Kfrs[session.Mode], context.Org, context.User);
                    return InvokeResult<string>.Create(JsonConvert.SerializeObject(new Result { Operation = "evict", Items = matches, SessionId = context.SessionId }));
                }

                return InvokeResult<string>.FromError($"Unsupported session_kfr operation '{args.Operation}'.");
            }
            catch (Exception ex)
            {
                _logger.AddException("[SessionKfrTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("session_kfr failed to process request.");
            }
        }


        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Manage the Known Facts Registry (KFR) for the current session.", p =>
            {
                p.String("operation", "Operation to perform: list | clear | upsert | evict.", required: true);
                p.Array("entry", "list of entries for our kfr", 
                    new JsonScheamArrayEntry() { Name = "kfrId", Description="generated id of kfr, for new kfrs, do not supply one will be generated", Type="string",  },
                    new JsonScheamArrayEntry() { Name = "kind", Description = "kind of kfr, must be one of (goal, plan, activeContract, constraint, openQuestion)", Type = "string", },
                    new JsonScheamArrayEntry() { Name = "value", Description = "generated id of kfr", Type = "string", },
                    new JsonScheamArrayEntry() { Name = "requiresResolution", Description = "generated id of kfr", Type = "bool", }
                    );
                p.StringArray("kfrIds", "KFR ids to evict (used with operation=evict).");
                p.Boolean("force", "If true, allow evicting entries that require resolution (used with operation=evict).");
            });
        }
    }
}