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
