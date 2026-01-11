using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{
    [Authorize]
    [ConfirmedUser]
    public class DdrsController : LagoVistaBaseController
    {
        private readonly IDdrManager _ddrManager;

        public DdrsController(IDdrManager ddrManager,  UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            _ddrManager = ddrManager ?? throw new ArgumentNullException(nameof(ddrManager));
        }

        [HttpGet("/api/ddrs")]
        public Task<ListResponse<DetailedDesignReviewSummary>> GetDDRsForOrgAsync()
        {
            return _ddrManager.GetDdrsAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        [HttpGet("/api/ddr/{id}")]
        public async Task<DetailResponse<DetailedDesignReview>> GetDdr(string id)
        {
            return DetailResponse<DetailedDesignReview>.Create(await  _ddrManager.GetDdrByIdAsync(id, OrgEntityHeader, UserEntityHeader));
        }

        [HttpPut("/api/ddr")]
        public async Task UpdateDdrAsync([FromBody] DetailedDesignReview ddr)
        {
            await _ddrManager.UpdateDdrAsync(ddr, OrgEntityHeader, UserEntityHeader);
        }


        [HttpDelete("/api/ddr/{id}")]
        public async Task<InvokeResult> DeleteDdr(string id)
        {
            await _ddrManager.DeleteDdrAsync(id, OrgEntityHeader, UserEntityHeader);
            return InvokeResult.Success;
        }
    }
}
