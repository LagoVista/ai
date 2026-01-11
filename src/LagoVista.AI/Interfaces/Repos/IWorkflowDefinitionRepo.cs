using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;

namespace LagoVista.AI.Interfaces.Repos
{
    /// <summary>
    /// Repository abstraction for storing and retrieving workflow definitions
    /// used by the Agent Workflow Registry (TUL-006).
    /// </summary>
    public interface IWorkflowDefinitionRepo
    {
        Task AddWorkflowDefinitionAsync(WorkflowDefinition definition);

        Task UpdateWorkflowDefinitionAsync(WorkflowDefinition definition);

        Task DeleteWorkflowDefinitionAsync(string workflowId);

        Task<WorkflowDefinition> GetWorkflowDefinitionAsync(string workflowId);

        /// <summary>
        /// Returns a paged list of workflow definitions.
        /// Implementation may filter by status/visibility as appropriate.
        /// </summary>
        Task<ListResponse<WorkflowDefinition>> GetWorkflowDefinitionsAsync(string orgId, ListRequest listRequest);

        /// <summary>
        /// Returns true if the given WorkflowId is already in use.
        /// </summary>
        Task<bool> QueryWorkflowIdInUseAsync(string workflowId, string orgId);
    }
}
