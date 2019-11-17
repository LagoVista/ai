using LagoVista.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.PlatformSupport;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class ExperimentResultManager : ManagerBase, IExperimentResultManager
    {
        private readonly IExperimentResultRepo _repo;

        public ExperimentResultManager(IExperimentResultRepo repo, ILogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security) 
            : base(logger, appConfig, dependencyManager, security)
        {
            this._repo = repo;
        }

        public Task AddExperimentResultAsync(ExperimentResult result, EntityHeader org, EntityHeader user)
        {
            AuthorizeOrgAccessAsync(user, org, typeof(ExperimentResult), Core.Validation.Actions.Create);
            return this._repo.AddResultAsync(result);
        }

        public Task<ListResponse<ExperimentResult>> GetExperimentResultsAsync(string modelId, int revisionId, EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            AuthorizeOrgAccessAsync(user, org, typeof(ExperimentResult), Core.Validation.Actions.Read);
            return this._repo.GetResultsAsync(modelId, revisionId, listRequest);
        }
    }
}
