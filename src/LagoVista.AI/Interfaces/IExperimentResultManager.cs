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
