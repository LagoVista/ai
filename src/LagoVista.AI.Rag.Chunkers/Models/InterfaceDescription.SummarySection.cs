using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for InterfaceDescription (IDX-0042).
    ///
    /// NOTE: primary declaration should be:
    ///   public partial class InterfaceDescription
    /// </summary>
    public partial class InterfaceDescription : ISummarySectionBuilder
    {
        public IEnumerable<SummarySection> BuildSections()
        {
            var symbol = string.IsNullOrWhiteSpace(InterfaceName) ? "(unknown-interface)" : InterfaceName;
            var sections = new List<SummarySection>();

            var overview = new StringBuilder();
            overview.AppendLine($"Interface: {symbol}");

            if (!string.IsNullOrWhiteSpace(Namespace))
                overview.AppendLine($"Namespace: {Namespace}");

            if (!string.IsNullOrWhiteSpace(FullName))
                overview.AppendLine($"Full Name: {FullName}");

            overview.AppendLine($"IsGeneric: {IsGeneric}, GenericArity: {GenericArity}");

            if (!string.IsNullOrWhiteSpace(Role))
                overview.AppendLine($"Role: {Role}");

            if (!string.IsNullOrWhiteSpace(PrimaryEntity))
                overview.AppendLine($"Primary Entity: {PrimaryEntity}");

            if (BaseInterfaces != null && BaseInterfaces.Count > 0)
                overview.AppendLine("Base Interfaces: " + string.Join(", ", BaseInterfaces));

            if (ImplementedBy != null && ImplementedBy.Count > 0)
            {
                overview.AppendLine();
                overview.AppendLine("Implemented By:");
                foreach (var impl in ImplementedBy)
                    overview.AppendLine(" - " + impl);
            }

            if (UsedByControllers != null && UsedByControllers.Count > 0)
            {
                overview.AppendLine();
                overview.AppendLine("Used By Controllers:");
                foreach (var endpointKey in UsedByControllers)
                    overview.AppendLine(" - " + endpointKey);
            }

            if (LineStart.HasValue || LineEnd.HasValue)
            {
                overview.AppendLine();
                overview.AppendLine($"Lines: {LineStart ?? 0}-{LineEnd ?? 0}");
            }

            sections.Add(new SummarySection
            {
                SectionKey = "interface-overview",
                Symbol = symbol,
                SymbolType = "Interface",
                SectionNormalizedText = overview.ToString().Trim()
            });

            var methodsText = new StringBuilder();
            methodsText.AppendLine($"Methods for interface {symbol}:");

            if (Methods == null || Methods.Count == 0)
            {
                methodsText.AppendLine("No methods declared.");
            }
            else
            {
                foreach (var m in Methods.OrderBy(m => m.Name))
                {
                    var asyncFlag = m.IsAsync ? "async" : "sync";
                    var parameters = m.Parameters != null && m.Parameters.Count > 0
                        ? string.Join(", ", m.Parameters.Select(p => $"{p.Type} {p.Name}"))
                        : "no parameters";

                    methodsText.AppendLine();
                    methodsText.AppendLine($"- {m.Name} ({asyncFlag})");
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
                SectionKey = "interface-methods",
                Symbol = symbol,
                SymbolType = "Interface",
                SectionNormalizedText = methodsText.ToString().Trim()
            });

            return sections;
        }
    }
}
