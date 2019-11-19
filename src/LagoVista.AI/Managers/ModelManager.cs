using LagoVista.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.PlatformSupport;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class ModelManager : ManagerBase, IModelManager
    {
        // Storage for the meta data about the model.
        IModelRepo _repo;

        // Storage for the actual model.
        IMLModelRepo _modelRepo;

        public ModelManager(IModelRepo modelRepo, IMLModelRepo mlModelRepo, ILogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security) 
            : base(logger, appConfig, dependencyManager, security)
        {
            this._repo = modelRepo;
            this._modelRepo = mlModelRepo;
        }

        public async Task<InvokeResult> AddModelAsync(Model model, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(model, Actions.Create);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _repo.AddModelAsync(model);

            return InvokeResult.Success;
        }

        public async Task<InvokeResult> AddRevisionAsync(string modelId, ModelRevision revision, EntityHeader org, EntityHeader user)
        {
            var model = await GetModelAsync(modelId, org, user);
            model.Revisions.Add(revision);
            return await UpdateModelAsync(model, org, user);
        }

        public async Task<InvokeResult> UploadModel(string modelId, int revision, byte[] model, EntityHeader org, EntityHeader user)
        {
            await AuthorizeOrgAccessAsync(user, org, typeof(Model), Actions.Update);
            return await this._modelRepo.AddModelAsync(org.Id, modelId, revision, model);
        }

        public async Task<DependentObjectCheckResult> CheckInUseAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetModelAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return await CheckForDepenenciesAsync(model);
        }

        public async Task<InvokeResult> DeleteModelsync(string id, EntityHeader org, EntityHeader user)
        {
            var host = await _repo.GetModelAsync(id);

            await AuthorizeAsync(host, AuthorizeResult.AuthorizeActions.Delete, user, org);
            await ConfirmNoDepenenciesAsync(host);
            await _repo.DeleteModelAsync(id);
            return InvokeResult.Success;
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

        public async Task<ListResponse<ModelSummary>> GetModelsForCategoryAsync(string categoryKey, EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeAsync(user, org, "GetModelsForCatgory");

            return await _repo.GetModelSummariesForCategoryAsync(org.Id, categoryKey, listRequest);
        }

        public async Task<InvokeResult<byte[]>> GetMLModelAsync(string modelId, int revision, EntityHeader org, EntityHeader user)
        {
            //Do this for a security check.
            await this.GetModelAsync(modelId, org, user);

            return await _modelRepo.GetModelAsync(org.Id, modelId, revision);
        }
    }
}
