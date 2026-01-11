using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.CloudStorage.Interfaces;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.CloudRepos
{
    public class DdrRepo : DocumentDBRepoBase<DetailedDesignReview>, IDdrRepo, IDdrConsumptionFieldProvider
    {
        private readonly bool _shouldConsolidateCollections;
        private readonly IAdminLogger _logger;
        private readonly ICacheProvider _cacheProvider;

        public DdrRepo(IMLRepoSettings settings, IDocumentCloudCachedServices services) :
            base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, services)
        {
            _logger = services.AdminLogger ?? throw new ArgumentNullException(nameof(services.AdminLogger));
            _cacheProvider = services.CacheProvider ?? throw new ArgumentNullException(nameof(services.CacheProvider));
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }


        private static String GetDdrKey(DetailedDesignReview ddr)
        {
            return $"ddr_{ddr.OwnerOrganization.Id}_{ddr.DdrIdentifier.Replace("-", "_")}".ToLower();
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public async Task AddDdrAsync(DetailedDesignReview ddr)
        {
            await _cacheProvider.AddAsync(GetDdrKey(ddr), ddr.AgentInstructions);
            await CreateDocumentAsync(ddr);
        }

        public Task DeleteDdrAsync(string ddrId)
        {
            return DeleteDocumentAsync(ddrId);
        }

        public async Task<InvokeResult<IDictionary<string, DdrModelFields>>> GetDdrModelSummaryAsync(string orgId, IEnumerable<string> ddrIds, CancellationToken cancellationToken = default)
        {

            _logger.Trace($"[DdrRepo_GetDdrModelSummaryAsync] - getting {String.Join(", ", ddrIds)}");

            if (string.IsNullOrWhiteSpace(orgId))
                return InvokeResult<IDictionary<string, DdrModelFields>>.FromError("orgId is required.");

            if (ddrIds == null)
                return InvokeResult<IDictionary<string, DdrModelFields>>.Create(new Dictionary<string, DdrModelFields>());

            var ids = ddrIds.Where(id => !string.IsNullOrWhiteSpace(id))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();

            if (ids.Length == 0)
                return InvokeResult<IDictionary<string, DdrModelFields>>.Create(new Dictionary<string, DdrModelFields>());

            // Build Redis keys for all requested DDR IDs (batch).
            var cacheKeyByDdrId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids)
            {
                var key = $"ddr_{orgId}_instructional_{id.Replace("-", "_")}".ToLower();
                cacheKeyByDdrId[id] = key;
            }

            // 1) Batch fetch from cache
            var cached = await _cacheProvider.GetManyAsync(cacheKeyByDdrId.Values);

            // 2) Map cache hits back to DDR IDs
            var result = new Dictionary<string, DdrModelFields>(StringComparer.OrdinalIgnoreCase);
            var missingIds = new List<string>();

            foreach (var id in ids)
            {
                var cacheKey = cacheKeyByDdrId[id];
                cached.TryGetValue(cacheKey, out var value);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    result[id] = JsonConvert.DeserializeObject< DdrModelFields>(value);
                }
                else
                {
                    missingIds.Add(id);
                }
            }

            // 3) Fill cache misses from DocumentDB and backfill cache
            if (missingIds.Count > 0)
            {
                var ddrs = await GetDdrs(missingIds.ToArray(), orgId);

                // Fail-fast if any requested DDR wasn't found
                var foundIds = new HashSet<string>(ddrs.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);
                var notFound = missingIds.Where(id => !foundIds.Contains(id)).ToArray();
                if (notFound.Length > 0)
                {
                    _logger.AddError("[DdrRepo_GetDdrModelSummaryAsync]", $"Could not find DDR(s) by ID: {string.Join(", ", notFound)} org: {orgId}.");
                    return InvokeResult<IDictionary<string, DdrModelFields>>.FromError($"DDR(s) not found: {string.Join(", ", notFound)}");
                }

                foreach (var ddr in ddrs)
                {
                    _logger.Trace($"[DdrRepo_GetDdrModelSummaryAsync] - adding {ddr.DdrIdentifier} ({ddr.Id})");

                    if (cancellationToken.IsCancellationRequested) return InvokeResult<IDictionary<string, DdrModelFields>>.FromError("Operation canceled.");

                    var content = new DdrModelFields()
                    {
                        Id = ddr.Id,
                        DdrIdentifier = ddr.DdrIdentifier,
                        Title = ddr.Title,
                        AgentInstructions = ddr.AgentInstructions,
                        ReferentialSummary = ddr.ReferentialSummary
                    };

                    result[ddr.Id] = content;

                    // Backfill cache only when non-empty (consistent with Add/Update behavior).
                    await _cacheProvider.AddAsync(GetDdrKey(ddr), JsonConvert.SerializeObject(content));
                }
            }

            _logger.Trace($"[DdrRepo_GetDdrModelSummaryAsync] Requested {ddrIds.Count()} - Loaded {result.Count}.");

            return InvokeResult<IDictionary<string, DdrModelFields>>.Create(result);
        }


        public Task<DetailedDesignReview> GetDdrByIdAsync(string ddrId)
        {
            return GetDocumentAsync(ddrId);
        }

        public async Task<DetailedDesignReview> GetDdrByTlaIdentiferAsync(string tlaIdentifier, EntityHeader org, bool throwOnNotFound = true)
        {
            var catalog = await QueryAsync(qry => qry.OwnerOrganization.Id == org.Id && qry.DdrIdentifier == tlaIdentifier && qry.IsDeleted == false);
            if (!catalog.Any() && throwOnNotFound)
            {
                _logger.AddError("[DdrRepo_GetDdrByTlaIdentiferAsync]", $"Could not find DDR by TLA {tlaIdentifier} org: {org.Id}.");
                throw new RecordNotFoundException(nameof(DetailedDesignReview), tlaIdentifier);
            }

            return catalog.SingleOrDefault();          
        }

        public async Task<List<DetailedDesignReview>> GetDdrs(string[] ddrs, string orgId)
        {
           var result = await QueryAsync(rec => ddrs.Contains(rec.Id)  && rec.OwnerOrganization.Id == orgId, rec => rec.DdrIdentifier , ListRequest.CreateForAll());
            return result.Model.ToList();
        }

        public Task<ListResponse<DetailedDesignReviewSummary>> GetDdrsAsync(EntityHeader org, ListRequest listRequest)
        {
            return QuerySummaryDescendingAsync<DetailedDesignReviewSummary, DetailedDesignReview>(qry => qry.OwnerOrganization.Id == org.Id, qry => qry.LastUpdatedDate, listRequest);
        }

        public Task<ListResponse<DetailedDesignReviewSummary>> GetDdrsByTlaAsync(string tla, EntityHeader org, ListRequest listRequest)
        {
            return QuerySummaryDescendingAsync<DetailedDesignReviewSummary, DetailedDesignReview>(qry => qry.OwnerOrganization.Id == org.Id && qry.Tla == tla, qry => qry.LastUpdatedDate, listRequest);
        }

 

        public async Task UpdateDdrAsync(DetailedDesignReview ddr)
        {
            await _cacheProvider.AddAsync(GetDdrKey(ddr), ddr.AgentInstructions);
            await UpsertDocumentAsync(ddr);
        }
    }
}
