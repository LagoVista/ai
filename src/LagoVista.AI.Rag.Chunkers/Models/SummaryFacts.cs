using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        public virtual RagContentType ContentTypeId { get => RagContentType.SourceCode; }
        public abstract string Subtype { get;  }
        public virtual string SubtypeFlavor { get;  }

        public string OrgId { get; set; }
        public string OrgNamespace { get; set; }
        public string RepoId { get; set; }
        public string ProjectId { get; set; }

        public string Repo { get; set; }
        public string Branch { get; set; }
        public string CommitSha { get; set; }
        public string Path { get; set; }

        public string BlobUri { get; set; }

        public string SourceSystem { get; set; } = "GitHub";
        public string SourceObjectId { get; set; }

        public string SymbolName { get; set; }

        /// <summary>
        /// Primary entity name that this Manager orchestrates, e.g. "Device".
        /// May be null if heuristics cannot determine a clear entity.
        /// </summary>
        public string PrimaryEntity { get; set; }


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
            OrgNamespace = ctx.DocumentIdentity.OrgNamespace;
            ProjectId = ctx.DocumentIdentity.ProjectId;
            Branch = ctx.GitRepoInfo.BranchRef;
            Path = ctx.RelativePath;
            CommitSha = ctx.GitRepoInfo.CommitSha;
            BlobUri = ctx.BlobUri;
            RepoId = ctx.RepoId;
            
        }

        protected virtual InvokeResult PopulateAdditionalRagProperties(RagVectorPayload payload)
        {
            return InvokeResult.Success;
            // Override in derived classes to populate additional properties
        }   

        public IEnumerable<InvokeResult<IRagPoint>> BuildRagPoints()
        {
            var payloadResults = new List<InvokeResult<IRagPoint>>();

            if (_summarySections == null)
                throw new ArgumentNullException($"Must create summary sections prior to calling CreateIRagPoints - {QualifiedName}.");

            var dualColonRegEx = new Regex(@"::");

            foreach (var section in _summarySections)
            {
                var payload = new RagVectorPayload()
                {
                    DocId = this.DocId,
                    OrgNamespace = this.OrgNamespace,
                    ProjectId = this.ProjectId,
                    Repo = this.Repo,
                    RepoBranch = this.Branch,
                    CommitSha = this.CommitSha,
                    SectionKey = section.SectionKey,
                    EmbeddingModel =  section.EmbeddingModel,
                    BusinessDomainKey = section.DomainKey,
                    ContentTypeId = ContentTypeId,
                    Subtype = this.Subtype,
                    SubtypeFlavor = this.SubtypeFlavor,
                    Language = "en-US",
                };

                payload.Title = $"{section.SymbolType}: {section.Symbol} - {section.SectionKey} (Chunk {section.PartIndex} of {section.PartTotal})";
                payload.SemanticId = $"{this.OrgNamespace}:{this.ProjectId}:{this.RepoId}:{section.SymbolType}:{section.Symbol}:{section.SectionKey}:{section.PartIndex}".ToLower();

                if(dualColonRegEx.Match(payload.SemanticId).Success)
                {
                    throw new ArgumentNullException("Semantic ID should not have two :: in a row, that means a field is missing, code should encorce this.");
                }

                section.PopulateRagPayload(payload);

                payload.FullDocumentBlobUri = this.BlobUri.ToLower();
                payload.DescriptionBlobUri = $"{this.BlobUri}.{section.ModelClassName}/{section.SectionKey}.{section.PartIndex}".ToLower().Replace(" ", "_").ToLower();

                var result = PopulateAdditionalRagProperties(payload);

                var point = new RagPoint
                {
                    PointId = Guid.NewGuid().ToString(),
                    Payload = payload,
                    Vector = section.Vectors,
                    FinderSnippet = Encoding.UTF8.GetBytes(section.FinderSnippet ?? section.SectionNormalizedText),
                    Contents = Encoding.UTF8.GetBytes(section.SectionNormalizedText)
                };

                payloadResults.Add(InvokeResult<IRagPoint>.Create( point));
            }

            var uniqueBlobIds = payloadResults.Select(pay => pay.Result.Payload.DescriptionBlobUri).Distinct();
            if(uniqueBlobIds.Count() != payloadResults.Count())
            {
                throw new ArgumentNullException("Blob uris within a vector payload must be unique");
            }
          
            return payloadResults;
        }

    }
}
