using LagoVista.AI.Indexing;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Services
{
    public interface IEntityIndexDocumentBuilder
    {
        Task<InvokeResult<EntityIndexDocument>> BuildAsync(IEntityBase entity);
    }
}
