using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace LagoVista.AI.Services
{
    public sealed class OpenAIErrorFormatter : IOpenAIErrorFormatter
    {
        private readonly IAdminLogger _logger;

        public OpenAIErrorFormatter(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> FormatAsync(HttpResponseMessage httpResponse)
        {
            var errorBody = await SafeReadBodyAsync(httpResponse);
            if (string.IsNullOrWhiteSpace(errorBody)) return null;

            try
            {
                var parsed = JsonConvert.DeserializeObject<OpenAIErrorResponse>(errorBody);
                return parsed != null ? "Reason: " + parsed : "Raw: " + errorBody;
            }
            catch (Exception ex)
            {
                _logger.AddException("[OpenAIErrorFormatter_FormatAsync__Deserialize]", ex);
                return "Raw: " + errorBody;
            }
        }

        private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response)
        {
            try { return response?.Content == null ? null : await response.Content.ReadAsStringAsync(); }
            catch { return null; }
        }
    }
}