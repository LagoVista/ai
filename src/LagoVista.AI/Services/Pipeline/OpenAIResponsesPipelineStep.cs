// File: ./src/LagoVista.AI.Services/OpenAIResponsesClient.cs
using System;
using System.Diagnostics;
using System.Net.Http;
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
        private readonly IOpenAISettings _settings;
        private readonly IAdminLogger _log;
        private readonly IResponsesRequestBuilder _builder;
        private readonly IAgentExecuteResponseParser _parser;
        private readonly ILLMEventPublisher _events;
        private readonly IOpenAIResponsesInvoker _invoker;
        private readonly IOpenAIStreamingResponseReader _streamReader;
        private readonly IOpenAINonStreamingResponseReader _nonStreamReader;
        private readonly ILLMWorkflowNarrator _narrator;

        public OpenAIResponsesClientPipelineStap(
            IOpenAISettings settings, IAdminLogger log, IResponsesRequestBuilder builder, IAgentExecuteResponseParser parser,
            ILLMEventPublisher events, IOpenAIResponsesInvoker invoker,
            IOpenAIStreamingResponseReader streamReader, IOpenAINonStreamingResponseReader nonStreamReader,
            ILLMWorkflowNarrator narrator)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
            _streamReader = streamReader ?? throw new ArgumentNullException(nameof(streamReader));
            _nonStreamReader = nonStreamReader ?? throw new ArgumentNullException(nameof(nonStreamReader));
            _narrator = narrator ?? throw new ArgumentNullException(nameof(narrator));
        }

        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null) return InvokeResult<AgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "OPENAI_CLIENT_NULL_CONTEXT");

            var vr = ctx.Validate(PipelineSteps.LLMClient);
            if (!vr.Successful) return InvokeResult<AgentPipelineContext>.FromInvokeResult(vr);

            var sid = ctx.Session.Id;
            var ct = ctx.CancellationToken;

            try
            {
                var req = await _builder.BuildAsync(ctx);
                var reqJson = JsonConvert.SerializeObject(req);
                _log.Trace("[OpenAIResponsesClient__ExecuteAsync] Call LLM with JSON\r\n=====\r\n" + reqJson + "\r\n====");
    
                await _events.PublishAsync(sid, "LLMStarted", "in-progress", "Calling OpenAI model...", null, ct);
                await _narrator.ConnectingAsync(ct);

                var sw = Stopwatch.StartNew();
                var invoke = await _invoker.InvokeAsync(_settings.OpenAIUrl, ctx.AgentContext.LlmApiKey, reqJson, ct);
                if (!invoke.Successful) return await FailAsync(sid, invoke.ToInvokeResult(), "LLM invoke failed.", ct);

                using (var resp = invoke.Result)
                {
                    await _narrator.ThinkingAsync(ct);

                    var finalJson =  ctx.Envelope.Stream ? await _streamReader.ReadAsync(resp, sid, ct) : await _nonStreamReader.ReadAsync(resp, ct);
                    if (!finalJson.Successful) return await FailAsync(sid, finalJson.ToInvokeResult(), "LLM read failed.", ct);

                    await _narrator.SummarizingAsync(ct);

                    var parsed = await _parser.ParseAsync(ctx, finalJson.Result);
                    if (!parsed.Successful) return await FailAsync(sid, parsed.ToInvokeResult(), "Response parse failed.", ct);

                    sw.Stop();
                    await _events.PublishAsync(sid, "LLMCompleted", "completed", "Model response received.", sw.Elapsed.TotalMilliseconds, ct);
                    return parsed;
                }
            }
            catch (TaskCanceledException tex) when (tex.CancellationToken == ct)
            {
                await _events.PublishAsync(sid, "LLMCancelled", "aborted", "LLM call was cancelled.", null, CancellationToken.None);
                return InvokeResult<AgentPipelineContext>.FromError("LLM call was cancelled.", "OPENAI_CLIENT_CANCELLED");
            }
            catch (Exception ex)
            {
                _log.AddException("[OpenAIResponsesClient_ExecuteAsync__Exception]", ex);
                await _events.PublishAsync(sid, "LLMFailed", "failed", "Unexpected exception during LLM call.", null, CancellationToken.None);
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
