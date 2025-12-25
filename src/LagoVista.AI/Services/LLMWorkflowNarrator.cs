namespace LagoVista.AI.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LagoVista.AI.Interfaces;

    public sealed class LlmWorkflowNarrator : ILLMWorkflowNarrator
    {
        private readonly IAgentStreamingContext _streaming;
        private static readonly Random _random = new Random();

        private static readonly string[] ConnectingMessages =
        {
        "reaching out…",
        "establishing a connection…",
        "opening a line…",
        "getting in touch…",
        "knocking on the door…",
        "tapping the shoulder…",
        "checking availability…",
        "lining things up…",
        "syncing up…",
        "setting up the link…",
        "spinning up a connection…",
        "calling it in…"
    };

        private static readonly string[] ThinkingMessages =
        {
        "let me mull that over…",
        "one sec, connecting the dots…",
        "calling in a second opinion…",
        "asking my inner narrator…",
        "running it through the gears…",
        "spinning up some thoughts…"
    };

        public LlmWorkflowNarrator(IAgentStreamingContext streaming)
        {
            _streaming = streaming ?? throw new ArgumentNullException(nameof(streaming));
        }

        public Task ConnectingAsync(CancellationToken cancellationToken)
            => _streaming.AddWorkflowAsync(ConnectingMessages[_random.Next(ConnectingMessages.Length)], cancellationToken);

        public Task ThinkingAsync(CancellationToken cancellationToken)
            => _streaming.AddWorkflowAsync(ThinkingMessages[_random.Next(ThinkingMessages.Length)], cancellationToken);

        public Task SummarizingAsync(CancellationToken cancellationToken)
            => _streaming.AddWorkflowAsync("got it, give me a minute to summarize...", cancellationToken);
    }
}
