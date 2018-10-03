using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IHubManager
    {
        Task<InvokeResult<Hub>> GetHubForOrgAsync(EntityHeader org, EntityHeader user);
    }
}
