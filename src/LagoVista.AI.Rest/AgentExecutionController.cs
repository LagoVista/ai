using System;
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
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

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
        public async Task<InvokeResult<AgentExecuteResponse>> ExecuteAsync([FromBody] AgentExecuteRequest request)
        {
            Console.WriteLine($">>>> Received AgentExecuteRequest: {request.ResponseContinuationId}\r\n{JsonConvert.SerializeObject(request)}\r\n");

            var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;

            var result = await _agentRequestHandler.HandleAsync(request, OrgEntityHeader, UserEntityHeader, cancellationToken);

            if(result.Successful)
                Console.WriteLine($">>>> Received AgentExecuteRequest: {request.ResponseContinuationId} => {result.Result.ResponseContinuationId}\r\n====\r\n");
            else
                Console.WriteLine($">>>> Received AgentExecuteRequest: {request.ResponseContinuationId} => FAILED: {result.Errors[0].Message}\r\n====\r\n");

            return result;
        }

        [HttpGet("/api/ai/agent/ping")]
        public IActionResult Ping()
        {
            return Ok();
        }
    }
}
