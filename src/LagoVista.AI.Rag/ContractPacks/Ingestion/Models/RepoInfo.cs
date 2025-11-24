namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Models
{
    public sealed class RepoInfo
    {
        public string RepositoryRoot { get; set; }
        public string RemoteUrl { get; set; }      // origin if available; else first remote
        public string CommitSha { get; set; }      // 40-hex commit
        public string BranchRef { get; set; }      // e.g. "refs/heads/main" or null if detached
        public bool IsDetachedHead => string.IsNullOrWhiteSpace(BranchRef);

        public override string ToString()
        {
            return $"Repository Root: {RepositoryRoot}, Remote Url: {RemoteUrl}, Commit SHA: {CommitSha}, Branch Ref: {BranchRef}";
        }

    }
 }
