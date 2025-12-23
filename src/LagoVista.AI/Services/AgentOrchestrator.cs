using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
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
    /// - Delegate turn execution to downstream pipeline step.
    /// - Publish high-level AptixOrchestratorEvent notifications.
    ///
    /// Heavy lifting (RAG, LLM, transcripts) is delegated to collaborators so
    /// this class remains easy to mock and test.
    /// </summary>
    public class AgentOrchestrator : IAgentOrchestrator
    {
        private readonly IAgentSessionManager _sessionManager;
        private readonly IAgentSessionFactory _sessionFactory;
        private readonly IAgentTurnExecutor _next;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IAdminLogger _adminLogger;
        private readonly IAgentContextManager _contextManager;
        private readonly IAgentTurnTranscriptStore _transcriptStore;
        private readonly IAgentStreamingContext _agentStreamingContext;

        public AgentOrchestrator(
            IAgentSessionManager sessionManager,
            IAgentContextManager agentContextManager,
            IAgentTurnTranscriptStore agentTranscriptStore,
            IAgentSessionFactory sessionFactory,
            IAgentTurnExecutor next,
            INotificationPublisher notificationPublisher,
            IAdminLogger adminLogger,
            IAgentStreamingContext agentStreamingContext)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _notificationPublisher = notificationPublisher ?? throw new ArgumentNullException(nameof(notificationPublisher));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
            _contextManager = agentContextManager ?? throw new ArgumentNullException(nameof(agentContextManager));
            _transcriptStore = agentTranscriptStore ?? throw new ArgumentNullException(nameof(agentTranscriptStore));
            _agentStreamingContext = agentStreamingContext ?? throw new ArgumentNullException(nameof(agentStreamingContext));
        }


        public async Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx)
        {
            if (ctx == null)
            {
                return InvokeResult<AgentPipelineContext>.FromError("AgentPipelineContext cannot be null.", "AGENT_ORCH_NULL_CONTEXT");
            }

            var request = ctx.Request;

            if (request == null)
            {
                const string msg = "AgentExecutionRequest cannot be null.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteAsync__ValidateRequest]", msg);
                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_ORCH_NULL_REQUEST");
            }

            // Routing rule: new session if ConversationId is empty (request handler already normalizes this).
            var isNewSession = string.IsNullOrWhiteSpace(request.ConversationId);

            if (isNewSession)
            {
                return await ExecuteNewSessionAsync(ctx);
            }

            return await ExecuteFollowupTurnAsync(ctx);
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
                ModeInstructionDdrs = new string[]
                {
                    "You are operating in General mode. Provide helpful and accurate responses to a wide range of user queries.",
                    "Focus on clarity and conciseness in your answers.",
                    "If you don't know the answer, admit it rather than making something up."
                },
                BehaviorHints = new string[] { "preferConversationalTone" },
                HumanRoleHints = new string[] { "The human is seeking general information and assistance." },
                AssociatedToolIds = new string[] { "agent_hello_world", "agent_hello_world_client", "add_agent_mode", "update_agent_mode" },
                ToolGroupHints = new string[] { "general_tools", "workspace" },
                RagScopeHints = new string[0],
                StrongSignals = new string[] { },
                WeakSignals = new string[] { },
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

        private async Task<InvokeResult<AgentPipelineContext>> ExecuteNewSessionAsync(
            AgentPipelineContext ctx)
        {
            var correlationId = ctx.CorrelationId ?? Guid.NewGuid().ToId();

            _adminLogger.Trace("[AgentOrchestrator_ExecuteNewSessionAsync] Starting new session. " +
                               $"correlationId={correlationId}, org={ctx.Org?.Id}, user={ctx.User?.Id}");

            if (ctx.Request.AgentContext == null || EntityHeader.IsNullOrEmpty(ctx.Request.AgentContext))
            {
                const string msg = "AgentContext is required.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteNewSessionAsync__ValidateRequest]", msg);
                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_ORCH_MISSING_AGENT_CONTEXT");
            }

            if (string.IsNullOrWhiteSpace(ctx.Request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteNewSessionAsync__ValidateRequest]", msg);
                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_ORCH_MISSING_INSTRUCTION");
            }

            var context = await _contextManager.GetAgentContextWithSecretsAsync(ctx.Request.AgentContext.Id, ctx.Org, ctx.User);
            if (!context.AgentModes.Any(mode => mode.Key == "general"))
            {
                await AddGeneralMode(context, ctx.Org, ctx.User);
            }

            ctx.AgentContext = context;

            var session = await _sessionFactory.CreateSession(ctx.Request, context, OperationKinds.Code, ctx.Org, ctx.User);
            var turn = _sessionFactory.CreateTurnForNewSession(session, ctx.Request, ctx.Org, ctx.User);

            ctx.Session = session;
            ctx.Turn = turn;

            ctx.Request.CurrentTurnId = turn.Id;

            if (ctx.CancellationToken.IsCancellationRequested)
            {
                return InvokeResult<AgentPipelineContext>.Abort();
            }

            await _sessionManager.AddAgentSessionAsync(session, ctx.Org, ctx.User);
            await _sessionManager.AddAgentSessionTurnAsync(session.Id, turn, ctx.Org, ctx.User);

            await PublishSessionStartedAsync(session);
            await PublishTurnCreatedAsync(session, turn);

            var stopwatch = Stopwatch.StartNew();
            await PublishTurnExecutionStartedAsync(session, turn);

            _adminLogger.Trace("[AgentOrchestrator_ExecuteNewSessionAsync] Session Ready. " +
                               $"correlationId={correlationId}, org={ctx.Org?.Id}, user={ctx.User?.Id}");

            var requestEnvelope = new
            {
                OrgId = ctx.Org?.Id,
                SessionId = session.Id,
                TurnId = turn.Id,
                Request = ctx.Request,
            };

            var requestJson = JsonConvert.SerializeObject(requestEnvelope);
            var requestBlobResult = await _transcriptStore.SaveTurnRequestAsync(ctx.Org.Id, session.Id, turn.Id, requestJson, ctx.CancellationToken);

            if (!requestBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteNewSessionTurnAsync__Transcript]", "Failed to store turn request transcript.");
                return InvokeResult<AgentPipelineContext>.FromInvokeResult(requestBlobResult.ToInvokeResult());
            }

            await _sessionManager.SetRequestBlobUriAsync(session.Id, turn.Id, requestBlobResult.Result.ToString(), ctx.Org, ctx.User);

            await _agentStreamingContext.AddWorkflowAsync("...connected, let's get started...");

            var downstream = await _next.ExecuteAsync(ctx);
            if (downstream.Aborted)
            {
                await _sessionManager.AbortTurnAsync(session.Id, turn.Id, ctx.Org, ctx.User);
                return downstream;
            }

            stopwatch.Stop();

            if (!downstream.Successful)
            {
                var warnings = ctx.Response?.Warnings ?? new List<string>();

                await _sessionManager.FailAgentSessionTurnAsync(
                    session.Id,
                    turn.Id,
                    null,
                    stopwatch.Elapsed.TotalMilliseconds,
                    downstream.Errors.Select(er => er.Message).ToList(),
                    warnings,
                    ctx.Org,
                    ctx.User);

                await PublishTurnFailedAsync(session, turn, downstream.Transform<AgentExecuteResponse>(), stopwatch.ElapsedMilliseconds);

                return downstream;
            }

            // Downstream must set ctx.Response on success.
            if (ctx.Response != null)
            {
                ctx.Response.RequestEnvelopeUrl = requestBlobResult.Result.ToString();
            }

            _adminLogger.Trace("[AgentOrchestrator_ExecuteNewSessionAsync] Session Completed. " +
                               $"Success={downstream.Successful} correlationId={correlationId}, org={ctx.Org?.Id}, user={ctx.User?.Id}");

            var response = ctx.Response;

            await _sessionManager.CompleteAgentSessionTurnAsync(
                session.Id,
                turn.Id,
                response.Text,
                response.FullResponseUrl,
                response.ResponseContinuationId,
                response.Usage.PromptTokens,
                response.Usage.CompletionTokens,
                response.Usage.TotalTokens,
                stopwatch.Elapsed.TotalMilliseconds,
                response.Warnings,
                ctx.Org,
                ctx.User);

            await PublishTurnCompletedAsync(session, turn, stopwatch.ElapsedMilliseconds);

            return InvokeResult<AgentPipelineContext>.Create(ctx);
        }

        private async Task<InvokeResult<AgentPipelineContext>> ExecuteFollowupTurnAsync(
            AgentPipelineContext ctx)
        {
            var correlationId = ctx.CorrelationId ?? Guid.NewGuid().ToId();

            _adminLogger.Trace("[AgentOrchestrator_ExecuteFollowupTurnAsync] Starting follow-up turn. " +
                               $"correlationId={correlationId}, org={ctx.Org?.Id}, user={ctx.User?.Id}");

            if (string.IsNullOrWhiteSpace(ctx.Request.ConversationId))
            {
                const string msg = "ConversationId is required.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteFollowupTurnAsync__ValidateRequest]", msg);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_ORCH_MISSING_SESSION_ID");
            }

            if (string.IsNullOrWhiteSpace(ctx.Request.Instruction))
            {
                const string msg = "Instruction is required.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteFollowupTurnAsync__ValidateRequest]", msg);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_ORCH_MISSING_INSTRUCTION");
            }

            var session = await _sessionManager.GetAgentSessionAsync(ctx.Request.ConversationId, ctx.Org, ctx.User);
            ctx.Session = session;

            if (session == null)
            {
                const string msg = "Session not found.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteFollowupTurnAsync__LoadSession]", msg);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_ORCH_SESSION_NOT_FOUND");
            }

            AgentSessionTurn previousTurn;
            if (!string.IsNullOrWhiteSpace(ctx.Request.PreviousTurnId))
            {
                previousTurn = await _sessionManager.GetAgentSessionTurnAsync(ctx.Request.ConversationId, ctx.Request.PreviousTurnId, ctx.Org, ctx.User);
            }
            else
            {
                previousTurn = await _sessionManager.GetLastAgentSessionTurnAsync(ctx.Request.ConversationId, ctx.Org, ctx.User);
            }

            if (previousTurn == null)
            {
                const string msg = "No previous turns found for this session.";
                _adminLogger.AddError("[AgentOrchestrator_ExecuteFollowupTurnAsync__PreviousTurn]", msg);

                return InvokeResult<AgentPipelineContext>.FromError(msg, "AGENT_ORCH_NO_PREVIOUS_TURN");
            }

            var context = await _contextManager.GetAgentContextAsync(session.AgentContext.Id, ctx.Org, ctx.User);
            if (!context.AgentModes.Any(mode => mode.Key == "general"))
            {
                await AddGeneralMode(context, ctx.Org, ctx.User);
            }

            ctx.AgentContext = context;

            var turn = _sessionFactory.CreateTurnForExistingSession(session, ctx.Request, ctx.Org, ctx.User);
            ctx.Turn = turn;
            ctx.Request.CurrentTurnId = turn.Id;

            turn.SequenceNumber = previousTurn.SequenceNumber + 1;
            turn.ConversationId = string.IsNullOrWhiteSpace(previousTurn.ConversationId) ? Guid.NewGuid().ToId() : previousTurn.ConversationId;
            turn.PreviousOpenAIResponseId = previousTurn.OpenAIResponseId;

            await _sessionManager.AddAgentSessionTurnAsync(session.Id, turn, ctx.Org, ctx.User);

            await PublishTurnCreatedAsync(session, turn);

            var stopwatch = Stopwatch.StartNew();
            await PublishTurnExecutionStartedAsync(session, turn);

            var requestEnvelope = new
            {
                OrgId = ctx.Org?.Id,
                SessionId = session.Id,
                TurnId = turn.Id,
                turn.PreviousOpenAIResponseId,
                Request = ctx.Request,
            };

            var requestJson = JsonConvert.SerializeObject(requestEnvelope);
            var requestBlobResult = await _transcriptStore.SaveTurnRequestAsync(ctx.Org.Id, session.Id, turn.Id, requestJson, ctx.CancellationToken);

            if (!requestBlobResult.Successful)
            {
                _adminLogger.AddError("[AgentTurnExecutor_ExecuteFollowupTurnAsync__Transcript]", "Failed to store turn request transcript.");
                return InvokeResult<AgentPipelineContext>.FromInvokeResult(requestBlobResult.ToInvokeResult());
            }

            var downstream = await _next.ExecuteAsync(ctx);

            if (downstream.Aborted)
            {
                await _sessionManager.AbortTurnAsync(session.Id, turn.Id, ctx.Org, ctx.User);
                return downstream;
            }

            stopwatch.Stop();

            if (!downstream.Successful)
            {
                var warnings = ctx.Response?.Warnings ?? new List<string>();

                await _sessionManager.FailAgentSessionTurnAsync(
                    session.Id,
                    turn.Id,
                    null,
                    stopwatch.Elapsed.TotalMilliseconds,
                    downstream.Errors.Select(er => er.Message).ToList(),
                    warnings,
                    ctx.Org,
                    ctx.User);

                await PublishTurnFailedAsync(session, turn, downstream.Transform<AgentExecuteResponse>(), stopwatch.ElapsedMilliseconds);

                return downstream;
            }

            if (ctx.Response != null)
            {
                ctx.Response.RequestEnvelopeUrl = requestBlobResult.Result.ToString();
            }

            var response = ctx.Response;

            await _sessionManager.CompleteAgentSessionTurnAsync(
                session.Id,
                turn.Id,
                response.Text,
                response.FullResponseUrl,
                response.ResponseContinuationId,
                response.Usage.PromptTokens,
                response.Usage.CompletionTokens,
                response.Usage.TotalTokens,
                stopwatch.Elapsed.TotalMilliseconds,
                response.Warnings,
                ctx.Org,
                ctx.User);

            await PublishTurnCompletedAsync(session, turn, stopwatch.ElapsedMilliseconds);

            return InvokeResult<AgentPipelineContext>.Create(ctx);
        }

        private async Task PublishSessionStartedAsync(AgentSession session)
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

        private async Task PublishTurnCreatedAsync(AgentSession session, AgentSessionTurn turn)
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

        private async Task PublishTurnExecutionStartedAsync(AgentSession session, AgentSessionTurn turn)
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

        private async Task PublishTurnCompletedAsync(AgentSession session, AgentSessionTurn turn, long elapsedMilliseconds)
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

        private async Task PublishTurnFailedAsync(AgentSession session, AgentSessionTurn turn, InvokeResult<AgentExecuteResponse> execResult, long elapsedMilliseconds)
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
