using System;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// DomainDescriptionBuilder (IDX-072).
    ///
    /// Canonical description builder for SubtypeKind.DomainDescription. This builder is
    /// deterministic and does not call any LLMs. It parses the domain document, queries
    /// the domain catalog for model classes, and constructs a DomainDescriptionRag.
    /// </summary>
    public sealed class DomainDescriptionBuilder : IDescriptionBuilder
    {
        private readonly IAdminLogger _logger;

        public DomainDescriptionBuilder(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public string Name => "DomainDescriptionBuilder";

        /// <inheritdoc />
        public string Description => "Builds DomainDescriptionRag descriptions for SubtypeKind.DomainDescription documents (IDX-072).";

        /// <summary>
        /// Builds a DomainDescriptionRag from the provided file context and symbol text.
        /// </summary>
        public async Task<InvokeResult<IRagDescription>> BuildAsync(
            IndexFileContext fileContext,
            string symbolText,
            IDomainCatalogService domainCatalogService,
            IResourceDictionary resourceDictionary)
        {
            if (fileContext == null) throw new ArgumentNullException(nameof(fileContext));
            if (domainCatalogService == null) throw new ArgumentNullException(nameof(domainCatalogService));

            if (string.IsNullOrWhiteSpace(symbolText))
            {
                return InvokeResult<IRagDescription>.FromError("DomainDescriptionBuilder requires non-empty document text.");
            }

            try
            {
                var parsed = DomainDocumentParser.Parse(symbolText, fileContext);

                if (string.IsNullOrWhiteSpace(parsed.DomainName))
                {
                    return InvokeResult<IRagDescription>.FromError("Unable to determine domain name from document.");
                }

                var domainKey = DomainDescriptionSectionKeyHelper.NormalizeDomainName(parsed.DomainName);

                // Domain catalog lookups are synchronous; we treat an empty list as a valid state.
                var classes = domainCatalogService.GetClassesForDomain(domainKey);

                var description = new DomainDescriptionRag(
                    parsed.DomainName,
                    parsed.DomainSummary,
                    parsed.DomainNarrative,
                    classes);

                // Populate common indexing metadata from the file context.
                description.SetCommonProperties(fileContext);

                return InvokeResult<IRagDescription>.Create(description);
            }
            catch (Exception ex)
            {
                _logger.AddException("DomainDescriptionBuilder_BuildAsync", ex);
                return InvokeResult<IRagDescription>.FromException("DomainDescriptionBuilder.BuildAsync failed.", ex);
            }
        }

        /// <summary>
        /// Internal representation of parsed domain document pieces.
        /// </summary>
        private sealed class ParsedDomainDocument
        {
            public string DomainName { get; set; } = string.Empty;

            public string DomainSummary { get; set; } = string.Empty;

            public string DomainNarrative { get; set; } = string.Empty;
        }

        /// <summary>
        /// Deterministic parser for domain description documents.
        /// This parser intentionally uses simple, stable rules so that
        /// the same document always yields the same result.
        /// </summary>
        private static class DomainDocumentParser
        {
            public static ParsedDomainDocument Parse(string symbolText, IndexFileContext fileContext)
            {
                var result = new ParsedDomainDocument();

                // Normalize newlines.
                var text = (symbolText ?? string.Empty).Replace("\r\n", "\n");

                // Attempt to extract a domain name from the first markdown-style H1 heading,
                // falling back to the file name (without extension) if no heading is found.
                var lines = text.Split('\n');
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith("# "))
                    {
                        result.DomainName = line.Substring(2).Trim();
                        break;
                    }

                    if (line.StartsWith("Domain:", StringComparison.OrdinalIgnoreCase))
                    {
                        result.DomainName = line.Substring("Domain:".Length).Trim();
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(result.DomainName))
                {
                    // Fall back to file name (without extension) if we cannot find a header.
                    if (!string.IsNullOrWhiteSpace(fileContext.RelativePath))
                    {
                        var fileName = System.IO.Path.GetFileNameWithoutExtension(fileContext.RelativePath);
                        result.DomainName = string.IsNullOrWhiteSpace(fileName) ? "Unknown" : fileName;
                    }
                    else
                    {
                        result.DomainName = "Unknown";
                    }
                }

                // Domain summary: first non-empty line after the header, or first non-empty line overall.
                bool headerSeen = false;
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();

                    if (!headerSeen)
                    {
                        if (line.StartsWith("# ") || line.StartsWith("Domain:", StringComparison.OrdinalIgnoreCase))
                        {
                            headerSeen = true;
                        }
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        result.DomainSummary = line;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(result.DomainSummary))
                {
                    // Fallback: first non-empty line in the file.
                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.Trim();
                        if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("# "))
                        {
                            result.DomainSummary = line;
                            break;
                        }
                    }
                }

                // Narrative: everything after the summary line.
                var summaryIndex = -1;
                if (!string.IsNullOrWhiteSpace(result.DomainSummary))
                {
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (string.Equals(lines[i].Trim(), result.DomainSummary, StringComparison.Ordinal))
                        {
                            summaryIndex = i;
                            break;
                        }
                    }
                }

                if (summaryIndex >= 0 && summaryIndex + 1 < lines.Length)
                {
                    var narrativeLines = new ArraySegment<string>(lines, summaryIndex + 1, lines.Length - (summaryIndex + 1));
                    result.DomainNarrative = string.Join("\n", narrativeLines).Trim();
                }
                else
                {
                    result.DomainNarrative = string.Empty;
                }

                return result;
            }
        }
    }
}
