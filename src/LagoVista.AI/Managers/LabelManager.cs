using System;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Managers
{
    public class LabelManager : ManagerBase, ILabelManager
    {
        private readonly ILabelRepo _repo;
        private readonly ILabelSetRepo _labelSetRepo;

        public LabelManager(ILabelRepo labelRepo, ILabelSetRepo labelSetRepo, IAdminLogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
                  : base(logger, appConfig, dependencyManager, security)
        {
            _repo = labelRepo ?? throw new NullReferenceException(nameof(labelRepo));
            _labelSetRepo = labelSetRepo ?? throw new NullReferenceException(nameof(labelSetRepo));
        }

        public async Task<InvokeResult> AddLabelAsync(Models.Label label, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(label, Actions.Create);
            await AuthorizeAsync(label, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _repo.AddLabelAsync(label);

            return InvokeResult.Success;
        }

        public async Task<InvokeResult> AddLabelSetAsync(Models.ModelLabelSet labelSet, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(labelSet, Actions.Create);
            await AuthorizeAsync(labelSet, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _labelSetRepo.AddLabelAsync(labelSet);

            return InvokeResult.Success;
        }

        public async Task<InvokeResult> DeleteLabelSetAsync(string id, EntityHeader org, EntityHeader user)
        {
            var labelSet = await GetLabelSetAsync(id, org, user);
            await AuthorizeAsync(labelSet, AuthorizeResult.AuthorizeActions.Delete, user, org);

            await _labelSetRepo.DeleteLabelSetAsync(id);

            return InvokeResult.Success;
        }

        public async Task<Models.Label> GetLabelAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetLabelAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
        }

        public async Task<ModelLabelSet> GetLabelSetAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _labelSetRepo.GetLabelSetAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
        }

        public async Task<ListResponse<ModelLabelSetSummary>> GetLabelSetsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(Models.ModelLabelSet));
            return await _labelSetRepo.GetLabelSetsForOrgAsync(org.Id, listRequest);
        }

        public async Task<ListResponse<LabelSummary>> GetLabelsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest request)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(Models.Label));
            return await _repo.GetLabelsForOrgAsync(org.Id, request);
        }

        public async Task<ListResponse<LabelSummary>> SearchLabelsAsync(string search, EntityHeader org, EntityHeader user, ListRequest request)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(Models.Label));
            return await _repo.SearchLabelsForOrgAsync(org.Id, search, request);
        }

        public async Task<InvokeResult> UpdateLabelAsync(Models.Label label, EntityHeader org, EntityHeader user)
        {
            await AuthorizeAsync(label, AuthorizeResult.AuthorizeActions.Update, user, org);

            var result = Validator.Validate(label, Actions.Update);
            await _repo.UpdateLabelAsync(label);

            return result.ToInvokeResult();
        }

        public async Task<InvokeResult> UpdateLabelSetAsync(ModelLabelSet labelSet, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(labelSet, Actions.Create);
            await AuthorizeAsync(labelSet, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _labelSetRepo.AddLabelAsync(labelSet);

            return InvokeResult.Success;
        }
    }
}
