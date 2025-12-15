using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services
{
    /// <summary>
    /// Orchestrates Aptix agent sessions and turns.
    ///
    /// Responsibilities:
    /// - Validate incoming requests for new sessions and follow-up turns.
    /// - Create sessions/turns via IAgentSessionFactory.
    /// - Persist sessions/turns via IAgentSessionManager.
    /// - Execute turns via IAgentTurnExecutor.
    /// - Publish high-level AptixOrchestratorEvent notifications.
    ///
    /// Heavy lifting (RAG, LLM, transcripts) is delegated to collaborators so
    /// this class remains easy to mock and test.
    /// </summary>
    public class AgentOrchestrator : IAgentOrchestrator
    {
        private readonly IAgentSessionManager _sessionManager;
        private readonly IAgentSessionFactory _sessionFactory;
        private readonly IAgentTurnExecutor _turnExecutor;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IAdminLogger _adminLogger;
        private readonly IAgentContextManager _contextManager;
        private readonly IAgentTurnTranscriptStore _transcriptStore;
        private readonly IAgentStreamingContext _agentStreamingContext;


        public AgentOrchestrator(IAgentSessionManager sessionManager, IAgentContextManager agentContextManager, IAgentTurnTranscriptStore agentTranscriptStore,
            IAgentSessionFactory sessionFactory, IAgentTurnExecutor turnExecutor, INotificationPublisher notificationPublisher, IAdminLogger adminLogger, IAgentStreamingContext agentStreamingContext)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            _turnExecutor = turnExecutor ?? throw new ArgumentNullException(nameof(turnExecutor));
            _notificationPublisher = notificationPublisher ?? throw new ArgumentNullException(nameof(notificationPublisher));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _contextManager = agentContextManager ?? throw new ArgumentNullException(nameof(agentContextManager));
            _transcriptStore = agentTranscriptStore ?? throw new ArgumentNullException(nameof(agentTranscriptStore));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
        }

        private async Task AddGeneralMode(AgentContext context, EntityHeader org, EntityHeader user)
        {

            var mode = new AgentMode()
            {
                Id = Guid.NewGuid().ToId(),
                Key = "general",
                DisplayName = "General Mode",
                Description = "General-purpose assistance for everyday Q&A, explanation, and lightweight help.",
                WhenToUse = "Use this mode for everyday Q&A, explanation, and lightweight assistance.",
                WelcomeMessage = "You are now in General mode. Use this mode for broad questions and lightweight assistance",
                ModeInstructions = new string[]
                {
                    "You are operating in General mode. Provide helpful and accurate responses to a wide range of user queries.",
                    "Focus on clarity and conciseness in your answers.",
                    "If you don't know the answer, admit it rather than making something up."
                },
                BehaviorHints = new string[]
                {
                    "preferConversationalTone",
                },
                HumanRoleHints = new string[]
                {
                    "The human is seeking general information and assistance."
                },
                AssociatedToolIds = new string[]
                { "agent_hello_world", "agent_hello_world_client", "add_agent_mode", "update_agent_mode" },
                ToolGroupHints = new string[]
                {
                    "general_tools",
                    "workspace"
                },
                RagScopeHints = new string[0],
                StrongSignals = new string[]
                {

                },
                WeakSignals = new string[]
                {
                },
                ExampleUtterances = new string[]
                {
                       "Review this PR diff and suggest improvements.",
                        "Does this function handle edge cases?",
                "Propose a minimal patch to fix naming and add a comment.",
                "Flag any security issues in this handler."
                },
                Status = "active",
                Version = "v1",
                IsDefault = true

            };

            context.AgentModes.Add(mode);
            await _contextManager.UpdateAgentContextAsync(context, org, user);
        }

        public async Task<InvokeResult<AgentExecuteResponse>> BeginNewSessionAsync(AgentExecuteRequest request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToId();

            _adminLogger.Trace("[AgentOrchestrator_BeginNewSessionAsync] Starting new session. " + $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}");

            if (request == null)
            {
                const string msg = "NewAgentExecutionSession cannot be null.";
                _adminLogger.AddError("[AgentOrchestrator_BeginNewSessionAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_ORCH_NULL_REQUEST");
            }

            if (request.AgentContext == null || EntityHeader.IsNullOrEmpty(request.AgentContext))
            {
                const string msg = "AgentContext is required.";
                _adminLogger.AddError("[AgentOrchestrator_BeginNewSessionAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_ORCH_MISSING_AGENT_CONTEXT");
            }

            if (string.IsNullOrWhiteSpace(request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentOrchestrator_BeginNewSessionAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_ORCH_MISSING_INSTRUCTION");
            }

            var context = await _contextManager.GetAgentContextWithSecretsAsync(request.AgentContext.Id, org, user);
            if(!context.AgentModes.Any(mode =>mode.Key == "general"))
            {
                await AddGeneralMode(context, org, user);
            }

            var session = await _sessionFactory.CreateSession(request, context, OperationKinds.Code, org, user);
            var turn = _sessionFactory.CreateTurnForNewSession(session, request, org, user);
           
            if(cancellationToken.IsCancellationRequested)
            {
                return InvokeResult<AgentExecuteResponse>.Abort();
            }

            await _sessionManager.AddAgentSessionAsync(session, org, user);
            await _sessionManager.AddAgentSessionTurnAsync(session.Id, turn, org, user);

            await PublishSessionStartedAsync(session, org, user);
            await PublishTurnCreatedAsync(session, turn, org, user);

            var stopwatch = Stopwatch.StartNew();
            await PublishTurnExecutionStartedAsync(session, turn, org, user);

            _adminLogger.Trace("[AgentOrchestrator_BeginNewSessionAsync] Session Ready. " + $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}");

            var requestEnvelope = new
            {
                OrgId = org?.Id,
                SessionId = session.Id,
                TurnId = turn.Id,
                Request = request,
            };

            var requestJson = JsonConvert.SerializeObject(requestEnvelope);
            var requestBlobResult = await _transcriptStore.SaveTurnRequestAsync(org.Id, session.Id, turn.Id, requestJson, cancellationToken);
            Console.WriteLine("[AgentOrchestrator_BeginNewSessionAsync] Session Ready 1");
            if (!requestBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteNewSessionTurnAsync__Transcript]", "Failed to store turn request transcript.");
                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(requestBlobResult.ToInvokeResult());
            }
            await _sessionManager.SetRequestBlobUriAsync(session.Id, turn.Id, requestBlobResult.Result.ToString(), org, user);

            await _agentStreamingContext.AddWorkflowAsync("...connected, let's get started...");

            var execResult = await _turnExecutor.ExecuteNewSessionTurnAsync(context, session, turn, request, org, user, cancellationToken);
            if(execResult.Aborted)
            {
                await _sessionManager.AbortTurnAsync(session.Id, turn.Id, org, user);
                return execResult;
            }

            if(!execResult.Successful)
            {
                return execResult;
            }
           
            execResult.Result.RequestEnvelopeUrl = requestBlobResult.Result.ToString();

            _adminLogger.Trace("[AgentOrchestrator_BeginNewSessionAsync] Session Completed. " + $"Success={execResult.Successful} correlationId={correlationId}, org={org?.Id}, user={user?.Id}");

            stopwatch.Stop();

            if (!execResult.Successful)
            {
                var warnings = execResult.Result?.Warnings ?? new List<string>();

                await _sessionManager.FailAgentSessionTurnAsync(session.Id, turn.Id, null, stopwatch.Elapsed.TotalMilliseconds, execResult.Errors.Select(er => er.Message).ToList(), warnings, org, user);

                await PublishTurnFailedAsync(session, turn, execResult, stopwatch.ElapsedMilliseconds, org, user);

                return execResult;
            }

            var response = execResult.Result;

            await _sessionManager.CompleteAgentSessionTurnAsync(session.Id, turn.Id, response.Text, response.FullResponseUrl, response.ResponseContinuationId, 
                response.Usage.PromptTokens, response.Usage.CompletionTokens, response.Usage.TotalTokens,
                stopwatch.Elapsed.TotalMilliseconds, response.Warnings, org, user);

            await PublishTurnCompletedAsync(session, turn, stopwatch.ElapsedMilliseconds, org, user);

            return execResult;
        }

        public async Task<InvokeResult<AgentExecuteResponse>> ExecuteTurnAsync(AgentExecuteRequest request, EntityHeader org, EntityHeader user, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToId();

            _adminLogger.Trace("[AgentOrchestrator_ExecuteTurnAsync] Starting follow-up turn. " + $"correlationId={correlationId}, org={org?.Id}, user={user?.Id}");

            if (request == null)
            {
                const string msg = "AgentExecutionRequest cannot be null.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteTurnAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_ORCH_NULL_REQUEST");
            }

            if (string.IsNullOrWhiteSpace(request.ConversationId))
            {
                const string msg = "ConversationId is required.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteTurnAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_ORCH_MISSING_SESSION_ID");
            }

            if (string.IsNullOrWhiteSpace(request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteTurnAsync__ValidateRequest]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_ORCH_MISSING_INSTRUCTION");
            }

            var session = await _sessionManager.GetAgentSessionAsync(request.ConversationId, org, user);

            if (session == null)
            {
                const string msg = "Session not found.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteTurnAsync__LoadSession]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_ORCH_SESSION_NOT_FOUND");
            }

            AgentSessionTurn previousTurn;
            if (!string.IsNullOrWhiteSpace(request.PreviousTurnId))
            {
                previousTurn = await _sessionManager.GetAgentSessionTurnAsync(request.ConversationId, request.PreviousTurnId, org, user);
            }
            else
            {
                previousTurn = await _sessionManager.GetLastAgentSessionTurnAsync(request.ConversationId, org, user);
            }

            if (previousTurn == null)
            {
                const string msg = "No previous turns found for this session.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteTurnAsync__PreviousTurn]", msg);

                return InvokeResult<AgentExecuteResponse>.FromError(msg, "AGENT_ORCH_NO_PREVIOUS_TURN");
            }

            var context = await _contextManager.GetAgentContextAsync(session.AgentContext.Id, org, user);
            if (!context.AgentModes.Any(mode => mode.Key == "general"))
            {
                await AddGeneralMode(context, org, user);
            }

            var turn = _sessionFactory.CreateTurnForExistingSession(session, request, org, user);

            turn.SequenceNumber = previousTurn.SequenceNumber + 1;
            turn.ConversationId = string.IsNullOrWhiteSpace(previousTurn.ConversationId) ? Guid.NewGuid().ToId() : previousTurn.ConversationId;
            turn.PreviousOpenAIResponseId = previousTurn.OpenAIResponseId;

            await _sessionManager.AddAgentSessionTurnAsync(session.Id, turn, org, user);

            await PublishTurnCreatedAsync(session, turn, org, user);

            var stopwatch = Stopwatch.StartNew();
            await PublishTurnExecutionStartedAsync(session, turn, org, user);

            var requestEnvelope = new
            {
                OrgId = org?.Id,
                SessionId = session.Id,
                TurnId = turn.Id,
                turn.PreviousOpenAIResponseId,
                Request = request,
            };

            var requestJson = JsonConvert.SerializeObject(requestEnvelope);
            var requestBlobResult = await _transcriptStore.SaveTurnRequestAsync(org.Id, session.Id, turn.Id, requestJson, cancellationToken);

            if (!requestBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteFollowupTurnAsync__Transcript]", "Failed to store turn request transcript.");
                return InvokeResult<AgentExecuteResponse>.FromInvokeResult(requestBlobResult.ToInvokeResult());
            }

            var execResult = await _turnExecutor.ExecuteFollowupTurnAsync(context, session, turn, request, org, user, cancellationToken);

            if (execResult.Aborted)
            {
                await _sessionManager.AbortTurnAsync(session.Id, turn.Id, org, user);
                return execResult;
            }

            stopwatch.Stop();

            if (!execResult.Successful)
            {
                var warnings = execResult.Result?.Warnings ?? new List<string>();

                await _sessionManager.FailAgentSessionTurnAsync(session.Id, turn.Id, null, stopwatch.Elapsed.TotalMilliseconds, execResult.Errors.Select(er => er.Message).ToList(), warnings, org, user);

                await PublishTurnFailedAsync(session, turn, execResult, stopwatch.ElapsedMilliseconds, org, user);

                return execResult;
            }

            var response = execResult.Result;

            await _sessionManager.CompleteAgentSessionTurnAsync(session.Id, turn.Id, response.Text, response.FullResponseUrl, response.ResponseContinuationId,
                  response.Usage.PromptTokens, response.Usage.CompletionTokens, response.Usage.TotalTokens,
                  stopwatch.Elapsed.TotalMilliseconds, response.Warnings, org, user);

            await PublishTurnCompletedAsync(session, turn, stopwatch.ElapsedMilliseconds, org, user);

            execResult.Result.RequestEnvelopeUrl = requestBlobResult.Result.ToString();

            return execResult;
        }

        private async Task PublishSessionStartedAsync(AgentSession session, EntityHeader org, EntityHeader user)
        {
            var evt = new AptixOrchestratorEvent
            {
                SessionId = session.Id,
                TurnId = null,
                Stage = "SessionStarted",
                Status = "pending",
                Message = "Session created.",
                ElapsedMs = null,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, session.Id, evt, NotificationVerbosity.Normal);
        }

        private async Task PublishTurnCreatedAsync(AgentSession session, AgentSessionTurn turn, EntityHeader org, EntityHeader user)
        {
            var evt = new AptixOrchestratorEvent
            {
                SessionId = session.Id,
                TurnId = turn.Id,
                Stage = "TurnCreated",
                Status = "pending",
                Message = "Turn created.",
                ElapsedMs = null,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, session.Id, evt, NotificationVerbosity.Normal);
        }

        private async Task PublishTurnExecutionStartedAsync(AgentSession session, AgentSessionTurn turn, EntityHeader org, EntityHeader user)
        {
            var evt = new AptixOrchestratorEvent
            {
                SessionId = session.Id,
                TurnId = turn.Id,
                Stage = "TurnExecutionStarted",
                Status = "in-progress",
                Message = "Executing turn.",
                ElapsedMs = null,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, session.Id, evt, NotificationVerbosity.Normal);
        }

        private async Task PublishTurnCompletedAsync(AgentSession session, AgentSessionTurn turn, long elapsedMilliseconds, EntityHeader org, EntityHeader user)
        {
            var evt = new AptixOrchestratorEvent
            {
                SessionId = session.Id,
                TurnId = turn.Id,
                Stage = "TurnCompleted",
                Status = "completed",
                Message = "Turn completed successfully.",
                ElapsedMs = elapsedMilliseconds,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, session.Id, evt, NotificationVerbosity.Normal);
        }

        private async Task PublishTurnFailedAsync(AgentSession session, AgentSessionTurn turn, InvokeResult<AgentExecuteResponse> execResult, long elapsedMilliseconds, EntityHeader org, EntityHeader user)
        {
            var message = execResult.Errors != null && execResult.Errors.Count > 0 ? execResult.Errors[0].Message : "Turn failed.";

            var evt = new AptixOrchestratorEvent
            {
                SessionId = session.Id,
                TurnId = turn.Id,
                Stage = "TurnFailed",
                Status = "failed",
                Message = message,
                ElapsedMs = elapsedMilliseconds,
                Timestamp = DateTime.UtcNow.ToJSONString()
            };

            await _notificationPublisher.PublishAsync(Targets.WebSocket, Channels.Entity, session.Id, evt, NotificationVerbosity.Normal);
        }
    }
}
