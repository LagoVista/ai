using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace LagoVista.AI.Rest
{
    [ConfirmedUser]
    [Authorize()]
    public class AgentExecutionController : LagoVistaBaseController
    {
        private readonly IAgentRequestHandler _agentRequestHandler;
        private readonly IAgentSessionManager _sessionManager;

        public AgentExecutionController(IAgentRequestHandler agentRequestHandler, IAgentSessionManager sessionManager, UserManager<AppUser> userManager, IAdminLogger logger)
            : base(userManager, logger)
        {
            _agentRequestHandler = agentRequestHandler;
            _sessionManager = sessionManager;
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
            var requestJson = JsonConvert.SerializeObject(request);
            var sw = Stopwatch.StartNew();
            Console.WriteLine($">>>> Received AgentExecuteRequest: {request.ResponseContinuationId}, size {(requestJson.Length / 1024.0).ToString("0.00")}kb \r\n{requestJson}\r\n");

            var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;

            try
            {
                var result = await _agentRequestHandler.HandleAsync(request, OrgEntityHeader, UserEntityHeader, cancellationToken);



                if (result.Successful)
                {
                    var responseJSON = JsonConvert.SerializeObject(result.Result);
                    Console.WriteLine($">>>> Handeed AgentExecuteRequest: {request.ResponseContinuationId} => {result.Result.ResponseContinuationId} in {sw.Elapsed.TotalSeconds.ToString("0.00")} seconds, response size:  {(responseJSON.Length / 1024.0).ToString("0.00")}kb\r\n====\r\n{responseJSON}\r\n");

                }
                else
                    Console.WriteLine($">>>> Handeed AgentExecuteRequest: {request.ResponseContinuationId} => FAILED: {result.Errors[0].Message}\r\n====\r\n");

                return result;
            }
            catch(ValidationException val)
            {
                return InvokeResult<AgentExecuteResponse>.FromErrors(val.Errors.ToArray());
            }
            catch(RecordNotFoundException ex)
            {
                return InvokeResult<AgentExecuteResponse>.FromError(ex.Message);
            }
            catch(Exception ex)
            {
                return InvokeResult<AgentExecuteResponse>.FromException("[AgentExecutionController_AgentExecutionController]", ex);
            }
        }

        [HttpPost("/api/ai/agent/sessions")]
        public  Task<ListResponse<AgentSessionSummary>> GetSessions()
        {
            return _sessionManager.GetAgentSessionsAsync(GetListRequestFromHeader(), OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agent/session/{id}")]
        public Task<AgentSession> GetSession(string id)
        {
            return _sessionManager.GetAgentSessionAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agent/ping")]
        public IActionResult Ping()
        {
            return Ok();
        }
    }
}
