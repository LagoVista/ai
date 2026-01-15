using System.Threading;
using LagoVista.AI.Models.Context;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

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
        ACP    
    }

    public interface IAgentPipelineContext : IHasSession, IHasTimeStamp, IHasEnvelope, IToolContextSource
    {
        AgentPipelineContextTypes Type { get; }
        string CorrelationId { get; }
        CancellationToken CancellationToken { get; }

        AgentSessionTurn PreviousTurn { get; }
        AgentSessionTurn ThisTurn { get; }

        ResponsePayload ResponsePayload { get; }

        AgentContext AgentContext { get; }

        AgentContextRole Role { get; }

        AgentMode Mode { get; }

        PromptKnowledgeProvider PromptKnowledgeProvider { get; }
        CompositionTrace Trace { get; }

        ResponseTypes ResponseType { get; }

        bool HasPendingToolCalls { get; }
        bool HasClientToolCalls { get; }

        string ToolManifestId { get; }
        void AttachAgentContext(AgentContext context, AgentContextRole conversationContext, AgentMode mode);
        void AttachSession(AgentSession session, AgentSessionTurn thisTurn);
        void AttachClientToolSession(AgentSession session, AgentSessionTurn thisTurn);
        void AttachNewChapterTurn(AgentSessionTurn newChapterTurn);
        void AttachSession(AgentSession session, AgentSessionTurn previousSessoin, AgentSessionTurn thisTurn);
        void AttachToolManifest(ToolCallManifest toolManifest);

        void SetInstructions(string instructions);

        void SetResponsePayload(ResponsePayload payload);


        bool IsTerminal { get;}

        void SetTerminal(string reason);

        IEnumerable<RagContent> RagContent { get; }

        void AddRagContent(IEnumerable<RagContent> content);
        void ClearRagConent();

        int GetToolCallCount(string toolName);

        void AddToolCall(string toolName);
    }
}

