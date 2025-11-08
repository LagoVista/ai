// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: f4a6d5a8ff235ca31b62adbc107de023e115f5f807eca5f4b6cf6a8fa4afc130
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IExperimentResultManager
    {
        Task AddExperimentResultAsync(ExperimentResult result, EntityHeader org, EntityHeader uesr);

        Task<ListResponse<ExperimentResult>> GetExperimentResultsAsync(string modelId, int revisionId, EntityHeader org, EntityHeader user, ListRequest listRequest);
    }
}
