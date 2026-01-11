using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;

namespace LagoVista.AI.Chunkers.Interfaces
{
    public interface IDomainMetadataSource
    {
        Task<IReadOnlyList<DomainMetadata>> GetDomainsAsync(
            IReadOnlyList<DiscoveredFile> files,
            IReadOnlyList<ModelMetadata> models,
            CancellationToken cancellationToken);
    }
}
