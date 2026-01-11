using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class AgentSessionRepo : DocumentDBRepoBase<AgentSession>, IAgentSessionRepo
    {
        private readonly bool _shouldConsolidateCollections;

        public AgentSessionRepo(IMLRepoSettings settings, IAdminLogger adminLogger, ICacheProvider cacheProvider) :
            base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, adminLogger, cacheProvider)
        {
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections; 

        public Task AddSessionAsync(AgentSession session)
        {
            return CreateDocumentAsync(session);
        }

        public Task<AgentSession> GetAgentSessionAsync(string agentSessionId)
        {
            return GetDocumentAsync(agentSessionId);
        }

        public Task<ListResponse<AgentSessionSummary>> GetSessionSummariesForOrgAsync(string orgId, ListRequest listRequest)
        {
            return QuerySummaryDescendingAsync<AgentSessionSummary, AgentSession>(session => session.OwnerOrganization.Id == orgId, sess => sess.CreationDate, listRequest);
        }

        public Task<ListResponse<AgentSessionSummary>> GetSessionSummariesForUserAsync(string orgId, string userId, ListRequest listRequest)
        {
            return QuerySummaryDescendingAsync<AgentSessionSummary, AgentSession>(session => session.OwnerOrganization.Id == orgId && session.CreatedBy.Id == userId, sess => sess.CreationDate, listRequest);
        }

        public Task UpdateSessionAsyunc(AgentSession session)
        {
            return UpsertDocumentAsync(session);
        }
    }
}
