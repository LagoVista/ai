using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Embedding.Interfaces;
using LagoVista.AI.Rag.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace LagoVista.AI.Rag.ContractPacks.Embedding.Services
{
    public sealed class OpenAiEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly EmbeddingsConfig _config;
        private readonly string _baseUrl;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public OpenAiEmbeddingService(HttpClient httpClient, EmbeddingsConfig config)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(_config.ApiKey))
                throw new ArgumentNullException(nameof(config.ApiKey));

            var url = string.IsNullOrWhiteSpace(_config.BaseUrl) ? "https://api.openai.com/v1" : _config.BaseUrl;
            _baseUrl = url.TrimEnd('/');
        }

        public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            if (text == null)
                text = string.Empty;

            var request = new EmbeddingRequest
            {
                Model = string.IsNullOrWhiteSpace(_config.Model) ? "text-embedding-3-large" : _config.Model,
                Input = text
            };

            var json = JsonConvert.SerializeObject(request, JsonSettings);

            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/embeddings"))
            {
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.ApiKey);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var embeddingResponse = JsonConvert.DeserializeObject<EmbeddingResponse>(responseJson, JsonSettings);

                    if (embeddingResponse == null || embeddingResponse.Data == null || embeddingResponse.Data.Count == 0)
                        throw new InvalidOperationException("OpenAI embedding response did not contain any data.");

                    var first = embeddingResponse.Data[0];
                    if (first == null || first.Embedding == null)
                        throw new InvalidOperationException("OpenAI embedding response contained null embedding.");

                    return first.Embedding;
                }
            }
        }

        private sealed class EmbeddingRequest
        {
            [JsonProperty("model")]
            public string Model { get; set; }

            [JsonProperty("input")]
            public string Input { get; set; }
        }

        private sealed class EmbeddingResponse
        {
            [JsonProperty("data")]
            public List<EmbeddingData> Data { get; set; }
        }

        private sealed class EmbeddingData
        {
            [JsonProperty("embedding")]
            public float[] Embedding { get; set; }
        }
    }
}
