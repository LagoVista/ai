using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.CloudStorage.DocumentDB;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
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

        public DdrRepo(IMLRepoSettings settings, IAdminLogger logger, ICacheProvider cacheProvider) :
            base(settings.MLDocDbStorage.Uri, settings.MLDocDbStorage.AccessKey, settings.MLDocDbStorage.ResourceName, logger, cacheProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
            _shouldConsolidateCollections = settings.ShouldConsolidateCollections;
        }

        private static String GetDdrInstructionKey(DetailedDesignReview ddr)
        {
            return $"ddr_{ddr.OwnerOrganization.Id}_instructional_{ddr.DdrIdentifier.Replace("-","_")}".ToLower();
        }

        private static String GetDdrReferenceKey(DetailedDesignReview ddr)
        {
            return $"ddr_{ddr.OwnerOrganization.Id}_refernential_{ddr.DdrIdentifier.Replace("-", "_")}".ToLower();
        }

        protected override bool ShouldConsolidateCollections => _shouldConsolidateCollections;

        public async Task AddDdrAsync(DetailedDesignReview ddr)
        {
            if (!String.IsNullOrEmpty(ddr.AgentInstructions))
                await _cacheProvider.AddAsync(GetDdrInstructionKey(ddr), ddr.AgentInstructions);

            if (!String.IsNullOrEmpty(ddr.ReferentialSummary))
                await _cacheProvider.AddAsync(GetDdrReferenceKey(ddr), ddr.ReferentialSummary);

            await CreateDocumentAsync(ddr);
        }

        public Task DeleteDdrAsync(string ddrId)
        {
            return DeleteDocumentAsync(ddrId);
        }

        public async Task<InvokeResult<IDictionary<string, string>>> GetAgentInstructionsAsync(string orgId, IEnumerable<string> ddrIds, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(orgId))
                return InvokeResult<IDictionary<string, string>>.FromError("orgId is required.");

            if (ddrIds == null)
                return InvokeResult<IDictionary<string, string>>.Create(new Dictionary<string, string>());

            var ids = ddrIds.Where(id => !string.IsNullOrWhiteSpace(id))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();

            if (ids.Length == 0)
                return InvokeResult<IDictionary<string, string>>.Create(new Dictionary<string, string>());

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
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var missingIds = new List<string>();

            foreach (var id in ids)
            {
                var cacheKey = cacheKeyByDdrId[id];
                cached.TryGetValue(cacheKey, out var value);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    result[id] = value;
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
                var foundIds = new HashSet<string>(ddrs.Select(d => d.DdrIdentifier), StringComparer.OrdinalIgnoreCase);
                var notFound = missingIds.Where(id => !foundIds.Contains(id)).ToArray();
                if (notFound.Length > 0)
                {
                    _logger.AddError("[DdrRepo_GetAgentInstructionsAsync]", $"Could not find DDR(s) by TLA: {string.Join(", ", notFound)} org: {orgId}.");
                    return InvokeResult<IDictionary<string, string>>.FromError($"DDR(s) not found: {string.Join(", ", notFound)}");
                }

                foreach (var ddr in ddrs)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return InvokeResult<IDictionary<string, string>>.FromError("Operation canceled.");

                    var content = ddr.AgentInstructions ?? string.Empty;
                    result[ddr.DdrIdentifier] = content;

                    // Backfill cache only when non-empty (consistent with Add/Update behavior).
                    if (!string.IsNullOrWhiteSpace(content))
                        await _cacheProvider.AddAsync(GetDdrInstructionKey(ddr), content);
                }
            }

            return InvokeResult<IDictionary<string, string>>.Create(result);
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
           var result = await QueryAsync(rec => ddrs.Contains(rec.DdrIdentifier), rec => rec.DdrIdentifier, ListRequest.CreateForAll());
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

        public async Task<InvokeResult<IDictionary<string, string>>> GetReferentialSummariesAsync(
     string orgId,
     IEnumerable<string> ddrIds,
     CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(orgId))
                return InvokeResult<IDictionary<string, string>>.FromError("orgId is required.");

            if (ddrIds == null)
                return InvokeResult<IDictionary<string, string>>.Create(new Dictionary<string, string>());

            var ids = ddrIds.Where(id => !string.IsNullOrWhiteSpace(id))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();

            if (ids.Length == 0)
                return InvokeResult<IDictionary<string, string>>.Create(new Dictionary<string, string>());

            // Build Redis keys for all requested DDR IDs (batch).
            var cacheKeyByDdrId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids)
            {
                // NOTE: key name matches existing implementation (includes current spelling).
                var key = $"ddr_{orgId}_refernential_{id.Replace("-", "_")}".ToLower();
                cacheKeyByDdrId[id] = key;
            }

            // 1) Batch fetch from cache
            var cached = await _cacheProvider.GetManyAsync(cacheKeyByDdrId.Values);

            // 2) Map cache hits back to DDR IDs
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var missingIds = new List<string>();

            foreach (var id in ids)
            {
                var cacheKey = cacheKeyByDdrId[id];
                cached.TryGetValue(cacheKey, out var value);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    result[id] = value;
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
                var foundIds = new HashSet<string>(ddrs.Select(d => d.DdrIdentifier), StringComparer.OrdinalIgnoreCase);
                var notFound = missingIds.Where(id => !foundIds.Contains(id)).ToArray();
                if (notFound.Length > 0)
                {
                    _logger.AddError("[DdrRepo_GetReferentialSummariesAsync]", $"Could not find DDR(s) by TLA: {string.Join(", ", notFound)} org: {orgId}.");
                    return InvokeResult<IDictionary<string, string>>.FromError($"DDR(s) not found: {string.Join(", ", notFound)}");
                }

                foreach (var ddr in ddrs)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return InvokeResult<IDictionary<string, string>>.FromError("Operation canceled.");

                    var content = ddr.ReferentialSummary ?? string.Empty;
                    result[ddr.DdrIdentifier] = content;

                    // Backfill cache only when non-empty (consistent with Add/Update behavior).
                    if (!string.IsNullOrWhiteSpace(content))
                        await _cacheProvider.AddAsync(GetDdrReferenceKey(ddr), content);
                }
            }

            return InvokeResult<IDictionary<string, string>>.Create(result);
        }


        public async Task UpdateDdrAsync(DetailedDesignReview ddr)
        {
            if (!String.IsNullOrEmpty(ddr.AgentInstructions))
                await _cacheProvider.AddAsync(GetDdrInstructionKey(ddr), ddr.AgentInstructions);

            if (!String.IsNullOrEmpty(ddr.ReferentialSummary))
                await _cacheProvider.AddAsync(GetDdrReferenceKey(ddr), ddr.ReferentialSummary);

            await UpsertDocumentAsync(ddr);
        }
    }
}
