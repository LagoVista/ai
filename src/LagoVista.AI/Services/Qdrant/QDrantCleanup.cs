// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: b8c119bae4bed0bfb3b2f1eaa33cea1574b1b356b77313ecc4ddd8badb6a869a
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Qdrant
{
    public static class QdrantCleanup
    {
        /// <summary>
        /// Delete all points for a single asset by its stable doc_id (works for code & docs).
        /// Optional filters: org, project, content_type, index_version.
        /// </summary>
        public static async Task<HttpResponseMessage> DeleteByDocIdAsync(
            HttpClient http,
            string baseUrl,
            string collection,
            string apiKey,
            string docId,
            string orgId = null,
            string projectId = null,
            RagContentType? contentType = null,
            int? indexVersion = null,
            CancellationToken ct = default)
        {
            if (http == null) throw new ArgumentNullException(nameof(http));
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("baseUrl required", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(collection)) throw new ArgumentException("collection required", nameof(collection));
            if (string.IsNullOrWhiteSpace(docId)) throw new ArgumentException("docId required", nameof(docId));

            var must = new List<object>
            {
                KvMatch("doc_id", docId)
            };
            if (!string.IsNullOrWhiteSpace(orgId)) must.Add(KvMatch("org_id", orgId));
            if (!string.IsNullOrWhiteSpace(projectId)) must.Add(KvMatch("project_id", projectId));
            if (contentType.HasValue) must.Add(KvMatch("content_type", contentType.Value.ToString()));
            if (indexVersion.HasValue) must.Add(KvMatch("index_version", indexVersion.Value));

            var body = new
            {
                filter = new { must = must.ToArray() },
                wait = true
            };

            var url = baseUrl.TrimEnd('/') + "/collections/" + collection + "/points/delete";
            var req = BuildJsonPost(url, body, apiKey);
            return await http.SendAsync(req, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete all points for a code file by repo + path (commit-agnostic).
        /// Optional filters: org, project, index_version.
        /// </summary>
        public static async Task<HttpResponseMessage> DeleteByRepoPathAsync(
            HttpClient http,
            string baseUrl,
            string collection,
            string apiKey,
            string repo,
            string path,
            string orgId = null,
            string projectId = null,
            int? indexVersion = null,
            CancellationToken ct = default)
        {
            if (http == null) throw new ArgumentNullException(nameof(http));
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("baseUrl required", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(collection)) throw new ArgumentException("collection required", nameof(collection));
            if (string.IsNullOrWhiteSpace(repo)) throw new ArgumentException("repo required", nameof(repo));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path required", nameof(path));

            var must = new List<object>
            {
                KvMatch("repo", repo),
                KvMatch("path", path),
                KvMatch("content_type", RagContentType.SourceCode.ToString())
            };
            if (!string.IsNullOrWhiteSpace(orgId)) must.Add(KvMatch("org_id", orgId));
            if (!string.IsNullOrWhiteSpace(projectId)) must.Add(KvMatch("project_id", projectId));
            if (indexVersion.HasValue) must.Add(KvMatch("index_version", indexVersion.Value));

            var body = new
            {
                filter = new { must = must.ToArray() },
                wait = true
            };

            var url = baseUrl.TrimEnd('/') + "/collections/" + collection + "/points/delete";
            var req = BuildJsonPost(url, body, apiKey);
            return await http.SendAsync(req, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete by explicit point IDs (fastest when you have the exact stale set).
        /// </summary>
        public static async Task<HttpResponseMessage> DeleteByIdsAsync(
            HttpClient http,
            string baseUrl,
            string collection,
            string apiKey,
            IEnumerable<string> pointIds,
            CancellationToken ct = default)
        {
            if (http == null) throw new ArgumentNullException(nameof(http));
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("baseUrl required", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(collection)) throw new ArgumentException("collection required", nameof(collection));
            if (pointIds == null) throw new ArgumentNullException(nameof(pointIds));

            var ids = pointIds.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
            if (ids.Length == 0) throw new ArgumentException("No valid pointIds provided.", nameof(pointIds));

            var body = new
            {
                points = ids,
                wait = true
            };

            var url = baseUrl.TrimEnd('/') + "/collections/" + collection + "/points/delete";
            var req = BuildJsonPost(url, body, apiKey);
            return await http.SendAsync(req, ct).ConfigureAwait(false);
        }

        // ---------- helpers ----------

        private static object KvMatch(string key, object value)
            => new { key, match = new { value } };

        private static HttpRequestMessage BuildJsonPost(string url, object body, string apiKey)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.TryAddWithoutValidation("api-key", apiKey);
            return req;
        }
    }
}
