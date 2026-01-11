using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Indexing.Models;

namespace LagoVista.AI.Quality.Interfaces
{
    public interface ITitleDescriptionRefinementCatalogStore
    {
        Task<TitleDescriptionCatalog> LoadAsync(CancellationToken cancellationToken);
        Task SaveAsync(TitleDescriptionCatalog catalog, CancellationToken cancellationToken);
    }
}
