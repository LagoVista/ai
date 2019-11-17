using LagoVista.Core.Validation;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IMLModelRepo
    {
        Task<InvokeResult> AddModelAsync(string orgId, string modelId, int revisionId, byte[] model);
        Task<InvokeResult<Byte[]>> GetModelAsync(string orgId, string modelId, int revisionId);
    }
}
