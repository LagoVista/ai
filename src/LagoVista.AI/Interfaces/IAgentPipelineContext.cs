using System.Threading;
using LagoVista.AI.Models.Context;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Interfaces
{

    public interface IHasSession
    {
        AgentSession Session { get; }
    }

    public interface IHasTimeStamp
    {
        string TimeStamp { get; }
    }

    public interface IHasEnvelope
    {
        Envelope Envelope { get; }
    }

    public interface IToolContextSource
    {
        AgentToolExecutionContext ToToolContext();
    }

    public interface IAgentPipelineContext : IHasSession, IHasTimeStamp, IHasEnvelope, IToolContextSource
    {
        AgentPipelineContextTypes Type { get; }
        string CorrelationId { get; }
        CancellationToken CancellationToken { get; }

        AgentSessionTurn Turn { get; }

        AgentContext AgentContext { get; }
        ConversationContext ConversationContext { get; }

        ContentProvider PromptContentProvider { get; }
        CompositionTrace Trace { get; }

        bool HasPendingToolCalls { get; }
        bool HasClientToolCalls { get; }

        string ToolManifestId { get; }

        // If you actually use these in steps, keep them. Otherwise, drop.
        InvokeResult Validate(PipelineSteps step);
    }
}

