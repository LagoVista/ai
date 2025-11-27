using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
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
        private readonly IAgentRequestHandler _agentRequestHandler;

        public AgentExecutionController(IAgentRequestHandler agentRequestHandler, UserManager<AppUser> userManager, IAdminLogger logger)
            : base(userManager, logger)
        {
            _agentRequestHandler = agentRequestHandler;
        }

        /// <summary>
        /// Execute an Aptix agent request using the configured AgentContext
        /// and ConversationContext. This is the endpoint used by the Aptix CLI
        /// and other clients. The payload is normalized by AgentRequestHandler
        /// and dispatched to the orchestrator.
        /// </summary>
        [HttpPost("/api/ai/agent/execute")]
        public Task<InvokeResult<AgentExecuteResponse>> ExecuteAsync([FromBody] AgentRequestEnvelope request)
        {
            var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;

            return _agentRequestHandler.HandleAsync(request, OrgEntityHeader, UserEntityHeader, cancellationToken);
        }

        [HttpGet("/api/ai/agent/ping")]
        public IActionResult Ping()
        {
            return Ok();
        }
    }
}
