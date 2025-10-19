using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IVectorDatabaseManager
    {
        Task<InvokeResult> AddVectorDatabaseAsync(LagoVista.AI.Models.VectorDatabase vectorDatabase, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateVectorDatabaseAsync(Models.VectorDatabase vectorDatabase, EntityHeader org, EntityHeader user);
        Task<Models.VectorDatabase> GetVectorDatabaseAsync(string id, EntityHeader org, EntityHeader user);
        Task<Models.VectorDatabase> GetVectorDatabaseWithSecretsAsync(string id, EntityHeader org, EntityHeader user);
        Task<InvokeResult> DeleteVectorDatabaseAsync(string id, EntityHeader org, EntityHeader user);
        Task<ListResponse<VectorDatabaseSummary>> GetVectorDatabasesForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);
    }
}
