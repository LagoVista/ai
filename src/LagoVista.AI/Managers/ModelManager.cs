using LagoVista.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.PlatformSupport;
using LagoVista.Core.Validation;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class ModelManager : ManagerBase, IModelManager
    {
        public ModelManager(ILogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security) 
            : base(logger, appConfig, dependencyManager, security)
        {
        }

        IModelRepo _repo;

        public async Task<InvokeResult> AddModelAsync(Model model, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(model, Actions.Create);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _repo.AddModelAsync(model);

            return InvokeResult.Success;
        }

        public Task<InvokeResult<ModelRevision>> AddRevisionAsync(string modelId, ModelRevision revision, EntityHeader org, EntityHeader user)
        {
            throw new NotImplementedException();
        }

        public async Task<InvokeResult> UploadModel(string modelId, string revisionId, byte[] model, EntityHeader org, EntityHeader user)
        {
            await this._repo.AddMLModelAsync(org.Id, modelId, revisionId, model);
            return InvokeResult.Success;
        }

        public async Task<DependentObjectCheckResult> CheckInUseAsync(string id, EntityHeader org, EntityHeader user)
        {
            var host = await _repo.GetModelAsync(id);
            await AuthorizeAsync(host, AuthorizeResult.AuthorizeActions.Read, user, org);
            return await CheckForDepenenciesAsync(host);
        }

        public async Task<InvokeResult> DeleteModelsync(string id, EntityHeader org, EntityHeader user)
        {
            var host = await _repo.GetModelAsync(id);

            await AuthorizeAsync(host, AuthorizeResult.AuthorizeActions.Delete, user, org);
            await ConfirmNoDepenenciesAsync(host);
            await _repo.DeleteModelAsync(id);
            return InvokeResult.Success;
        }

        public Task<InvokeResult<byte[]>> GetMLModelAsync(string id, string revisionId, EntityHeader org, EntityHeader user)
        {
            throw new NotImplementedException();
        }

        public async Task<Model> GetModelAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetModelAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
        }

        public async Task<ListResponse<ModelSummary>> GetModelsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(ModelCategory));
            return await _repo.GetModelSummariesForOrgAsync(org.Id, listRequest);
        }

        public Task<bool> QueryKeyInUse(string key, EntityHeader org)
        {
            return _repo.QueryKeyInUseAsync(key, org.Id);
        }

        public async Task<InvokeResult> UpdateModelAsync(Model model, EntityHeader org, EntityHeader user)
        {
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Update, user, org);

            var result = Validator.Validate(model, Actions.Update);
            await _repo.UpdateModelAsync(model);

            return result.ToInvokeResult();

        }
    }
}
