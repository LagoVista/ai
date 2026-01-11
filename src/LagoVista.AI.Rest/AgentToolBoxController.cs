using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{
    [ConfirmedUser]
    [AppBuilder]
    public class AgentToolBoxController : LagoVistaBaseController
    {
        private readonly IAgentToolBoxManager _agentToolBoxManager;
        private readonly IAgentToolRegistry _toolRegistry;

        public AgentToolBoxController(IAgentToolBoxManager agentToolBoxMgr, IAgentToolRegistry toolRegistry, UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            this._agentToolBoxManager = agentToolBoxMgr ?? throw new ArgumentNullException(nameof(agentToolBoxMgr));
            this._toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        }


        [HttpGet("/api/ai/toolbox/{id}")]
        public async Task<DetailResponse<AgentToolBox>> GetVectorDatabase(string id)
        {
            var db = await _agentToolBoxManager.GetAgentToolBoxAsync(id, OrgEntityHeader, UserEntityHeader);
            return DetailResponse<AgentToolBox>.Create(db);
        }

        [HttpGet("/api/ai/toolbox/factory")]
        public DetailResponse<AgentToolBox> CreateAgentToolBox()
        {
            var result = DetailResponse<AgentToolBox>.Create();
            SetAuditProperties(result.Model);
            SetOwnedProperties(result.Model);
            return result;
        }

        [HttpGet("/api/ai/toolboxes")]
        public Task<ListResponse<AgentToolBoxSummary>> GetVectorDatabases()
        {
            return _agentToolBoxManager.GetAgentToolBoxesForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        [HttpDelete("/api/ai/toolbox/{id}")]
        public Task<InvokeResult> DeleteAgentToolBoxAsync(string id)
        {
            return _agentToolBoxManager.DeleteAgentToolBoxAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPost("/api/ai/toolbox")]
        public Task<InvokeResult> AddAgentToolBox([FromBody] AgentToolBox ctx)
        {
            return _agentToolBoxManager.AddAgentToolBoxAsync(ctx, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPut("/api/ai/toolbox")]
        public Task<InvokeResult> UpdateAgentToolBox([FromBody] AgentToolBox ctx)
        {
            SetUpdatedProperties(ctx);
            return _agentToolBoxManager.UpdateAgentToolBoxAsync(ctx, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agenttools")]
        public ListResponse<AgentToolSummary> GetAgentTools()
        {
            return ListResponse<AgentToolSummary>.Create(_toolRegistry.GetAllTools());
        }

    }
}
