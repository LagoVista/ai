using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LagoVista.AI.Models.Context
{
    /// <summary>
    /// AGN-030: Context classification for PromptKnowledgeProvider registers.
    ///
    /// Session: persistent session context (maps to per-request 'instructions' lane).
    /// Consumable: data intended to be consumed by the LLM and cleared after successful delivery.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ContextClassification
    {
        Session = 0,
        Consumable = 1
    }
}
