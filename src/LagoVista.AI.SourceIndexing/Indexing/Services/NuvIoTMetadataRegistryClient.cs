using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.Services
{
    public class NuvIoTMetadataRegistryClient : IMetadataRegistryClient
    {
        public Task ReportFacetsAsync(string orgId, string projectId, string repoId, IReadOnlyList<FacetValue> facets, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
