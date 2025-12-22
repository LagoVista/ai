using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Executes a single logical turn for an Aptix agent session,
    /// given the already-loaded AgentContext.
    /// </summary>
    public interface IAgentTurnExecutor : IAgentPipelineStep
    {
    }
}
