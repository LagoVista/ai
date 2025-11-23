using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    public abstract class SummaryFacts : IRagableEntity
    {
        IEnumerable<SummarySection> _summarySections;

        public string DocId { get; set; }
        // ---------- Identity / Domain ----------
        public string ModelName { get; set; }
        public string Namespace { get; set; }
        public string QualifiedName { get; set; }   // Namespace + ModelName
        public string Domain { get; set; }          // e.g. "Devices", "Alerts"

        public abstract RagContentType ContentType { get; }
        public abstract string Subtype { get; set; }
        public virtual string SubtypeFlavor { get; set; }

        public async Task<InvokeResult> CreateEmbeddingsAsync(IEmbedder embeddingService)
        {
            var result = new InvokeResult();
            foreach (var section in _summarySections)
            {
                var embedResult = await section.CreateEmbeddingsAsync(embeddingService);
                result.Errors.AddRange(embedResult.Errors);
                result.Warnings.AddRange(embedResult.Warnings);
            }

            return result;
        }

        protected virtual InvokeResult PopulateAdditionalRagProperties(RagVectorPayload payload)
        {
            return InvokeResult.Success;
            // Override in derived classes to populate additional properties
        }   

        public IEnumerable<InvokeResult<RagVectorPayload>> CreateRagPayloads()
        {
            var payloadResults = new List<InvokeResult<RagVectorPayload>>();

            foreach (var section in _summarySections)
            {
                var payload = new RagVectorPayload()
                {
                    DocId = this.DocId,
                    EmbeddingModel =  section.EmbeddingModel,
                    DomainKey = Domain,
                    ContentType = ContentType.ToString(),
                    Subtype = this.Subtype,
                    SubtypeFlavor = this.SubtypeFlavor,
                    PartIndex = section.PartIndex,
                    PartTotal = section.PartTotal,
                };

                var result = PopulateAdditionalRagProperties(payload);
            }
          
            return payloadResults;
        }

    }
}
