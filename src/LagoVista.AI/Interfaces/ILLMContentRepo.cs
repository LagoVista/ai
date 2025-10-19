using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ILLMContentRepo
    {
        Task<InvokeResult> AddImageContentAsync(string orgId, string path, string fileName, byte[] model, string contentType);
        Task<InvokeResult<byte[]>> GetImageContentAsync(string orgId, string path, string fileName);
        Task<InvokeResult> AddTextContentAsync(string orgId, string path, string fileName, string content, string contentType);
        Task<InvokeResult<string>> GetTextContentAsync(string orgId, string path, string fileName);
    }
}
