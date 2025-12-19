using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class DdrRepo : DocumentDBRepoBase<DetailedDesignReview>, IDdrRepo
    {
        private readonly bool _shouldConsolidateCollections;
        private readonly IAdminLogger _logger;
        public DdrRepo(IMLRepoSettings settings, IAdminLogger logger) :
            base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, logger)
        {
            _logger = logger;
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;


        public Task AddDdrAsync(DetailedDesignReview ddr)
        {
            return CreateDocumentAsync(ddr);
        }

        public Task<DetailedDesignReview> GetDdrByIdAsync(string ddrId)
        {
            return GetDocumentAsync(ddrId);
        }

        public async Task<DetailedDesignReview> GetDdrByTlaIdentiferAsync(string tlaIdentifier, EntityHeader org, bool throwOnNotFound = true)
        {
            var catalog = await QueryAsync(qry => qry.OwnerOrganization.Id == org.Id && qry.DdrIdentifier == tlaIdentifier);
            if (!catalog.Any())
            {
                _logger.AddError("[DdrRepo_GetDdrByTlaIdentiferAsync]", $"Could not find DDR by TLA {tlaIdentifier} org: {org.Id}.");
                throw new RecordNotFoundException(nameof(DetailedDesignReview), tlaIdentifier);
            }

            var ddr = catalog.SingleOrDefault();
            if (ddr == null && throwOnNotFound)
                throw new RecordNotFoundException(typeof(DetailedDesignReview).Name, tlaIdentifier);

            return ddr;
        }

        public Task<ListResponse<DetailedDesignReviewSummary>> GetDdrsAsync(EntityHeader org, ListRequest listRequest)
        {
            return QuerySummaryDescendingAsync<DetailedDesignReviewSummary, DetailedDesignReview>(qry => qry.OwnerOrganization.Id == org.Id, qry => qry.LastUpdatedDate, listRequest);
        }

        public Task<ListResponse<DetailedDesignReviewSummary>> GetDdrsByTlaAsync(string tla, EntityHeader org, ListRequest listRequest)
        {
            return QuerySummaryDescendingAsync<DetailedDesignReviewSummary, DetailedDesignReview>(qry => qry.OwnerOrganization.Id == org.Id && qry.Tla == tla, qry => qry.LastUpdatedDate, listRequest);
        }

        public Task UpdateDdrAsync(DetailedDesignReview ddr)
        {
            return UpsertDocumentAsync(ddr);
        }
    }
}
