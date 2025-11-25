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

        public string Symbol { get; set; }
        public string SymbolType { get; set; }

        public string DomainKey { get; set; }
        public string ModelClassName { get; set; }
        public string ModelName { get; set; }

        public float[] Vectors { get; set; }

        public string EmbeddingModel { get; set; }

        public string SectionNormalizedText { get; set; }

        public async Task<InvokeResult> CreateEmbeddingsAsync(IEmbedder embeder)
        {
            var result = await embeder.EmbedAsync(SectionNormalizedText);
            if(result.Successful)
            {
                Vectors = result.Result.Vector;
                EmbeddingModel = result.Result.EmbeddingModel;
            }

            return result.ToInvokeResult();
        }

        public InvokeResult PopulateRagPayload(RagVectorPayload payload)
        {
            payload.Symbol = Symbol;
            payload.SymbolType = SymbolType;
            payload.SectionKey = SectionKey;
            payload.PartIndex = PartIndex;
            payload.PartTotal = PartTotal;
            payload.EmbeddingModel = EmbeddingModel;
            return InvokeResult.Success;
        }
    }
}
