using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// IDX-0051 – Extracts normalized RESX resource chunks from raw XML text.
    ///
    /// Input:  full XML text of a .resx file.
    /// Output: collection of ResxResourceChunk objects that contain everything
    ///         needed to build embedding text and RagVectorPayload metadata.
    /// </summary>
    public class ResxResourceExtractor : IResourceExtractor
    {
        /// <summary>
        /// Extracts RESX resource entries from XML text.
        /// </summary>
        /// <param name="xmlText">Raw .resx XML content.</param>
        /// <param name="relativePath">Repo-relative path to the .resx file.</param>
        /// <returns>Immutable list of <see cref="ResxResourceChunk"/>.</returns>
        public IReadOnlyList<ResxResourceChunk> Extract(string xmlText, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(xmlText))
            {
                throw new ArgumentNullException(nameof(xmlText));
            }

            if (relativePath == null)
            {
                throw new ArgumentNullException(nameof(relativePath));
            }

            // Normalize path early so everything downstream is consistent.
            relativePath = relativePath.Replace('\\', '/');

            XDocument document;
            try
            {
                document = XDocument.Parse(xmlText, LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse RESX XML for '{relativePath}'.", ex);
            }

            if (document.Root == null || !string.Equals(document.Root.Name.LocalName, "root", StringComparison.OrdinalIgnoreCase))
            {
                // Not a standard .resx root; return empty to keep the caller simple.
                return Array.Empty<ResxResourceChunk>();
            }

            var culture = ResolveCultureFromPath(relativePath)
                          ?? ResolveCultureFromHeaders(document)
                          ?? string.Empty;

            var sourceFile = Path.GetFileName(relativePath);
            var chunks = new List<ResxResourceChunk>();

            foreach (var dataElement in document.Root.Elements("data"))
            {
                var key = dataElement.Attribute("name")?.Value;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var valueElement = dataElement.Element("value");
                var commentElement = dataElement.Element("comment");

                var value = valueElement?.Value ?? string.Empty;
                var comment = commentElement?.Value;

                var chunk = new ResxResourceChunk
                {
                    ResourceKey = key.Trim(),
                    ResourceValue = value.Trim(),
                    Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),

                    SourceFile = sourceFile,
                    RelativePath = relativePath,

                    Culture = culture,
                    SubKind = ResourceSubKind.UiString, // V1 default; can be refined later.

                    UsageCount = 0,
                    PrimaryUsageKinds = Array.Empty<string>(),

                    IsDuplicate = false,
                    IsUiSharedCandidate = false
                };

                chunks.Add(chunk);
            }

            return chunks;
        }

        /// <summary>
        /// Derives culture from the file name, e.g.:
        ///   Resources.resx       -> ""
        ///   Resources.en.resx    -> "en"
        ///   Resources.en-US.resx -> "en-US".
        /// </summary>
        private static string ResolveCultureFromPath(string relativePath)
        {
            var fileName = Path.GetFileName(relativePath);
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            // Match optional .culture before .resx
            // Examples:
            //   Resources.resx
            //   Resources.en.resx
            //   Resources.en-US.resx
            var match = Regex.Match(fileName, @"^(?<base>.+?)\.(?<culture>[a-zA-Z]{2}(?:-[a-zA-Z]{2})?)\.resx$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                // No explicit culture fragment.
                return string.Equals(Path.GetExtension(fileName), ".resx", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : null;
            }

            return match.Groups["culture"].Value;
        }

        /// <summary>
        /// Attempts to detect culture from RESX headers (e.g. a project-specific
        /// resheader like &lt;resheader name="Language"&gt;en-US&lt;/resheader&gt;).
        /// </summary>
        private static string ResolveCultureFromHeaders(XDocument document)
        {
            if (document.Root == null)
            {
                return null;
            }

            var headers = document.Root.Elements("resheader");
            if (!headers.Any())
            {
                return null;
            }

            foreach (var header in headers)
            {
                var nameAttr = header.Attribute("name")?.Value;
                if (string.IsNullOrWhiteSpace(nameAttr))
                {
                    continue;
                }

                if (!string.Equals(nameAttr, "Language", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(nameAttr, "Culture", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var valueElement = header.Element("value");
                var value = valueElement?.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Helper to build the human-readable embedding text for a single chunk.
        /// This is not required for extraction but keeps the logic centralized.
        /// </summary>
        public static string BuildEmbeddingText(ResxResourceChunk chunk)
        {
            if (chunk == null)
            {
                throw new ArgumentNullException(nameof(chunk));
            }

            // Keep this small and focused – the goal is to
            // describe the resource clearly to the LLM.
            return $"RESX Resource Entry\n\n" +
                   $"Key: {chunk.ResourceKey}\n" +
                   $"Value: {chunk.ResourceValue}\n" +
                   $"Comment: {chunk.Comment ?? ""}\n" +
                   $"Culture: {chunk.Culture}\n" +
                   $"Source: {chunk.RelativePath}";
        }
    }
}
