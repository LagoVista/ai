using LagoVista.AI.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;


namespace LagoVista.AI.Services
{
    public class OpenAIEmbedder : IEmbedder
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly int _expectedDims;

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

        public OpenAIEmbedder(IOpenAISettings aiSettings, IAdminLogger adminLogger)
        {
            _model = "text-embedding-3-large";
            _expectedDims = 3072;
            _http = new HttpClient { BaseAddress = new Uri(aiSettings.OpenAIUrl) };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiSettings.OpenAIApiKey);
            _http.Timeout = TimeSpan.FromSeconds(120);
        }

        public async Task<float[]> EmbedAsync(string text)
        {
            // Defensive clamp for very long inputs: OpenAI models support long inputs, but you can trim if needed.
            // Here we keep it simple; callers should chunk long files before embedding.
            var payload = new { model = _model, input = text };

            var resp = await PostWithRetryAsync("/v1/embeddings", payload);
            var er = await resp.Content.ReadAsAsync<EmbeddingResponse>();
            var vec = er.Data.FirstOrDefault().Embedding;
          
            if (_expectedDims > 0 && vec.Length != _expectedDims)
                throw new InvalidOperationException($"Embedding dims {vec.Length} != expected {_expectedDims}. Check model + Qdrant.VectorSize.");

            return vec;
        }


        private async Task<HttpResponseMessage> PostWithRetryAsync(string path, object body)
        {
            const int maxRetries = 4;
            var delay = TimeSpan.FromMilliseconds(400);


            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var resp = await _http.PostAsJsonAsync(path, body);
                if ((int)resp.StatusCode < 500 && resp.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (resp.IsSuccessStatusCode)
                        return resp;

                    var errorContent = await resp.Content.ReadAsStringAsync();

                    Console.WriteLine(errorContent);
                    throw new InvalidOperationException($"OpenAI API error {resp.StatusCode}: {errorContent}");
                }
                    // Retry on 429/5xx with backoff
                resp.Dispose();
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }

            // Last try (let it throw if fails)
            var final = await _http.PostAsJsonAsync(path, body);
            return final.EnsureSuccessStatusCode();
        }
    }
}