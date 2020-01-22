using LagoVista.AI.Models.TrainingData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{
    [ConfirmedUser]
    [AppBuilder]
    public class TrainingDataController : LagoVistaBaseController
    {
        readonly I _mgr;

        public TrainingDataController(IModelManager modelManager, UserManager<AppUser> userManager, IAdminLogger logger)
            : base(userManager, logger)
        {
            _mgr = modelManager;
        }
    
        public Task<InvokeResult> AddTrainingDataSetAsync([FromBody] TrainingDataSet set)
        {
            throw new NotImplementedException();
        }

        public Task<InvokeResult> UpdateTrainingDataSetAsync([FromBody] TrainingDataSet set)
        {
            throw new NotImplementedException();
        }
    }
}
