using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Indexing.Models
{
    /// <summary>
    /// Represents a file that used to be in the index but has disappeared from
    /// the local file system. Orchestration passes this to the pipeline or
    /// registry so they can remove vectors / metadata.
    /// </summary>
    public class MissingFileContext
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
        /// Repository identifier (logical).
        /// </summary>
        public string RepoId { get; set; }

        /// <summary>
        /// Relative path of the file that has gone missing.
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Previously assigned document identity, if known.
        /// </summary>
        public DocumentIdentity DocumentIdentity { get; set; }
    }
}
