using LagoVista.AI.Indexing;
using LagoVista.Core.Models;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Services
{
    public interface IEntityIndexDocumentBuilder
    {
        Task<EntityIndexDocument> BuildAsync(EntityBase entity);
    }
}
