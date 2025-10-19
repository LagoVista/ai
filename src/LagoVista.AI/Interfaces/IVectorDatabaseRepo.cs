using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IVectorDatabaseRepo
    {
        Task AddVectorDatabaseAsync(VectorDatabase vectorDatabase);
        Task UpdateVectorDatabaseAsync(VectorDatabase vectorDatabase);
        Task DeleteVectorDatabaseAsync(string id);
        Task<VectorDatabase> GetVectorDatabaseAsync(string id);
        Task<ListResponse<VectorDatabaseSummary>> GetVectorDatabasesForOrgAsync(string orgId, ListRequest listRequest);
        Task<bool> QueryKeyInUseAsync(string key, string org);

    }
}
