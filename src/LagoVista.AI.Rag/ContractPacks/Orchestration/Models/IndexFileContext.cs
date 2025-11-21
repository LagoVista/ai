using System.Collections.Generic;

namespace LagoVista.AI.Rag.Models
{
    /// <summary>
    /// Context for indexing a single file. This is the contract boundary between
    /// orchestration and the concrete indexing pipeline.
    /// </summary>
    public class IndexFileContext
    {
        /// <summary>
        /// Organization / tenant identifier.
        /// </summary>
        public string OrgId { get; set; }

        /// <summary>
        /// Project identifier, if applicable.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Repository identifier (logical, not necessarily remote URL).
        /// </summary>
        public string RepoId { get; set; }

        /// <summary>
        /// The absolute path of the file on disk.
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// The path of the file relative to the configured SourceRoot.
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Optional language or subkind hints discovered earlier in the pipeline.
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Optional document identity if already computed.
        /// </summary>
        public DocumentIdentity DocumentIdentity { get; set; }

        /// <summary>
        /// Arbitrary metadata that earlier stages in the pipeline want to attach
        /// to this indexing operation (e.g. git info, repo tags, etc.).
        /// </summary>
        public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
