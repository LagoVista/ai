using LagoVista.AI.Models.Context;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Threading;

namespace LagoVista.AI.Models
{
    public enum PipelineSteps
    {
        RequestHandler,
        SessionRestorer,
        AgentContextResolver,
        ClientToolContinuationResolver,
        AgentSessionCreator,
        AgentContextLoader,
        ContextProviderInitializer,
        PromptContentProvider,
        Reasoner,
        LLMClient
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
            if(request == null) throw new ArgumentNullException(nameof(request));
            if(org == null) throw new ArgumentNullException(nameof(org));
            if(user == null) throw new ArgumentNullException(nameof(user));

            Envelope = new Envelope(request.AgentContextId, request.ConversationContextId, request.SessionId, request.TurnId, request.Instruction, 
                request.Streaming, request.ToolResults, request.ClipboardImages, request.InputArtifacts, request.RagScope, org, user);

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
            TimeStamp = DateTime.UtcNow.ToJSONString();
        }

        public AgentPipelineContextTypes Type { get; }

        public string TimeStamp { get; } 

        // Identity / correlation
        public string CorrelationId { get; }

        public AgentSession Session { get; private set; }

        public void AttachSession(AgentSession session, AgentSessionTurn turn)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Turn = turn ?? throw new ArgumentNullException(nameof(turn));   
            RefreshEnvelope();
        }

        public void AttachToolManifest(ToolCallManifest toolManifest)
        {
            if (toolManifest == null) throw new ArgumentNullException(nameof(toolManifest));
            PromptContentProvider.ToolCallManifest = toolManifest;
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

        public bool HasPendingToolCalls
        {
            get => PromptContentProvider.ToolCallManifest.ToolCalls.Any();
        }

        public bool HasClientToolCalls
        {
            get => PromptContentProvider.ToolCallManifest.ToolCalls.Where(tc=>tc.RequiresClientExecution).Any();
        }

        public ContentProvider PromptContentProvider { get; } = new ContentProvider();

        // Trace (optional)
        public CompositionTrace Trace { get; } = new CompositionTrace();

        public CancellationToken CancellationToken { get; } 


        public string ToolManifestId
        {
            get
            {
                if (Session == null) throw new InvalidOperationException("Attempt to generate tool manifest id, prior to restoring session.");
                if (Turn == null) throw new InvalidOperationException("Attempt to generate tool manifest id, prior to restoring turn.");

                return $"{Session.Id}.{Turn.Id}";
            }
        }

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

        public AgentExecuteResponse CreateResponse()
        {
            return new AgentExecuteResponse()
            {

            };
        }

        public Envelope Envelope { get; private set; }

        private void RefreshEnvelope()
        {
            var existing = Envelope;

            Envelope = new Envelope(AgentContext?.Id ?? existing.AgentContextId, ConversationContext?.Id ?? existing.ConversationContextId , Session?.Id ?? existing.SessionId, 
                                    Turn?.Id ?? existing.TurnId, existing.Instructions, existing.Stream, existing.ToolResults, existing.ClipBoardImages,
                                    existing.InputArtifacts, existing.RagScope, existing.Org, existing.User);
        }

        public InvokeResult Validate(PipelineSteps step)
        {
            var result = new InvokeResult();

            switch (step)
            {
                case PipelineSteps.RequestHandler:
                    break;
                case PipelineSteps.SessionRestorer:
                    break;
                case PipelineSteps.AgentContextResolver:
                    break;
                case PipelineSteps.ClientToolContinuationResolver:
                    break;
                case PipelineSteps.AgentSessionCreator:
                    break;
                case PipelineSteps.AgentContextLoader:
                    break;
                case PipelineSteps.ContextProviderInitializer:
                    break;
                case PipelineSteps.Reasoner:
                    break;
                case PipelineSteps.LLMClient:
                    break;
            }

            return result;
        }

        public void LogStepErrorDetails(IAdminLogger logger, PipelineSteps step, string error, TimeSpan ts)
        {

        }

        public void LogStepErrorDetails(IAdminLogger logger, PipelineSteps step, InvokeResult error, TimeSpan ts)
        {

        }

        public void LogDetails(IAdminLogger logger, PipelineSteps step, TimeSpan? ts = null)
        {
            var kvps = new List<KeyValuePair<string, string>>()
            {
                CorrelationId.ToKVP("CorrelationId"),
                (ts.HasValue ? "end" : "start").ToKVP("action"),
                step.ToString().ToKVP("step"),
                Envelope.Org.Text.ToKVP("Org"),
                Envelope.User.Text.ToKVP("User"),
            };

            logger.Trace("[AgentContextLoaderPipelineStap__ExecuteAsync] - Restoring Agent Context.", kvps.ToArray());          
        }
    }

    public class Envelope
    {
        public Envelope(string agentContextId, string conversationContextId, string sessionId, string turnId, string instructions, bool stream,
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
            Stream = stream;
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