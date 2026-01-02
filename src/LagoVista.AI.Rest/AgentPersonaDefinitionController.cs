using LagoVista.AI.Models;
using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{
    /// <summary>
    /// REST Class for Experiments
    /// </summary>
    [ConfirmedUser]
    [AppBuilder]
    public class AgentPersonaDefinitionController : LagoVistaBaseController
    {
        private readonly IAgentPersonaDefinitionManager _agentPersonaDefinitionManager;

        public AgentPersonaDefinitionController(IAgentPersonaDefinitionManager AgentPersonaDefinitionMgr, UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            this._agentPersonaDefinitionManager = AgentPersonaDefinitionMgr;
        }

        [HttpGet("/api/ai/agentpersona/{id}")]
        public async Task<DetailResponse<AgentPersonaDefinition>> GetVectorDatabase(string id)
        {
            var db = await _agentPersonaDefinitionManager.GetAgentPersonaDefinitionAsync(id, OrgEntityHeader, UserEntityHeader);
            return DetailResponse<AgentPersonaDefinition>.Create(db);
        }

        [HttpGet("/api/ai/agentpersona/factory")]
        public DetailResponse<AgentPersonaDefinition> CreateAgentPersonaDefinition()
        {
            var result = DetailResponse<AgentPersonaDefinition>.Create();
            SetAuditProperties(result.Model);
            SetOwnedProperties(result.Model);
            return result;
        }

        [HttpGet("/api/ai/agentpersonas")]
        public Task<ListResponse<AgentPersonaDefinitionSummary>> GetVectorDatabases()
        {
            return _agentPersonaDefinitionManager.GetAgentPersonaDefinitionsForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        [HttpDelete("/api/ai/agentpersona/{id}")]
        public Task<InvokeResult> DeleteAgentPersonaDefinitionAsync(string id)
        {
            return _agentPersonaDefinitionManager.DeleteAgentPersonaDefinitionAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPost("/api/ai/agentpersona")]
        public Task<InvokeResult> AddAgentPersonaDefinition([FromBody] AgentPersonaDefinition ctx)
        {
            return _agentPersonaDefinitionManager.AddAgentPersonaDefinitionAsync(ctx, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPut("/api/ai/agentpersona")]
        public Task<InvokeResult> UpdateAgentPersonaDefinition([FromBody] AgentPersonaDefinition ctx)
        {
            SetUpdatedProperties(ctx);
            return _agentPersonaDefinitionManager.UpdateAgentPersonaDefinitionAsync(ctx, OrgEntityHeader, UserEntityHeader);
        }  
    }
}
