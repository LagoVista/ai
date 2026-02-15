using System.Text;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    public enum DocumentType
    {
        CSharp,
        Html,
        TypeScript,
        JavaScript,
        Css,
    }

    /// <summary>
    /// Canonical document identity used across all RAG and indexing operations.
    /// Implements IDX-001, IDX-002, IDX-003 semantics.
    /// </summary>
    public sealed class DocumentIdentity
    {
        /// <summary>
        /// Organization / tenant identifier (guid)
        /// </summary>
        public string OrgId { get; set; }


        /// <summary>
        /// Unique org namespace (human readable)
        /// </summary>
        public string OrgNamespace { get; set; }

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

        public DocumentType Type { get; set; } 
       
    }
}
