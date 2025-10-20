using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{
    /// <summary>
    /// REST Class for Experiments
    /// </summary>
    [ConfirmedUser]
    [AppBuilder]
    public class VectorDatabaseController : LagoVistaBaseController
    {
        private readonly IVectorDatabaseManager _vectorDbManager;

        public VectorDatabaseController(IVectorDatabaseManager vectorDbMgr, UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            this._vectorDbManager = vectorDbMgr;
        }


        [HttpGet("/api/ml/vectordb/{id}")]
        public async Task<DetailResponse<VectorDatabase>> GetVectorDatabase(string id)
        {
            var db = await _vectorDbManager.GetVectorDatabaseAsync(id, OrgEntityHeader, UserEntityHeader);
            return DetailResponse<VectorDatabase>.Create(db);
        }

        [HttpGet("/api/ml/vectordb/factory")]
        public async Task<DetailResponse<VectorDatabase>> CreateVectorDb()
        {
            var result = DetailResponse<VectorDatabase>.Create();
            SetAuditProperties(result.Model);
            SetOwnedProperties(result.Model);
            return result;
        }



        [HttpGet("/api/ml/vectordb/{id}/secrets")]
        public async Task<DetailResponse<VectorDatabase>> GetVectorDatabaseWithSecrets(string id)
        {
            var db = await _vectorDbManager.GetVectorDatabaseWithSecretsAsync(id, OrgEntityHeader, UserEntityHeader);
            return DetailResponse<VectorDatabase>.Create(db);
        }

        [HttpGet("/api/ml/vectordbs")]
        public Task<ListResponse<VectorDatabaseSummary>> GetVectorDatabases()
        {
            return _vectorDbManager.GetVectorDatabasesForOrgAsync(OrgEntityHeader, UserEntityHeader, GetListRequestFromHeader());
        }

        [HttpDelete("/api/ml/vectordb/{id}")]
        public Task<InvokeResult> DeleteVectorDbAsync(string id)
        {
            return _vectorDbManager.DeleteVectorDatabaseAsync(id, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPost("/api/ml/vectordb")]
        public Task AddVectorDb([FromBody] VectorDatabase db)
        {
            return _vectorDbManager.AddVectorDatabaseAsync(db, OrgEntityHeader, UserEntityHeader);
        }

        [HttpPut("/api/ml/vectordb")]
        public Task UpdateVectorDb([FromBody] VectorDatabase db)
        {
            SetUpdatedProperties(db);
            return _vectorDbManager.UpdateVectorDatabaseAsync(db, OrgEntityHeader, UserEntityHeader);
        }


    }
}
