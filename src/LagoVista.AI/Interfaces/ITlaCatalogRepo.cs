using LagoVista.AI.Models;
using LagoVista.Core.Models;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface ITlaCatalogRepo
    {
        Task<DdrTlaCatalog> GetTlaCatalogAsync(EntityHeader org, EntityHeader user);

        Task UpdateTlaCatalog(DdrTlaCatalog ddrTla);

    }
}
