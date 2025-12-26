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
        private readonly IAgentContextResolverPipelineStep _contextResolver;
        private readonly IAgentSessionRestorerPipelineStep _sessionRestorer;
        private readonly IClientToolCallSessionRestorerPipelineStep _toolSessionRestorer;
        private readonly IAgentSessionManager _agentSessionManager;
        private readonly IAdminLogger _adminLogger;
        private readonly IAgentStreamingContext _agentStreamingContext;
        private readonly IAgentExecuteResponseBuilder _responseBuilder;
        private readonly IAgentPipelineContextValidator _validator;

        public AgentRequestHandlerPipelineStep(
            IAgentContextResolverPipelineStep sessionCreator,
            IAgentSessionRestorerPipelineStep sessionRestorer,
            IClientToolCallSessionRestorerPipelineStep toolSessionRestorer,
            IAdminLogger adminLogger,
            IAgentPipelineContextValidator validator,
            IAgentExecuteResponseBuilder responseBuilder,
            IAgentSessionManager agentSessionManager,
            IAgentStreamingContext agentStreamingContext)
        {
            _contextResolver = sessionCreator ?? throw new ArgumentNullException(nameof(sessionCreator));
            _sessionRestorer = sessionRestorer ?? throw new ArgumentNullException(nameof(sessionRestorer));
            _toolSessionRestorer = toolSessionRestorer ?? throw new ArgumentNullException(nameof(toolSessionRestorer));
            _agentSessionManager = agentSessionManager ?? throw new ArgumentNullException(nameof(agentSessionManager));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
            _responseBuilder = responseBuilder ?? throw new ArgumentNullException(nameof(responseBuilder));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public async Task<InvokeResult<AgentExecuteResponse>> HandleAsync(AgentExecuteRequest request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            var ctx = new AgentPipelineContext(request, org, user, cancellationToken);

            var preValidation = _validator.ValidatePreStep(ctx, PipelineSteps.RequestHandler);
            if(!preValidation.Successful) return InvokeResult<AgentExecuteResponse>.FromInvokeResult(preValidation.ToInvokeResult());

            var sw = Stopwatch.StartNew();
            ctx.LogDetails(_adminLogger, PipelineSteps.RequestHandler);

            InvokeResult<IAgentPipelineContext> result = null;

            switch (ctx.Type)
            {
                case AgentPipelineContextTypes.Initial:
                    await _agentStreamingContext.AddWorkflowAsync("Welcome to Aptix, Finding the next available agent...please wait...", cancellationToken);
                    result = await _contextResolver.ExecuteAsync(ctx);
                    break;
                case AgentPipelineContextTypes.FollowOn:
                    await _agentStreamingContext.AddWorkflowAsync("Welcome Back to Aptix, let's get started...", cancellationToken);
                    result = await _sessionRestorer.ExecuteAsync(ctx);
                    break;
                case AgentPipelineContextTypes.ClientToolCallContinuation:
                    await _agentStreamingContext.AddWorkflowAsync("Welcome Back to Aptix, resuming tool execution...", cancellationToken);
                    result = await _toolSessionRestorer.ExecuteAsync(ctx);
                    break;
                default:
                    return InvokeResult<AgentExecuteResponse>.FromError($"don't know how to handle {ctx.Type}.");
            }
            
            if(result == null)
                return InvokeResult<AgentExecuteResponse>.FromError("null result from next");

            if (result.Successful)
            {
                await _agentSessionManager.UpdateSessionAsync(result.Result.Session, org, user);
                ctx.LogDetails(_adminLogger, PipelineSteps.RequestHandler, sw.Elapsed);

                var postValidation = _validator.ValidatePostStep(result.Result, PipelineSteps.RequestHandler);
                if(!postValidation.Successful) return InvokeResult<AgentExecuteResponse>.FromInvokeResult(postValidation.ToInvokeResult());

                var response = await _responseBuilder.BuildAsync(result.Result);
                return response;
            }
            else
            {
                if (ctx.Session != null) // failed path will check to see if we have a session, if we do, save it for diagnostics
                    await _agentSessionManager.UpdateSessionAsync(ctx.Session, org, user);

                ctx.LogStepErrorDetails(_adminLogger, PipelineSteps.RequestHandler, result.ToInvokeResult(), sw.Elapsed);
                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(result.ToInvokeResult());
            }
        }
    }
}
