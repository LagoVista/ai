using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Provides OpenAI endpoint and authentication settings used by LagoVista AI services.
    /// </summary>
    public interface IOpenAISettings
    {
        public string OpenAIUrl { get; }
        /// <summary>
        /// Base URL for the OpenAI API endpoint.
        /// </summary>
        public string OpenAIApiKey { get; }
        /// <summary>
        /// API key used to authenticate requests to OpenAI.
        /// </summary>
    }
}
