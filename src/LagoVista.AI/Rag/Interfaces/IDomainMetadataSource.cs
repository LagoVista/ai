using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Interfaces
{
    public interface IDomainMetadataSource
    {
        Task<IReadOnlyList<DomainMetadata>> GetDomainsAsync(
            IReadOnlyList<DiscoveredFile> files,
            IReadOnlyList<ModelMetadata> models,
            CancellationToken cancellationToken);
    }
}
