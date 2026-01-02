using LagoVista.AI.Interfaces;
using LagoVista.AI.Models.Context;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using RingCentral;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Policy;
using System.Threading;

namespace LagoVista.AI.Models
{
    public enum PipelineSteps
    {
        RequestHandler = 10,
        SessionRestorer = 20,
        AgentContextResolver = 30,
        ClientToolContinuationResolver = 40,
        AgentSessionCreator = 50,
        AgentContextLoader = 60,
        PromptKnowledgeProviderInitializer = 70,
        Reasoner = 80,
        LLMClient = 90,
        ResponseBuilder = 100
    }

    public enum AgentPipelineContextTypes
    {
        Initial,
        FollowOn,
        ClientToolCallContinuation
    }

    public sealed class AgentPipelineContext : IAgentPipelineContext
    {
   
        public AgentPipelineContext(AgentExecuteRequest request, EntityHeader org, EntityHeader user, CancellationToken token = default)
        {
            if(request == null) throw new ArgumentNullException(nameof(request));
            if(org == null) throw new ArgumentNullException(nameof(org));
            if(user == null) throw new ArgumentNullException(nameof(user));

            var hasToolResults = request.ToolResults?.Any() ?? false;

            Envelope = new Envelope(request.AgentContextId, request.RoleId, request.SessionId, request.TurnId, null, request.Instruction, 
                request.Streaming, request.ToolResults, request.ClipboardImages, request.InputArtifacts, request.RagScope, org, user);

            if (String.IsNullOrEmpty(request.SessionId) && String.IsNullOrEmpty(request.TurnId) && !hasToolResults)
            {
                Type = AgentPipelineContextTypes.Initial;
            }
            else if (!String.IsNullOrEmpty(request.SessionId) && !String.IsNullOrEmpty(request.TurnId) && !hasToolResults)
            {
                Type = AgentPipelineContextTypes.FollowOn;
            }
            else if (!String.IsNullOrEmpty(request.SessionId) && !String.IsNullOrEmpty(request.TurnId) && hasToolResults)
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

        public void SetResponsePayload(ResponsePayload payload)
        {
            ResponsePayload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public ResponsePayload ResponsePayload { get; private set; }

        public void AttachSession(AgentSession session, AgentSessionTurn turn)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            ThisTurn = turn ?? throw new ArgumentNullException(nameof(turn));   
            RefreshEnvelope();
        }

        public void AttachSession(AgentSession session, AgentSessionTurn previousTurn, AgentSessionTurn thisTurn)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            ThisTurn = thisTurn ?? throw new ArgumentNullException(nameof(thisTurn));
            PreviousTurn = previousTurn ?? throw new ArgumentNullException(nameof(previousTurn));
            RefreshEnvelope();
        }

        public void AttachToolManifest(ToolCallManifest toolManifest)
        {
            if (toolManifest == null) throw new ArgumentNullException(nameof(toolManifest));
            PromptKnowledgeProvider.ToolCallManifest = toolManifest;
        }

        public AgentSessionTurn ThisTurn { get; private set; }
        public AgentSessionTurn PreviousTurn { get; private set; }

        public void AttachAgentContext(AgentContext context, AgentContextRole role)
        {
            AgentContext = context ?? throw new ArgumentNullException(nameof(context));
            Role = role ?? throw new ArgumentNullException(nameof(role));
            RefreshEnvelope();
        }

        // Loaded context objects
        public AgentContext AgentContext { get; private set; }

        public AgentContextRole Role { get; private set; }

        public bool HasPendingToolCalls
        {
            get => PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Any();
        }

        public bool HasClientToolCalls
        {
            get => PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Where(tc=>tc.RequiresClientExecution).Any();
        }

        public PromptKnowledgeProvider PromptKnowledgeProvider { get; } = new PromptKnowledgeProvider();

        // Trace (optional)
        public CompositionTrace Trace { get; } = new CompositionTrace();

        public CancellationToken CancellationToken { get; } 


        public string ToolManifestId
        {
            get
            {
                if (Session == null) throw new InvalidOperationException("Attempt to generate tool manifest id, prior to restoring session.");
                if (ThisTurn == null) throw new InvalidOperationException("Attempt to generate tool manifest id, prior to restoring turn.");

                return $"{Session.Id}.{ThisTurn.Id}";
            }
        }

        public AgentToolExecutionContext ToToolContext()
        {
            return new AgentToolExecutionContext()
            {
                AgentContext = AgentContext,
                Role = Role,
                Org = Envelope.Org,
                User = Envelope.User,
                SessionId = Envelope.SessionId,
                CurrentTurnId = Envelope.PreviousTurnId,            
            };
        }

        public Envelope Envelope { get; private set; }

        public ResponseTypes ResponseType
        {
            get
            {
                if (ResponsePayload != null)
                {
                    return ResponseTypes.Final;
                }

                if(HasClientToolCalls)
                {
                    return ResponseTypes.ToolContinuation;
                }

                return ResponseTypes.NotReady;
            }
        } 

        private void RefreshEnvelope()
        {
            var existing = Envelope;

            // turn is a litte interesting on the envelope, if we have it coming in we don't ovwrwrite it because on new turns it will be null
            Envelope = new Envelope(AgentContext?.Id ?? existing.AgentContextId, Role?.Id ?? existing.RoleId, Session?.Id ?? existing.SessionId, 
                                    PreviousTurn?.Id ?? existing.PreviousTurnId, ThisTurn?.Id ?? existing.ThisTurnId, existing.Instructions, existing.Stream, existing.ToolResults, existing.ClipBoardImages,
                                    existing.InputArtifacts, existing.RagScope, existing.Org, existing.User);
        }

        public void LogStepErrorDetails(IAdminLogger logger, PipelineSteps step, string error, TimeSpan ts)
        {
            logger.AddError($"[AgentPipelineContext__LogStepErrorDetails] - {step} Error restoring Agent Context.", error,
                CorrelationId.ToKVP("CorrelationId"),
                step.ToString().ToKVP("step"),
                Envelope.Org.Text.ToKVP("Org"),
                Envelope.User.Text.ToKVP("User"));
        }

        public void LogStepErrorDetails(IAdminLogger logger, PipelineSteps step, InvokeResult error, TimeSpan ts)
        {
            logger.AddError($"[AgentPipelineContext__LogStepErrorDetails] - {step} Error restoring Agent Context.", error.ErrorMessage,
                CorrelationId.ToKVP("CorrelationId"),
                step.ToString().ToKVP("step"),
                Envelope.Org.Text.ToKVP("Org"),
                Envelope.User.Text.ToKVP("User"));
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

            logger.Trace($"[AgentPipelineContext__LogDetails] - {step}.", kvps.ToArray());          
        }
    }

    public class Envelope
    {
        public Envelope(string agentContextId, string roleId, string sessionId, string previousTurnid, string thisTurnId, string instructions, bool stream,
                        IEnumerable<ToolResultSubmission> toolResults, IEnumerable<ClipboardImage> clipboardImages, IEnumerable<InputArtifact> inputArtifacts,
                        RagScope ragScope, EntityHeader org, EntityHeader user)
        {
            AgentContextId = agentContextId;
            RoleId = roleId;
            SessionId = sessionId;
            PreviousTurnId = previousTurnid;
            ThisTurnId = thisTurnId;
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
        public string RoleId { get; }
        public string SessionId { get; }
        public string PreviousTurnId { get; }
        public string ThisTurnId { get; set; }
        public EntityHeader Org { get; }
        public EntityHeader User { get; }
        public bool Stream { get; }
    }
}