using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.PlatformSupport;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Managers
{
    public class LabelManager : ManagerBase, ILabelManager
    {
        private readonly ILabelRepo _repo;

        public LabelManager(ILabelRepo labelRepo, IAdminLogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
                  : base(logger, appConfig, dependencyManager, security)
        {
            _repo = labelRepo ?? throw new NullReferenceException(nameof(labelRepo));
        }

        public async Task<InvokeResult> AddLabelAsync(Models.Label label, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(label, Actions.Create);
            await AuthorizeAsync(label, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _repo.AddLabelAsync(label);

            return InvokeResult.Success;
        }

        public async Task<Models.Label> GetLabelAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetLabelAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
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
    }
}
