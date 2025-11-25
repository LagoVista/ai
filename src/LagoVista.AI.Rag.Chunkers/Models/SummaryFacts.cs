using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
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
        protected IEnumerable<SummarySection> _summarySections;
        /// <summary>
        /// Logical document identifier (IDX-001) for the source file.
        /// </summary>
        public string DocId { get; set; }

        // ---------- Identity / Domain ----------
        public string Namespace { get; set; }
        public string QualifiedName { get; set; }   // Namespace + ModelName

        public string BusinessDomainKey { get; set; }          // e.g. "Devices", "Alerts"

        public RagContentType ContentTypeId { get => RagContentType.SourceCode; }
        public abstract string Subtype { get;  }
        public virtual string SubtypeFlavor { get;  }

        public string OrgId { get; set; }
        public string ProjectId { get; set; }

        public string Repo { get; set; }
        public string Branch { get; set; }
        public string CommitSha { get; set; }
        public string Path { get; set; }

        public string BlobUri { get; set; }

        public string SourceSystem { get; set; } = "GitHub";
        public string SourceObjectId { get; set; }

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

        public void SetCommonProperties(IndexFileContext ctx)
        {
            DocId = ctx.DocumentIdentity.DocId;
            Repo = ctx.GitRepoInfo.RemoteUrl;
            OrgId = ctx.DocumentIdentity.OrgId;
            ProjectId = ctx.DocumentIdentity.ProjectId;
            Branch = ctx.GitRepoInfo.BranchRef;
            Path = ctx.RelativePath;
            CommitSha = ctx.GitRepoInfo.CommitSha;
            BlobUri = ctx.BlobUri;
        }

        protected virtual InvokeResult PopulateAdditionalRagProperties(RagVectorPayload payload)
        {
            return InvokeResult.Success;
            // Override in derived classes to populate additional properties
        }   

        public IEnumerable<InvokeResult<IRagPoint>> CreateIRagPoints()
        {
            var payloadResults = new List<InvokeResult<IRagPoint>>();

            foreach (var section in _summarySections)
            {
                var payload = new RagVectorPayload()
                {
                    DocId = this.DocId,
                    OrgId = this.OrgId,
                    ProjectId = this.ProjectId,
                    Repo = this.Repo,
                    RepoBranch = this.Branch,
                    CommitSha = this.CommitSha,
                    SectionKey = section.SectionKey,
                    EmbeddingModel =  section.EmbeddingModel,
                    BusinessDomainKey = this.BusinessDomainKey,
                    ContentTypeId = ContentTypeId,
                    Subtype = this.Subtype,
                    BlobUri =  $"{this.BlobUri}.{section.PartIndex}.{section.SectionKey}",
                    SubtypeFlavor = this.SubtypeFlavor,
                    Language = "en-US",
                };

                section.PopulateRagPayload(payload);

                var result = PopulateAdditionalRagProperties(payload);

                var point = new RagPoint
                {
                    PointId = Guid.NewGuid().ToString(),
                    Payload = payload,
                    Vector = section.Vectors,
                    Contents = Encoding.UTF8.GetBytes(section.SectionNormalizedText)
                };
            }
          
            return payloadResults;
        }

    }
}
