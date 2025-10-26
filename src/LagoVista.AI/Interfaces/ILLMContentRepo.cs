using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI
{
    public interface ILLMContentRepo
    {
        Task<InvokeResult> AddImageContentAsync(AgentContext vectorDb, string path, string fileName, byte[] model, string contentType);
        Task<InvokeResult<byte[]>> GetImageContentAsync(AgentContext vectorDb, string path, string fileName);
        Task<InvokeResult> AddTextContentAsync(AgentContext vectorDb, string path, string fileName, string content, string contentType);
        Task<InvokeResult<string>> GetTextContentAsync(AgentContext vectorDb, string path, string fileName);
    }
}
