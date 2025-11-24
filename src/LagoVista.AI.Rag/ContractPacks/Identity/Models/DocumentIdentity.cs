using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Rag.Models
{
    /// <summary>
    /// Canonical document identity used across all RAG and indexing operations.
    /// Implements IDX-001, IDX-002, IDX-003 semantics.
    /// </summary>
    public sealed class DocumentIdentity
    {
        /// <summary>
        /// Organization / tenant identifier.
        /// </summary>
        public string OrgId { get; set; }

        /// <summary>
        /// Optional project identifier.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Repository identifier.
        /// </summary>
        public string RepoId { get; set; }

        /// <summary>
        /// Path of the document relative to the repo or source root.
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Computed deterministic document id (IDX-001).
        /// </summary>
        public string DocId { get; set; }

        /// <summary>
        /// Primary symbol name (optional but strongly recommended).
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Symbol type such as Model, Manager, Domain, Controller, etc.
        /// </summary>
        public string SymbolType { get; set; }

        /// <summary>
        /// Optional chunk identifier (IDX-003).
        /// </summary>
        public string ChunkId { get; set; }

        /// <summary>
        /// Optional section key (IDX-003) for summary sections such as
        /// "model-properties" or "domain-overview".
        /// </summary>
        public string SectionKey { get; set; }

        /// <summary>
        /// Compute the canonical ChunkId.
        /// </summary>
        public void ComputeChunkId()
        {
            ChunkId = BuildId(OrgId, ProjectId, RepoId, RelativePath, SectionKey);
        }

        private static string BuildId(params string[] parts)
        {
            var sb = new StringBuilder();

            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    if (sb.Length > 0)
                        sb.Append(":");

                    sb.Append(part.Trim().ToLowerInvariant());
                }
            }

            return sb.ToString();
        }
    }
}
