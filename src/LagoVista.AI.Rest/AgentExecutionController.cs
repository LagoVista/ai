using System;
using System.Threading.Tasks;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LagoVista.AI.Rest
{
    [Authorize(AuthenticationSchemes = "APIToken")]
    public class AgentExecutionController : LagoVistaBaseController
    {
        private readonly IAgentExecutionService _agentExecutionService;
        public AgentExecutionController(IAgentExecutionService agentExecutionService,
            UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            _agentExecutionService = agentExecutionService
                ?? throw new ArgumentNullException(nameof(agentExecutionService));
        }

        [HttpPost("/api/ai/agent/execute")]
        public Task<InvokeResult<AgentExecuteResponse>> ExecuteAsync([FromBody] AgentExecuteRequest request)
        {
            return _agentExecutionService.ExecuteAsync(request, OrgEntityHeader, UserEntityHeader);
        }


        [HttpGet("/api/ai/agent/ping")]
        public IActionResult Ping()
        {
            return Ok("pong");
        }
    }
}
