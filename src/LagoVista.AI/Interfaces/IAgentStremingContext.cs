using LagoVista.AI.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentStreamingContext
    {
        int Index { get; set; }
        Func<AgentStreamEvent, Task> Current { get; set; }

        Task AddPartialAsync(string deltaText, CancellationToken token = default);
    }

}
