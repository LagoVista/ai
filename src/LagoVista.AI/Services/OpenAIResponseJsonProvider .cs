using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;

namespace LagoVista.AI.Services
{
    public sealed class OpenAIResponseJsonProvider : IOpenAIResponseJsonProvider
    {
        private readonly IOpenAIStreamingResponseReader _streamReader;
        private readonly IOpenAINonStreamingResponseReader _nonStreamReader;

        public OpenAIResponseJsonProvider(
            IOpenAIStreamingResponseReader streamReader,
            IOpenAINonStreamingResponseReader nonStreamReader)
        {
            _streamReader = streamReader ?? throw new ArgumentNullException(nameof(streamReader));
            _nonStreamReader = nonStreamReader ?? throw new ArgumentNullException(nameof(nonStreamReader));
        }

        public Task<InvokeResult<string>> GetFinalJsonAsync(AgentPipelineContext ctx, HttpResponseMessage httpResponse, CancellationToken cancellationToken = default)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (httpResponse == null) return Task.FromResult(InvokeResult<string>.FromError("HttpResponseMessage is null.", "OPENAI_JSONPROVIDER_NULL_RESPONSE"));
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(InvokeResult<string>.Abort());

            return ctx.Envelope.Stream
                ? _streamReader.ReadAsync(httpResponse, ctx.Session.Id, cancellationToken)
                : _nonStreamReader.ReadAsync(httpResponse, cancellationToken);
        }
    }
}
