using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for RepositoryDescription (IDX-0040).
    ///
    /// NOTE: primary declaration should be:
    ///   public partial class RepositoryDescription
    /// </summary>
    public partial class RepositoryDescription : ISummarySectionBuilder
    {
        public IEnumerable<SummarySection> BuildSections(DomainModelHeaderInformation headerInfo, int maxTokens = 6500)
        {
            var symbol = string.IsNullOrWhiteSpace(ClassName) ? "(unknown-repo)" : ClassName;
            var sections = new List<SummarySection>();

            var overview = new StringBuilder();
            overview.AppendLine($"Repository: {symbol}");

            if (!string.IsNullOrWhiteSpace(Namespace))
                overview.AppendLine($"Namespace: {Namespace}");

            if (!string.IsNullOrWhiteSpace(BaseTypeName))
                overview.AppendLine($"Base: {BaseTypeName}");

            overview.AppendLine($"Kind: {RepositoryKind}");

            if (!string.IsNullOrWhiteSpace(PrimaryEntity))
                overview.AppendLine($"Primary Entity: {PrimaryEntity}");

            if (ImplementedInterfaces != null && ImplementedInterfaces.Count > 0)
                overview.AppendLine("Implements: " + string.Join(", ", ImplementedInterfaces));

            if (DependencyInterfaces != null && DependencyInterfaces.Count > 0)
                overview.AppendLine("Depends On: " + string.Join(", ", DependencyInterfaces));

            if (StorageProfile != null)
            {
                overview.AppendLine();
                overview.AppendLine("Storage Profile:");
                if (!string.IsNullOrWhiteSpace(StorageProfile.StorageKind)) overview.AppendLine($" - Kind: {StorageProfile.StorageKind}");
                if (!string.IsNullOrWhiteSpace(StorageProfile.EntityType)) overview.AppendLine($" - Entity: {StorageProfile.EntityType}");
                if (!string.IsNullOrWhiteSpace(StorageProfile.CollectionOrTable)) overview.AppendLine($" - Collection/Table: {StorageProfile.CollectionOrTable}");
                if (!string.IsNullOrWhiteSpace(StorageProfile.PartitionKeyField)) overview.AppendLine($" - PartitionKey: {StorageProfile.PartitionKeyField}");
            }

            if (!string.IsNullOrWhiteSpace(Summary))
            {
                overview.AppendLine();
                overview.AppendLine("Summary:");
                overview.AppendLine(Summary.Trim());
            }

            sections.Add(new SummarySection
            {
                SectionKey = "repository-overview",
                Symbol = symbol,
                SymbolType = "Repository",
                SectionNormalizedText = overview.ToString().Trim()
            });

            var methodsText = new StringBuilder();
            methodsText.AppendLine($"Methods for repository {symbol}:");

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
                }
            }

            sections.Add(new SummarySection
            {
                SectionKey = "repository-methods",
                Symbol = symbol,
                SymbolType = "Repository",
                SectionNormalizedText = methodsText.ToString().Trim()
            });

            return sections;
        }
    }
}
