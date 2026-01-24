// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: a7fb9a939e2da670e9c2211b79ff61410ebf5f0aa2fb25218bde76c74e310dee
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.PlatformSupport;
using LagoVista.Core.Validation;
using System;
using System.Linq;
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

        public async Task<InvokeResult<AiModelRevision>> UploadModelAsync(string modelId, int revisionIndex, byte[] mlModel, EntityHeader org, EntityHeader user)
        {
            await AuthorizeOrgAccessAsync(user, org, typeof(Model), Actions.Update);
            await this._modelRepo.AddModelAsync(org.Id, modelId, revisionIndex, mlModel);

            var model = await this.GetModelAsync(modelId, org, user);



            var revision = new AiModelRevision();


            return InvokeResult<AiModelRevision>.Create(revision);
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
            var summaries = await _repo.GetModelSummariesForCategoryAsync(org.Id, categoryKey, listRequest);
            await AuthorizeOrgAccessAsync(user, org, typeof(ModelSummary), Actions.Read, summaries);
            return summaries;
        }

        public async Task<InvokeResult<byte[]>> GetMLModelAsync(string modelId, int revision, EntityHeader org, EntityHeader user)
        {
            //Do this for a security check.
            await this.GetModelAsync(modelId, org, user);

            return await _modelRepo.GetModelAsync(org.Id, modelId, revision);
        }

        public async Task<InvokeResult<AiModelRevision>> AddRevisionAsync(string modelId, AiModelRevision revision, EntityHeader org, EntityHeader user)
        {
            revision.Id = Guid.NewGuid().ToId();

            var model = await this.GetModelAsync(modelId, org, user);
            model.Revisions.Add(revision);
            revision.VersionNumber = model.Revisions.Count;
            revision.MinorVersionNumber = 1;

            if(model.Revisions.Any())
            {
                if (!revision.Preprocessors.Any())
                {
                    revision.Preprocessors.AddRange(model.Revisions.Last().Preprocessors);
                }

                if(!revision.Settings.Any())
                {
                    revision.Settings.AddRange(model.Revisions.Last().Settings);
                }

                if (String.IsNullOrEmpty(revision.TrainingSettings))
                {
                    revision.TrainingSettings = revision.TrainingSettings;
                }
            }

            await this.UpdateModelAsync(model, org, user);

            return InvokeResult<AiModelRevision>.Create(revision);
        }
    }
}
