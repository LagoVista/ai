// File: ./src/LagoVista.AI.Services/OpenAIResponsesClient.cs
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services
{
    // Target: keep this boring and (roughly) < 100 LOC by delegating all details.
    public sealed class OpenAIResponsesClientPipelineStap : ILLMClient
    {
        private readonly IAdminLogger _log;
        private readonly IResponsesRequestBuilder _builder;
        private readonly IAgentExecuteResponseParser _parser;
        private readonly ILLMEventPublisher _events;
        private readonly IOpenAIResponsesExecutor _invoker;

        public OpenAIResponsesClientPipelineStap(
            IOpenAISettings settings, IAdminLogger log, IResponsesRequestBuilder builder, IAgentExecuteResponseParser parser,
            ILLMEventPublisher events, IOpenAIResponsesExecutor invoker)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null) return InvokeResult<AgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "OPENAI_CLIENT_NULL_CONTEXT");

            var vr = ctx.Validate(PipelineSteps.LLMClient);
            if (!vr.Successful) return InvokeResult<AgentPipelineContext>.FromInvokeResult(vr);

            try
            {
                var req = await _builder.BuildAsync(ctx);
                var reqJson = JsonConvert.SerializeObject(req);
                _log.Trace("[OpenAIResponsesClient__ExecuteAsync] Call LLM with JSON\r\n=====\r\n" + reqJson + "\r\n====");
    
                await _events.PublishAsync(ctx.Session.Id, "LLMStarted", "in-progress", "Calling OpenAI model...", null, ctx.CancellationToken);
              
                var sw = Stopwatch.StartNew();
                var invoke = await _invoker.InvokeAsync(ctx, reqJson);
                if (!invoke.Successful) return await FailAsync(ctx.Session.Id, invoke.ToInvokeResult(), "LLM invoke failed.", ctx.CancellationToken);

                var parsed = await _parser.ParseAsync(ctx, invoke.Result);
                if (!parsed.Successful) return await FailAsync(ctx.Session.Id, parsed.ToInvokeResult(), "Response parse failed.", ctx.CancellationToken);

                sw.Stop();
                await _events.PublishAsync(ctx.Session.Id, "LLMCompleted", "completed", "Model response received.", sw.Elapsed.TotalMilliseconds, ctx.CancellationToken);
                return parsed;
            }
            catch (Exception ex)
            {
                _log.AddException("[OpenAIResponsesClient_ExecuteAsync__Exception]", ex);
                await _events.PublishAsync(ctx.Session.Id, "LLMFailed", "failed", "Unexpected exception during LLM call.", null, CancellationToken.None);
                return InvokeResult<AgentPipelineContext>.FromError("Unexpected exception during LLM call.", "OPENAI_CLIENT_EXCEPTION");
            }
        }

        private async Task<InvokeResult<AgentPipelineContext>> FailAsync(string sid, InvokeResult err, string fallbackMsg, CancellationToken ct)
        {
            await _events.PublishAsync(sid, "LLMFailed", "failed", err?.ErrorMessage ?? fallbackMsg, null, ct);
            return InvokeResult<AgentPipelineContext>.FromInvokeResult(err);
        }
    }
}
