using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.Azure.Cosmos.Linq;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class VectorDatabaseRepo : DocumentDBRepoBase<VectorDatabase>, IVectorDatabaseRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public VectorDatabaseRepo(IMLRepoSettings settings, IAdminLogger logger) :
            base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, logger)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddVectorDatabaseAsync(VectorDatabase VectorDatabase)
        {
            return this.CreateDocumentAsync(VectorDatabase);
        }

        public Task DeleteVectorDatabaseAsync(string id)
        {
            return this.DeleteDocumentAsync(id);  
        }

        public Task<VectorDatabase> GetVectorDatabaseAsync(string id)
        {
            return this.GetDocumentAsync(id);
        }

        public  Task<ListResponse<VectorDatabaseSummary>> GetVectorDatabasesForOrgAsync(string orgId, ListRequest request)
        {
            return QuerySummaryAsync<VectorDatabaseSummary, VectorDatabase>(vc => vc.OwnerOrganization.Id == orgId, vdb => vdb.Name, request);
        }

        public async Task<bool> QueryKeyInUseAsync(string key, string orgId)
        {
            var items = await base.QueryAsync(attr => (attr.OwnerOrganization.Id == orgId || attr.IsPublic == true) && attr.Key == key);
            return items.Any();
        }

        
        public Task UpdateVectorDatabaseAsync(VectorDatabase VectorDatabase)
        {
            return this.UpsertDocumentAsync(VectorDatabase);
        }
    }
}

