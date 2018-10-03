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
