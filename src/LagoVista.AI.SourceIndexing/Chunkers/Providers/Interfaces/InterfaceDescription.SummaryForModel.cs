using LagoVista.AI.Chunkers.Providers.Default;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Chunkers.Providers.Interfaces
{
    public partial class InterfaceDescription
    {
        public override string BuildSummaryForModel()
        {
            var maxMethods = 50;
            var maxProps = 30;

            var iface = this;

            if (iface == null) throw new ArgumentNullException(nameof(iface));

            var sb = new StringBuilder();

            // Header
            sb.AppendLine($"Interface: {iface.InterfaceName}{FormatRole(iface.Role)}");
            if (!string.IsNullOrWhiteSpace(iface.FullName))
                sb.AppendLine($"FullName: {iface.FullName}");

            if (!string.IsNullOrWhiteSpace(SourcePath))
                sb.AppendLine($"Path: {SourcePath}");

            if (iface.LineStart.HasValue || iface.LineEnd.HasValue)
                sb.AppendLine($"Lines: {iface.LineStart?.ToString() ?? "?"}-{iface.LineEnd?.ToString() ?? "?"}");

            // Optional: concise summaries if you already populate them
            if (!string.IsNullOrWhiteSpace(iface.OverviewSummary))
                sb.AppendLine($"Overview: {iface.OverviewSummary}");

            // Key facts (short lists)
            AppendList(sb, "OperatesOn", iface.OperatesOnTypes);
            AppendList(sb, "Returns", iface.ReturnTypes);

            // Methods
            var methods = iface.Methods ?? Array.Empty<MethodDescription>();
            if (methods.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Methods:");

                // Use the same logic as your finder snippet, but we keep the method name as an anchor.
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int emitted = 0;

                foreach (var m in methods)
                {
                    if (emitted >= maxMethods) break;

                    var atom = BuildMethodAtom(m);
                    if (string.IsNullOrWhiteSpace(atom)) continue;

                    var methodName = m.Name ?? "";
                    var line = $"- {methodName} :: {atom}";

                    // Dedupe by atom (handles overloads)
                    if (!seen.Add(atom)) continue;

                    sb.AppendLine(line);
                    emitted++;
                }

                if (methods.Count > emitted)
                    sb.AppendLine($"… ({methods.Count - emitted} more methods omitted)");
            }

            // Properties
            var props = iface.Properties ?? Array.Empty<PropertyDescription>();
            if (props.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Properties:");

                int emitted = 0;
                foreach (var p in props)
                {
                    if (emitted >= maxProps) break;
                    foreach (var pline in BuildPropertyLines(p))
                    {
                        sb.AppendLine($"- {pline}");
                        emitted++;
                        if (emitted >= maxProps) break;
                    }
                }

                if (props.Count > 0 && emitted >= maxProps)
                    sb.AppendLine("… (more properties omitted)");
            }

            // Footer hint for escalation
            sb.AppendLine();
            sb.AppendLine("Note: Request Level 1 (raw interface code) if exact parameter names/overloads are needed.");

            return sb.ToString().TrimEnd();
        }

        private static string FormatRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role)) return "";
            return $" ({role})";
        }

        private static void AppendList(StringBuilder sb, string label, IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0) return;

            // Keep short and stable
            var distinct = items.Where(s => !string.IsNullOrWhiteSpace(s))
                                .Select(s => s.Trim())
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Take(12)
                                .ToList();

            if (distinct.Count == 0) return;

            sb.AppendLine($"{label}: {string.Join(", ", distinct)}{(items.Count > distinct.Count ? " …" : "")}");
        }

        private static IEnumerable<string> BuildPropertyLines(PropertyDescription p)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Type))
                yield break;

            var typeShort = ShortType(p.Type);
            var token = IsPrimitive(typeShort) ? ToWords(p.Name).ToLowerInvariant() : typeShort;

            if (p.HasGetter)
                yield return $"get {token} -> {typeShort}{(p.HasSetter ? "" : " (readonly)")}";
            if (p.HasSetter)
                yield return $"set {token} -> {typeShort}";
        }

        private static string ShortType(string fullOrShort)
        {
            var t = (fullOrShort ?? "").Trim();
            var idx = t.LastIndexOf('.');
            return idx >= 0 ? t.Substring(idx + 1) : t;
        }

        private static bool IsPrimitive(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return false;
            return t.Equals("string", StringComparison.OrdinalIgnoreCase)
                || t.Equals("bool", StringComparison.OrdinalIgnoreCase)
                || t.Equals("int", StringComparison.OrdinalIgnoreCase)
                || t.Equals("long", StringComparison.OrdinalIgnoreCase)
                || t.Equals("double", StringComparison.OrdinalIgnoreCase)
                || t.Equals("decimal", StringComparison.OrdinalIgnoreCase)
                || t.Equals("DateTime", StringComparison.OrdinalIgnoreCase)
                || t.Equals("Guid", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToWords(string pascalOrCamel)
        {
            if (string.IsNullOrWhiteSpace(pascalOrCamel)) return pascalOrCamel;
            // Minimal split: "UserName" -> "User Name"
            var sb = new StringBuilder();
            sb.Append(pascalOrCamel[0]);
            for (int i = 1; i < pascalOrCamel.Length; i++)
            {
                var c = pascalOrCamel[i];
                if (char.IsUpper(c) && char.IsLower(pascalOrCamel[i - 1]))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}