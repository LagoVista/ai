// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 264df9c6d09b4b15e8814ba311fe52817a4e0982ca685331c8d81f4a1d9513de
// IndexVersion: 2
// --- END CODE INDEX META ---
using Newtonsoft.Json;
using System;

namespace LagoVista.AI.Models
{
    public class Notebook
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("last_modified")]
        public string LastUpdatedDate { get; set; }

        [JsonProperty("created")]
        public string CreationDate { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
