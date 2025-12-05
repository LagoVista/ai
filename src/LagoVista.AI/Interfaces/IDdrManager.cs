using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IDdrManager
    {
        Task<InvokeResult> AddDdrAsync(DetailedDesignReview ddr, EntityHeader org, EntityHeader user);

        /// <summary>
        /// Gets the DDR by it's unique id (DB76727D7BF14CD6AE72C9227E2FA3B1) 
        /// </summary>
        /// <param name="ddrId"></param>
        /// <param name="org"></param>
        /// <param name="uesr"></param>
        /// <returns></returns>
        Task<DetailedDesignReview> GetDdrByIdAsync(string ddrId, EntityHeader org, EntityHeader user);
        Task<InvokeResult> UpdateDdrAsync(DetailedDesignReview ddr, EntityHeader org, EntityHeader uesr);
        /// <summary>
        /// Get the DDIR by its identifier (TLA-###)
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="org"></param>
        /// <param name="uesr"></param>
        /// <returns></returns>
        Task<DetailedDesignReview> GetDdrByTlaIdentiferAsync(string tlaIdentifier, EntityHeader org, EntityHeader user);

        Task<ListResponse<DetailedDesignReviewSummary>> GetDdrsAsync(EntityHeader org, EntityHeader user, ListRequest listRequest);
        Task<ListResponse<DetailedDesignReviewSummary>> GetDdrsByTlaAsync(string tla, int revisionId, EntityHeader org, EntityHeader user, ListRequest listRequest);

        Task<List<DdrTla>> GetTlaCatalogAsync(EntityHeader org, EntityHeader user);

        Task<InvokeResult> AddTlaCatalog(DdrTla ddrTla, EntityHeader org, EntityHeader user);

        Task<InvokeResult> UpdateTlaCatalog(DdrTla ddrTla, EntityHeader org, EntityHeader user);

        Task<InvokeResult<int>> AllocateTlaIndex(string tla, EntityHeader org, EntityHeader user);
    }
}
