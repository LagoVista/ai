// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: a961dd8edf3efff9bcde7978516f4fa0598417b87299b618f830c0dd34de3d78
// IndexVersion: 2
// --- END CODE INDEX META ---
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    //{"user": "ai", "id": "a9", "kind": "api_token", "created": "2018-08-29T17:29:17.817525Z", "last_activity": null, "note": "Requested via api", "token": "1f03af94ba8c4a32a081043613e167ec"}
    public class TokenResponse
    {
        [JsonProperty("user")]
        public String User { get; set; }

        [JsonProperty("id")]
        public String Id { get; set; }

        [JsonProperty("kind")]
        public String Kind { get; set; }

        [JsonProperty("created")]
        public String Created { get; set; }

        [JsonProperty("last_activity")]
        public String LastActivity { get; set; }

        [JsonProperty("note")]
        public String Note { get; set; }

        [JsonProperty("token")]
        public String Token { get; set; }
    }
}
