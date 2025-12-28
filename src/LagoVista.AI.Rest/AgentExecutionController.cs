using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace LagoVista.AI.Rest
{
    [ConfirmedUser]
    [Authorize()]
    public class AgentExecutionController : LagoVistaBaseController
    {
        private readonly IAgentRequestHandlerStep _agentRequestHandler;
        private readonly IAgentSessionManager _sessionManager;
        private readonly IAdminLogger _adminLogger;

        public AgentExecutionController(IAgentRequestHandlerStep agentRequestHandler, IAgentSessionManager sessionManager, UserManager<AppUser> userManager, IAdminLogger logger)
            : base(userManager, logger)
        {
            _agentRequestHandler = agentRequestHandler;
            _sessionManager = sessionManager;
            _adminLogger = logger;
        }

        /// <summary>
        /// Execute an Aptix agent request using the configured AgentContext
        /// and ConversationContext. This is the endpoint used by the Aptix CLI
        /// and other clients. The payload is normalized by AgentRequestHandler
        /// and dispatched to the orchestrator.
        /// </summary>
        [HttpPost("/api/ai/agent/execute")]
        public async Task<IActionResult> ExecuteAsync([FromBody] AgentExecuteRequest request, [FromServices] IAgentStreamingContext streamingContext)
        {
            var requestJson = JsonConvert.SerializeObject(request);
            var sw = Stopwatch.StartNew();
            Console.WriteLine($">>>> Received AgentExecuteRequest, size {(requestJson.Length / 1024.0).ToString("0.00")}kb \r\n{requestJson}\r\n");

            var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;

            try
            {

                if (request.Streaming)
                {
                        Response.StatusCode = 200;
                        Response.ContentType = "application/x-ndjson";
                        Response.Headers.CacheControl = "no-cache";
                        Response.Headers["X-Accel-Buffering"] = "no";
                        await Response.Body.FlushAsync();
                        streamingContext.Current = async ev =>
                        {
                            var json = JsonConvert.SerializeObject(
                                ev,
                                Formatting.None,
                                new JsonSerializerSettings
                                {
                                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                                    NullValueHandling = NullValueHandling.Ignore
                                });

                            await Response.WriteAsync(json, cancellationToken);
                            await Response.WriteAsync("\n", cancellationToken);
                            await Response.Body.FlushAsync(cancellationToken);
                        };

                        var result = await _agentRequestHandler.HandleAsync(request, OrgEntityHeader, UserEntityHeader, cancellationToken);
                        await streamingContext.AddWorkflowAsync("processing completed, returning result");

                        if (streamingContext.Current != null)
                        {
                            await streamingContext.Current(new AgentStreamEvent
                            {
                                Kind = "final",
                                Final = result,
                                Index = 99999
                            });
                        } 

                        return new EmptyResult();
                }
                else {
                    var result = await _agentRequestHandler.HandleAsync(request, OrgEntityHeader, UserEntityHeader, cancellationToken);
                    if (result.Successful)
                    {
                        var responseJSON = JsonConvert.SerializeObject(result.Result);
                        Console.WriteLine($">>>> Handeed AgentExecuteReques in {sw.Elapsed.TotalSeconds.ToString("0.00")} seconds, response size:  {(responseJSON.Length / 1024.0).ToString("0.00")}kb\r\n====\r\n{responseJSON}\r\n");

                    }
                    else
                        Console.WriteLine($">>>> Handeed AgentExecuteRequest => FAILED: {result.Errors[0].Message}\r\n====\r\n");

                        return new JsonResult(result);
                    }           
            }
            catch(OperationCanceledException)
            {
                _adminLogger.Trace("[AgentExecutionController_ExecuteAsync] Operation Cancelled");

                if (streamingContext.Current != null)
                {
                    await streamingContext.Current(new AgentStreamEvent
                    {
                        Kind = "final",
                        Final = InvokeResult<AgentExecuteResponse>.FromError("Request Cancelled"),
                        Index = 99999
                    });
                }

                return new EmptyResult();
            }
            catch(ValidationException val)
            {
                if (streamingContext.Current != null)
                {
                    await streamingContext.Current(new AgentStreamEvent
                    {
                        Kind = "final",
                        Final = InvokeResult<AgentExecuteResponse>.FromErrors(val.Errors.ToArray()),
                        Index = 99999
                    });
                }

                return new EmptyResult();
            }
            catch(RecordNotFoundException ex)
            {
                _adminLogger.AddException("[AgentExecutionController_ExecuteAsync] - Record Not Found Exception", ex);

                if (streamingContext.Current != null)
                {
                    await streamingContext.Current(new AgentStreamEvent
                    {
                        Kind = "final",
                        Final = InvokeResult<AgentExecuteResponse>.FromError(ex.Message),
                        Index = 99999
                    });
                }

                return new EmptyResult();
            }
            catch(Exception ex)
            {
                _adminLogger.AddException("[AgentExecutionController_ExecuteAsync] - Unhandled Exception", ex);

                if (streamingContext.Current != null)
                {
                    await streamingContext.Current(new AgentStreamEvent
                    {
                        Kind = "final",
                        Final = InvokeResult<AgentExecuteResponse>.FromException("[AgentExecutionController_ExecuteAsync]", ex),
                        Index = 99999
                    });
                }

                return new EmptyResult();
            }
        }

        [HttpGet("/api/ai/agent/session/{sessionid}/branch/{turnid}")]
        public Task<InvokeResult<AgentSession>> GetAgentSessions(string sessionid, string turnid)
        {
            return _sessionManager.BranchSessionAsync(sessionid, turnid, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agent/sessions")]
        public Task<ListResponse<AgentSessionSummary>> GetAgentSessions()
        {
            return _sessionManager.GetAgentSessionsForUserAsync(UserEntityHeader.Id, GetListRequestFromHeader(), OrgEntityHeader, UserEntityHeader);
        }


        [HttpGet("/api/ai/agent/session/{id}")]
        public async Task<InvokeResult<AgentSession>> GetSession(string id)
        {
            return InvokeResult<AgentSession>.Create(await  _sessionManager.GetAgentSessionAsync(id, OrgEntityHeader, UserEntityHeader));
        }

        [HttpDelete("/api/ai/agent/session/{id}")]
        public  Task<InvokeResult<AgentSessionSummary>> DeleteSession(string id)
        {
            return _sessionManager.DeleteSessionAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agent/session/{id}/archive")]
        public Task<InvokeResult<AgentSessionSummary>> ArchiveSession(string id)
        {
            return _sessionManager.ArchiveSessionAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agent/session/{id}/complete")]
        public Task<InvokeResult<AgentSessionSummary>> CopmpleteSession(string id)
        {
            return _sessionManager.CompleteSessionAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agent/session/{id}/rename")]
        public Task<InvokeResult<AgentSessionSummary>> NameSession(string id, string name)
        {
            return _sessionManager.SetSessionNameAsync(id, name, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agent/session/{id}/share")]
        public Task<InvokeResult<AgentSessionSummary>> ShareSession(string id)
        {
            return _sessionManager.ShareSessionAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/agent/ping")]
        public IActionResult Ping()
        {
            return Ok();
        }
    }
}
