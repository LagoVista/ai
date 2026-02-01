using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Indexing.Utils;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Services;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for RepositoryDescription (IDX-0040).
    ///
    /// NOTE: primary declaration should be:
    ///   public sealed partial class RepositoryDescription
    /// </summary>
    public sealed partial class RepositoryDescription : ISummarySectionBuilder
    {
        /// <summary>
        /// Builds human-readable summary sections for this repository, enriched with
        /// domain/model context and respecting a token budget.
        /// </summary>
        public IEnumerable<SummarySection> BuildSections(
            DomainModelHeaderInformation headerInfo,
            int maxTokens = 6500)
        {
            if (maxTokens <= 0)
            {
                maxTokens = 6500;
            }

            // NOTE: These property names assume RepositoryDescription exposes
            // ClassName, Namespace, BaseTypeName, RepositoryKind, PrimaryEntity,
            // ImplementedInterfaces, and Methods (list of RepositoryMethodDescription).
            // Adjust the property names if your actual model differs.

            var symbol = string.IsNullOrWhiteSpace(ClassName) ? "(unknown-repository)" : ClassName;
            var sections = new List<SummarySection>();
            _summarySections = sections;

            // -----------------------------------------------------------------
            // repository-overview
            // -----------------------------------------------------------------
            var overview = new StringBuilder();

            var domainLine = BuildDomainLine(headerInfo);
            var modelLine = BuildModelLine(headerInfo);

            if (!string.IsNullOrWhiteSpace(domainLine))
                overview.AppendLine(domainLine);

            if (!string.IsNullOrWhiteSpace(modelLine))
                overview.AppendLine(modelLine);

            if (!string.IsNullOrWhiteSpace(domainLine) || !string.IsNullOrWhiteSpace(modelLine))
                overview.AppendLine();

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

            sections.Add(new SummarySection
            {
                SectionKey = "repository-overview",
                SectionType = "Overview",
                Flavor = "RepositoryDescription",
                SymbolName = symbol,
                SymbolType = "Repository",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = overview.ToString().Trim()
            });

            // -----------------------------------------------------------------
            // repository-methods (uses MethodSummaryBuilder + domain/model info)
            // Chunk into multiple SummarySections if we exceed maxTokens.
            // -----------------------------------------------------------------

            var methodsHeader = BuildRepositoryMethodsHeader(symbol, headerInfo);
            var headerTokens = TokenEstimator.EstimateTokens(methodsHeader);

            var orderedMethods = (Methods ?? new List<RepositoryMethodDescription>())
                .OrderBy(m => m.MethodName)
                .ToList();

            if (orderedMethods.Count == 0)
            {
                var emptyText = new StringBuilder();
                emptyText.Append(methodsHeader);
                emptyText.AppendLine("No methods discovered.");

                sections.Add(new SummarySection
                {
                    SectionKey = "repository-methods",
                    SectionType = "Methods",
                    Flavor = "RepositoryDescription",
                    SymbolName = symbol,
                    SymbolType = "Repository",
                    DomainKey = headerInfo?.DomainKey,
                    ModelClassName = headerInfo?.ModelClassName,
                    ModelName = headerInfo?.ModelName,
                    SectionNormalizedText = emptyText.ToString().Trim()
                });

               

                return sections;
            }

            var currentText = new StringBuilder();
            currentText.Append(methodsHeader);
            var currentTokens = headerTokens;

            foreach (var method in orderedMethods)
            {
                var visibility =
                    method.IsPublic ? "public" :
                    method.IsProtectedOrInternal ? "protected/internal" :
                    method.IsPrivate ? "private" :
                    "unknown";

                var paramListRaw = (method.Parameters != null && method.Parameters.Count > 0)
                    ? string.Join(", ", method.Parameters.Select(p => $"{p.TypeName} {p.Name}"))
                    : string.Empty;

                var parametersForDisplay = string.IsNullOrWhiteSpace(paramListRaw)
                    ? "no parameters"
                    : paramListRaw;

                // Build method summary sentence; avoid repeating taglines/signature.
                var summaryContext = new MethodSummaryContext
                {
                    MethodName = method.MethodName,
                    SubKind = "RepositoryMethod",
                    ModelName = headerInfo != null
                        ? (!string.IsNullOrWhiteSpace(headerInfo.ModelClassName)
                            ? headerInfo.ModelClassName
                            : (!string.IsNullOrWhiteSpace(headerInfo.ModelName)
                                ? headerInfo.ModelName
                                : PrimaryEntity))
                        : PrimaryEntity,
                    DomainName = headerInfo?.DomainName,
                    DomainTagline = null,
                    ModelTagline = null,
                    Signature = null
                };

                var summarySentence = MethodSummaryBuilder.BuildSummary(summaryContext);

                var methodBlock = new StringBuilder();
                methodBlock.AppendLine();
                methodBlock.AppendLine($"- {summarySentence}");
                methodBlock.AppendLine($"  Kind: {method.MethodKind}, {visibility}, {(method.IsSignificant ? "significant" : "non-significant")}");
                methodBlock.AppendLine($"  Returns: {method.ReturnType}");
                methodBlock.AppendLine($"  Params: {parametersForDisplay}");

                if (method.LineStart.HasValue || method.LineEnd.HasValue)
                    methodBlock.AppendLine($"  Lines: {method.LineStart ?? 0}-{method.LineEnd ?? 0}");

                if (!string.IsNullOrWhiteSpace(method.Summary))
                {
                    methodBlock.AppendLine("  XML Summary:");
                    methodBlock.AppendLine("  " + method.Summary.Trim());
                }

                var methodBlockText = methodBlock.ToString();
                var methodBlockTokens = TokenEstimator.EstimateTokens(methodBlockText);

                if (currentTokens + methodBlockTokens > maxTokens && currentTokens > headerTokens)
                {
                    sections.Add(new SummarySection
                    {
                        SectionKey = "repository-methods",
                        SectionType = "Methods",
                        Flavor = "RepositoryDescription",
                        SymbolName = symbol,
                        SymbolType = "Repository",
                        DomainKey = headerInfo?.DomainKey,
                        ModelClassName = headerInfo?.ModelClassName,
                        ModelName = headerInfo?.ModelName,
                        SectionNormalizedText = currentText.ToString().Trim()
                    });

                    currentText.Clear();
                    currentText.Append(methodsHeader);
                    currentTokens = headerTokens;
                }

                currentText.Append(methodBlockText);
                currentTokens = TokenEstimator.EstimateTokens(currentText.ToString());
            }

            if (currentTokens > 0)
            {
                sections.Add(new SummarySection
                {
                    SectionKey = "repository-methods",
                    SectionType = "Methods",
                    Flavor = "RepositoryDescription",
                    SymbolName = symbol,
                    SymbolType = "Repository",
                    DomainKey = headerInfo?.DomainKey,
                    ModelClassName = headerInfo?.ModelClassName,
                    ModelName = headerInfo?.ModelName,
                    SectionNormalizedText = currentText.ToString().Trim()
                });
            }

            return sections;
        }

        private static string BuildDomainLine(DomainModelHeaderInformation header)
        {
            if (header == null) return null;

            var hasName = !string.IsNullOrWhiteSpace(header.DomainName);
            var hasTagline = !string.IsNullOrWhiteSpace(header.DomainTagLine);

            if (!hasName && !hasTagline) return null;

            if (hasName && hasTagline)
                return $"Domain: {header.DomainName} — {header.DomainTagLine}";

            if (hasName)
                return $"Domain: {header.DomainName}";

            return header.DomainTagLine;
        }

        private static string BuildModelLine(DomainModelHeaderInformation header)
        {
            if (header == null) return null;

            var modelName = !string.IsNullOrWhiteSpace(header.ModelName)
                ? header.ModelName
                : header.ModelClassName;

            var hasName = !string.IsNullOrWhiteSpace(modelName);
            var hasTagline = !string.IsNullOrWhiteSpace(header.ModelTagLine);

            if (!hasName && !hasTagline) return null;

            if (hasName && hasTagline)
                return $"Model: {modelName} — {header.ModelTagLine}";

            if (hasName)
                return $"Model: {modelName}";

            return header.ModelTagLine;
        }

        private static string BuildRepositoryMethodsHeader(
            string symbol,
            DomainModelHeaderInformation headerInfo)
        {
            var sb = new StringBuilder();

            var domainLine = BuildDomainLine(headerInfo);
            var modelLine = BuildModelLine(headerInfo);

            if (!string.IsNullOrWhiteSpace(domainLine))
                sb.AppendLine(domainLine);

            if (!string.IsNullOrWhiteSpace(modelLine))
                sb.AppendLine(modelLine);

            if (!string.IsNullOrWhiteSpace(domainLine) || !string.IsNullOrWhiteSpace(modelLine))
                sb.AppendLine();

            sb.AppendLine($"Methods for repository {symbol}:");

            return sb.ToString();
        }
    }
}
