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
                Vectors = result.Result;
            }

            return result.ToInvokeResult();
        }

        public InvokeResult PopulateRagPayload(RagVectorPayload payload)
        {
            payload.EmbeddingModel = EmbeddingModel;
            payload.

            return InvokeResult.Success;
        }
    }


    /// <summary>
    /// Contract implemented by structured description models that know
    /// how to project themselves into SummarySection instances.
    /// </summary>
    public interface ISummarySectionBuilder
    {
        IEnumerable<SummarySection> BuildSections(DomainModelHeaderInformation headerInfo, int maxTokens = 6500);
    }
}
