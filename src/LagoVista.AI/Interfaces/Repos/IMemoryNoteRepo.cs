using LagoVista.AI.Models;
using LagoVista.Core.Models.UIMetaData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface IMemoryNoteRepo
    {
        Task AddMemoryNoteAsync(AgentSessionMemoryNote note);
        Task UpdateMemoryNoteAsync(AgentSessionMemoryNote note);
        Task<ListResponse<AgentSessionMemoryNote>> GetMemoryNotesForSessionAsync(string orgId, string sessionId);
    }
}
