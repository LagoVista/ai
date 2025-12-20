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
            var ddrs = await _ddrManager.GetDdrs(preload, org, user).ConfigureAwait(false);

            var ordered = preload
                .Select(id => new
                {
                    Id = (id ?? string.Empty).Trim(),
                    Ddr = ddrs?.FirstOrDefault(d => string.Equals((d?.DdrIdentifier ?? string.Empty).Trim(), (id ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                          ?? ddrs?.FirstOrDefault(d => string.Equals((d?.Id ?? string.Empty).Trim(), (id ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                })
                .ToList();

            var instructions = BuildInstructionsText(mode, ordered);
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
            System.Collections.Generic.IEnumerable<dynamic> ordered)
        {
            var sb = new StringBuilder();

            sb.AppendLine("### AGN-018 — Mode DDR Instructions (Authoritative)");
            sb.AppendLine("These instructions MUST be followed for this request.");
            sb.AppendLine();

            sb.AppendLine($"[MODE: {mode?.Key ?? mode?.DisplayName ?? "(unknown)"}]");
            sb.AppendLine();

            sb.AppendLine("[DDR SET]");
            foreach (var item in ordered)
            {
                var ddr = item.Ddr;
                var label = ddr?.DdrIdentifier ?? item.Id;
                var title = ddr?.Title ?? ddr?.Name;
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
                var ddr = item.Ddr;
                var label = ddr?.DdrIdentifier ?? item.Id;

                var llm = ddr?.LlmSummary;
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
