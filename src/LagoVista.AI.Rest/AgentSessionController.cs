using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
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
    public class AgentSessionController : LagoVistaBaseController
    {

        IAgentSessionManager _mgr;

        public AgentSessionController(IAgentSessionManager mgr, UserManager<AppUser> userManager, IAdminLogger logger)
            : base(userManager, logger)
        {
            _mgr = mgr;
        }

        [HttpGet("/api/ai/agent/sessions")]
        public Task<ListResponse<AgentSessionSummary>> GetAgentSessions()
        {
            return _mgr.GetAgentSessionsForUserAsync(UserEntityHeader.Id, GetListRequestFromHeader(), OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agent/session/{id}")]
        public async Task<InvokeResult<AgentSession>> GetAgentSession(string id)
        {
            var session = await _mgr.GetAgentSessionAsync(id, OrgEntityHeader, UserEntityHeader);

            return InvokeResult<AgentSession>.Create(session);
        }

    }
}
