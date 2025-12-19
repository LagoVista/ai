using LagoVista.AI.Rag.ContractPacks.Registry.Interfaces;
using LagoVista.AI.Rag.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Registry.Services
{
    public class NuvIoTMetadataRegistryClient : IMetadataRegistryClient
    {
        public Task ReportFacetsAsync(string orgId, string projectId, string repoId, IReadOnlyList<FacetValue> facets, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
