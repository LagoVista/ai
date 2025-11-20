using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Types;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Lightweight HTTP client for reporting facet values to the metadata registry.
    ///
    /// Implements the client side of IDX-033: the ingestor accumulates facet data
    /// during a run and calls ReportAsync once at the end.
    ///
    /// This class intentionally has no dependency on specific logging or DI
    /// frameworks; callers can inject a simple Action<string> for diagnostics.
    /// </summary>
    public class MetadataRegistryClient
    {
        private readonly HttpClient _httpClient;
        private readonly MetadataRegistryConfig _config;
        private readonly Action<string> _log;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public MetadataRegistryClient(HttpClient httpClient, MetadataRegistryConfig config, Action<string> log = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log;
        }

        /// <summary>
        /// Returns true when the registry is configured with a non-empty BaseUrl.
        /// Callers can skip facet reporting entirely when this is false.
        /// </summary>
        public bool IsEnabled
        {
            get { return !string.IsNullOrWhiteSpace(_config.BaseUrl); }
        }

        /// <summary>
        /// Report facet values discovered during an ingestion run to the registry.
        ///
        /// This method is best-effort: failures are logged but do not throw by default
        /// unless <paramref name="cancellationToken"/> is cancelled.
        /// </summary>
        public async Task ReportAsync(MetadataRegistryReport report, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled) return;
            if (report == null) throw new ArgumentNullException(nameof(report));

            try
            {
                var baseUrl = _config.BaseUrl.TrimEnd('/');
                var path = string.IsNullOrWhiteSpace(_config.ReportPath) ? "/api/metadata/facets" : _config.ReportPath;
                if (!path.StartsWith("/", StringComparison.Ordinal)) path = "/" + path;

                var uri = new Uri(baseUrl + path);

                var json = JsonSerializer.Serialize(report, JsonOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                if (!string.IsNullOrWhiteSpace(_config.ApiKey))
                {
                    // Simple convention: use ApiKey as a bearer token; callers can adapt via a proxy if needed.
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
                }

                var response = await _httpClient.PostAsync(uri, content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _log?.Invoke($"[MetadataRegistryClient] Non-success status {(int)response.StatusCode} when reporting facets for project '{report.ProjectId}' repo '{report.Repo}'.");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[MetadataRegistryClient] Error reporting facets: {ex.Message}");
            }
        }
    }
}
