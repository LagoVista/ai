using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Creates Agent Knowledge Packs (AKPs) for a given Org + AgentContext + AgentContextRoles + ModeKey.
    ///
    /// This service is responsible for assembling and resolving DDR consumption fields into AKP items.
    /// </summary>
    public interface IAgentKnowledgePackService
    {
        Task<InvokeResult<AgentKnowledgePack>> CreateAsync(IAgentPipelineContext context, bool changedMode);
    }
}
