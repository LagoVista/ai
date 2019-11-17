using System;
using System.Threading.Tasks;
using LagoVista.Core.Validation;

namespace LagoVista.AI.CloudRepos
{
    public class MLModelRepo : IMLModelRepo
    {
        public Task AddModelAsync(string orgId, string modelId, string revisionId, byte[] model)
        {
            throw new NotImplementedException();
        }

        public Task<InvokeResult<byte[]>> GetModelAsync(string orgId, string modelId)
        {
            throw new NotImplementedException();
        }
    }
}
