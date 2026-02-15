using LagoVista.Core.Interfaces;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
using LagoVista.Core.Validation;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Bridge between structured models and normalized, human-readable
    /// text for embedding into the vector store.
    /// </summary>
    public sealed class SummarySection
    {
        public string SectionKey { get; set; }
        public string SectionType { get; set; }
        public string Flavor { get; set; }
        public int PartIndex { get; set; } = 1;
        public int PartTotal { get; set; } = 1;

        public string SymbolName { get; set; }
        public string SymbolType { get; set; }

        public string DomainKey { get; set; }
        public string ModelClassName { get; set; }
        public string ModelName { get; set; }

        public float[] Vectors { get; set; }

        public string EmbeddingModel { get; set; }

        /// <summary>
        /// Canonical, normalized text used for embeddings. In unified
        /// Finder Snippet mode, this will often be the same as
        /// <see cref="FinderSnippet"/>.
        /// </summary>
        public string SectionNormalizedText { get; set; }

        public string RawSourceArtifact { get; set; }
        public string BackingArtifact { get; set; }

        /// <summary>
        /// Optional, highly structured Finder Snippet text as defined
        /// in IDX-068. When present, callers may treat this as the
        /// canonical snippet text for retrieval while keeping richer
        /// narrative content in backing artifacts.
        /// </summary>
        public string FinderSnippet { get; set; }

        public async Task<InvokeResult> CreateEmbeddingsAsync(IEmbedder embeder)
        {
            // For now we continue to embed SectionNormalizedText. In unified
            // Finder Snippet mode, builders should set SectionNormalizedText
            // to the FinderSnippet text so existing callers continue to work.
            var result = await embeder.EmbedAsync(SectionNormalizedText);
            if (result.Successful)
            {
                Vectors = result.Result.Vector;
                EmbeddingModel = result.Result.EmbeddingModel;
            }

            return result.ToInvokeResult();
        }

        public InvokeResult PopulateRagPayload(RagVectorPayload payload)
        {
            payload.Extra.SymbolName = SymbolName;
            payload.Extra.SymbolType = SymbolType;
            payload.Meta.SectionKey = SectionKey;
            payload.Meta.PartIndex = PartIndex;
            payload.Meta.PartTotal = PartTotal;
            payload.Meta.EmbeddingModel = EmbeddingModel;
            return InvokeResult.Success;
        }
    }
}
