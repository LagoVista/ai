using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// AGN-018 implementation: Builds a deterministic per-mode DDR instruction block and stores it in AgentSession.DdrCache.
    ///
    /// Source of DDR IDs:
    /// - AgentMode.PreloadDDRs (ordered) is the authoritative list.
    ///
    /// Source of DDR instruction text:
    /// - DetailedDesignReview.LlmSummary
    ///
    /// Cache key:
    /// - Normalized mode key (trim + ToLowerInvariant)
    /// </summary>
    public sealed class DdrInstructionsProvider : IDdrInstructionsProvider
    {
        private readonly IDdrManager _ddrManager;

        public DdrInstructionsProvider(IDdrManager ddrManager)
        {
            _ddrManager = ddrManager ?? throw new ArgumentNullException(nameof(ddrManager));
        }

        public async Task EnsureModeInstructionsCachedAsync(
            AgentSession session,
            AgentMode mode,
            EntityHeader org,
            EntityHeader user)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (mode == null) throw new ArgumentNullException(nameof(mode));
            if (org == null) throw new ArgumentNullException(nameof(org));
            if (user == null) throw new ArgumentNullException(nameof(user));

            var modeKey = NormalizeModeKey(mode.Key ?? session.Mode ?? "general");

            if (session.DdrCache == null)
            {
                session.DdrCache = new System.Collections.Generic.Dictionary<string, string>();
            }

            if (session.DdrCache.ContainsKey(modeKey))
            {
                return;
            }

            var preload = mode.PreloadDDRs ?? Array.Empty<string>();
            if (preload.Length == 0)
            {
                session.DdrCache[modeKey] = string.Empty;
                return;
            }

            // Fetch DDRs in bulk. We will re-order deterministically to match AgentMode.PreloadDDRs.
            var ddrs = await _ddrManager.GetDdrs(preload, org, user);

            var instructions = BuildInstructionsText(mode, ddrs);
            session.DdrCache[modeKey] = instructions;
        }

        public string GetCachedModeInstructions(AgentSession session, string modeKey)
        {
            if (session?.DdrCache == null) return null;

            var key = NormalizeModeKey(modeKey);
            if (string.IsNullOrWhiteSpace(key)) return null;

            return session.DdrCache.TryGetValue(key, out var value) ? value : null;
        }

        private static string NormalizeModeKey(string modeKey)
        {
            return string.IsNullOrWhiteSpace(modeKey)
                ? null
                : modeKey.Trim().ToLowerInvariant();
        }

        private static string BuildInstructionsText(
            AgentMode mode,
            System.Collections.Generic.IEnumerable<DetailedDesignReview> ordered)
        {
            var sb = new StringBuilder();

            var headerInstructions = @"You are operating under a strict instruction contract.
If any instruction is ambiguous, contradictory, incomplete, or missing required constraints, you MUST stop processing immediately and explicitly report the issue.
You MUST NOT infer intent, resolve ambiguity, or proceed on a best-guess basis.
If no such issues are detected, you may proceed strictly according to the provided instructions.";

            sb.AppendLine(headerInstructions);
            sb.AppendLine();

          
            sb.AppendLine("[DDR SET]");
            foreach (var item in ordered)
            {
                var ddr = item;
                var label = ddr.DdrIdentifier ?? item.Id;
                var title = ddr.Title ?? ddr?.Name;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    sb.AppendLine($"- {label} — {title}");
                }
                else
                {
                    sb.AppendLine($"- {label}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("[MODE DDR RULES]");

            foreach (var item in ordered)
            {
                var ddr = item;
                var label = ddr?.DdrIdentifier ?? item.Id;

                var llm = ddr.ModeInstructions;
                if (string.IsNullOrWhiteSpace(llm))
                {
                    sb.AppendLine($"\n#### {label}\n(no-llm-summary available)\n");
                    continue;
                }

                sb.AppendLine($"\n#### {label}\n{NormalizeBlock(llm)}\n");
            }

            return sb.ToString().TrimEnd();
        }

        private static string NormalizeBlock(string text)
        {
            var normalized = (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");

            var lines = normalized
                .Split('\n')
                .Select(l => l?.TrimEnd() ?? string.Empty)
                .ToArray();

            return string.Join("\n", lines).Trim();
        }
    }
}
