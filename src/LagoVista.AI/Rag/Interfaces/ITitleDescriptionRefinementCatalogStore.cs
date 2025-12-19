using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Interfaces
{
    public interface ITitleDescriptionRefinementCatalogStore
    {
        Task<TitleDescriptionCatalog> LoadAsync(CancellationToken cancellationToken);
        Task SaveAsync(TitleDescriptionCatalog catalog, CancellationToken cancellationToken);
    }
}
