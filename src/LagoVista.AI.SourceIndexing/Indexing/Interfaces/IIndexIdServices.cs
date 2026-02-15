namespace LagoVista.AI.Indexing.Interfaces
{
    public interface IIndexIdServices
    {
        string BuildCanonicalPath(string projectId, string pathInRepo);
        string BuildCanonicalString(string repoUrl, string canonicalPath);
        string ComputeDocId(string repoUrl, string canonicalPath);
        string ComputeDocId(string repoUrl, string projectId, string pathInRepo);
        string NewPointId();
        string NormalizeRepoUrl(string repoUrl);
    }
}