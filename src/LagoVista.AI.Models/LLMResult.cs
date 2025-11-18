namespace LagoVista.Core.AI.Models
{
    public class LLMResult
    {
        /// <summary>
        /// Final text answer returned by the provider.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Provider-specific response id (e.g. OpenAI Responses response.id).
        /// </summary>
        public string ResponseId { get; set; }

        /// <summary>
        /// Optional raw JSON response for diagnostics or logging.
        /// </summary>
        public string RawResponseJson { get; set; }
    }
}
