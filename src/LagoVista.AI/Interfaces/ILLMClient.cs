using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Abstraction over the LLM provider used by Aptix (e.g. OpenAI Responses API).
    ///
    /// The implementation may emit progress / narration events over notifications
    /// using the provided sessionId, but callers only see the final LLMResult.
    /// </summary>
    public interface ILLMClient : IAgentPipelineStep
    {
        bool UseStreaming { get; set; }
    }
}
