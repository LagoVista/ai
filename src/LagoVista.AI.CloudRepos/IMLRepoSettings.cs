using LagoVista.Core.Interfaces;


namespace LagoVista.AI.CloudRepos
{
    public interface IMLRepoSettings
    {
        IConnectionSettings MLDocDbStorage { get; set; }
        IConnectionSettings MLBlobStorage { get; set; }
        IConnectionSettings MLTableStorage { get; set; }
        bool ShouldConsolidateCollections { get; }
    }
}
