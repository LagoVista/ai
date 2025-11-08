// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: c51d839538661aa3fae7f0bedf51b4645c36afeb3975a535b923b9fdde81eba7
// IndexVersion: 2
// --- END CODE INDEX META ---
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IOpenAISettings
    {
        public string OpenAIUrl { get; }
        public string OpenAIApiKey { get; }
    }
}
