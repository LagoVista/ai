using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Fetch a public web page and return extracted text for LLM analysis.
    ///
    /// Security characteristics:
    /// - GET only
    /// - Blocks localhost and private IP ranges
    /// - Enforces download and output size limits
    /// </summary>
    public sealed class FetchWebPageTool : IAgentTool
    {
        private readonly IAdminLogger _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        public const string ToolName = "agent_fetch_webpage";
        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        public const string ToolUsageMetadata = "Use this tool when the user asks to look up information on a public website. Provide a URL and receive extracted text for analysis.";
        public const string ToolSummary = "make an http web request and return the contents";
        public FetchWebPageTool(IAdminLogger logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        private sealed class Args
        {
            public string Url { get; set; }
            public int? TimeoutMs { get; set; }
            public int? MaxBytes { get; set; }
            public int? MaxChars { get; set; }
            public bool? FollowRedirects { get; set; }
        }

        private sealed class Result
        {
            public string RequestedUrl { get; set; }
            public string FinalUrl { get; set; }
            public int StatusCode { get; set; }
            public string ContentType { get; set; }
            public bool Truncated { get; set; }
            public string Text { get; set; }
            public string SessionId { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);
        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return InvokeResult<string>.FromError("agent_fetch_webpage requires a non-empty arguments object.");
            try
            {
                var args = JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args();
                if (string.IsNullOrWhiteSpace(args.Url))
                    return InvokeResult<string>.FromError("agent_fetch_webpage requires 'url'.");
                if (!Uri.TryCreate(args.Url.Trim(), UriKind.Absolute, out var uri))
                    return InvokeResult<string>.FromError("agent_fetch_webpage requires a valid absolute URL.");
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                    return InvokeResult<string>.FromError("agent_fetch_webpage supports only http/https URLs.");
                await EnsureSafeDestinationAsync(uri, cancellationToken);
                var timeoutMs = args.TimeoutMs.HasValue ? Math.Clamp(args.TimeoutMs.Value, 500, 30000) : 15000;
                var maxBytes = args.MaxBytes.HasValue ? Math.Clamp(args.MaxBytes.Value, 4096, 2_000_000) : 500_000;
                var maxChars = args.MaxChars.HasValue ? Math.Clamp(args.MaxChars.Value, 512, 50_000) : 20_000;
                var followRedirects = args.FollowRedirects ?? true;
                using var http = CreateHttpClient(followRedirects, timeoutMs);
                using var req = new HttpRequestMessage(HttpMethod.Get, uri);
                req.Headers.UserAgent.ParseAdd("Aptix/1.0");
                req.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,text/plain;q=0.8,*/*;q=0.7");
                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var finalUrl = resp.RequestMessage?.RequestUri?.ToString() ?? uri.ToString();
                var contentType = resp.Content?.Headers?.ContentType?.ToString();
                var bytes = await ReadUpToAsync(resp, maxBytes, cancellationToken);
                var raw = DecodeToString(bytes, resp.Content?.Headers?.ContentType?.CharSet);
                var text = ExtractText(raw, contentType);
                var truncated = false;
                if (text.Length > maxChars)
                {
                    text = text.Substring(0, maxChars);
                    truncated = true;
                }

                var payload = new Result
                {
                    RequestedUrl = uri.ToString(),
                    FinalUrl = finalUrl,
                    StatusCode = (int)resp.StatusCode,
                    ContentType = contentType,
                    Truncated = truncated,
                    Text = text,
                    SessionId = context?.Request?.SessionId,
                };
                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (InvokeValidationException vex)
            {
                return InvokeResult<string>.FromError(vex.Message);
            }
            catch (Exception ex)
            {
                _logger.AddException("[FetchWebPageTool_ExecuteAsync__Exception]", ex);
                return InvokeResult<string>.FromError("agent_fetch_webpage failed to fetch the requested page.");
            }
        }

        private HttpClient CreateHttpClient(bool followRedirects, int timeoutMs)
        {
            var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
            return http;
        }

        private static async Task<byte[]> ReadUpToAsync(HttpResponseMessage resp, int maxBytes, CancellationToken ct)
        {
            if (resp.Content == null)
                return Array.Empty<byte>();
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var ms = new System.IO.MemoryStream();
            var buffer = new byte[8192];
            var total = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                if (read <= 0)
                    break;
                var remaining = maxBytes - total;
                if (remaining <= 0)
                    break;
                var toWrite = Math.Min(read, remaining);
                await ms.WriteAsync(buffer, 0, toWrite, ct);
                total += toWrite;
                if (total >= maxBytes)
                    break;
            }

            return ms.ToArray();
        }

        private static string DecodeToString(byte[] bytes, string charset)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;
            try
            {
                if (!string.IsNullOrWhiteSpace(charset))
                    return Encoding.GetEncoding(charset).GetString(bytes);
            }
            catch
            {
            }

            return Encoding.UTF8.GetString(bytes);
        }

        private static string ExtractText(string raw, string contentType)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;
            var isHtml = (contentType ?? string.Empty).Contains("text/html", StringComparison.OrdinalIgnoreCase) || raw.Contains("<html", StringComparison.OrdinalIgnoreCase);
            if (!isHtml)
                return NormalizeWhitespace(raw);
            var s = Regex.Replace(raw, "<script[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, "<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, "<[^>]+>", " ");
            s = WebUtility.HtmlDecode(s);
            return NormalizeWhitespace(s);
        }

        private static string NormalizeWhitespace(string input)
        {
            var s = Regex.Replace(input ?? string.Empty, "[\\r\\n\\t]+", " ");
            return Regex.Replace(s, "\\s{2,}", " ").Trim();
        }

        private static async Task EnsureSafeDestinationAsync(Uri uri, CancellationToken ct)
        {
            if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                throw new InvokeValidationException("agent_fetch_webpage blocks localhost.");
            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.Host);
            }
            catch
            {
                throw new InvokeValidationException("agent_fetch_webpage could not resolve host.");
            }

            if (addresses.Any(IsPrivateOrLocal))
                throw new InvokeValidationException("agent_fetch_webpage blocks private or local network destinations.");
        }

        private static bool IsPrivateOrLocal(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip))
                return true;
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                return b[0] == 10 || b[0] == 127 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 169 && b[1] == 254);
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.Equals(IPAddress.IPv6Loopback);
            }

            return true;
        }

        private sealed class InvokeValidationException : Exception
        {
            public InvokeValidationException(string message) : base(message)
            {
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName, "Fetch a public web page and return extracted text for analysis. Blocks private/local destinations and enforces size limits.", p =>
            {
                p.String("url", "Absolute http/https URL to fetch.", required: true);
                p.Integer("timeoutMs", "Request timeout in milliseconds (500–30000).");
                p.Integer("maxBytes", "Maximum bytes to download (4096–2000000).");
                p.Integer("maxChars", "Maximum characters returned after extraction (512–50000).");
                p.Boolean("followRedirects", "Follow redirects (default true).");
            });
        }
    }
}