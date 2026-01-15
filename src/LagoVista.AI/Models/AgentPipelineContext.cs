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
        ClientToolCallSessionRestorer = 35,
        ClientToolContinuationResolver = 40,
        AgentSessionCreator = 50,
        AgentContextLoader = 60,
        AcpCommandHandler = 65,
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

        private string _instructions;

        public AgentPipelineContext(AgentExecuteRequest request, EntityHeader org, EntityHeader user, CancellationToken token = default)
        {
            if(request == null) throw new ArgumentNullException(nameof(request));
            if(org == null) throw new ArgumentNullException(nameof(org));
            if(user == null) throw new ArgumentNullException(nameof(user));

            _instructions = request.Instruction;

            var hasToolResults = request.ToolResults?.Any() ?? false;

            Envelope = new Envelope(request.AgentContextId, request.RoleId, request.SessionId, request.TurnId, null, request.AgentPersonaId, request.Instruction, 
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

        public void AttachNewChapterTurn(AgentSessionTurn newChapterTurn)
        {
            ThisTurn = newChapterTurn ?? throw new ArgumentNullException(nameof(newChapterTurn));
            RefreshEnvelope();
        }

        public void AttachToolManifest(ToolCallManifest toolManifest)
        {
            if (toolManifest == null) throw new ArgumentNullException(nameof(toolManifest));
            PromptKnowledgeProvider.ToolCallManifest = toolManifest;
        }

        public AgentSessionTurn ThisTurn { get; private set; }
        public AgentSessionTurn PreviousTurn { get; private set; }

        public void AttachAgentContext(AgentContext context, AgentContextRole role, AgentMode mode)
        {
            AgentContext = context ?? throw new ArgumentNullException(nameof(context));
            Role = role ?? throw new ArgumentNullException(nameof(role));
            Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            RefreshEnvelope();
        }

        public void SetInstructions(string instructions)
        {
            _instructions = instructions;
            RefreshEnvelope();
        }

        // Loaded context objects
        public AgentContext AgentContext { get; private set; }

        public AgentContextRole Role { get; private set; }

        public AgentMode Mode { get; private set; }

        public bool IsTerminal { get; private set;}
        public string IsTerminalReason { get; private set; }

        public void SetTerminal(string reason)
        {
            if(String.IsNullOrEmpty(reason))
            {
                throw new ArgumentNullException(nameof(reason));
            }

            IsTerminal = true;
            IsTerminalReason = reason;
        }

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
                    return ResponsePayload.AcpIntents.Any() ? ResponseTypes.ACP :  ResponseTypes.Final;
                }

                if(HasClientToolCalls)
                {
                    return ResponseTypes.ToolContinuation;
                }

                return ResponseTypes.NotReady;
            }
        }

        public IEnumerable<RagContent> RagContent { get; private set; } = new List<RagContent>();

        private void RefreshEnvelope()
        {
            var existing = Envelope;

            // turn is a litte interesting on the envelope, if we have it coming in we don't ovwrwrite it because on new turns it will be null
            Envelope = new Envelope(AgentContext?.Id ?? existing.AgentContextId, Role?.Id ?? existing.RoleId, Session?.Id ?? existing.SessionId, 
                                    PreviousTurn?.Id ?? existing.PreviousTurnId, ThisTurn?.Id ?? existing.ThisTurnId, existing.AgentPersonaId, _instructions, existing.Stream, existing.ToolResults, existing.ClipBoardImages,
                                    existing.InputArtifacts, existing.RagScope, existing.Org, existing.User);
        }

    

        public void AttachClientToolSession(AgentSession session, AgentSessionTurn turn)
        {
            // for client continuation we don't bump the turns;
            this.Session = session;
            this.ThisTurn = turn;
            this.PreviousTurn = turn;
        }

        public void AddRagContent(IEnumerable<RagContent> content)
        {
            RagContent = content;;
        }

        public void ClearRagConent()
        {
            RagContent = new List<RagContent>();
        }

        private List<string> _toolCalls = new List<string>();

        public int GetToolCallCount(string toolName)
        {
            return _toolCalls.Where(tc => tc == toolName).Count();
        }

        public void AddToolCall(string toolName)
        {
            _toolCalls.Add(toolName);
        }
    }

    public class Envelope
    {
        public Envelope(string agentContextId, string roleId, string sessionId, string previousTurnid, string thisTurnId, string agentPersonaId, string instructions, bool stream,
                        IEnumerable<ToolResultSubmission> toolResults, IEnumerable<ClipboardImage> clipboardImages, IEnumerable<InputArtifact> inputArtifacts,
                        RagScope ragScope, EntityHeader org, EntityHeader user)
        {
            AgentContextId = agentContextId;
            RoleId = roleId;
            SessionId = sessionId;
            PreviousTurnId = previousTurnid;
            AgentPersonaId = agentPersonaId;
            ThisTurnId = thisTurnId;
            Org = org ?? throw new ArgumentNullException(nameof(org));
            User = user ?? throw new ArgumentNullException(nameof(user));
            ToolResults = toolResults?.ToList() ?? new List<ToolResultSubmission>();
            ClipBoardImages = clipboardImages?.ToList() ?? new List<ClipboardImage>();
            InputArtifacts = inputArtifacts?.ToList() ?? new List<InputArtifact>();
            OriginalInstructions = instructions;
            RagScope = ragScope ?? new RagScope();
            Stream = stream;
        }

        public RagScope RagScope { get; }

        public IReadOnlyList<ToolResultSubmission> ToolResults { get; }
        public IReadOnlyList<ClipboardImage> ClipBoardImages { get; }
        public IReadOnlyList<InputArtifact> InputArtifacts { get; }

        public string OriginalInstructions { get; }
        public string AgentContextId { get; }
        public string AgentPersonaId { get; }
        public string RoleId { get; }
        public string SessionId { get; }
        public string PreviousTurnId { get; }
        public string ThisTurnId { get; set; }
        public EntityHeader Org { get; }
        public EntityHeader User { get; }
        public bool Stream { get; }
    }
}