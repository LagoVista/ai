using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Manager abstraction for CRUD and list operations on WorkflowDefinition
    /// entities that back the Agent Workflow Registry (TUL-006).
    /// </summary>
    public interface IWorkflowDefinitionManager
    {
        Task<InvokeResult> AddWorkflowDefinitionAsync(WorkflowDefinition definition, EntityHeader org, EntityHeader user);

        Task<InvokeResult> UpdateWorkflowDefinitionAsync(WorkflowDefinition definition, EntityHeader org, EntityHeader user);

        Task<InvokeResult> DeleteWorkflowDefinitionAsync(string workflowId, EntityHeader org, EntityHeader user);

        Task<WorkflowDefinition> GetWorkflowDefinitionAsync(string workflowId, EntityHeader org, EntityHeader user);

        Task<ListResponse<WorkflowDefinition>> GetWorkflowDefinitionsAsync(ListRequest listRequest, EntityHeader org, EntityHeader user);

        /// <summary>
        /// Returns true if the given WorkflowId is already in use.
        /// </summary>
        Task<bool> QueryWorkflowIdInUseAsync(string workflowId, EntityHeader org);

        /// <summary>
        /// Hook point to check whether a workflow is in use before deletion.
        /// For now this is expected to always be "no dependencies" but the
        /// signature follows other manager patterns.
        /// </summary>
        Task<DependentObjectCheckResult> CheckInUseAsync(string workflowId, EntityHeader org, EntityHeader user);
    }
}
