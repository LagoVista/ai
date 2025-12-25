using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{

    public sealed class OpenAIErrorResponse
    {
        [JsonProperty("error")]
        public OpenAIError Error { get; set; }

        public override string ToString()
        {
            if (Error == null)
            {
                return base.ToString();
            }

            var paramInfo = string.IsNullOrEmpty(Error.Param) ? "" : " (param: " + Error.Param + ")";
            var codeInfo = string.IsNullOrEmpty(Error.Code) ? "" : " (code: " + Error.Code + ")";
            return "OpenAI error: " + Error.Message + " [" + Error.Type + "]" + paramInfo + codeInfo;
        }
    }
}
