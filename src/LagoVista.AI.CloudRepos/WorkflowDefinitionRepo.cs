using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.CloudRepos
{
    /// <summary>
    /// Cosmos/DocumentDB-backed repository for persisting WorkflowDefinition
    /// instances that back the Agent Workflow Registry (TUL-006).
    /// </summary>
    public class WorkflowDefinitionRepo : DocumentDBRepoBase<WorkflowDefinition>, IWorkflowDefinitionRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public WorkflowDefinitionRepo(IMLRepoSettings settings, IAdminLogger logger)
            : base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, logger)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public Task AddWorkflowDefinitionAsync(WorkflowDefinition definition)
        {
            return CreateDocumentAsync(definition);
        }

        public Task UpdateWorkflowDefinitionAsync(WorkflowDefinition definition)
        {
            return UpsertDocumentAsync(definition);
        }

        public Task DeleteWorkflowDefinitionAsync(string workflowId)
        {
            return DeleteDocumentAsync(workflowId);
        }

        public Task<WorkflowDefinition> GetWorkflowDefinitionAsync(string workflowId)
        {
            return GetDocumentAsync(workflowId);
        }

        public Task<ListResponse<WorkflowDefinition>> GetWorkflowDefinitionsAsync(string orgId, ListRequest listRequest)
        {
            // Initial implementation returns all workflow definitions paged.
            // Callers can later extend this to filter by org, status, or visibility
            // if needed.
            return QueryAsync(wf => wf.OwnerOrganization.Id == orgId, listRequest);
        }

        public async Task<bool> QueryWorkflowIdInUseAsync(string workflowId, string orgId)
        {
            var result = await QueryAsync(wf => wf.WorkflowId == workflowId&& wf.OwnerOrganization.Id == orgId, new ListRequest { PageSize = 1 });
            return result.Model.Any();
        }
    }
}
