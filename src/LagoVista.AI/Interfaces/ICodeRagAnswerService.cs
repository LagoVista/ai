using LagoVista.AI.Services;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface ICodeRagAnswerService
    {
        Task<InvokeResult<AnswerResult>> AnswerAsync(string question, string repo = null, string language = "csharp", int topK = 8);
        Task<InvokeResult<string>> GetContentAsync(string path, string file, int start, int end);
    }
}
