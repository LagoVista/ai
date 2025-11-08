// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 1e584c621d6bea5e8dd538f535ba49746eeb093e428b0affbf913d22e0f1e2db
// IndexVersion: 2
// --- END CODE INDEX META ---
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
    public class AiConversationManager : ManagerBase, IAiConversationManager
    {
        IAiConversationRepo _repo;

        public AiConversationManager(IAiConversationRepo modelRepo, ILogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security) 
            : base(logger, appConfig, dependencyManager, security)
        {
            this._repo = modelRepo;
        }

        public async Task<InvokeResult> AddAiConversationAsync(AiConversation AiConversation, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(AiConversation, Actions.Create);
            await AuthorizeAsync(AiConversation, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _repo.AddAiConversationAsync(AiConversation);

            return InvokeResult.Success;
        }

        public async Task<DependentObjectCheckResult> CheckInUseAsync(string id, EntityHeader org, EntityHeader user)
        {
            var host = await _repo.GetAiConversationAsync(id);
            await AuthorizeAsync(host, AuthorizeResult.AuthorizeActions.Read, user, org);
            return await CheckForDepenenciesAsync(host);
        }

        public async Task<InvokeResult> DeleteAiConversationAsync(string id, EntityHeader org, EntityHeader user)
        {
            var cateogry = await _repo.GetAiConversationAsync(id);

            await AuthorizeAsync(cateogry, AuthorizeResult.AuthorizeActions.Delete, user, org);
            await ConfirmNoDepenenciesAsync(cateogry);
            await _repo.DeleteAiConversationAsync(id);
            return InvokeResult.Success;
        }

        public async Task<AiConversation> GetAiConversationAsync(string id, EntityHeader org, EntityHeader user)
        {
            var model = await _repo.GetAiConversationAsync(id);
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Read, user, org);
            return model;
        }

        public async Task<ListResponse<AiConversationSummary>> GetConversationsForOrgAsync(EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(AiConversation));
            return await _repo.GetAiConversationSummariesForOrgAsync(org.Id, listRequest);
        }

        public async Task<ListResponse<AiConversationSummary>> GetConversationsForUserAsync(string userId, EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            await AuthorizeOrgAccessAsync(user, org.Id, typeof(AiConversation));
            return await _repo.GetAiConversationSummariesForUserAsync(org.Id, userId, listRequest);
        }

        public Task<bool> QueryKeyInUse(string key, EntityHeader org)
        {
            return _repo.QueryKeyInUseAsync(key, org.Id);
        }

        public async Task<InvokeResult> UpdateAiConversationAsync(AiConversation model, EntityHeader org, EntityHeader user)
        {
            await AuthorizeAsync(model, AuthorizeResult.AuthorizeActions.Update, user, org);

            var result = Validator.Validate(model, Actions.Update);
            await _repo.UpdateAiConversationAsync(model);

            return result.ToInvokeResult();
        }
    }
}
