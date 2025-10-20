using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static System.Net.WebRequestMethods;

namespace LagoVista.AI.Services
{
	// Partial, drop-in companion to your existing QdrantClient using Newtonsoft.Json
	// for .NET Standard 2.1 compatibility.
	public partial class QdrantClient
	{
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
			IReadOnlyList<QdrantPoint> points,
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
					await UpsertJsonGzipAsync(collection, batch, ct).ConfigureAwait(false);
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
		private async Task UpsertJsonGzipAsync(string collection, List<QdrantPoint> batch, CancellationToken ct)
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

	/// <summary>
	/// Minimal GZip wrapper for HttpContent. Sets Content-Encoding: gzip.
	/// </summary>
	internal sealed class GZipContent : HttpContent
	{
		private readonly HttpContent _inner;
		public GZipContent(HttpContent inner)
		{
			_inner = inner ?? throw new ArgumentNullException(nameof(inner));
			Headers.ContentType = inner.Headers.ContentType;
			Headers.ContentEncoding.Add("gzip");
		}

		protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
		{
			using (var gzip = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true))
			{
				await _inner.CopyToAsync(gzip).ConfigureAwait(false);
			}
		}

		protected override bool TryComputeLength(out long length)
		{
			length = -1;
			return false;
		}
	}

	/// <summary>
	/// Exception that carries HTTP status and response body for Qdrant calls.
	/// </summary>
	public sealed class QdrantHttpException : Exception
	{
		public HttpStatusCode StatusCode { get; }
		public string ResponseBody { get; }

		public QdrantHttpException(string message, HttpStatusCode statusCode, string responseBody)
			: base($"{message} (HTTP {(int)statusCode}): {Truncate(responseBody, 512)}")
		{
			StatusCode = statusCode;
			ResponseBody = responseBody;
		}

		private static string Truncate(string s, int max)
		{
			if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? string.Empty;
			return s.Substring(0, max) + "...";
		}
	}
}
