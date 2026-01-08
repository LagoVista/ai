// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 3dd7bc397d00e26abf16b7ab9f823ee154a27604c67d83190d46507289cffa64
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Utils.Types;
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Qdrant
{
    public partial class QdrantClient : IQdrantClient
    {
        private HttpClient _http;
        private IAdminLogger _adminLogger;
        private readonly IQdrantSettings _settings;

        public const int VECTOR_SIZE = 3072;
        public const string VECTOR_DISTANCE = "Cosine";

        public QdrantClient(IQdrantSettings settings, IAdminLogger adminLogger)
        {
            _http = new HttpClient { BaseAddress = new Uri(settings.QdrantEndpoint) };
            _http.DefaultRequestHeaders.Add("api-key", settings.QdrantApiKey);
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _settings = settings;
        }

        public QdrantClient(AgentContext vectorDb, IAdminLogger adminLogger)
        {
            _http = new HttpClient { BaseAddress = new Uri(vectorDb.VectorDatabaseUri) };
            _http.DefaultRequestHeaders.Add("api-key", vectorDb.VectorDatabaseApiKey);
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task EnsureCollectionAsync(string name)
        {
            var exists = await _http.GetAsync($"/collections/{name}");
            if (exists.IsSuccessStatusCode) return;

            var req = new
            {
                vectors = new { size = VECTOR_SIZE, distance = VECTOR_DISTANCE }
            };
            var resp = await _http.PutAsJsonAsync($"/collections/{name}", req);
            resp.EnsureSuccessStatusCode();
        }

        public async Task UpsertAsync(string collection, IEnumerable<IRagPoint> points, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var req = new { points = points.Select(p => new { id = p.PointId, vector = p.Vector, payload = p.Payload }) };
            var resp = await _http.PutAsJsonAsync($"/collections/{collection}/points?wait=true", req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _adminLogger.AddError("[QdrantClient__UpsertAsync]", $"[QdrantClient__UpsertAsync] error uploading batch of {points.Count()} HTTP Code {resp.StatusCode} - {body}");

                throw new QdrantHttpException("Could not upload", resp.StatusCode, body);
            }
            else
                _adminLogger.Trace($"[QdrantClient__UpsertAsync] Uploaded batch of {points.Count()} in {sw.Elapsed.TotalMilliseconds}ms");
        }

        public async Task<List<QdrantScoredPoint>> SearchAsync(string collection, QdrantSearchRequest req)
        {
            _adminLogger.Trace($"[QdrantClient__SearchAsync] Search started with collection {collection}");

            var sw = Stopwatch.StartNew();
            var resp = await _http.PostAsJsonAsync($"/collections/{collection}/points/search", req);

            if (resp.IsSuccessStatusCode)
            {

                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsAsync<QdrantSearchResponse>();

                _adminLogger.Trace($"[QdrantClient__SearchAsync] Search completed in {sw.Elapsed.TotalMilliseconds}ms, found {json.Result.Count} results.");

                return json!.Result ?? new List<QdrantScoredPoint>();
            }
            else
            {
                var response = await resp.Content.ReadAsStringAsync();
                throw new QdrantHttpException("Qdrant search failed", resp.StatusCode, response);
            }
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


        public Task DeleteByDocIdAsync(string collection, string docId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(docId))
                throw new ArgumentException("DocId cannot be null or empty.", nameof(docId));

            var filter = new QdrantFilter
            {
                Must =
                {
                    new QdrantCondition
                    {
                        Key = "DocId",
                        Match = new QdrantMatch
                        {
                            Value = docId.Trim()
                        }
                    }
                }
            };

            return DeleteByFilterAsync(collection, filter, ct);
        }


        public Task DeleteByDocIdsAsync(string collection, IEnumerable<string> docIds, CancellationToken ct = default)
        {
            if (docIds == null)
                throw new ArgumentNullException(nameof(docIds));

            var ids = docIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct()
                .ToArray();

            if (ids.Length == 0)
                return Task.CompletedTask;

            var filter = new QdrantFilter
            {
                Must =
                {
                    new QdrantCondition
                    {
                        Key = "DocId",
                        Match = new QdrantMatch
                        {
                            Value = ids
                        }
                    }
                }
            };

            return DeleteByFilterAsync(collection, filter, ct);
        }


        /// <summary>
		/// Delete points by a Qdrant payload filter (e.g., delete all where path == file).
		/// </summary>
		public async Task DeleteByFilterAsync(string collection, QdrantFilter filter, CancellationToken ct = default)
        {
            var url = $"collections/{collection}/points/delete";
            var payload = new { filter };
            var json = JsonConvert.SerializeObject(payload);
            using (var req = new HttpRequestMessage(HttpMethod.Post, url))
            {
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using (var resp = await _http.SendAsync(req, ct).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        var body = await SafeReadAsync(resp).ConfigureAwait(false);
                        throw new QdrantHttpException("Qdrant delete-by-filter failed", resp.StatusCode, body);
                    }
                }
            }
        }


        /// <summary>
        /// Upsert in smaller batches to avoid 413 Payload Too Large. Adaptive:
        /// on 413, halves batch size and retries. Uses lightweight size estimation
        /// if maxPerBatch is not provided.
        /// </summary>
        public async Task UpsertInBatchesAsync(
            string collection,
            IReadOnlyList<IRagPoint> points,
            int vectorDims,
            int? maxPerBatch = null,
            CancellationToken ct = default)
        {
            if (points == null || points.Count == 0) return;


            long bytesPerPoint = (long)vectorDims * 28 + 2000;
            long targetBytes = 2_500_000; // ~2.5 MB per request
            int estBatch = (int)Math.Max(8, Math.Min(128, targetBytes / Math.Max(1, bytesPerPoint)));
            int batchSize = maxPerBatch ?? estBatch;

            int i = 0;
            while (i < points.Count)
            {
                int take = Math.Min(batchSize, points.Count - i);
                var batch = points.Skip(i).Take(take).ToList();
                try
                {
                    await UpsertAsync(collection, batch, ct);
                    //await UpsertJsonGzipAsync(collection, batch, ct).ConfigureAwait(false);
                    i += take;
                }
                catch (QdrantHttpException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                {
                    if (batchSize <= 1) throw;
                    batchSize = Math.Max(1, batchSize / 2);
                }
                catch (QdrantHttpException ex) when (
                    (int)ex.StatusCode == 429 ||
                    ex.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    ex.StatusCode == HttpStatusCode.GatewayTimeout)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Posts an upsert using gzip-compressed JSON body for smaller request payloads.
        /// Compatible with .NET Standard 2.1 (Newtonsoft.Json version).
        /// </summary>
        private async Task UpsertJsonGzipAsync(string collection, List<RagPoint> batch, CancellationToken ct)
        {
            var url = $"collections/{collection}/points?wait=true";
            var payload = new { points = batch };
            var json = JsonConvert.SerializeObject(payload);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var gz = new GZipContent(content))
            using (var req = new HttpRequestMessage(HttpMethod.Put, url) { Content = gz })
            {
                req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                using (var resp = await _http.SendAsync(req, ct).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        var body = await SafeReadAsync(resp).ConfigureAwait(false);
                        throw new QdrantHttpException("Qdrant upsert failed", resp.StatusCode, body);
                    }
                }
            }
        }

        private static async Task<string> SafeReadAsync(HttpResponseMessage resp)
        {
            try { return await resp.Content.ReadAsStringAsync().ConfigureAwait(false); }
            catch { return string.Empty; }
        }



        private static object KvMatch(string key, object value)
            => new { key, match = new { value } };

        private static HttpRequestMessage BuildJsonPost(string url, object body, string apiKey)
        {
            var json = JsonConvert.SerializeObject(body);
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.TryAddWithoutValidation("api-key", apiKey);
            return req;
        }
    }

    public class QdrantCollectionConfig
    {
        public string Name { get; set; } = "code_chunks";
        public int VectorSize { get; set; }
        public string Distance { get; set; } = "Cosine";
    }


    public class QdrantSearchRequest
    {
        [JsonProperty("vector")] public float[] Vector { get; set; } = Array.Empty<float>();
        [JsonProperty("limit")] public int Limit { get; set; } = 8;
        [JsonProperty("with_payload")] public bool WithPayload { get; set; } = true;
        [JsonProperty("filter")] public RagScope Filter { get; set; }
    }

    public class QdrantFilter
    {
        [JsonProperty("must")] public List<QdrantCondition> Must { get; set; } = new List<QdrantCondition>();
        [JsonProperty("should")] public List<QdrantCondition> Should { get; set; }
        [JsonProperty("must_not")] public List<QdrantCondition> MustNot { get; set; }
    }

    public class QdrantCondition
    {
        [JsonProperty("key")] public string Key { get; set; } = string.Empty;
        [JsonProperty("match")] public QdrantMatch Match { get; set; }
        [JsonProperty("range")] public QdrantRange Range { get; set; }
    }

    public class QdrantMatch { [JsonProperty("value")] public object Value { get; set; } }

    public class QdrantRange
    {
        [JsonProperty("gte")] public double Gte { get; set; }
        [JsonProperty("lte")] public double Lte { get; set; }
    }

    public class QdrantSearchResponse
    {
        [JsonProperty("result")] public List<QdrantScoredPoint> Result { get; set; }
    }

    public class QdrantScoredPoint
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("score")] public double Score { get; set; }
        [JsonProperty("payload")] public Dictionary<string, object> Payload { get; set; }
    }
}

