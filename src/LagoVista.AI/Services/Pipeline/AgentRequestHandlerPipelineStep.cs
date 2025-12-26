using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.UserAdmin.Interfaces.Managers;

namespace LagoVista.AI.Services.Pipeline
{
    /// <summary>
    /// AGN-032 Step: AgentRequestHandler
    ///
    /// Expects:
    /// - Transport request and org/user identity.
    ///
    /// Updates:
    /// - Constructs initial AgentPipelineContext.
    /// - Normalizes request fields needed by downstream steps.
    /// - Routes to one of the three session paths:
    ///     - AgentSessionCreatorPipelineStep
    ///     - AgentSessionRestorerPipelineStep
    ///     - ClientToolCallSessionRestorerPipelineStep
    ///
    /// Next:
    /// - AgentSessionCreatorPipelineStep OR AgentSessionRestorerPipelineStep OR ClientToolCallSessionRestorerPipelineStep
    /// </summary>
    public sealed class AgentRequestHandlerPipelineStep : IAgentRequestHandlerStep
    {
        private readonly IAgentSessionCreatorPipelineStep _sessionCreator;
        private readonly IAgentSessionRestorerPipelineStep _sessionRestorer;
        private readonly IClientToolCallSessionRestorerPipelineStep _toolSessionRestorer;
        private readonly IAgentSessionManager _agentSessionManager;
        private readonly IAdminLogger _adminLogger;
        private readonly IAgentStreamingContext _agentStreamingContext;
        private readonly IAgentExecuteResponseBuilder _responseBuilder; 

        public AgentRequestHandlerPipelineStep(
            IAgentSessionCreatorPipelineStep sessionCreator,
            IAgentSessionRestorerPipelineStep sessionRestorer,
            IClientToolCallSessionRestorerPipelineStep toolSessionRestorer,
            IAdminLogger adminLogger,
            IAgentExecuteResponseBuilder responseBuilder,
            IAgentSessionManager agentSessionManager,
            IAgentStreamingContext agentStreamingContext)
        {
            _sessionCreator = sessionCreator ?? throw new ArgumentNullException(nameof(sessionCreator));
            _sessionRestorer = sessionRestorer ?? throw new ArgumentNullException(nameof(sessionRestorer));
            _toolSessionRestorer = toolSessionRestorer ?? throw new ArgumentNullException(nameof(toolSessionRestorer));
            _agentSessionManager = agentSessionManager ?? throw new ArgumentNullException(nameof(agentSessionManager));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
            _responseBuilder = responseBuilder ?? throw new ArgumentNullException(nameof(responseBuilder));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> HandleAsync(AgentExecuteRequest request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            var ctx = new AgentPipelineContext(request, org, user, cancellationToken);

            var validationResult = ctx.Validate(PipelineSteps.RequestHandler);
            if(!validationResult.Successful) return InvokeResult<AgentExecuteResponse>.FromInvokeResult(validationResult.ToInvokeResult());

            var sw = Stopwatch.StartNew();
            ctx.LogDetails(_adminLogger, PipelineSteps.RequestHandler);

            InvokeResult<IAgentPipelineContext> result = null;

            switch (ctx.Type)
            {
                case AgentPipelineContextTypes.Initial:
                    await _agentStreamingContext.AddWorkflowAsync("Welcome to Aptix, Finding the next available agent...please wait...", cancellationToken);
                    result = await _sessionCreator.ExecuteAsync(ctx);
                    break;
                case AgentPipelineContextTypes.FollowOn:
                    await _agentStreamingContext.AddWorkflowAsync("Welcome Back to Aptix, let's get started...", cancellationToken);
                    result = await _sessionRestorer.ExecuteAsync(ctx);
                    break;
                case AgentPipelineContextTypes.ClientToolCallContinuation:
                    await _agentStreamingContext.AddWorkflowAsync("Welcome Back to Aptix, resuming tool execution...", cancellationToken);
                    result = await _toolSessionRestorer.ExecuteAsync(ctx);
                    break;
            }
            
            await _agentSessionManager.UpdateSessionAsync(result.Result.Session, org, user);

            if (result.Successful)
            {
                ctx.LogDetails(_adminLogger, PipelineSteps.RequestHandler, sw.Elapsed);
                var response = await _responseBuilder.BuildAsync(result.Result);
                return response;
            }
            else
            {
                ctx.LogStepErrorDetails(_adminLogger, PipelineSteps.RequestHandler, result.ToInvokeResult(), sw.Elapsed);
                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(result.ToInvokeResult());
            }
        }
    }
}
