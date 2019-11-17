using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IExperimentResultRepo
    {
        Task AddResultAsync(ExperimentResult result);

        Task<ListResponse<ExperimentResult>> GetResultsAsync(string modelId, int versionNumber, ListRequest listRequest);
    }
}
