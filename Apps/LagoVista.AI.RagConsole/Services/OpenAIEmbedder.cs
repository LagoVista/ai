using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;


namespace RagCli.Services
{
    /// <summary>
    /// OpenAI Embeddings client for models like text-embedding-3-large (3072 dims) or -small (1536).
    /// </summary>
    public class OpenAIEmbedder : IEmbedder
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly int _expectedDims;


        public OpenAIEmbedder(string apiKey, string model = "text-embedding-3-large", string baseUrl = "https://api.openai.com", int expectedDims = 3072)
        {
            _model = model;
            _expectedDims = expectedDims;
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _http.Timeout = TimeSpan.FromSeconds(120);
        }


        public async Task<float[]> EmbedAsync(string text)
        {
            // Defensive clamp for very long inputs: OpenAI models support long inputs, but you can trim if needed.
            // Here we keep it simple; callers should chunk long files before embedding.
            var payload = new { model = _model, input = text };


            var resp = await PostWithRetryAsync("/v1/embeddings", payload);
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var arr = json.GetProperty("data")[0].GetProperty("embedding");
            var vec = new float[arr.GetArrayLength()];
            int i = 0; foreach (var v in arr.EnumerateArray()) vec[i++] = (float)v.GetDouble();


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