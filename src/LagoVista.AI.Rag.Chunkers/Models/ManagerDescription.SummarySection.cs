using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LagoVista.AI.Rag.Chunkers.Services;

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
        /// <summary>
        /// Builds human-readable summary sections for this manager, enriched with
        /// domain/model context and respecting a token budget.
        /// </summary>
        /// <param name="headerInfo">
        /// Domain/model context used for taglines and method summaries. May be null.
        /// </param>
        /// <param name="maxTokens">
        /// Approximate token budget per SummarySection. Used to decide when to
        /// split manager-methods into multiple sections. Final RagChunk slicing
        /// still happens downstream.
        /// </param>
        public IEnumerable<SummarySection> BuildSections(
            DomainModelHeaderInformation headerInfo,
            int maxTokens = 6500)
        {
            if (maxTokens <= 0)
            {
                maxTokens = 6500;
            }

            var symbol = string.IsNullOrWhiteSpace(ClassName) ? "(unknown-manager)" : ClassName;
            var sections = new List<SummarySection>();

            // ---------------------------------------------------------------------
            // manager-overview
            // ---------------------------------------------------------------------
            var overview = new StringBuilder();

            var domainLine = BuildDomainLine(headerInfo);
            var modelLine = BuildModelLine(headerInfo);

            if (!string.IsNullOrWhiteSpace(domainLine))
                overview.AppendLine(domainLine);

            if (!string.IsNullOrWhiteSpace(modelLine))
                overview.AppendLine(modelLine);

            if (!string.IsNullOrWhiteSpace(domainLine) || !string.IsNullOrWhiteSpace(modelLine))
                overview.AppendLine();

            overview.AppendLine($"Manager: {symbol}");

            if (!string.IsNullOrWhiteSpace(Namespace))
                overview.AppendLine($"Namespace: {Namespace}");

            if (!string.IsNullOrWhiteSpace(BaseTypeName))
                overview.AppendLine($"Base Type: {BaseTypeName}");

            if (ManagerType != null)
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

            sections.Add(new SummarySection
            {
                SectionKey = "manager-overview",
                SectionType = "Overview",
                Flavor = "ManagerDescription",
                Symbol = symbol,
                SymbolType = "Manager",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = overview.ToString().Trim()
            });

            // ---------------------------------------------------------------------
            // manager-constructors (now also grounded with Domain/Model header)
            // ---------------------------------------------------------------------
            var ctorText = new StringBuilder();

            var ctorDomainLine = BuildDomainLine(headerInfo);
            var ctorModelLine = BuildModelLine(headerInfo);

            if (!string.IsNullOrWhiteSpace(ctorDomainLine))
                ctorText.AppendLine(ctorDomainLine);

            if (!string.IsNullOrWhiteSpace(ctorModelLine))
                ctorText.AppendLine(ctorModelLine);

            if (!string.IsNullOrWhiteSpace(ctorDomainLine) || !string.IsNullOrWhiteSpace(ctorModelLine))
                ctorText.AppendLine();

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
                SectionType = "Constructors",
                Flavor = "ManagerDescription",
                Symbol = symbol,
                SymbolType = "Manager",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = ctorText.ToString().Trim()
            });

            // ---------------------------------------------------------------------
            // manager-methods (uses MethodSummaryBuilder + domain/model info)
            // Now: split into multiple SummarySections if we exceed maxTokens.
            // ---------------------------------------------------------------------

            var methodsHeader = BuildManagerMethodsHeader(symbol, headerInfo);
            var headerTokens = TokenEstimator.EstimateTokens(methodsHeader);

            var orderedMethods = (Methods ?? new List<ManagerMethodDescription>())
                .OrderBy(m => m.MethodName)
                .ToList();

            if (orderedMethods.Count == 0)
            {
                var emptyText = new StringBuilder();
                emptyText.Append(methodsHeader);
                emptyText.AppendLine("No methods discovered.");

                sections.Add(new SummarySection
                {
                    SectionKey = "manager-methods",
                    SectionType = "Methods",
                    Flavor = "ManagerDescription",
                    Symbol = symbol,
                    SymbolType = "Manager",
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

                var signature = string.IsNullOrWhiteSpace(method.ReturnType)
                    ? $"{method.MethodName}({paramListRaw})"
                    : $"{method.ReturnType} {method.MethodName}({paramListRaw})";

                // Build method summary sentence without re-injecting taglines or signature,
                // since those are already covered by the section header and metadata.
                var summaryContext = new MethodSummaryContext
                {
                    MethodName = method.MethodName,
                    SubKind = "ManagerMethod",
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

                // If adding this method would exceed the budget and we already have
                // some methods in the current section, flush the current section and
                // start a new one with the same header.
                if (currentTokens + methodBlockTokens > maxTokens && currentTokens > headerTokens)
                {
                    sections.Add(new SummarySection
                    {
                        SectionKey = "manager-methods",
                        SectionType = "Methods",
                        Flavor = "ManagerDescription",
                        Symbol = symbol,
                        SymbolType = "Manager",
                        DomainKey = headerInfo?.DomainKey,
                        ModelClassName = headerInfo?.ModelClassName,
                        ModelName = headerInfo?.ModelName,
                        SectionNormalizedText = currentText.ToString().Trim()
                    });

                    currentText.Clear();
                    currentText.Append(methodsHeader);
                    currentTokens = headerTokens;
                }

                // If the method block by itself is larger than the budget, we still
                // append it to avoid losing information. In that case, the section
                // will slightly exceed maxTokens, which is preferable to dropping it.
                currentText.Append(methodBlockText);
                currentTokens = TokenEstimator.EstimateTokens(currentText.ToString());
            }

            // Flush the final section.
            if (currentTokens > 0)
            {
                sections.Add(new SummarySection
                {
                    SectionKey = "manager-methods",
                    SectionType = "Methods",
                    Flavor = "ManagerDescription",
                    Symbol = symbol,
                    SymbolType = "Manager",
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

        private static string BuildManagerMethodsHeader(
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

            sb.AppendLine($"Methods for manager {symbol}:");

            return sb.ToString();
        }
    }
}
