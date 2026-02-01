using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Services;              // CSharpSymbolSplitter, ModelSourceAnalyzer
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Partial class containing the guts for model (EntityDescription) extraction.
    ///
    /// This implementation uses ModelSourceAnalyzer directly (instead of
    /// ModelStructureDescriptionBuilder) and surfaces only the subset needed
    /// for IDX-071 (ModelClassEntry), while still honoring resource-based
    /// resolution of EntityDescription attributes.
    /// </summary>
    public sealed partial class DomainCatalogService 
    {
        public Task<InvokeResult> ProcessAsync(IndexingPipelineContext ctx, IndexingWorkItem workItem)
        {
            return Task.FromResult(InvokeResult.Success);
        }

        /// <summary>
        /// Extracts all ModelClassEntry instances from the provided files.
        ///
        /// Rules:
        /// - Only .cs files are considered.
        /// - Any file under tests/... is ignored.
        /// - Uses CSharpSymbolSplitter to obtain one-class snippets.
        /// - For each snippet, calls ModelSourceAnalyzer.Analyze with the
        ///   provided resources dictionary.
        /// - Only models with all required fields are included in the catalog.
        /// </summary>
        private async Task<IReadOnlyList<ModelClassEntry>> ExtractModelClassesAsync(
            IReadOnlyList<DiscoveredFile> files,
            IReadOnlyDictionary<string, string> resources,
            CancellationToken cancellationToken)
        {
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            var models = new List<ModelClassEntry>();

            _adminLogger.Trace($"[DomainCatalogService__ExtractModelClassesAsync] - scanning {files.Count} files for [EntityDescription] models.");

            for (var idx = 0; idx < files.Count; idx++)
            {
                var file = files[idx];

                if (idx % 100 == 0)
                {
                    _adminLogger.Trace(
                        $"[DomainCatalogService__ExtractModelClassesAsync] - scanned {idx} of {files.Count} files - found {models.Count} models, {(idx * 100.0 / Math.Max(1, files.Count)):0.0}% complete.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Only consider C# files.
                if (!file.FullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Exclude tests root: tests/... should never contribute to the catalog.
                var relative = (file.RelativePath ?? string.Empty).Replace('\\', '/');
                if (relative.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ||
                    relative.Equals("tests", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!File.Exists(file.FullPath))
                {
                    throw new FileNotFoundException(
                        $"Discovered file does not exist on disk: '{file.FullPath}'.",
                        file.FullPath);
                }

                var source = await File.ReadAllTextAsync(file.FullPath, cancellationToken).ConfigureAwait(false);

                // Fast pre-check: only pay CSharpSymbolSplitter/analyzer cost if the file
                // even mentions [EntityDescription].
                if (source.IndexOf("[EntityDescription", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                //var splitterResults = CSharpSymbolSplitter.Chunk(source);
                //if (!splitterResults.Successful)
                //{
                //    throw new InvalidOperationException(
                //        $"SymbolSplitter failed for file '{file.RelativePath ?? file.FullPath}'.");
                //}

                //foreach (var snippet in splitterResults.Result)
                //{
                //    cancellationToken.ThrowIfCancellationRequested();

                //    var text = snippet.Text;
                //    if (string.IsNullOrWhiteSpace(text))
                //    {
                //        continue;
                //    }

                //    var modelEntry = ExtractModelFromSnippet(text, relative, resources);
                //    if (modelEntry == null)
                //    {
                //        continue; // not an interesting or incomplete model
                //    }

                //    models.Add(modelEntry);
                //}
            }

            return models;
        }

        /// <summary>
        /// Core helper to turn a single-class snippet into a ModelClassEntry.
        ///
        /// This uses ModelSourceAnalyzer.Analyze to honor EntityDescription
        /// attributes and resource resolution. Only a minimal projection of
        /// the analyzer result is used for the catalog.
        ///
        /// This is intentionally private and exercised via tests using
        /// reflection, similar to the domain extraction helper.
        /// </summary>
        private static ModelClassEntry ExtractModelFromSnippet(
            string source,
            string relativePath,
            IReadOnlyDictionary<string, string> resources)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            var analysisResult = ModelSourceAnalyzer.Analyze(source, resources);
            if (!analysisResult.Successful || analysisResult.Result == null)
            {
                // Not a valid/interesting model snippet for our purposes.
                return null;
            }

            var analysis = analysisResult.Result;

            // Pull the minimal set needed for IDX-071. These properties mirror
            // the ones previously used by ModelStructureDescriptionBuilder.
            var domainKey = analysis.Domain;
            var modelName = analysis.ModelName;
            var qualifiedName = analysis.QualifiedName;
            var title = analysis.Title;
            var description = analysis.Description;
            var help = analysis.Help;

            // Ensure all mandatory values are present. If any are missing, this
            // model is not catalog-worthy and will be skipped by the caller.
            if (string.IsNullOrWhiteSpace(domainKey) ||
                string.IsNullOrWhiteSpace(modelName) ||
                string.IsNullOrWhiteSpace(qualifiedName) ||
                string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(description) ||
                string.IsNullOrWhiteSpace(help))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidOperationException("Relative path is required for ModelClassEntry.");
            }

            var normalizedPath = relativePath.Replace('\\', '/');

            // Construct the catalog entry – ctor enforces non-empty fields.
            return new ModelClassEntry(
                domainKey: domainKey,
                className: modelName,
                qualifiedClassName: qualifiedName,
                title: title,
                description: description,
                helpText: help,
                relativePath: normalizedPath);
        }
    }
}
