using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
using LagoVista.Core.Validation;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace LagoVista.AI.Rag.Chunkers.Models
 {
        /// <summary>
        /// Normalized chunk for a C# source component (file, type, member).
        /// Replaces RawChunk/RagChunk for C# assets.
        /// </summary>
        public class CSharpComponentChunk
        {
            /// <summary>
            /// Symbol name (method, type, property, etc.).
            /// </summary>
            public string SymbolName { get; set; }

            /// <summary>
            /// High-level kind: "file", "type", "method", "property", "field", "event", etc.
            /// </summary>
            public string SymbolKind { get; set; }

            /// <summary>
            /// Optional grouping key (often matches SymbolKind).
            /// </summary>
            public string SectionKey { get; set; }

            /// <summary>
            /// 1-based starting line number in the original file.
            /// </summary>
            public int LineStart { get; set; }

            /// <summary>
            /// 1-based ending line number in the original file (inclusive).
            /// </summary>
            public int LineEnd { get; set; }

            /// <summary>
            /// 0-based character index in the original file where this chunk begins.
            /// </summary>
            public int StartCharacter { get; set; }

            /// <summary>
            /// 0-based character index in the original file where this chunk ends (exclusive).
            /// </summary>
            public int EndCharacter { get; set; }

            /// <summary>
            /// Pre-computed token estimate for this chunk.
            /// </summary>
            public int EstimatedTokens { get; set; }
            public string EmbeddingModel { get; set; }

            /// <summary>
            /// Normalized text for this chunk, including any injected summary comment.
            /// </summary>
            public string Text { get; set; }

            /// <summary>
            /// Sequential ordering number for this chunk within the file.
            /// </summary>
            public int PartIndex { get; set; }

            /// <summary>
            /// Total number of chunks produced for the file.
            /// </summary>
            public int PartTotal { get; set; }

        public float[] Vector { get; set; }

        public async Task<InvokeResult> CreateEmbeddingsAsync(IEmbedder embeddingService)
        {
           var result = await embeddingService.EmbedAsync(Text);
            Vector = result.Result.Vector;
            EmbeddingModel = result.Result.EmbeddingModel;

            return result.ToInvokeResult();
        }

        public IEnumerable<InvokeResult<IRagPoint>> CreateIRagPoints(IndexFileContext fileContext)
        {
            var dualColonRegEx = new Regex(@"::");

            var payload = new RagVectorPayload()
            {
                DocId = fileContext.DocumentIdentity.DocId,
                OrgNamespace = fileContext.DocumentIdentity.OrgNamespace,
                ProjectId = fileContext.DocumentIdentity.ProjectId,
                Repo = fileContext.GitRepoInfo.RemoteUrl,
                RepoBranch = fileContext.GitRepoInfo.BranchRef,
                Path = fileContext.RelativePath,
                CommitSha = fileContext.GitRepoInfo.CommitSha,
                SourceSystem = "GitHub",
                Symbol = SymbolName,
                SymbolType = SymbolKind,
                SectionKey = SectionKey,
                EmbeddingModel = EmbeddingModel,
                PartIndex = PartIndex,
                PartTotal = PartTotal,
                CharStart = StartCharacter,
                CharEnd = EndCharacter,
                ContentTypeId = RagContentType.SourceCode,
                Language = "en-US",
                Subtype = "RawCode",
                SysDomain = "Backend",
            };

            payload.FullDocumentBlobUri = fileContext.BlobUri;
            payload.SourceSliceBlobUri = $"{fileContext.BlobUri}.{SymbolKind}.{SymbolName}.{PartIndex}";

            payload.Title = $"{SymbolKind}: {SymbolName} - {SectionKey} (Chunk {PartIndex} of {PartTotal})";
            payload.SemanticId = $"{fileContext.DocumentIdentity.OrgNamespace}:{fileContext.DocumentIdentity.ProjectId}:{fileContext.DocumentIdentity.RepoId}:{SymbolKind}:{SymbolName}:{SectionKey}:{PartIndex}".ToLower();
            if (dualColonRegEx.Match(payload.SemanticId).Success)
            {
                throw new ArgumentNullException("Semantic ID should not have two :: in a row, that means a field is missing, code should encorce this.");
            }

            var ragPoint = new RagPoint
            {
                PointId = Guid.NewGuid().ToString(),
                Payload = payload,
                Vector = Vector,
                Contents = System.Text.ASCIIEncoding.ASCII.GetBytes(Text)
            };

            return new List<InvokeResult<IRagPoint>>() { InvokeResult<IRagPoint>.Create(ragPoint) };
        }
    }
}
