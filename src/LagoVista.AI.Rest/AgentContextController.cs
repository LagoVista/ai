// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 3deeacfc4503d772cfa5990528a4649d71c9698541a6f875517981776a79291f
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{
    /// <summary>
    /// REST Class for Experiments
    /// </summary>
    [ConfirmedUser]
    [AppBuilder]
    public class AgentContextController : LagoVistaBaseController
    {
        private readonly IAgentContextManager _agentContextManager;

        public AgentContextController(IAgentContextManager AgentContextMgr, UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            this._agentContextManager = AgentContextMgr;
        }


        [HttpGet("/api/ai/agentcontext/{id}")]
        public async Task<DetailResponse<AgentContext>> GetVectorDatabase(string id)
        {
            var db = await _agentContextManager.GetAgentContextAsync(id, OrgEntityHeader, UserEntityHeader);
            return DetailResponse<AgentContext>.Create(db);
        }

        [HttpGet("/api/ai/agentcontext/factory")]
        public DetailResponse<AgentContext> CreateAgentContext()
        {
            var result = DetailResponse<AgentContext>.Create();
            SetAuditProperties(result.Model);
            SetOwnedProperties(result.Model);
            return result;
        }


        [HttpGet("/api/ai/agentcontext/{id}/secrets")]
        public async Task<DetailResponse<AgentContext>> GetVectorDatabaseWithSecrets(string id)
        {
            var db = await _agentContextManager.GetAgentContextWithSecretsAsync(id, OrgEntityHeader, UserEntityHeader);
            return DetailResponse<AgentContext>.Create(db);
        }

        [HttpGet("/api/ai/agentcontexts")]
        public Task<ListResponse<AgentContextSummary>> GetVectorDatabases()
        {
            return _agentContextManager.GetAgentContextsForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        [HttpDelete("/api/ai/agentcontext/{id}")]
        public Task<InvokeResult> DeleteAgentContextAsync(string id)
        {
            return _agentContextManager.DeleteAgentContextAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPost("/api/ai/agentcontext")]
        public Task<InvokeResult> AddAgentContext([FromBody] AgentContext ctx)
        {
            return _agentContextManager.AddAgentContextAsync(ctx, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPut("/api/ai/agentcontext")]
        public Task<InvokeResult> UpdateAgentContext([FromBody] AgentContext ctx)
        {
            SetUpdatedProperties(ctx);
            return _agentContextManager.UpdateAgentContextAsync(ctx, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agent/conversation/context/factory")]
        public DetailResponse<ConversationContext> CreateConversationContext()
        {
            var result = DetailResponse<ConversationContext>.Create();
            return result;
        }
    }
}
