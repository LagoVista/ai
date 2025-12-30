using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Upsert one or more KFR entries into the current session branch.
    /// </summary>
    public sealed class SessionKfrUpsertTool : SessionKfrToolBase
    {
        public const string ToolName = "session_kfr_upsert";
        public const string ToolSummary = "KFR: upsert working-memory entries";

        public const string ToolUsageMetadata = @"Tool Usage Instructions — KFR (Upsert)

Purpose:
Maintain the Known Facts Registry (KFR) for the next few turns by adding or updating entries.

When to Call:
Call only when working state changes, including:
- A new goal or plan is established or revised.
- A new active contract becomes relevant.
- A new constraint/invariant is identified.
- A new open question is raised or resolved.

Rules:
- Keep entries short and operational.
- For Goal and Plan kinds: treat as single-cardinality (new upsert replaces prior).
- Do not store rationale, narrative, or history.

Input:
- entry[] must contain one or more objects.
- Each entry requires kind and value.
";

        public override string Name => ToolName;

        public SessionKfrUpsertTool(IAdminLogger logger, IAgentSessionManager sessions)
            : base(logger, sessions)
        {
        }

        private sealed class Args
        {
            public List<KfrPayload> Entry { get; set; }
        }

        private sealed class KfrPayload
        {
            public string KfrId { get; set; }
            public string Kind { get; set; } // goal | plan | activeContract | constraint | openQuestion
            public string Value { get; set; }
            public bool RequiresResolution { get; set; }
        }

        public override Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (context?.Session == null)
                return Fail($"{ToolName} requires a valid execution context.");

            try
            {
                var args = string.IsNullOrWhiteSpace(argumentsJson)
                    ? new Args()
                    : (JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args());

                if (args.Entry == null || args.Entry.Count == 0)
                    return Fail($"{ToolName} requires 'entry' (one or more items).");

                var branchList = GetBranchList(context);
                var updated = new List<AgentSessionKfrEntry>();
                var stamp = UtcStamp();

                foreach (var incoming in args.Entry)
                {
                    if (incoming == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(incoming.Kind))
                        return Fail($"{ToolName} requires entry.kind.");

                    if (!Enum.TryParse<KfrKind>(incoming.Kind, true, out var kind))
                        return Fail($"Invalid KFR kind '{incoming.Kind}'.");

                    if (string.IsNullOrWhiteSpace(incoming.Value))
                        return Fail($"{ToolName} requires entry.value.");

                    AgentSessionKfrEntry entry = null;

                    if (!string.IsNullOrWhiteSpace(incoming.KfrId))
                    {
                        var id = incoming.KfrId.Trim();
                        entry = branchList.SingleOrDefault(k => k.KfrId.Equals(id, StringComparison.OrdinalIgnoreCase));
                        if (entry != null)
                        {
                            entry.Kind = kind;
                            entry.Value = incoming.Value;
                            entry.RequiresResolution = incoming.RequiresResolution;
                            entry.IsActive = true;
                            entry.LastUpdatedDate = stamp;

                            if (kind == KfrKind.Goal || kind == KfrKind.Plan)
                            {
                                foreach (var other in branchList.Where(k => k.Kind == kind && k.IsActive && !k.KfrId.Equals(entry.KfrId, StringComparison.OrdinalIgnoreCase)))
                                    other.IsActive = false;
                            }

                            updated.Add(entry);
                            continue;
                        }
                    }

                    if (kind == KfrKind.Goal || kind == KfrKind.Plan)
                    {
                        foreach (var existing in branchList.Where(k => k.Kind == kind && k.IsActive))
                            existing.IsActive = false;
                    }

                    var nextSeq = branchList
                        .Where(k => k.Kind == kind && !string.IsNullOrWhiteSpace(k.KfrId))
                        .Select(k =>
                        {
                            var parts = k.KfrId.Split('-');
                            if (parts.Length < 3) return 0;
                            return int.TryParse(parts[^1], out var n) ? n : 0;
                        })
                        .DefaultIfEmpty(0)
                        .Max() + 1;

                    var newId = string.IsNullOrWhiteSpace(incoming.KfrId)
                        ? $"KFR-{kind.ToString().ToUpperInvariant()}-{nextSeq:000}"
                        : incoming.KfrId.Trim();

                    entry = new AgentSessionKfrEntry
                    {
                        KfrId = newId,
                        Kind = kind,
                        CreatedByUser = context.Envelope.User.Text,
                        CreatedByUserId = context.Envelope.User.Id,
                        Value = incoming.Value,
                        RequiresResolution = incoming.RequiresResolution,
                        IsActive = true,
                        CreationDate = stamp,
                        LastUpdatedDate = stamp
                    };

                    branchList.Add(entry);
                    updated.Add(entry);
                }

                return Task.FromResult(Ok("upsert", context, updated));
            }
            catch (Exception ex)
            {
                Logger.AddException("[SessionKfrUpsertTool_ExecuteAsync__Exception]", ex);
                return Fail($"{ToolName} failed to process request.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(
                ToolName,
                "Upsert one or more KFR entries into the current session branch.",
                p =>
                {
                    p.ObjectArray(
                        "entry",
                        "Entries to upsert (one or more).",
                        new JsonScheamArrayEntry { Name = "kfrId", Type = "string", Description = "Existing id; omit for new." },
                        new JsonScheamArrayEntry { Name = "kind", Type = "string", Description = "goal | plan | activeContract | constraint | openQuestion" },
                        new JsonScheamArrayEntry { Name = "value", Type = "string", Description = "Entry value." },
                        new JsonScheamArrayEntry { Name = "requiresResolution", Type = "boolean", Description = "True if unresolved." }
                    );
                });
        }
    }
}
