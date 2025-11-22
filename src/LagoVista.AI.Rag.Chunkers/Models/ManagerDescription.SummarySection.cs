using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for ManagerDescription (IDX-0039).
    ///
    /// NOTE: primary declaration should be:
    ///   public sealed partial class ManagerDescription
    /// </summary>
    public sealed partial class ManagerDescription : ISummarySectionBuilder
    {
        public IEnumerable<SummarySection> BuildSections()
        {
            var symbol = string.IsNullOrWhiteSpace(ClassName) ? "(unknown-manager)" : ClassName;
            var sections = new List<SummarySection>();

            var overview = new StringBuilder();
            overview.AppendLine($"Manager: {symbol}");

            if (!string.IsNullOrWhiteSpace(Namespace))
                overview.AppendLine($"Namespace: {Namespace}");

            if (!string.IsNullOrWhiteSpace(BaseTypeName))
                overview.AppendLine($"Base Type: {BaseTypeName}");

            overview.AppendLine($"Manager Type: {ManagerType}");

            if (!string.IsNullOrWhiteSpace(PrimaryEntity))
                overview.AppendLine($"Primary Entity: {PrimaryEntity}");

            if (!string.IsNullOrWhiteSpace(PrimaryInterface))
                overview.AppendLine($"Primary Interface: {PrimaryInterface}");

            if (ImplementedInterfaces != null && ImplementedInterfaces.Count > 0)
                overview.AppendLine("Implements: " + string.Join(", ", ImplementedInterfaces));

            if (DependencyInterfaces != null && DependencyInterfaces.Count > 0)
                overview.AppendLine("Depends On: " + string.Join(", ", DependencyInterfaces));

            if (!string.IsNullOrWhiteSpace(Summary))
            {
                overview.AppendLine();
                overview.AppendLine("Summary:");
                overview.AppendLine(Summary.Trim());
            }

            if (!string.IsNullOrWhiteSpace(DocId) || !string.IsNullOrWhiteSpace(FileName))
            {
                overview.AppendLine();
                overview.AppendLine("Source:");
                if (!string.IsNullOrWhiteSpace(DocId)) overview.AppendLine($" - DocId: {DocId}");
                if (!string.IsNullOrWhiteSpace(FileName)) overview.AppendLine($" - File: {FileName}");
            }

            sections.Add(new SummarySection
            {
                SectionKey = "manager-overview",
                Symbol = symbol,
                SymbolType = "Manager",
                SectionNormalizedText = overview.ToString().Trim()
            });

            var ctorText = new StringBuilder();
            ctorText.AppendLine($"Constructors for manager {symbol}:");

            if (Constructors == null || Constructors.Count == 0)
            {
                ctorText.AppendLine("No constructors discovered.");
            }
            else
            {
                foreach (var ctor in Constructors)
                {
                    if (!string.IsNullOrWhiteSpace(ctor.SignatureText))
                        ctorText.AppendLine($"- {ctor.SignatureText}");
                    else
                        ctorText.AppendLine("- (constructor)");

                    if (ctor.Parameters != null && ctor.Parameters.Count > 0)
                    {
                        var deps = string.Join(", ", ctor.Parameters.Select(p => $"{p.TypeName} {p.Name}"));
                        ctorText.AppendLine($"  Dependencies: {deps}");
                    }

                    if (ctor.LineStart.HasValue || ctor.LineEnd.HasValue)
                        ctorText.AppendLine($"  Lines: {ctor.LineStart ?? 0}-{ctor.LineEnd ?? 0}");
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "manager-constructors",
                Symbol = symbol,
                SymbolType = "Manager",
                SectionNormalizedText = ctorText.ToString().Trim()
            });

            var methodsText = new StringBuilder();
            methodsText.AppendLine($"Methods for manager {symbol}:");

            if (Methods == null || Methods.Count == 0)
            {
                methodsText.AppendLine("No methods discovered.");
            }
            else
            {
                foreach (var m in Methods.OrderBy(m => m.MethodName))
                {
                    var visibility = m.IsPublic
                        ? "public"
                        : m.IsProtectedOrInternal
                            ? "protected/internal"
                            : m.IsPrivate ? "private" : "unknown";

                    var parameters = m.Parameters != null && m.Parameters.Count > 0
                        ? string.Join(", ", m.Parameters.Select(p => $"{p.TypeName} {p.Name}"))
                        : "no parameters";

                    methodsText.AppendLine();
                    methodsText.AppendLine($"- {m.MethodName}");
                    methodsText.AppendLine($"  Kind: {m.MethodKind}, {visibility}, {(m.IsSignificant ? "significant" : "non-significant")}");
                    methodsText.AppendLine($"  Returns: {m.ReturnType}");
                    methodsText.AppendLine($"  Params: {parameters}");

                    if (m.LineStart.HasValue || m.LineEnd.HasValue)
                        methodsText.AppendLine($"  Lines: {m.LineStart ?? 0}-{m.LineEnd ?? 0}");

                    if (!string.IsNullOrWhiteSpace(m.Summary))
                    {
                        methodsText.AppendLine("  Summary:");
                        methodsText.AppendLine("  " + m.Summary.Trim());
                    }
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "manager-methods",
                Symbol = symbol,
                SymbolType = "Manager",
                SectionNormalizedText = methodsText.ToString().Trim()
            });

            return sections;
        }
    }
}
