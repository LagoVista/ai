using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.CloudRepos
{
    public class ModelRepo : DocumentDBRepoBase<Model>, IModelRepo
    {
        private readonly bool _shouldConsolidateCollections;
        public ModelRepo(IMLRepoSettings repoSettings, IAdminLogger logger) : 
            base(repoSettings.MLDocDbStorage.Uri, repoSettings.MLDocDbStorage.AccessKey, repoSettings.MLDocDbStorage.ResourceName, logger)
        {
            _shouldConsolidateCollections = repoSettings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddMLModelAsync(string orgId, string modelId, string revisionId, byte[] model)
        {
            throw new NotImplementedException();
        }

        public Task AddModelAsync(Model model)
        {
            throw new NotImplementedException();
        }

        public Task DeleteModelAsync(string id)
        {
            throw new NotImplementedException();
        }

        public Task<InvokeResult<byte[]>> GetMLModelAsync(string orgId, string modelId)
        {
            throw new NotImplementedException();
        }

        public Task<Model> GetModelAsync(string modelId)
        {
            throw new NotImplementedException();
        }

        public Task<ListResponse<ModelSummary>> GetModelSummariesForOrgAsync(string orgId, ListRequest listRequest)
        {
            throw new NotImplementedException();
        }

        public Task<bool> QueryKeyInUseAsync(string key, string org)
        {
            throw new NotImplementedException();
        }

        public Task UpdateModelAsync(Model model)
        {
            throw new NotImplementedException();
        }
    }
}
