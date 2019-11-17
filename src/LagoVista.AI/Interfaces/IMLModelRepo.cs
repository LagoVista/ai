using LagoVista.Core.Validation;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface IMLModelRepo
    {
        Task AddModelAsync(string orgId, string modelId, string revisionId, byte[] model);
        Task<InvokeResult<Byte[]>> GetModelAsync(string orgId, string modelId);
    }
}
