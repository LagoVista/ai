using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Services;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// SummarySection implementation for InterfaceDescription (IDX-0042).
    ///
    /// NOTE: primary declaration should be:
    ///   public sealed partial class InterfaceDescription
    /// </summary>
    public sealed partial class InterfaceDescription : ISummarySectionBuilder
    {
        /// <summary>
        /// Builds human-readable summary sections for this interface (contract),
        /// enriched with domain/model context and respecting a token budget.
        /// </summary>
        public IEnumerable<SummarySection> BuildSections(
            DomainModelHeaderInformation headerInfo,
            int maxTokens = 6500)
        {
            if (maxTokens <= 0)
            {
                maxTokens = 6500;
            }

            // NOTE: These property names assume InterfaceDescription exposes
            // InterfaceName, Namespace, FullName, IsGeneric, GenericArity,
            // Role, PrimaryEntity, LinesStart/LinesEnd, and Methods
            // (list of InterfaceMethodDescription). Adjust if needed.

            var symbol = string.IsNullOrWhiteSpace(InterfaceName) ? "(unknown-interface)" : InterfaceName;
            var sections = new List<SummarySection>();
            _summarySections = sections;

            // -----------------------------------------------------------------
            // interface-overview
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

            if (LineStart.HasValue || LineEnd.HasValue)
                overview.AppendLine($"Lines: {LineStart ?? 0}-{LineEnd ?? 0}");

            sections.Add(new SummarySection
            {
                SectionKey = "interface-overview",
                SectionType = "Overview",
                Flavor = "InterfaceDescription",
                Symbol = symbol,
                SymbolType = "Interface",
                DomainKey = headerInfo?.DomainKey,
                ModelClassName = headerInfo?.ModelClassName,
                ModelName = headerInfo?.ModelName,
                SectionNormalizedText = overview.ToString().Trim()
            });

            // -----------------------------------------------------------------
            // interface-methods (uses MethodSummaryBuilder)
            // -----------------------------------------------------------------

            var methodsHeader = BuildInterfaceMethodsHeader(symbol, headerInfo);
            var headerTokens = TokenEstimator.EstimateTokens(methodsHeader);

            var orderedMethods = (Methods ?? new List<InterfaceMethodDescription>())
                .OrderBy(m => m.Name)
                .ToList();

            if (orderedMethods.Count == 0)
            {
                var emptyText = new StringBuilder();
                emptyText.Append(methodsHeader);
                emptyText.AppendLine("No methods discovered.");

                sections.Add(new SummarySection
                {
                    SectionKey = "interface-methods",
                    SectionType = "Methods",
                    Flavor = "InterfaceDescription",
                    Symbol = symbol,
                    SymbolType = "Interface",
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
                var paramListRaw = (method.Parameters != null && method.Parameters.Count > 0)
                    ? string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"))
                    : string.Empty;

                var parametersForDisplay = string.IsNullOrWhiteSpace(paramListRaw)
                    ? "no parameters"
                    : paramListRaw;

                var summaryContext = new MethodSummaryContext
                {
                    MethodName = method.Name,
                    SubKind = "InterfaceMethod",
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
                methodBlock.AppendLine($"- {summarySentence}{(method.IsAsync ? " (async)" : string.Empty)}");
                methodBlock.AppendLine($"  Returns: {method.ReturnType}");
                methodBlock.AppendLine($"  Params: {parametersForDisplay}");

                if (method.LineStart.HasValue || method.LineEnd.HasValue)
                    methodBlock.AppendLine($"  Lines: {method.LineStart ?? 0}-{method.LineEnd ?? 0}");

                var methodBlockText = methodBlock.ToString();
                var methodBlockTokens = TokenEstimator.EstimateTokens(methodBlockText);

                if (currentTokens + methodBlockTokens > maxTokens && currentTokens > headerTokens)
                {
                    sections.Add(new SummarySection
                    {
                        SectionKey = "interface-methods",
                        SectionType = "Methods",
                        Flavor = "InterfaceDescription",
                        Symbol = symbol,
                        SymbolType = "Interface",
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
                    SectionKey = "interface-methods",
                    SectionType = "Methods",
                    Flavor = "InterfaceDescription",
                    Symbol = symbol,
                    SymbolType = "Interface",
                    DomainKey = headerInfo?.DomainKey,
                    ModelClassName = headerInfo?.ModelClassName,
                    ModelName = headerInfo?.ModelName,
                    SectionNormalizedText = currentText.ToString().Trim()
                });
            }

            _summarySections = sections;

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

        private static string BuildInterfaceMethodsHeader(
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

            sb.AppendLine($"Methods for interface {symbol}:");

            return sb.ToString();
        }
    }
}
