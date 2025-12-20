using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LagoVista.AI.Interfaces
{
    public interface IDdrRepo
    {
        Task AddDdrAsync(DetailedDesignReview ddr);
        Task<DetailedDesignReview> GetDdrByIdAsync(string ddrId);
        Task UpdateDdrAsync(DetailedDesignReview ddr);
        Task<DetailedDesignReview> GetDdrByTlaIdentiferAsync(string tlaIdentifier, EntityHeader org, bool throwOnNotFOund = true);

        Task<ListResponse<DetailedDesignReviewSummary>> GetDdrsAsync(EntityHeader org, ListRequest listRequest);
        Task<ListResponse<DetailedDesignReviewSummary>> GetDdrsByTlaAsync(string tla, EntityHeader org, ListRequest listRequest);
        Task<List<DetailedDesignReview>> GetDdrs(string[] dds, string orgId);
    }
}
