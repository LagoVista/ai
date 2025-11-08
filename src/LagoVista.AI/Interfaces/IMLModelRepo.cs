// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 02ffd9b66564b69f794a6c858f73783a4a02f9656a9db27add83c67094966654
// IndexVersion: 2
// --- END CODE INDEX META ---
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
