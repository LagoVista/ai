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
using log4net.Util;
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

        private readonly object _initLock = new object();


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


        private readonly Dictionary<string, Task> _collectionInitTasks =
            new Dictionary<string, Task>(StringComparer.OrdinalIgnoreCase);

        public Task EnsureInitializedAsync(string collectionName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
                throw new ArgumentException("collectionName is required.", nameof(collectionName));

            lock (_initLock)
            {
                if (_collectionInitTasks.TryGetValue(collectionName, out var existing))
                    return existing;

                var task = EnsureCollectionAndIndexesAsync(collectionName, ct);
                _collectionInitTasks[collectionName] = task;
                return task;
            }
        }

        private async Task EnsureCollectionAndIndexesAsync(string name, CancellationToken ct)
        {
            await EnsureCollectionExistsAsync(name, ct).ConfigureAwait(false);
            await EnsurePayloadIndexesAsync(name, ct).ConfigureAwait(false);
        }

        private Task EnsureReadyAsync(string collection, CancellationToken ct)
            => EnsureInitializedAsync(collection, ct);

        private async Task EnsurePayloadIndexesAsync(string collectionName, CancellationToken ct)
        {

            var indexes = RagVectorPayload.Indexes;
            // Create field indexes one-by-one; tolerate "already exists" race.
            foreach (var spec in indexes)
            {
                await EnsurePayloadIndexAsync(collectionName, spec, ct).ConfigureAwait(false);
            }
        }

        private static object BuildQdrantFieldSchema(QdrantPayloadIndexKind kind)
        {
            // Qdrant REST expects "field_schema": { "type": "keyword" | "integer" | ... }
            // Keep this mapping tight and explicit.
            switch (kind)
            {
                case QdrantPayloadIndexKind.Keyword:
                    return new { type = "keyword" };

                case QdrantPayloadIndexKind.Integer:
                    return new { type = "integer" };

                case QdrantPayloadIndexKind.Boolean:
                    return new { type = "bool" };

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported payload index kind.");
            }
        }

        private async Task EnsurePayloadIndexAsync(string collectionName, QdrantPayloadIndexSpec spec, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
                throw new ArgumentException("collectionName is required.", nameof(collectionName));
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));
            if (string.IsNullOrWhiteSpace(spec.Path))
                throw new ArgumentException("spec.Path is required.", nameof(spec));

            // REST: PUT /collections/{collection_name}/index
            // Body: { field_name, field_schema }
            var url = $"/collections/{collectionName}/index";

            var requestBody = new
            {
                field_name = spec.Path,
                field_schema = BuildQdrantFieldSchema(spec.Kind)
            };

            using (var resp = await _http.PutAsJsonAsync(url, requestBody, ct).ConfigureAwait(false))
            {
                if (resp.IsSuccessStatusCode) return;

                var body = await SafeReadAsync(resp).ConfigureAwait(false);

                // Idempotency: if another process already created it, accept.
                if (resp.StatusCode == HttpStatusCode.Conflict ||
                    body.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }

                throw new QdrantHttpException(
                    $"Qdrant create payload index failed: '{spec.Path}' in '{collectionName}'",
                    resp.StatusCode,
                    body);
            }
        }

        private async Task EnsureCollectionExistsAsync(string name, CancellationToken ct)
        {
            // 1) Fast path: GET collection
            using (var exists = await _http.GetAsync($"/collections/{name}", ct).ConfigureAwait(false))
            {
                if (exists.IsSuccessStatusCode) return;

                if (exists.StatusCode != HttpStatusCode.NotFound)
                {
                    var body = await SafeReadAsync(exists).ConfigureAwait(false);
                    throw new QdrantHttpException($"Qdrant GET collection failed for '{name}'", exists.StatusCode, body);
                }
            }

            // 2) Create on 404; tolerate race if another process created it first
            var req = new
            {
                vectors = new { size = VECTOR_SIZE, distance = VECTOR_DISTANCE }
            };

            using (var resp = await _http.PutAsJsonAsync($"/collections/{name}", req, ct).ConfigureAwait(false))
            {
                if (resp.IsSuccessStatusCode) return;

                // Qdrant may respond with conflict/bad request depending on version/config;
                // treat "already exists" as success.
                var body = await SafeReadAsync(resp).ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.Conflict ||
                    body.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }

                throw new QdrantHttpException($"Qdrant create collection failed for '{name}'", resp.StatusCode, body);
            }
        }

        public async Task UpsertAsync(string collection, IEnumerable<IRagPoint> points, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            await EnsureReadyAsync(collection, CancellationToken.None);
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
            var sw = Stopwatch.StartNew();
            await EnsureReadyAsync(collection, CancellationToken.None);
            _adminLogger.Trace($"{this.Tag()} Search started with collection {collection}");

            var queryJson = JsonConvert.SerializeObject(req);
            _adminLogger.Trace($"[JSON.QDrantQuery]={queryJson}");

            var resp = await _http.PostAsJsonAsync($"/collections/{collection}/points/search", req);

            if (resp.IsSuccessStatusCode)
            {
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsAsync<QdrantSearchResponse>();
                _adminLogger.Trace($"[JSON.QDrantResult]={JsonConvert.SerializeObject(json)}");
                _adminLogger.Trace($"{this.Tag()} Search completed in {sw.Elapsed.TotalMilliseconds}ms, found {json.Result.Count} results.");

                return json!.Result ?? new List<QdrantScoredPoint>();
            }
            else
            {
                var response = await resp.Content.ReadAsStringAsync();
                _adminLogger.AddError(this.Tag(), $"ERROR Requesting Point: {response}");
                throw new QdrantHttpException("Qdrant search failed", resp.StatusCode, response);
            }
        }

        public async Task DeleteByIdsAsync(string collection, IEnumerable<string> ids)
        {
            await EnsureReadyAsync(collection, CancellationToken.None);

            if (string.IsNullOrWhiteSpace(collection))
                throw new ArgumentException("collection is required.", nameof(collection));
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            var idArray = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (idArray.Length == 0) return;

            var url = $"/collections/{collection}/points/delete";

            // Qdrant accepts: { "points": [ "id1", "id2", ... ] }
            var body = new { points = idArray };

            using (var resp = await _http.PostAsJsonAsync(url, body).ConfigureAwait(false))
            {
                if (resp.IsSuccessStatusCode) return;

                var payload = await SafeReadAsync(resp).ConfigureAwait(false);
                throw new QdrantHttpException("Qdrant delete-by-ids failed", resp.StatusCode, payload);
            }
        }


        public async Task DeleteByDocIdAsync(string collection, string docId, CancellationToken ct = default)
        {
            await EnsureReadyAsync(collection, CancellationToken.None);

            if (string.IsNullOrWhiteSpace(docId))
                throw new ArgumentException("DocId cannot be null or empty.", nameof(docId));

            var filter = new QdrantFilter
            {
                Must =
                {
                    new QdrantCondition
                    {
                        Key = "Meta.DocId",
                        Match = new QdrantMatch
                        {
                            Value = docId.Trim()
                        }
                    }
                }
            };

            await DeleteByFilterAsync(collection, filter, ct);
        }


        public async Task DeleteByDocIdsAsync(string collection, IEnumerable<string> docIds, CancellationToken ct = default)
        {
            await EnsureReadyAsync(collection, CancellationToken.None);

            if (docIds == null)
                throw new ArgumentNullException(nameof(docIds));

            var filter = new QdrantFilter
            {
                Must =
                {
                    new QdrantCondition
                    {
                        Key = "Meta.DocId",
                        Match = new QdrantMatch
                        {
                            Any = docIds.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray()
                        }
                    }
                }
            };

            await DeleteByFilterAsync(collection, filter, ct);
        }


        /// <summary>
		/// Delete points by a Qdrant payload filter (e.g., delete all where path == file).
		/// </summary>
		public async Task DeleteByFilterAsync(string collection, QdrantFilter filter, CancellationToken ct = default)
        {
            await EnsureReadyAsync(collection, CancellationToken.None);

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
        public async Task UpsertInBatchesAsync(string collection, IReadOnlyList<IRagPoint> points, int vectorDims, int? maxPerBatch = null, CancellationToken ct = default)
        {

            await EnsureReadyAsync(collection, CancellationToken.None);

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

        private RagScope _filter;
        [JsonIgnore] public RagScope Filter
        {
            get => _filter;
            set {
                _filter = value;
                RagFilter = value == null ? null : value.ToQdrantFilter();
            }
        }
        [JsonProperty("filter", NullValueHandling = NullValueHandling.Ignore)]
        public QdrantFilter RagFilter { get; set; }
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

    public class QdrantMatch {
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public object Value { get; set; }

        [JsonProperty("any", NullValueHandling = NullValueHandling.Ignore)]
        public IEnumerable<string> Any { get; set; }

        [JsonProperty("except", NullValueHandling = NullValueHandling.Ignore)]
        public IEnumerable<string> Except { get; set; }

        // Optional, only if you decide to support substring/full-text later:
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }
    }

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
        [JsonProperty("payload")] public RagVectorPayload Payload { get; set; }
    }

    static class QdrantRagScopeTranslator
    {
        public static QdrantFilter ToQdrantFilter(this RagScope scope)
        {
            if (scope == null || scope.Conditions == null || scope.Conditions.Count == 0)
                return null;

            scope.Validate();

            var filter = new QdrantFilter();

            foreach (var c in scope.Conditions)
            {
                var values = (c.Values ?? new List<string>())
                    .Where(v => !String.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (values.Count == 0)
                    continue;

                // Build a single Qdrant condition per RagScopeCondition.
                var cond = new QdrantCondition
                {
                    Key = c.Key,
                    Match = new QdrantMatch()
                };

                switch (c.Operator)
                {
                    case RagScopeOperator.IsEquals:
                        // IN
                        cond.Match.Any = values;
                        filter.Must.Add(cond);
                        break;

                    case RagScopeOperator.NotEquals:
                        // NOT IN (preferred native Qdrant operator)
                        cond.Match.Except = values;
                        filter.Must.Add(cond);
                        break;

                    case RagScopeOperator.Contains:
                        // For keyword fields and arrays, "contains" == "any-of membership"
                        // (If you mean full-text contains, see note below.)
                        cond.Match.Any = values;
                        filter.Must.Add(cond);
                        break;

                    case RagScopeOperator.DoesNotContain:
                        // Exclude if field contains any of the values
                        cond.Match.Except = values;
                        filter.Must.Add(cond);
                        break;

                    default:
                        throw new InvalidOperationException($"Unsupported operator: '{c.Operator}'.");
                }
            }

            // If nothing made it in, return null so request omits "filter"
            if ((filter.Must == null || filter.Must.Count == 0) &&
                (filter.MustNot == null || filter.MustNot.Count == 0) &&
                (filter.Should == null || filter.Should.Count == 0))
            {
                return null;
            }

            return filter;
        }
    }

}

