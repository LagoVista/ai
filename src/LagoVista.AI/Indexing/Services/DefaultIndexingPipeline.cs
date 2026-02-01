using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.Services
{
    /// <summary>
    /// Default indexing pipeline implementation. This is the place where
    /// we eventually wire together:
    ///  - Roslyn-based chunking
    ///  - Embedding calls
    ///  - Qdrant upsert
    ///  - Registry updates
    ///  - Inline manifest tracking
    ///
    /// For now, this class is a skeleton that reads the file and provides
    /// a single point where indexing logic will live.
    /// </summary>
    public sealed class DefaultIndexingPipeline : IIndexingPipeline
    {
        private readonly IPersistSourceFileStep _piplineStep;

        public DefaultIndexingPipeline(IPersistSourceFileStep piplineStep)
        {
            _piplineStep = piplineStep ?? throw new ArgumentNullException(nameof(piplineStep)); 
        }

        public async Task IndexFileAsync(DomainModelCatalog domainCatalog, Dictionary<string, string> resourceDictionary, IndexFileContext context, CancellationToken token = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (string.IsNullOrWhiteSpace(context.FullPath))
                throw new ArgumentException("IndexFileContext.FullPath is required.", nameof(context));

            if (!File.Exists(context.FullPath))
                return;

            using (var stream = File.OpenRead(context.FullPath))
            using (var reader = new StreamReader(stream))
            {
                var content = await reader.ReadToEndAsync().ConfigureAwait(false);
                context.Contents = content;
                var indexContext = new Models.IndexingPipelineContext(context, domainCatalog, resourceDictionary);
                var workItem = new Models.IndexingWorkItem();
                indexContext.AddWorkItem(workItem);
                await _piplineStep.ExecuteAsync(indexContext);
            }
        }
    }
}
