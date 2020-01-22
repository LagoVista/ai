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
    public class ModelCategoryManager : ManagerBase, IModelCategoryManager
    {
        IModelCategoryRepo _repo;

        public ModelCategoryManager(IModelCategoryRepo modelRepo, ILogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security) 
            : base(logger, appConfig, dependencyManager, security)
        {
            this._repo = modelRepo;
        }

        public async Task<InvokeResult> AddModelCategoryAsync(ModelCategory modelCategory, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(modelCategory, Actions.Create);
            await AuthorizeAsync(modelCategory, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _repo.AddModelCategoryAsync(modelCategory);

            return InvokeResult.Success;
        }

        public async Task<DependentObjectCheckResult> CheckInUseAsync(string id, EntityHeader org, EntityHeader user)
        {
            var host = await _repo.GetModelCategoryAsync(id);
            await AuthorizeAsync(host, AuthorizeResult.AuthorizeActions.Read, user, org);
            return await CheckForDepenenciesAsync(host);
        }

        public async Task<InvokeResult> DeleteModelCategoryAsync(string id, EntityHeader org, EntityHeader user)
        {
            var cateogry = await _repo.GetModelCategoryAsync(id);

            await AuthorizeAsync(cateogry, AuthorizeResult.AuthorizeActions.Delete, user, org);
            await ConfirmNoDepenenciesAsync(cateogry);
            await _repo.DeleteModelCategoryAsync(id);
            return InvokeResult.Success;
        }

        public async Task<ModelCategory> GetModelCategoryAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetModelCategoryAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
        }

        public async Task<ListResponse<ModelCategorySummary>> GetModelCategoriesForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(ModelCategory));
            return await _repo.GetModelCategorySummariesForOrgAsync(org.Id, listRequest);
        }

        public Task<bool> QueryKeyInUse(string key, EntityHeader org)
        {
            return _repo.QueryKeyInUseAsync(key, org.Id);
        }

        public async Task<InvokeResult> UpdateModelCategoryAsync(ModelCategory model, EntityHeader org, EntityHeader user)
        {
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Update, user, org);

            var result = Validator.Validate(model, Actions.Update);
            await _repo.UpdateModelCategoryAsync(model);

            return result.ToInvokeResult();
        }
    }
}
