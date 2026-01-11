// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: ee54bf85aeb56836ae7668fe275b6ba0ed02f41cc237092f8474a7ee110ccb21
// IndexVersion: 2
// --- END CODE INDEX META ---
using System.Threading.Tasks;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models.TrainingData;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.PlatformSupport;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Managers
{
    public class TrainingDataSetManager : ManagerBase, ITrainingDataSetManager
    {
        ITrainingDataSetRepo _repo;

        public TrainingDataSetManager(ITrainingDataSetRepo repo, ILogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
            : base(logger, appConfig, dependencyManager, security)
        {
            this._repo = repo;
        }

        public async Task<InvokeResult> AddTrainingDataSetManager(TrainingDataSet dataSet, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(dataSet, Actions.Create);
            await AuthorizeAsync(dataSet, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _repo.AddTrainingDataSetsAsync(dataSet);

            return InvokeResult.Success;
        }

        public async Task<DependentObjectCheckResult> CheckInUseAsync(string id, EntityHeader org, EntityHeader user)
        {
            var host = await _repo.GetTrainingDataSetAsync(id);
            await AuthorizeAsync(host, AuthorizeResult.AuthorizeActions.Read, user, org);
            return await CheckForDepenenciesAsync(host);
        }

        public async Task<InvokeResult> DeleteTrainingDataSetManager(string id, EntityHeader org, EntityHeader user)
        {
            var set = await _repo.GetTrainingDataSetAsync(id);

            await AuthorizeAsync(set, AuthorizeResult.AuthorizeActions.Delete, user, org);
            await ConfirmNoDepenenciesAsync(set);
            await _repo.GetTrainingDataSetAsync(id);
            return InvokeResult.Success;
        }

        public async Task<ListResponse<TrainingDataSetSummary>> GetForOrgAsync(EntityHeader org, EntityHeader user, ListRequest request)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(TrainingDataSetSummary));
            return await _repo.GetTrainingDataSetsForOrgAsync(org.Id, request);
        }

        public async Task<TrainingDataSet> GetTrainingDataSetAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetTrainingDataSetAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
        }

        public Task<bool> QueryKeyInUse(string key, EntityHeader org)
        {
            return _repo.QueryKeyInUseAsync(key, org.Id);
        }

        public async Task<InvokeResult> UpdateTrainingDataSetManager(TrainingDataSet set, EntityHeader org, EntityHeader user)
        {
            await AuthorizeAsync(set, AuthorizeResult.AuthorizeActions.Update, user, org);

            var result = Validator.Validate(set, Actions.Update);
            await _repo.UpdateTrainingDataSetsAsync(set);

            return result.ToInvokeResult();
        }
    }
}
