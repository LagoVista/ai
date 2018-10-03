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
