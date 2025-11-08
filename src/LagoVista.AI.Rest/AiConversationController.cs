// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 0d45aa1c5c7452ff99f3c0bcc180db1e4b0ea0642632bacc8baf27c1a29a4833
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using LagoVista.Core.Validation;
using System;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.IoT.Web.Common.Attributes;

namespace LagoVista.AI.Rest
{
    [ConfirmedUser]
    [AppBuilder]
    public class AiConversationController : LagoVistaBaseController
    {
        readonly IAiConversationManager _mgr;

        public AiConversationController(IAiConversationManager mgr, UserManager<AppUser> userManager, IAdminLogger logger)
            : base(userManager, logger)
        {
            _mgr = mgr;
        }

        [HttpPost("/api/ai/conversation")]
        public Task<InvokeResult> AddInstanceAsync([FromBody] AiConversation AiConversation)
        {
            return _mgr.AddAiConversationAsync(AiConversation, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPut("/api/ai/conversation")]
        public Task<InvokeResult> UpdateInstanceAsync([FromBody] AiConversation AiConversation)
        {
            SetUpdatedProperties(AiConversation);
            return _mgr.UpdateAiConversationAsync(AiConversation, OrgEntityHeader, UserEntityHeader);
        }

        [HttpDelete("/api/ai/conversation/{id}")]
        public Task<InvokeResult> DeleteAiConversationAsync(string id)
        {
            return _mgr.DeleteAiConversationAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/conversations")]
        public Task<ListResponse<AiConversationSummary>> GetAiConversationForOrg()
        {
            return  _mgr.GetConversationsForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        [HttpGet("/api/ai/conversations/user")]
        public Task<ListResponse<AiConversationSummary>> GetAiConversationForUser()
        {
            return _mgr.GetConversationsForUserAsync(UserEntityHeader.Id, OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        [HttpGet("/api/ai/conversation/{id}")]
        public async Task<DetailResponse<AiConversation>> GetAiConversationAsync(string id)
        {
            var modelCateogry = await _mgr.GetAiConversationAsync(id, OrgEntityHeader, UserEntityHeader);
            return DetailResponse<AiConversation>.Create(modelCateogry);
        }

        [HttpGet("/api/ai/conversation/factory")]
        public DetailResponse<AiConversation> CreateNewAiConversation()
        {
            var AiConversation = DetailResponse<AiConversation>.Create();
            SetAuditProperties(AiConversation.Model);
            SetOwnedProperties(AiConversation.Model);
            return AiConversation;
        }

        [HttpGet("/api/ml/conversation/interaction/factory")]
        public DetailResponse<AiConversationInteraction> CreateNewAiConversationInteraction()
        {
            var interaction = DetailResponse<AiConversationInteraction>.Create();
            interaction.Model.TimeStamp = DateTime.UtcNow.ToJSONString();
            interaction.Model.User = UserEntityHeader; 
            return interaction;
        }

        [HttpGet("/api/ai/conversation/{key}/keyinuse")]
        public Task<bool> AiConversationKeyInUseAsync(String key)
        {
            return _mgr.QueryKeyInUse(key, OrgEntityHeader);
        }
    }
}
