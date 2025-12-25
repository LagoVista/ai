using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services
{
    public sealed class OpenAIResponsesInvoker : LagoVista.AI.Interfaces.IOpenAIResponsesInvoker
    {
        private readonly IAdminLogger _logger;

        public OpenAIResponsesInvoker(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InvokeResult<HttpResponseMessage>> InvokeAsync(string baseUrl, string apiKey, string requestJson, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return InvokeResult<HttpResponseMessage>.FromError("OpenAI baseUrl is required.", "OPENAI_INVOKER_MISSING_BASEURL");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return InvokeResult<HttpResponseMessage>.FromError("OpenAI apiKey is required.", "OPENAI_INVOKER_MISSING_APIKEY");
            }

            // Create per-call client (matches your current pattern). We can swap to IHttpClientFactory later.
            using (var httpClient = CreateHttpClient(baseUrl, apiKey))
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/responses")
            {
                Content = new StringContent(requestJson ?? "{}", Encoding.UTF8, "application/json")
            })
            {
                HttpResponseMessage httpResponse = null;

                try
                {
                    httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        // Caller owns disposal.
                        return InvokeResult<HttpResponseMessage>.Create(httpResponse);
                    }

                    var errorBody = await SafeReadBodyAsync(httpResponse);
                    var msg = "LLM call failed with HTTP " + (int)httpResponse.StatusCode + " (" + httpResponse.ReasonPhrase + ").";

                    _logger.AddError("[OpenAIResponsesInvoker_InvokeAsync__HTTP]", msg);
                    if (!string.IsNullOrWhiteSpace(errorBody))
                    {
                        _logger.AddError("[OpenAIResponsesInvoker_InvokeAsync__Body]", errorBody);
                    }

                    var reasonSuffix = BuildReasonSuffix(errorBody);
                    var finalMsg = msg + (string.IsNullOrWhiteSpace(reasonSuffix) ? "" : " " + reasonSuffix);

                    httpResponse.Dispose();
                    return InvokeResult<HttpResponseMessage>.FromError(finalMsg, "OPENAI_INVOKER_HTTP_ERROR");
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    httpResponse?.Dispose();
                    return InvokeResult<HttpResponseMessage>.Abort();
                }
                catch (Exception ex)
                {
                    httpResponse?.Dispose();
                    _logger.AddException("[OpenAIResponsesInvoker_InvokeAsync__Exception]", ex);
                    return InvokeResult<HttpResponseMessage>.FromError("Unexpected exception during LLM call.", "OPENAI_INVOKER_EXCEPTION");
                }
            }
        }

        private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response)
        {
            try
            {
                return response?.Content == null ? null : await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return null;
            }
        }

        private string BuildReasonSuffix(string errorBody)
        {
            if (string.IsNullOrWhiteSpace(errorBody))
            {
                return null;
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<OpenAIErrorResponse>(errorBody);
                if (parsed != null)
                {
                    return "Reason: " + parsed;
                }
            }
            catch (Exception ex)
            {
                _logger.AddException("[OpenAIResponsesInvoker_InvokeAsync__ErrorDeserialize]", ex);
            }

            return "Raw: " + errorBody;
        }

        private static HttpClient CreateHttpClient(string baseUrl, string apiKey)
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(120)
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return client;
        }
    }
}
