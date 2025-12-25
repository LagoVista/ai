using LagoVista.AI.Models.Context;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace LagoVista.AI.Models
{
    public enum PipelineSteps
    {
        RequestHandler = 0,
        SessionLoader = 10,
        AgentContextRestorer = 20,
        ContextProviderInitializer = 30,
        ClientToolContinuationResolver = 40,
        Reasoner = 50,
        LLMClient = 60
    }

    public enum AgentPipelineContextTypes
    {
        Initial,
        FollowOn,
        ClientToolCallContinuation
    }

    public sealed class AgentPipelineContext
    {
   
        public AgentPipelineContext(AgentExecuteRequest request, EntityHeader org, EntityHeader user, CancellationToken token = default)
        {
            Envelope = new Envelope(request.AgentContextId, request.ConversationContextId, request.SessionId, request.TurnId, request.Instruction, 
                request.ToolResults, request.ClipboardImages, request.InputArtifacts, request.RagScope, org, user);

            if (String.IsNullOrEmpty(request.SessionId) && String.IsNullOrEmpty(request.TurnId) && !request.ToolResults.Any())
            {
                Type = AgentPipelineContextTypes.Initial;
            }
            else if (!String.IsNullOrEmpty(request.SessionId) && !String.IsNullOrEmpty(request.TurnId) && !request.ToolResults.Any())
            {
                Type = AgentPipelineContextTypes.FollowOn;
            }
            else if (!String.IsNullOrEmpty(request.SessionId) && !String.IsNullOrEmpty(request.TurnId) && request.ToolResults.Any())
            {
                Type = AgentPipelineContextTypes.ClientToolCallContinuation;
            }
            else
                throw new InvalidOperationException("Invalid Request");

            CancellationToken = token;
            CorrelationId = Guid.NewGuid().ToId();
        }

        public AgentPipelineContextTypes Type { get; }

        // Identity / correlation
        public string CorrelationId { get; }

        public AgentSession Session { get; private set; }

        public void AttachSession(AgentSession session, AgentSessionTurn turn)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Turn = turn ?? throw new ArgumentNullException(nameof(turn));   
            RefreshEnvelope();
        }

        public AgentSessionTurn Turn { get; private set; }


        public void AttachAgentContext(AgentContext context, ConversationContext conversationContext)
        {
            AgentContext = context ?? throw new ArgumentNullException(nameof(context));
            ConversationContext = conversationContext ?? throw new ArgumentNullException(nameof(conversationContext));
            RefreshEnvelope();
        }

        // Loaded context objects
        public AgentContext AgentContext { get; private set; }
 
        public ConversationContext ConversationContext { get; private set; }

        //// Output (set by final step)
        //public AgentExecuteResponse Response { get; set; }

        public ContentProvider PromptContentProvider { get; } = new ContentProvider();

        // Trace (optional)
        public CompositionTrace Trace { get; } = new CompositionTrace();

        public CancellationToken CancellationToken { get; } 


        public AgentToolExecutionContext ToToolContext()
        {
            return new AgentToolExecutionContext()
            {
                AgentContext = AgentContext,
                ConversationContext = ConversationContext,
                Org = Envelope.Org,
                User = Envelope.User,
                SessionId = Envelope.SessionId,
                CurrentTurnId = Envelope.TurnId,            
            };
        }

        public Envelope Envelope { get; private set; }

        private void RefreshEnvelope()
        {
            var existing = Envelope;

            Envelope = new Envelope(AgentContext?.Id ?? existing.AgentContextId, ConversationContext?.Id ?? existing.ConversationContextId , Session?.Id ?? existing.SessionId, 
                                    Turn?.Id ?? existing.TurnId, existing.Instructions, existing.ToolResults, existing.ClipBoardImages,
                                    existing.InputArtifacts, existing.RagScope, existing.Org, existing.User);
        }

        public static InvokeResult<AgentPipelineContext> ValidateInputs(AgentPipelineContext ctx, PipelineSteps step)
        {
            if (ctx == null) { return InvokeResult<AgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "AgentPipelineContext_Ctx_Is_Missing"); }

            
            if (step > PipelineSteps.SessionLoader)
            {
                if (ctx.Session == null) return InvokeResult<AgentPipelineContext>.FromError($"{nameof(AgentPipelineContext.Session)} is required on {nameof(AgentPipelineContext)} at {step}.", $"{nameof(AgentPipelineContext).ToUpper()}_{nameof(AgentPipelineContext.Session).ToUpper()}_Missing_{step}");
                if (ctx.Turn == null) return InvokeResult<AgentPipelineContext>.FromError($"{nameof(AgentPipelineContext.Turn)} is required on {nameof(AgentPipelineContext)} at {step}.", $"{nameof(AgentPipelineContext).ToUpper()}_{nameof(AgentPipelineContext.Turn).ToUpper()}_Missing_{step}");
            }

            if (step > PipelineSteps.AgentContextRestorer)
            {
                if (ctx.AgentContext == null) return InvokeResult<AgentPipelineContext>.FromError($"{nameof(AgentPipelineContext.AgentContext)} is required on {nameof(AgentPipelineContext)} at {step}.", $"{nameof(AgentPipelineContext).ToUpper()}_{nameof(AgentPipelineContext.AgentContext).ToUpper()}_Missing_{step}");
                if (ctx.ConversationContext == null) return InvokeResult<AgentPipelineContext>.FromError($"{nameof(AgentPipelineContext.ConversationContext)} is required on {nameof(AgentPipelineContext)} at {step}.", $"{nameof(AgentPipelineContext).ToUpper()}_{nameof(AgentPipelineContext.ConversationContext).ToUpper()}_Missing_{step}");
            }
          
            return InvokeResult<AgentPipelineContext>.Create(ctx);
        }
    }

    public class Envelope
    {
        public Envelope(string agentContextId, string conversationContextId, string sessionId, string turnId, string instructions,
                        IEnumerable<ToolResultSubmission> toolResults, IEnumerable<ClipboardImage> clipboardImages, IEnumerable<InputArtifact> inputArtifacts,
                        RagScope ragScope, EntityHeader org, EntityHeader user)
        {
            AgentContextId = agentContextId;
            ConversationContextId = conversationContextId;
            SessionId = sessionId;
            TurnId = turnId;
            Org = org ?? throw new ArgumentNullException(nameof(org));
            User = user ?? throw new ArgumentNullException(nameof(user));
            ToolResults = toolResults?.ToList() ?? new List<ToolResultSubmission>();
            ClipBoardImages = clipboardImages?.ToList() ?? new List<ClipboardImage>();
            InputArtifacts = inputArtifacts?.ToList() ?? new List<InputArtifact>();
            Instructions = instructions;
            RagScope = ragScope ?? new RagScope();
        }

        public RagScope RagScope { get; }

        public IReadOnlyList<ToolResultSubmission> ToolResults { get; }
        public IReadOnlyList<ClipboardImage> ClipBoardImages { get; }
        public IReadOnlyList<InputArtifact> InputArtifacts { get; }

        public string Instructions { get; }
        public string AgentContextId { get; }
        public string ConversationContextId { get; }
        public string SessionId { get; }
        public string TurnId { get; }
        public EntityHeader Org { get; }
        public EntityHeader User { get; }
        public bool Stream { get; }
    }
}