using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IRagAnswerService
    {
        Task<InvokeResult<string>> GetContentAsync(AgentContext vectorDatabse, string path, string file, int start, int end, EntityHeader org, EntityHeader user);
        Task<InvokeResult<string>> GetContentAsync(string vectorDbId, string path, string file, int start, int end, EntityHeader org, EntityHeader user);
        Task<InvokeResult<string>> GetContentAsync(string path, string file, int start, int end, EntityHeader org, EntityHeader user);
        Task<InvokeResult<AnswerResult>> AnswerAsync(string vectorDatabsaeId, string question, string conversationContextId, EntityHeader org, EntityHeader user, string repo = null, string language = "csharp", int topK = 8);
        Task<InvokeResult<AnswerResult>> AnswerAsync(string vectorDatabaseId, string question, EntityHeader org, EntityHeader user, string repo = null, string language = "csharp", int topK = 8);
        Task<InvokeResult<AnswerResult>> AnswerAsync(string question, EntityHeader org, EntityHeader user, string repo = null, string language = "csharp", int topK = 8);
    }
}
