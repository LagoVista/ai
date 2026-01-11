using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Chunkers.Utils;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Quality.Services;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Indexing.Services
{
    /// <summary>
    /// Default implementation of INormalizedChunkBuilderService.
    /// Drives the Item 7 pipeline from raw source file to NormalizedChunk BuildChunksAsyncinstances.
    /// </summary>
    public class SourceFileProcessor : ISourceFileProcessor
    {
        private readonly IChunkerServices _chunkerServics;
        private readonly ICodeDescriptionService _descriptionServices;
        private readonly IAdminLogger _adminLogger;
        private readonly IInterfaceSemanticEnricher _enricher;

        public SourceFileProcessor(IChunkerServices chunkerServices, ICodeDescriptionService descriptionServices, IInterfaceSemanticEnricher enricher,
            IAdminLogger adminLogger)
        {
            _chunkerServics = chunkerServices ?? throw new ArgumentNullException(nameof(chunkerServices));
            _descriptionServices = descriptionServices ?? throw new ArgumentNullException(nameof(descriptionServices));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _enricher = enricher ?? throw new ArgumentNullException(nameof(enricher));
        }

        /// <summary>
        /// Entry point: orchestrates the 7.x steps for a single file.
        /// </summary>
        public async Task<InvokeResult<ProcessedFileResults>> BuildChunks(IngestionConfig config, IndexFileContext ctx, DomainModelCatalog catalog, SubtypeKind? subTypeFilter, IReadOnlyDictionary<string, string> resources)
        {
            var filePath = ctx.FullPath;


            var result = new InvokeResult<ProcessedFileResults>()
            {
                Result = new ProcessedFileResults()
                {

                }
            };

            var fileText = File.ReadAllText(filePath);

            if (ctx.Contents == null)
            {
                ctx.Contents = System.Text.Encoding.UTF8.GetBytes(fileText);
            }

           
            var fullFileSourceCode = System.Text.Encoding.UTF8.GetString(ctx.Contents);

            var symbolSplitsResult = SplitSymbols(fullFileSourceCode);
            if (!symbolSplitsResult.Successful) return InvokeResult<ProcessedFileResults>.FromInvokeResult(symbolSplitsResult.ToInvokeResult());

            var fileInfo = new FileInfo(filePath);

            var splitSymbols = symbolSplitsResult.Result;
            foreach (var splitSymbol in splitSymbols)
            {
                var subKindResult = AnalyzeSubKinds(splitSymbol.Text, filePath);
                if (subTypeFilter.HasValue && subKindResult.SubKind != subTypeFilter)
                {
                    continue;
                }

                var symbolText = splitSymbol.Text;
                switch (subKindResult.SubKind)
                {
                    case SubtypeKind.Model:
                        {
                            var modelStructureDescription = _descriptionServices.BuildModelStructureDescription(ctx, symbolText, resources);
                            if (modelStructureDescription.Successful)
                            {
                                var headerInfo = FindDomainHeaderInfo(catalog, modelStructureDescription.Result);
                                modelStructureDescription.Result.BuildFinderSnippetSections(headerInfo.Result);
                                var stucturedResults = modelStructureDescription.Result.BuildRagPoints();
                                result.Result.RagPoints.AddRange(stucturedResults.Select(rp => rp.Result));

                            }

                            var modelMetaDataDescription = _descriptionServices.BuildModelMetadataDescription(ctx, symbolText, resources);
                            if (modelMetaDataDescription.Successful)
                            {
                                var headerInfo = FindDomainHeaderInfo(catalog, modelMetaDataDescription.Result);
                                modelMetaDataDescription.Result.BuildSections(headerInfo.Result);
                                var metaDataResults = modelMetaDataDescription.Result.BuildRagPoints();
                                result.Result.RagPoints.AddRange(metaDataResults.Select(rp => rp.Result));
                            }
                        }

                        break;
                    case SubtypeKind.Manager:
                        {
                            var managerDescription = _descriptionServices.BuildManagerDescription(ctx, symbolText);
                            if (managerDescription.Successful)
                            {
                                var headerInfo = FindDomainHeaderInfo(catalog, managerDescription.Result);
                                managerDescription.Result.BuildSections(headerInfo.Result);
                                var managerResults = managerDescription.Result.BuildRagPoints();
                                managerDescription.Result.BuildSections(headerInfo.Result);
                                result.Result.RagPoints.AddRange(managerResults.Select(rp => rp.Result));
                            }
                        }
                        break;

                    case SubtypeKind.Interface:
                        {
                            var interfaceDescription = _descriptionServices.BuildInterfaceDescription(ctx, symbolText);
                            if (interfaceDescription.Successful)
                            {
                                var headerInfo = FindDomainHeaderInfo(catalog, interfaceDescription.Result);
                                interfaceDescription.Result.BuildSections(headerInfo.Result);
                                var enrichResult = await _enricher.EnrichAsync(interfaceDescription.Result, config);
                                var interfaceResults = interfaceDescription.Result.BuildRagPoints();
                                result.Result.RagPoints.AddRange(interfaceResults.Select(rp => rp.Result));
                            }
                        }
                        break;

                    case SubtypeKind.Repository:
                        {
                            var repoDescription = _descriptionServices.BuildRepositoryDescription(ctx, symbolText);
                            if (repoDescription.Successful)
                            {
                                var headerInfo = FindDomainHeaderInfo(catalog, repoDescription.Result);
                                repoDescription.Result.BuildSections(headerInfo.Result);
                                var repoResults = repoDescription.Result.BuildRagPoints();
                                result.Result.RagPoints.AddRange(repoResults.Select(rp => rp.Result));
                            }
                        }
                        break;
                    case SubtypeKind.Controller:
                        {
                            var controllerDescription = _descriptionServices.BuildEndpointDescriptions(ctx, symbolText);
                            if (controllerDescription.Successful)
                            {
                                foreach (var endpoint in controllerDescription.Result)
                                {
                                    var headerInfo = FindDomainHeaderInfo(catalog, endpoint);
                                    endpoint.BuildSections(headerInfo.Result);
                                    var endpointRagPoints = endpoint.BuildRagPoints();
                                    result.Result.RagPoints.AddRange(endpointRagPoints.Select(rp => rp.Result));
                                }
                            }
                        }

                        break;

                    case SubtypeKind.SummaryListModel:
                        var summaryListDescription = _descriptionServices.BuildSummaryDescription(ctx, symbolText, resources);
                        if (summaryListDescription.Successful)
                        {
                            var headerInfo = FindDomainHeaderInfo(catalog, summaryListDescription.Result);
                            summaryListDescription.Result.BuildSections(headerInfo.Result);
                            var summaryResults = summaryListDescription.Result.BuildRagPoints();
                            result.Result.RagPoints.AddRange(summaryResults.Select(rp => rp.Result));
                        }
                        break;
                }

                //var chunks = _chunkerServics.ChunkCSharpWithRoslyn(symbolText, fileInfo.Kind);
                //foreach (var chunk in chunks.Result)
                //{
                //    var points = chunk.BuildRagPoints(ctx);
                //    result.Result.RagPoints.AddRange(points.Select(pt => pt.Result));
                //}
            }
        
            //foreach(var pt in result.Result.RagPoints)
            //{
            //    _adminLogger.Trace($"{pt.Payload.Subtype} - {pt.Payload.Title} {pt.Payload.BlobUri}");
            //    _adminLogger.Trace(System.Text.UTF8Encoding.UTF8.GetString(pt.Contents));
            //    _adminLogger.Trace(new String('-',80));
            //}

            result.Result.OriginalFileBlobUri = ctx.BlobUri;
            result.Result.OriginalFileContents = ctx.Contents;

            return result;
        }

        private InvokeResult<DomainModelHeaderInformation> FindDomainHeaderInfo(DomainModelCatalog catalog, SummaryFacts fact)
        {
            if (string.IsNullOrEmpty(fact.PrimaryEntity))
                return InvokeResult<DomainModelHeaderInformation>.FromError("Object does not have primary entity.");

            var model = catalog.GetModelByName(fact.PrimaryEntity);
            if (model.Successful)
            {
                var domainKey = model.Result.Structure.BusinessDomainKey;
                var domain = catalog.GetDomainByKey(domainKey);
                if (domain.Successful)
                {




                    return InvokeResult<DomainModelHeaderInformation>.Create(new DomainModelHeaderInformation()
                    {
                        DomainKey = domainKey,
                        DomainName = domain.Result.Title,
                        DomainTagLine = domain.Result.Description,
                        ModelName = model.Result.Structure.ModelName,
                        ModelClassName = fact.PrimaryEntity,
                        ModelTagLine = model.Result.Structure.Description
                    });
                }
                else
                    return InvokeResult<DomainModelHeaderInformation>.FromError("Domain not found in catalog.");
            }
            else
                return InvokeResult<DomainModelHeaderInformation>.FromError("Model not found in catalog.");
        }

        private InvokeResult<IReadOnlyList<SplitSymbolResult>> SplitSymbols(string sourceText)
        {
            return SymbolSplitter.Split(sourceText);
        }

        // 7.2 - Source Kind Analysis ---------------------------------------

        private SourceKindResult AnalyzeSubKinds(
            string sourceText,
            string fileName)
        {
            return _chunkerServics.DetectForFile(sourceText, fileName);
        }


        private IReadOnlyList<EnrichedSymbol> EnrichDomainModelMetadata(
            IReadOnlyList<SplitSymbolResult> symbolSplits,
            IReadOnlyList<SubKindDetectionResult> subKindResults)
        {
            // TODO: Use _domainModelCatalog to enrich symbols.
            // - Attach domain name, model name, EntityDescription, etc.
            throw new NotImplementedException();
        }

        // 7.4 - Roslyn Chunking --------------------------------------------

        private IReadOnlyList<SymbolChunkContext> BuildSymbolChunks(
            string sourceText,
            IReadOnlyList<EnrichedSymbol> enrichedSymbols)
        {
            // TODO: Use Roslyn-based chunker per symbol.
            // - Split into methods, properties, regions, etc.
            // - Preserve enough context to build normalized text later.
            throw new NotImplementedException();
        }

        // 7.5 - Normalized Text Construction -------------------------------

        private IReadOnlyList<SymbolChunkContext> BuildNormalizedSymbolChunks(
            string sourceText,
            IReadOnlyList<SymbolChunkContext> symbolChunks)
        {
            // TODO: For each chunk, build the final normalized text:
            // - Standard header (namespace + usings).
            // - Short semantic preface (placeholder for now).
            // - Symbol/region body with cleaned whitespace.
            throw new NotImplementedException();
        }

        // 7.6 - NormalizedChunk Creation -----------------------------------

        private IReadOnlyList<NormalizedChunk> CreateNormalizedChunks(
            string relativePath,
            IReadOnlyList<SymbolChunkContext> normalizedSymbolChunks)
        {
            // TODO: Map SymbolChunkContext -> NormalizedChunk.
            // - Deterministic Id.
            // - File path + symbol info.
            // - Hash, Kind/SubKind.
            // - Domain/model metadata.
            // - NormalizedText.
            throw new NotImplementedException();
        }

        // ------------------------------------------------------------------
        // Local helper models for the pipeline.
        // These keep the internal flow explicit without leaking extra types
        // outside the service.
        // ------------------------------------------------------------------

        private sealed class EnrichedSymbol
        {
            public SplitSymbolResult Symbol { get; set; }
            public SubKindDetectionResult SubKind { get; set; }
            public string DomainName { get; set; }
            public string ModelName { get; set; }
            public string EntityDisplayName { get; set; }
        }

        private sealed class SymbolChunkContext
        {
            public EnrichedSymbol Symbol { get; set; }
            public string ChunkIdHint { get; set; }
            public string NormalizedText { get; set; }
        }
    }
}
