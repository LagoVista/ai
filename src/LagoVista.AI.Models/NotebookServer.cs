// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 282ef24c0c0773038d1f369f5bf5f7f6e9dec70aac80dcc04a99d6a15b93cf96
// IndexVersion: 2
// --- END CODE INDEX META ---
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class NotebookServer
    {
        [JsonProperty("id")]
        public String Id { get; set; }

        public String HubUserId { get; set; }
    }
}
