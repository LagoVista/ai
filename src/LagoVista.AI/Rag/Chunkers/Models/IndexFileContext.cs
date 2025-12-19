
namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Context for indexing a single file. This is the contract boundary between
    /// orchestration and the concrete indexing pipeline.
    /// </summary>
    public class IndexFileContext
    {
        /// <summary>
        /// The absolute path of the file on disk.
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// A short textual description for the Repo.
        /// </summary>
        public string RepoId { get; set; }

        /// <summary>
        /// The path of the file relative to the configured SourceRoot.
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Optional language or subkind hints discovered earlier in the pipeline.
        /// </summary>
        public string Language { get; set; }

        public string BlobUri { get; set; }

        public byte[] Contents { get; set; }

        public GitRepoInfo GitRepoInfo { get; set; }

        /// <summary>
        /// Optional document identity if already computed.
        /// </summary>
        public DocumentIdentity DocumentIdentity { get; set; }
    }
}
