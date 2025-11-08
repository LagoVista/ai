// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: e70e9a2def53f8061145baf267da165d690ff37e5029e404d5065fc21c883d1b
// IndexVersion: 2
// --- END CODE INDEX META ---
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class Hub
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string Url { get; set; }
        public string AccessToken { get; set; }
        public bool IsSecure { get; set; }

        public string GetFullUri()
        {
            var fullUrl = (IsSecure ? "https" : "http") + $"://{Url}";
            return fullUrl;
        }
    }
}
