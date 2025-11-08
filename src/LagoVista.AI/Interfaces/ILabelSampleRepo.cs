// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 109d5a73004cdb05b2f6b0a60034f12cf736ad3aebb2c55603395dcfc6206f51
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.TrainingData;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ILabelSampleRepo
    {
        Task AddLabelSampleAsync(LabelSample label);
        Task<List<LabelSample>> GetLabelsForSampleAsync(string sampleId);
        Task RemoveLabelSampleAsync(string labelId, string sampleId);
    }
}
