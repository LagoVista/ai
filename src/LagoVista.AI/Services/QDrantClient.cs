using LagoVista.AI.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace LagoVista.AI.Services
{
    public class QdrantClient : IQdrantClient
    {
        private readonly HttpClient _http;

        public QdrantClient(IQdrantSettings settings)
        {
            _http = new HttpClient { BaseAddress = new Uri(settings.QdrantEndpoint) };
            _http.DefaultRequestHeaders.Add("api-key", settings.QdrantApiKey);
        }

        public async Task EnsureCollectionAsync(QdrantCollectionConfig cfg)
        {
            var name = cfg.Name;
            var exists = await _http.GetAsync($"/collections/{name}");
            if (exists.IsSuccessStatusCode) return;

            var req = new
            {
                vectors = new { size = cfg.VectorSize, distance = cfg.Distance }
            };
            var resp = await _http.PutAsJsonAsync($"/collections/{name}", req);
            resp.EnsureSuccessStatusCode();
        }

        public async Task UpsertAsync(string collection, IEnumerable<QdrantPoint> points)
        {
            var req = new { points = points.Select(p => new { id = p.Id, vector = p.Vector, payload = p.Payload }) };
            var resp = await _http.PutAsJsonAsync($"/collections/{collection}/points?wait=true", req);
            resp.EnsureSuccessStatusCode();
        }

        public async Task<List<QdrantScoredPoint>> SearchAsync(string collection, QdrantSearchRequest req)
        {
            var resp = await _http.PostAsJsonAsync($"/collections/{collection}/points/search", req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsAsync<QdrantSearchResponse>();
            return json!.Result ?? new List<QdrantScoredPoint>();
        }

        public async Task DeleteByIdsAsync(string collection, IEnumerable<string> ids)
        {
            var idArray = ids.ToArray();
            if (idArray.Length == 0) return;

            var payload = new
            {
                points = idArray.Select(id => new { id })
            };

            var resp = await _http.PostAsJsonAsync(
                $"collections/{collection}/points/delete",
                new { points = idArray });

            resp.EnsureSuccessStatusCode();
        }
    }

    public class QdrantCollectionConfig
    {
        public string Name { get; set; } = "code_chunks";
        public int VectorSize { get; set; }
        public string Distance { get; set; } = "Cosine";
    }

    public class QdrantPoint
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public static string NewId() => Guid.NewGuid().ToString("N");
        public float[] Vector { get; set; } = Array.Empty<float>();
        public Dictionary<string, object?> Payload { get; set; } = new Dictionary<string, object?>();
    }

    public class QdrantSearchRequest
    {
        [JsonProperty("vector")] public float[] Vector { get; set; } = Array.Empty<float>();
        [JsonProperty("limit")] public int Limit { get; set; } = 8;
        [JsonProperty("with_payload")] public bool WithPayload { get; set; } = true;
        [JsonProperty("filter")] public QdrantFilter? Filter { get; set; }
    }

    public class QdrantFilter
    {
        [JsonProperty("must")] public List<QdrantCondition> Must { get; set; } = new List<QdrantCondition>();
        [JsonProperty("should")] public List<QdrantCondition>? Should { get; set; }
        [JsonProperty("must_not")] public List<QdrantCondition>? MustNot { get; set; }
    }

    public class QdrantCondition
    {
        [JsonProperty("key")] public string Key { get; set; } = string.Empty;
        [JsonProperty("match")] public QdrantMatch? Match { get; set; }
        [JsonProperty("range")] public QdrantRange? Range { get; set; }
    }

    public class QdrantMatch { [JsonProperty("value")] public object? Value { get; set; } }

    public class QdrantRange
    {
        [JsonProperty("gte")] public double? Gte { get; set; }
        [JsonProperty("lte")] public double? Lte { get; set; }
    }

    public class QdrantSearchResponse
    {
        [JsonProperty("result")] public List<QdrantScoredPoint>? Result { get; set; }
    }

    public class QdrantScoredPoint
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("score")] public double Score { get; set; }
        [JsonProperty("payload")] public Dictionary<string, object>? Payload { get; set; }
    }
}

