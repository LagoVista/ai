using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RagCli.Types;

namespace RagCli.Services
{
    public class QdrantClient
    {
        private readonly HttpClient _http;

        public QdrantClient(string endpoint, string apiKey)
        {
            _http = new HttpClient { BaseAddress = new Uri(endpoint) };
            if (!string.IsNullOrWhiteSpace(apiKey))
                _http.DefaultRequestHeaders.Add("api-key", apiKey);
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
            var json = await resp.Content.ReadFromJsonAsync<QdrantSearchResponse>();
            return json!.Result ?? new();
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
        public Dictionary<string, object?> Payload { get; set; } = new();
    }

    public class QdrantSearchRequest
    {
        [JsonPropertyName("vector")] public float[] Vector { get; set; } = Array.Empty<float>();
        [JsonPropertyName("limit")] public int Limit { get; set; } = 8;
        [JsonPropertyName("with_payload")] public bool WithPayload { get; set; } = true;
        [JsonPropertyName("filter")] public QdrantFilter? Filter { get; set; }
    }

    public class QdrantFilter
    {
        [JsonPropertyName("must")] public List<QdrantCondition> Must { get; set; } = new();
        [JsonPropertyName("should")] public List<QdrantCondition>? Should { get; set; }
        [JsonPropertyName("must_not")] public List<QdrantCondition>? MustNot { get; set; }
    }

    public class QdrantCondition
    {
        [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
        [JsonPropertyName("match")] public QdrantMatch? Match { get; set; }
        [JsonPropertyName("range")] public QdrantRange? Range { get; set; }
    }

    public class QdrantMatch { [JsonPropertyName("value")] public object? Value { get; set; } }

    public class QdrantRange
    {
        [JsonPropertyName("gte")] public double? Gte { get; set; }
        [JsonPropertyName("lte")] public double? Lte { get; set; }
    }

    public class QdrantSearchResponse
    {
        [JsonPropertyName("result")] public List<QdrantScoredPoint>? Result { get; set; }
    }

    public class QdrantScoredPoint
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("score")] public double Score { get; set; }
        [JsonPropertyName("payload")] public Dictionary<string, object>? Payload { get; set; }
    }
}