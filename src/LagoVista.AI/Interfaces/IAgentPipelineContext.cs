using System.Threading;
using LagoVista.AI.Models.Context;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;

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

    public enum ResponseTypes
    {
        NotReady,
        Final,
        ToolContinuation,
    }

    public interface IAgentPipelineContext : IHasSession, IHasTimeStamp, IHasEnvelope, IToolContextSource
    {
        AgentPipelineContextTypes Type { get; }
        string CorrelationId { get; }
        CancellationToken CancellationToken { get; }

        AgentSessionTurn Turn { get; }

        ResponsePayload ResponsePayload { get; }

        AgentContext AgentContext { get; }
        ConversationContext ConversationContext { get; }

        PromptKnowledgeProvider PromptKnowledgeProvider { get; }
        CompositionTrace Trace { get; }

        ResponseTypes ResponseType { get; }

        bool HasPendingToolCalls { get; }
        bool HasClientToolCalls { get; }

        string ToolManifestId { get; }
        void AttachAgentContext(AgentContext context, ConversationContext conversationContext);
        void AttachSession(AgentSession session, AgentSessionTurn turn);
        void AttachToolManifest(ToolCallManifest toolManifest);

        // If you actually use these in steps, keep them. Otherwise, drop.
      
        void LogStepErrorDetails(IAdminLogger logger, PipelineSteps step, string error, TimeSpan ts);

        void LogStepErrorDetails(IAdminLogger logger, PipelineSteps step, InvokeResult error, TimeSpan ts);

        void LogDetails(IAdminLogger logger, PipelineSteps step, TimeSpan? ts = null);
    }
}

