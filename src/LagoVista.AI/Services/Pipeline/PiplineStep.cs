using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Diagnostics;
using System.IO.IsolatedStorage;
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
            _adminLogger.Trace($"{this.Tag()} - Start {StepType}");

            try
            {
                if (ctx == null) throw new ArgumentNullException(nameof(ctx));
                var preValidation = _validator.ValidatePreStep(ctx, StepType);
                if (!preValidation.Successful)
                    return InvokeResult<IAgentPipelineContext>.FromInvokeResult(preValidation);

                var sw = LogStart(ctx);

                if (ctx.CancellationToken.IsCancellationRequested) return InvokeResult<IAgentPipelineContext>.Abort();

                var result = await ExecuteStepAsync(ctx);
                _adminLogger.Trace($"{this.Tag()} - Completed {StepType}");

                if (ctx.CancellationToken.IsCancellationRequested) return InvokeResult<IAgentPipelineContext>.Abort();

                if (!result.Successful) return result;
             
                var postvalidation = _validator.ValidatePostStep(result.Result, StepType);
                if (!postvalidation.Successful)
                {
                    LogContractViolation(ctx, postvalidation.ErrorMessage, sw.Elapsed);
                    return InvokeResult<IAgentPipelineContext>.FromInvokeResult(postvalidation);
                }

                _adminLogger.Trace($"{this.Tag()}- Success {StepType} {sw.Elapsed.TotalMilliseconds}ms");

                LogSuccess(ctx, sw.Elapsed);

                if (_next == null || ctx.IsTerminal)
                {
                    return result;
                }

                return await _next.ExecuteAsync(result.Result);
            }
            catch(Exception ex)
            {
                _adminLogger.AddException($"{this.Tag()} - Exception in {StepType}", ex);
                return InvokeResult<IAgentPipelineContext>.FromError(ex.Message, ex.GetType().ToString());  
            }
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

        protected virtual void LogContractViolation(IAgentPipelineContext ctx, string message, TimeSpan elapsed) =>
            ctx.LogStepErrorDetails(_adminLogger, StepType, message, elapsed);
    }
}
