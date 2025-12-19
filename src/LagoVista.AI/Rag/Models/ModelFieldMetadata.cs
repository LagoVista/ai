using Newtonsoft.Json;

namespace LagoVista.AI.Rag.Models
{
    /// <summary>
    /// Captures contextual information derived from FormField attributes
    /// within an entity model. This is used to supply the LLM with richer
    /// editing context during refinement.
    /// </summary>
    public class ModelFieldMetadata
    {
        [JsonProperty("propertyName")]
        public string PropertyName { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("help")]
        public string Help { get; set; }

        [JsonProperty("isRequired")]
        public bool IsRequired { get; set; }

        [JsonProperty("fieldType")]
        public string FieldType { get; set; }
    }
}
