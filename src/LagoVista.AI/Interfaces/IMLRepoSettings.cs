// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: bf2666dbdbffedd3e07b9c1b2147071fdf3a6843d54c9f2fc2eefcc1f12341a2
// IndexVersion: 2
// --- END CODE INDEX META ---
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
