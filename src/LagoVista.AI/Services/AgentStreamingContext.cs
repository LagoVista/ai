using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace LagoVista.AI.Services
{
    public class AgentStreamingContext : IAgentStreamingContext
    {
        public int Index { get; set; } = 1;
        public Func<AgentStreamEvent, Task>? Current { get; set; }

        public Task AddPartialAsync(string deltaText, CancellationToken token = default)
        {
            if (Current == null)
            {
                return Task.CompletedTask;
            }

            var streamEvent = new AgentStreamEvent
            {
                Kind = "partial",
                DeltaText = deltaText,
                Index = this.Index++
            };

            // The callback itself (in the controller) will honor cancellation
            return Current(streamEvent);
        }

    }
}
