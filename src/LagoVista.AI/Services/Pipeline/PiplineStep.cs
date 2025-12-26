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
        private readonly IAgentPipelineContextValidator _validator;

        public PipelineStep(IAgentPipelineStep next, IAgentPipelineContextValidator validator, IAdminLogger adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public PipelineStep(IAgentPipelineContextValidator validator, IAdminLogger adminLogger) 
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _next = null;
        }

        public async Task<InvokeResult<IAgentPipelineContext>> ExecuteAsync(IAgentPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            var preValidation = _validator.ValidatePreStep(ctx, StepType);
            if (!preValidation.Successful)
                return InvokeResult<IAgentPipelineContext>.FromInvokeResult(preValidation);

            var sw = LogStart(ctx);

            if (ctx.CancellationToken.IsCancellationRequested) return InvokeResult<IAgentPipelineContext>.Abort();

            var result = await ExecuteStepAsync(ctx); 

            if (ctx.CancellationToken.IsCancellationRequested) return InvokeResult<IAgentPipelineContext>.Abort();

            if (!result.Successful)
            {
                LogFailure(ctx, result, sw.Elapsed);
                return result;
            }
           
            if(!_validator.ValidatePostStep(result.Result, StepType).Successful)
            {
                var contractViolation = _validator.ValidatePostStep(result.Result, StepType);
                LogContractViolation(ctx, contractViolation.ErrorMessage, sw.Elapsed);
                return InvokeResult<IAgentPipelineContext>.FromInvokeResult(contractViolation);
            }

            LogSuccess(ctx, sw.Elapsed);

            if (_next == null)
            {
                return result;
            }

            return await _next.ExecuteAsync(result.Result);
         }

        protected abstract PipelineSteps StepType { get; }

        protected abstract Task<InvokeResult<IAgentPipelineContext>> ExecuteStepAsync(IAgentPipelineContext ctx);

        protected virtual Stopwatch LogStart(IAgentPipelineContext ctx)
        {
            ctx.LogDetails(_adminLogger, StepType);
            return Stopwatch.StartNew();
        }

        protected virtual void LogSuccess(IAgentPipelineContext ctx, TimeSpan elapsed) =>
            ctx.LogDetails(_adminLogger, StepType, elapsed);

        protected virtual void LogFailure(IAgentPipelineContext ctx, InvokeResult<IAgentPipelineContext> result, TimeSpan elapsed) =>
            ctx.LogStepErrorDetails(_adminLogger, StepType, result.ErrorMessage, elapsed);

        protected virtual void LogException(IAgentPipelineContext ctx, Exception ex, TimeSpan elapsed) =>
            ctx.LogStepErrorDetails(_adminLogger, StepType, ex.Message, elapsed);

        protected virtual void LogContractViolation(IAgentPipelineContext ctx, string message, TimeSpan elapsed) =>
            ctx.LogStepErrorDetails(_adminLogger, StepType, message, elapsed);
    }
}
