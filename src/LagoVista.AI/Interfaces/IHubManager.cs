// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: c73ba3b7826c3c5993bfae49c938b455e392e9d0e171e16ce78edbaead458acf
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IHubManager
    {
        Task<InvokeResult<Hub>> GetHubForOrgAsync(EntityHeader org, EntityHeader user);
    }
}
