// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 74273714f2ab7b165dfbd250f545925bc65cbf5c0542bb2e8e7e7bb8ba3ee42a
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime;
using System.Threading.Tasks;


namespace LagoVista.AI.Services
{
    public class OpenAIEmbedder : IEmbedder
    {
        private HttpClient _http;
        private readonly string _model;
        private readonly int _expectedDims;
        private readonly IAdminLogger _adminLogger;    

        private class EmbeddingResponse
        {
            [JsonProperty("data")]
            public EmbeddingData[] Data { get; set; }
        }

        private class EmbeddingData
        {
            [JsonProperty("embedding")]
            public float[] Embedding { get; set; }
        }

        public OpenAIEmbedder(IAdminLogger adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public OpenAIEmbedder(IOpenAISettings aiSettings, IAdminLogger adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));

            _model = "text-embedding-3-large";
            _expectedDims = 3072;
            _http = new HttpClient { BaseAddress = new Uri(aiSettings.OpenAIUrl) };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiSettings.OpenAIApiKey);
            _http.Timeout = TimeSpan.FromSeconds(30);
        }

        public OpenAIEmbedder(AgentContext vectorDb, IOpenAISettings aiSettings, IAdminLogger adminLogger)
        {
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));

            _model = "text-embedding-3-large";
            _expectedDims = 3072;
            _http = new HttpClient { BaseAddress = new Uri(aiSettings.OpenAIUrl) };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", vectorDb.LlmApiKey);
            _http.Timeout = TimeSpan.FromSeconds(30);
        }


        public async Task<float[]> EmbedAsync(string text)
        {
            var payload = new { model = _model, input = text };

            using (var resp = await PostWithRetryAsync("/v1/embeddings", payload))
            {
                var er = await resp.Content.ReadAsAsync<EmbeddingResponse>();
                var vec = er.Data.FirstOrDefault().Embedding;

                if (_expectedDims > 0 && vec.Length != _expectedDims)
                    throw new InvalidOperationException($"Embedding dims {vec.Length} != expected {_expectedDims}. Check model + Qdrant.VectorSize.");

                return vec;
            }
        }


        private async Task<HttpResponseMessage> PostWithRetryAsync(string path, object body)
        {
            const int maxRetries = 4;
            var delay = TimeSpan.FromMilliseconds(400);


            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var resp = await _http.PostAsJsonAsync(path, body);
                    if ((int)resp.StatusCode < 500 && resp.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
                    {
                        if (resp.IsSuccessStatusCode)
                        {
                            return resp;
                        }
                        var errorContent = await resp.Content.ReadAsStringAsync();

                        Console.WriteLine(errorContent);
                        throw new InvalidOperationException($"OpenAI API error {resp.StatusCode}: {errorContent}");
                    }

                    resp.Dispose();
                }
                catch (TaskCanceledException ex)
                {
                    _adminLogger.AddError("[OpenAIEmbedder__PostWithRetryAsync]", $"[OpenAIEmbedder__PostWithRetryAsync] - Timeout Exception - Attempt {attempt} of 5, will retry");
                }
                finally
                {
                    // Retry on 429/5xx with backoff
                    await Task.Delay(delay);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
                }
            }


            _adminLogger.AddError("[OpenAIEmbedder__PostWithRetryAsync]", $"[OpenAIEmbedder__PostWithRetryAsync] - Timeout Exception - final attempt before giving up and throwing exception");

            // Last try (let it throw if fails)
            var final = await _http.PostAsJsonAsync(path, body);
            return final.EnsureSuccessStatusCode();
        }
    }
}