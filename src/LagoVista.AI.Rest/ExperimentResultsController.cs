using LagoVista.AI.Models;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{
    [ConfirmedUser]
    [AppBuilder]
    public class ExperimentResultsController : LagoVistaBaseController
    {
        IExperimentResultManager _mgr;

        public ExperimentResultsController(IExperimentResultManager mgr, UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            this._mgr = mgr;
        }

        [HttpPost("/api/ml/model/experiment/result")]
        public Task AddResultAsync([FromBody] ExperimentResult result)
        {
           return _mgr.AddExperimentResultAsync(result, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ml/model/{id}/{revision}/experiment/results")]
        public Task GetResultsAsync(string id, int revision)
        {
            return _mgr.GetExperimentResultsAsync(id, revision, OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }
    }
}
