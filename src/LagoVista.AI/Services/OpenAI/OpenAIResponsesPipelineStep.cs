// File: ./src/LagoVista.AI.Services/OpenAIResponsesClient.cs
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.OpenAI
{
    public sealed class OpenAIResponsesClientPipelineStap : ILLMClient
    {
        private readonly IAdminLogger _log;
        private readonly IResponsesRequestBuilder _builder;
        private readonly IAgentExecuteResponseParser _parser;
        private readonly ILLMEventPublisher _events;
        private readonly IOpenAIResponsesExecutor _invoker;
        private readonly IAgentPipelineContextValidator _validator;

        public OpenAIResponsesClientPipelineStap(
            IOpenAISettings settings, IAdminLogger log, IResponsesRequestBuilder builder, IAgentExecuteResponseParser parser,
            IAgentPipelineContextValidator validator, ILLMEventPublisher events, IOpenAIResponsesExecutor invoker)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        private async Task<InvokeResult<string>> BuildNonToolResultdRequest(IAgentPipelineContext ctx)
        {
            var req = await _builder.BuildAsync(ctx);
            if (!req.Successful)
                return InvokeResult<string>.FromInvokeResult(req.ToInvokeResult());

            var json = JsonConvert.SerializeObject(req.Result);

            return InvokeResult<string>.Create(json);
        }

        private Task<InvokeResult<string>> BuildToolResultdRequest(IAgentPipelineContext ctx)
        {
            try
            {
                var result = OpenAIToolResultRequest.FromResults(ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults);
                result.Model = ctx.ConversationContext.ModelName;
                return Task.FromResult(InvokeResult<string>.Create( JsonConvert.SerializeObject(result)));
            }
            catch(InvalidOperationException ex)
            {
                return Task.FromResult(InvokeResult<string>.FromError(ex.Message));
            }
        }

        public async Task<InvokeResult<IAgentPipelineContext>> ExecuteAsync(IAgentPipelineContext ctx)
        {
            if (ctx == null) return InvokeResult<IAgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "OPENAI_CLIENT_NULL_CONTEXT");

            var preValidation = _validator.ValidatePreStep(ctx, PipelineSteps.LLMClient);
            if (!preValidation.Successful) return InvokeResult<IAgentPipelineContext>.FromInvokeResult(preValidation);

            //var requestBuildResult = ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCallResults.Any() ?
            //    await BuildToolResultdRequest(ctx) :
            //    await BuildNonToolResultdRequest(ctx);

            var requestBuildResult = await BuildNonToolResultdRequest(ctx);

            if(!requestBuildResult.Successful) await FailAsync(ctx.Session.Id, requestBuildResult.ToInvokeResult(), "Response parse failed.", ctx.CancellationToken);

            var requestJson = requestBuildResult.Result;

            _log.Trace("[JSON.LLMREQUEST]=" + requestJson);

            await _events.PublishAsync(ctx.Session.Id, "LLMStarted", "in-progress", "Calling OpenAI model...", null, ctx.CancellationToken);

            var sw = Stopwatch.StartNew();
            var invoke = await _invoker.InvokeAsync(ctx, requestJson);
            if (!invoke.Successful) return await FailAsync(ctx.Session.Id, invoke.ToInvokeResult(), "LLM invoke failed.", ctx.CancellationToken);

            _log.Trace($"[JSON.LLMRESPONSE]=" + invoke.Result.Trim());

            var parsed = await _parser.ParseAsync(ctx, invoke.Result);
            if (!parsed.Successful) return await FailAsync(ctx.Session.Id, parsed.ToInvokeResult(), "Response parse failed.", ctx.CancellationToken);

            sw.Stop();

            var postValidation = _validator.ValidatePostStep(ctx, PipelineSteps.LLMClient);
            if (!postValidation.Successful) return InvokeResult<IAgentPipelineContext>.FromInvokeResult(postValidation);

            await _events.PublishAsync(ctx.Session.Id, "LLMCompleted", "completed", "Model response received.", sw.Elapsed.TotalMilliseconds, ctx.CancellationToken);
            return parsed;
        }

        private async Task<InvokeResult<IAgentPipelineContext>> FailAsync(string sid, InvokeResult err, string fallbackMsg, CancellationToken ct)
        {
            _log.AddError("[OpenAIResponsesClientPipelineStap__ExecuteAsync]", err.ErrorMessage);
            await _events.PublishAsync(sid, "LLMFailed", "failed", err?.ErrorMessage ?? fallbackMsg, null, ct);
            return InvokeResult<IAgentPipelineContext>.FromInvokeResult(err);
        }
    }
}
