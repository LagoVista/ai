using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.PlatformSupport;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Managers
{
    /// <summary>
    /// Manager implementation for CRUD operations over WorkflowDefinition
    /// entities that feed the Agent Workflow Registry (TUL-006).
    ///
    /// This follows the same high-level pattern as other managers (for example,
    /// AiConversationManager), but the underlying WorkflowDefinition model is
    /// currently a simple POCO rather than a full EntityBase. Authorization
    /// and validation hooks are intentionally lightweight to keep the initial
    /// implementation focused on storage and retrieval.
    /// </summary>
    public class WorkflowDefinitionManager : ManagerBase, IWorkflowDefinitionManager
    {
        private readonly IWorkflowDefinitionRepo _repo;

        public WorkflowDefinitionManager(IWorkflowDefinitionRepo repo, ILogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
            : base(logger, appConfig, dependencyManager, security)
        {
            _repo = repo;
        }

        public async Task<InvokeResult> AddWorkflowDefinitionAsync(WorkflowDefinition definition, EntityHeader org, EntityHeader user)
        {
            // Placeholder for future validation/authorization if WorkflowDefinition
            // is later promoted to a full EntityBase-style model.
            if (definition == null)
            {
                return InvokeResult.FromError("Workflow definition cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(definition.WorkflowId))
            {
                return InvokeResult.FromError("WorkflowId is required.");
            }

            var inUse = await _repo.QueryWorkflowIdInUseAsync(definition.WorkflowId, org.Id);
            if (inUse)
            {
                return InvokeResult.FromError($"WorkflowId '{definition.WorkflowId}' is already in use.");
            }

            await _repo.AddWorkflowDefinitionAsync(definition);
            return InvokeResult.Success;
        }

        public async Task<InvokeResult> UpdateWorkflowDefinitionAsync(WorkflowDefinition definition, EntityHeader org, EntityHeader user)
        {
            if (definition == null)
            {
                return InvokeResult.FromError("Workflow definition cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(definition.WorkflowId))
            {
                return InvokeResult.FromError("WorkflowId is required.");
            }

            await _repo.UpdateWorkflowDefinitionAsync(definition);
            return InvokeResult.Success;
        }

        public async Task<InvokeResult> DeleteWorkflowDefinitionAsync(string workflowId, EntityHeader org, EntityHeader user)
        {
            if (string.IsNullOrWhiteSpace(workflowId))
            {
                return InvokeResult.FromError("workflowId is required.");
            }

            var depResult = await CheckInUseAsync(workflowId, org, user);
            if (depResult.IsInUse)
            {
                return InvokeResult.FromError("Workflow is in use and cannot be deleted.");
            }

            await _repo.DeleteWorkflowDefinitionAsync(workflowId);
            return InvokeResult.Success;
        }

        public Task<WorkflowDefinition> GetWorkflowDefinitionAsync(string workflowId, EntityHeader org, EntityHeader user)
        {
            return _repo.GetWorkflowDefinitionAsync(workflowId);
        }

        public Task<ListResponse<WorkflowDefinition>> GetWorkflowDefinitionsAsync(ListRequest listRequest, EntityHeader org, EntityHeader user)
        {
            return _repo.GetWorkflowDefinitionsAsync(org.Id, listRequest);
        }

        public Task<bool> QueryWorkflowIdInUseAsync(string workflowId, EntityHeader org)
        {
            return _repo.QueryWorkflowIdInUseAsync(workflowId, org.Id);
        }

        public Task<DependentObjectCheckResult> CheckInUseAsync(string workflowId, EntityHeader org, EntityHeader user)
        {
            // For now, workflow definitions are assumed to have no dependent objects
            // wired into the rest of the system. This can be expanded later as
            // workflows begin referencing other persisted assets.
            return Task.FromResult(new DependentObjectCheckResult());
        }
    }
}
