using System.Collections.Generic;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{
    public interface IRagAnswerService
    {
        Task<InvokeResult<AnswerResult>> AnswerAsync(string vectorDatabaseId, string question, EntityHeader org, EntityHeader user, string repo = null, string language = "csharp", int topK = 8);

        Task<InvokeResult<AnswerResult>> AnswerAsync(string vectorDatabaseId, string question, string conversationContextId, EntityHeader org, EntityHeader user, string repo = null, string language = "csharp", int topK = 8);

        Task<InvokeResult<AnswerResult>> AnswerAsync(string vectorDatabaseId, string question, string conversationContextId, EntityHeader org, EntityHeader user, string repo, string language, int topK, string ragScope, string workspaceId, List<ActiveFile> activeFiles);

        Task<InvokeResult<string>> GetContentAsync(AgentContext vectorDb, string path, int start, int end, EntityHeader org, EntityHeader user);

        Task<InvokeResult<string>> GetContentAsync(string vectorDbId, string path, int start, int end, EntityHeader org, EntityHeader user);

        Task<InvokeResult<string>> GetContentAsync(string path, int start, int end, EntityHeader org, EntityHeader user);

        Task<InvokeResult<AnswerResult>> AnswerAsync(string question, EntityHeader org, EntityHeader user, string repo = null, string language = "csharp", int topK = 8);
    }
}
