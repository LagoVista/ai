using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Models;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Models

{
    /// <summary>
    /// Canonical normalized chunk produced by the chunking pipeline
    /// and passed into the embedding / vector ingestion stage.
    /// </summary>
    public sealed class NormalizedChunk
    {
        /// <summary>
        /// Canonical document / chunk identity (IDX-001, IDX-002, IDX-003).
        /// </summary>
        public DocumentIdentity Identity { get; set; }

        /// <summary>
        /// High-level content kind (e.g. SourceCode, DomainDocument, Spec, etc).
        /// Should map to RagContentType in the next stage.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// Lower-level classification derived from SubKind detector (Model, Manager, Domain, etc).
        /// </summary>
        public string SubKind { get; set; }

        /// <summary>
        /// Final, LLM-ready text blob. This includes:
        /// - Header (org / project / repo / path / symbol)
        /// - Optional summary
        /// - Code / extracted text
        /// </summary>
        public string NormalizedText { get; set; }

        /// <summary>
        /// Optional token estimation to help with batching & limits.
        /// </summary>
        public int? EstimatedTokens { get; set; }

        /// <summary>
        /// Optional vector embedding (populated later).
        /// </summary>
        public float[] Embedding { get; set; }

        /// <summary>
        /// Optional extra metadata to attach to the vector payload.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
}
