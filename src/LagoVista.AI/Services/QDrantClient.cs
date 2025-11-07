using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Utils.Types;
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

namespace LagoVista.AI.Services
{
    public partial class QdrantClient : IQdrantClient
    {
        private  HttpClient _http;
        private IAdminLogger _adminLogger;


        public QdrantClient(IQdrantSettings settings, IAdminLogger adminLogger)
        {
            _http = new HttpClient { BaseAddress = new Uri(settings.QdrantEndpoint) };
            _http.DefaultRequestHeaders.Add("api-key", settings.QdrantApiKey);
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public QdrantClient(AgentContext vectorDb, IAdminLogger adminLogger)
        {
            _http = new HttpClient { BaseAddress = new Uri(vectorDb.VectorDatabaseUri) };
            _http.DefaultRequestHeaders.Add("api-key", vectorDb.VectorDatabaseApiKey);
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        public async Task EnsureCollectionAsync(QdrantCollectionConfig cfg, string name)
        {
            var exists = await _http.GetAsync($"/collections/{name}");
            if (exists.IsSuccessStatusCode) return;

            var req = new
            {
                vectors = new { size = cfg.VectorSize, distance = cfg.Distance }
            };
            var resp = await _http.PutAsJsonAsync($"/collections/{name}", req);
            resp.EnsureSuccessStatusCode();
        }

        public async Task UpsertAsync(string collection, IEnumerable<PayloadBuildResult> points, CancellationToken ct)
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
            IReadOnlyList<PayloadBuildResult> points,
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
        private async Task UpsertJsonGzipAsync(string collection, List<PayloadBuildResult> batch, CancellationToken ct)
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
        [JsonProperty("filter")] public QdrantFilter Filter { get; set; }
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

