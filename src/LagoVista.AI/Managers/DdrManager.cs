using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.PlatformSupport;
using LagoVista.Core.Validation;
using RingCentral;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class DdrManager : ManagerBase, IDdrManager
    {

        private readonly IDdrRepo _ddrRepo;
        private readonly ITlaCatalogRepo _tlaCatalogRepo;
        public DdrManager(IDdrRepo ddrRepo, ITlaCatalogRepo tlaCatalogRepo, ILogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security) : 
            base(logger, appConfig, dependencyManager, security)
        {
            _ddrRepo = ddrRepo ?? throw new ArgumentNullException(nameof(ddrRepo));
            _tlaCatalogRepo = tlaCatalogRepo ?? throw new ArgumentNullException(nameof(tlaCatalogRepo));
        }

        public async Task<InvokeResult> AddDdrAsync(DetailedDesignReview ddr, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(ddr, Actions.Create);

            await AuthorizeAsync(ddr, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _ddrRepo.AddDdrAsync(ddr);

            return InvokeResult.Success;
        }

        public async Task<InvokeResult> AddTlaCatalog(DdrTla ddrTla, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(ddrTla, Actions.Create);

            var tlaCatalog = await _tlaCatalogRepo.GetTlaCatalogAsync(org, user);
            await AuthorizeAsync(tlaCatalog, AuthorizeResult.AuthorizeActions.Create, user, org);
            if(tlaCatalog.Tlas.Any(tla=> tla.Tla == ddrTla.Tla))
            {
                return InvokeResult.FromError($"TLA {ddrTla.Tla} already exists in the catalog.");
            }

            tlaCatalog.Tlas.Add(ddrTla);
            await _tlaCatalogRepo.UpdateTlaCatalog(tlaCatalog);

            return InvokeResult.Success;
        }

        public async Task<InvokeResult<int>> AllocateTlaIndex(string tla, EntityHeader org, EntityHeader user)
        {
            var tlaCatalog = await _tlaCatalogRepo.GetTlaCatalogAsync(org, user);
            var existingTla = tlaCatalog.Tlas.SingleOrDefault(t => t.Tla == tla);
             if(existingTla == null)
                return InvokeResult<int>.FromError($"TLA {tla} does not exist.");

            existingTla.CurrentIndex++;
            await _tlaCatalogRepo.UpdateTlaCatalog(tlaCatalog);

            return InvokeResult<int>.Create(existingTla.CurrentIndex);
        }

        public async Task<InvokeResult> DeleteDdrAsync(string ddrId, EntityHeader org, EntityHeader user)
        {
            await _ddrRepo.DeleteDdrAsync(ddrId);

            return InvokeResult.Success;
        }

        public async Task<DetailedDesignReview> GetDdrByIdAsync(string ddrId, EntityHeader org, EntityHeader user)
        {
            var ddr = await _ddrRepo.GetDdrByIdAsync(ddrId);
            await AuthorizeAsync(ddr, AuthorizeResult.AuthorizeActions.Create, user, org);
            return ddr;
        }

        public async Task<DetailedDesignReview> GetDdrByTlaIdentiferAsync(string tlaIdentifier, EntityHeader org, EntityHeader user, bool throwOnNotFound = true)
        {
            var ddr = await _ddrRepo.GetDdrByTlaIdentiferAsync(tlaIdentifier, org, throwOnNotFound);
            if(ddr != null)
                await AuthorizeAsync(ddr, AuthorizeResult.AuthorizeActions.Create, user, org);
            
            return ddr;
        }

        public Task<List<DetailedDesignReview>> GetDdrs(string[] dds, EntityHeader org, EntityHeader user)
        {
            return _ddrRepo.GetDdrs(dds, org.Id);
        }

        public async Task<ListResponse<DetailedDesignReviewSummary>> GetDdrsAsync(EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            var ddrs = await _ddrRepo.GetDdrsAsync(org, listRequest);
            await AuthorizeOrgAccessAsync(user, org, typeof(DetailedDesignReview), Actions.Read);
            return ddrs;
        }

        public async Task<ListResponse<DetailedDesignReviewSummary>> GetDdrsByTlaAsync(string tla, int revisionId, EntityHeader org, EntityHeader user, ListRequest listRequest)
        {
            var ddrs = await _ddrRepo.GetDdrsByTlaAsync(tla, org, listRequest);
            await AuthorizeOrgAccessAsync(user, org, typeof(DetailedDesignReview), Actions.Read);
            return ddrs;
        }

        public async Task<List<DdrTla>> GetTlaCatalogAsync(EntityHeader org, EntityHeader user)
        {
            var catalog = await _tlaCatalogRepo.GetTlaCatalogAsync(org, user);
            await AuthorizeOrgAccessAsync(user, org, typeof(DdrTlaCatalog), Actions.Read);
            return catalog.Tlas;
        }

        public async Task<InvokeResult> UpdateDdrAsync(DetailedDesignReview ddr, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(ddr, Actions.Create);

            await AuthorizeAsync(ddr, AuthorizeResult.AuthorizeActions.Create, user, org);

            await _ddrRepo.UpdateDdrAsync(ddr);

            return InvokeResult.Success;
        }

        public async Task<InvokeResult> UpdateTlaCatalog(DdrTla ddrTla, EntityHeader org, EntityHeader user)
        {
            var catalog = await _tlaCatalogRepo.GetTlaCatalogAsync(org, user);
            await AuthorizeOrgAccessAsync(user, org, typeof(DdrTlaCatalog), Actions.Read);
            var existingTla = catalog.Tlas.SingleOrDefault(t => t.Tla == ddrTla.Tla);
            if (existingTla == null)
                return InvokeResult.FromError($"TLA {ddrTla.Tla} does not exist.");

            if (!String.IsNullOrEmpty(ddrTla.Title))
                existingTla.Title = ddrTla.Title;
            
            if(!String.IsNullOrEmpty(ddrTla.Summary))
                existingTla.Summary = ddrTla.Summary;

            await _tlaCatalogRepo.UpdateTlaCatalog(catalog);

            return InvokeResult.Success;
        }
    }
}
