using System.Threading.Tasks;
using LagoVista.Core.Validation;
using LagoVista.Core.AI.Models;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using LagoVista.Core.AI.Interfaces;

namespace LagoVista.AI.Rest
{
    [Authorize(AuthenticationSchemes = "APIToken")]
    public class AgentExecutionController : LagoVistaBaseController
    {
        private readonly IAgentExecutionService _agentExecutionService;

        public AgentExecutionController(IAgentExecutionService agentExecutionService,
                                        UserManager<AppUser> userManager,
                                        IAdminLogger logger) : base(userManager, logger)
        {
            _agentExecutionService = agentExecutionService;
        }

        /// <summary>
        /// Execute an Aptix agent request using the configured AgentContext
        /// and ConversationContext. This is the endpoint used by the Aptix CLI
        /// 'ask' command.
        /// </summary>
        [HttpPost("/api/ai/agent/execute")]
        public Task<InvokeResult<AgentExecuteResponse>> ExecuteAsync([FromBody] AgentExecuteRequest request)
        {
            return _agentExecutionService.ExecuteAsync(request, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agent/ping")]
        public IActionResult Ping() => Ok();
    }
}
