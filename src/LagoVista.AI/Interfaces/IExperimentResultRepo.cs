// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: de9884a5b47e3c980b78260dfc392426d1d2a1a9c65cb1f21678c4255f932316
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models.UIMetaData;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IExperimentResultRepo
    {
        Task AddResultAsync(ExperimentResult result);

        Task<ListResponse<ExperimentResult>> GetResultsAsync(string modelId, int versionNumber, ListRequest listRequest);
    }
}

