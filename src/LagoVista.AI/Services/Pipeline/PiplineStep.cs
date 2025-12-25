using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Pipeline
{
    public abstract class PipelineStep : IAgentPipelineStep
    {
        private readonly IAgentPipelineStep _next;
        private readonly IAdminLogger _adminLogger;

        public PipelineStep(IAgentPipelineStep next, IAdminLogger adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public PipelineStep(IAdminLogger adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _next = null;
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            var validationResult = ctx.Validate(StepType);
            if (!validationResult.Successful)
                return InvokeResult<AgentPipelineContext>.FromInvokeResult(validationResult);

            var sw = LogStart(ctx);

            if (ctx.CancellationToken.IsCancellationRequested) return InvokeResult<AgentPipelineContext>.Abort();

            var result = await ExecuteStepAsync(ctx); 

            if (ctx.CancellationToken.IsCancellationRequested) return InvokeResult<AgentPipelineContext>.Abort();

            if (!result.Successful)
            {
                LogFailure(ctx, result, sw.Elapsed);
                return result;
            }
           
            LogSuccess(ctx, sw.Elapsed);

            if (_next == null)
            {
                return result;
            }

            return await _next.ExecuteAsync(result.Result);
         }

        protected abstract PipelineSteps StepType { get; }

        protected abstract Task<InvokeResult<AgentPipelineContext>> ExecuteStepAsync(AgentPipelineContext ctx);

        protected virtual Stopwatch LogStart(AgentPipelineContext ctx)
        {
            ctx.LogDetails(_adminLogger, StepType);
            return Stopwatch.StartNew();
        }

        protected virtual void LogSuccess(AgentPipelineContext ctx, TimeSpan elapsed) =>
            ctx.LogDetails(_adminLogger, StepType, elapsed);

        protected virtual void LogFailure(AgentPipelineContext ctx, InvokeResult<AgentPipelineContext> result, TimeSpan elapsed) =>
            ctx.LogStepErrorDetails(_adminLogger, StepType, result.ErrorMessage, elapsed);

        protected virtual void LogException(AgentPipelineContext ctx, Exception ex, TimeSpan elapsed) =>
            ctx.LogStepErrorDetails(_adminLogger, StepType, ex.Message, elapsed);

        protected virtual void LogContractViolation(AgentPipelineContext ctx, string message, TimeSpan elapsed) =>
            ctx.LogStepErrorDetails(_adminLogger, StepType, message, elapsed);
    }
}
