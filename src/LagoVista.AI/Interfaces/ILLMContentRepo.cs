using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ILLMContentRepo
    {
        Task<InvokeResult> AddImageContentAsync(VectorDatabase vectorDb, string path, string fileName, byte[] model, string contentType);
        Task<InvokeResult<byte[]>> GetImageContentAsync(VectorDatabase vectorDb, string path, string fileName);
        Task<InvokeResult> AddTextContentAsync(VectorDatabase vectorDb, string path, string fileName, string content, string contentType);
        Task<InvokeResult<string>> GetTextContentAsync(VectorDatabase vectorDb, string path, string fileName);
    }
}
