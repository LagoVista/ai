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
    public interface ILLMClient
    {
        /// <summary>
        /// Execute an LLM call for the given agent + conversation context and prompts.
        ///
        /// If sessionId is provided, the implementation may emit progress events
        /// over notifications keyed to that session, but the call still returns a
        /// single final LLMResult.
        /// </summary>
        Task<InvokeResult<AgentExecuteResponse>> GetAnswerAsync(
            AgentContext agentContext,
            ConversationContext conversationContext,
            AgentExecuteRequest executeRequest,
            string ragContextBlock,
            string sessionId,
            CancellationToken cancellationToken = default);
    }
}
