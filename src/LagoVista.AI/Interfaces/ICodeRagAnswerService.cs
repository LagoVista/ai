using LagoVista.AI.Services;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface ICodeRagAnswerService
    {
        Task<AnswerResult> AnswerAsync(string question, string? repo = null, string? language = "csharp", int topK = 8);
    }
}
