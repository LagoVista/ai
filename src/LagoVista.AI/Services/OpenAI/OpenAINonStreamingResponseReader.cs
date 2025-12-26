using LagoVista.AI.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;

namespace LagoVista.AI.Services.OpenAI
{

    /// <summary>
    /// Reads a non-streaming /responses response and returns the body JSON.
    /// </summary>
    public sealed class OpenAINonStreamingResponseReader : IOpenAINonStreamingResponseReader
    {
        private readonly IAdminLogger _logger;

        public OpenAINonStreamingResponseReader(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InvokeResult<string>> ReadAsync(HttpResponseMessage httpResponse, CancellationToken cancellationToken = default)
        {
            if (httpResponse == null)
            {
                return InvokeResult<string>.FromError("HttpResponseMessage is null.", "OPENAI_NONSTREAM_NULL_RESPONSE");
            }

            string json;
            try
            {
                json = await httpResponse.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.AddException("[OpenAINonStreamingResponseReader_ReadAsync__ReadBodyException]", ex);
                return InvokeResult<string>.FromError("Failed to read non-streaming response body.", "OPENAI_NONSTREAM_READ_FAILED");
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return InvokeResult<string>.FromError("Empty response JSON.", "OPENAI_NONSTREAM_EMPTY_BODY");
            }

            return InvokeResult<string>.Create(json);
        }
    }
}
