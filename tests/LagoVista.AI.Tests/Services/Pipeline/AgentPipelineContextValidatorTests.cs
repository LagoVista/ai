using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Models.Context;
using LagoVista.AI.Services.Pipeline;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace LagoVista.AI.Tests.Services.Pipeline
{
    [TestFixture]
    public class AgentPipelineContextValidatorTests
    {
        private sealed class TestPipelineContext : IAgentPipelineContext
        {
            public AgentPipelineContextTypes Type { get; set; }
            public string CorrelationId { get; set; } = "corr_1";
            public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

            public AgentSessionTurn ThisTurn { get; set; }
            public AgentSession Session { get; set; }

            public ResponsePayload ResponsePayload { get; set; }

            public AgentContext AgentContext { get; set; }
            public AgentContextRole Role { get; set; }
            public AgentMode Mode { get; set; }

            public void SetInstructions(string instructoins) { }

            public PromptKnowledgeProvider PromptKnowledgeProvider { get; set; } = new PromptKnowledgeProvider();
            public CompositionTrace Trace { get; set; } = new CompositionTrace();

            public ResponseTypes ResponseType { get; set; }

            public bool HasPendingToolCalls { get; set; }
            public bool HasClientToolCalls { get; set; }

            public string ToolManifestId { get; set; }

            public string TimeStamp { get; set; } = DateTime.UtcNow.ToString("o");
            public Envelope Envelope { get; set; }

            public AgentSessionTurn PreviousTurn { get; set; }

            public bool IsTerminal => throw new NotImplementedException();

            public AgentToolExecutionContext ToToolContext() => new AgentToolExecutionContext();

            public void AttachAgentContext(AgentContext context, AgentContextRole role, AgentMode mode)
            {
                AgentContext = context;
                Role = role;
            }

            public void AttachSession(AgentSession session, AgentSessionTurn turn)
            {
                Session = session;
                ThisTurn = turn;
            }

            public void AttachToolManifest(ToolCallManifest toolManifest)
            {
                PromptKnowledgeProvider.ToolCallManifest = toolManifest;
            }

            // Not used by these tests.
            public InvokeResult Validate(PipelineSteps step) => InvokeResult.Success;
            public void LogStepErrorDetails(LagoVista.IoT.Logging.Loggers.IAdminLogger logger, PipelineSteps step, string error, TimeSpan ts) { }
            public void LogStepErrorDetails(LagoVista.IoT.Logging.Loggers.IAdminLogger logger, PipelineSteps step, InvokeResult error, TimeSpan ts) { }
            public void LogDetails(LagoVista.IoT.Logging.Loggers.IAdminLogger logger, PipelineSteps step, TimeSpan? ts = null) { }

            public void SetResponsePayload(ResponsePayload payload)
            {
                ResponsePayload = payload;
            }

            public void AttachSession(AgentSession session, AgentSessionTurn previousSessoin, AgentSessionTurn thisTurn)
            {
                throw new NotImplementedException();
            }

            public void SetTerminal(string reason)
            {
                throw new NotImplementedException();
            }

            public void AttachClientToolSession(AgentSession session, AgentSessionTurn thisTurn)
            {
                throw new NotImplementedException();
            }
        }

        private static Envelope NewEnvelope(string agentContextId = null, string conversationContextId = null, string sessionId = null, string turnId = null, bool stream = false, IEnumerable<ToolResultSubmission> toolResults = null)
        {
            var org = EntityHeader.Create("org_1", "Org");
            var user = EntityHeader.Create("user_1", "User");

            return new Envelope(
                agentContextId,
                conversationContextId,
                sessionId,
                null,
                turnId,
                null,
                instructions: "hi",
                stream: stream,
                toolResults: toolResults,
                clipboardImages: null,
                inputArtifacts: null,
                ragScope: new RagScope(),
                org: org,
                user: user);
        }

        [Test]
        public void ValidateCore_NullCtx_ReturnsFailure()
        {
            var sut = new AgentPipelineContextValidator();
            var result = sut.ValidateCore(null, PipelineSteps.ResponseBuilder, false);
            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public void ValidateCore_MissingOrg_ReturnsFailure()
        {
            var sut = new AgentPipelineContextValidator();

            var ctx = new TestPipelineContext
            {
                Type = AgentPipelineContextTypes.Initial,
                TimeStamp = DateTime.UtcNow.ToString("o"),
                CorrelationId = "corr_1",
                Envelope = null
            };

            var result = sut.ValidateCore(ctx, PipelineSteps.RequestHandler, false);
            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public void ValidateCore_Initial_WithNoInputs_ReturnsFailure()
        {
            var sut = new AgentPipelineContextValidator();

            // Create an envelope with empty Instructions and no artifacts/clipboard.
            var org = EntityHeader.Create("org_1", "Org");
            var user = EntityHeader.Create("user_1", "User");
            var env = new Envelope(null, null, null, null, null, null, instructions: null, stream: false, toolResults: null, clipboardImages: null, inputArtifacts: null, ragScope: new RagScope(), org: org, user: user);

            var ctx = new TestPipelineContext
            {
                Type = AgentPipelineContextTypes.Initial,
                TimeStamp = DateTime.UtcNow.ToString("o"),
                CorrelationId = "corr_1",
                Envelope = env
            };

            var result = sut.ValidateCore(ctx, PipelineSteps.RequestHandler, false);
            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public void ValidateToolCallManifest_CountMismatch_ReturnsFailure()
        {
            var sut = new AgentPipelineContextValidator();

            var manifest = new ToolCallManifest
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall { ToolCallId = "tc1", Name = "t1", RequiresClientExecution = true, ArgumentsJson = "{}" }
                },
                ToolCallResults = new List<AgentToolCallResult>()
            };

            var result = sut.ValidateToolCallManifest(manifest);
            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public void ValidateToolCallManifest_NameMismatch_IsHardFail()
        {
            var sut = new AgentPipelineContextValidator();

            var manifest = new ToolCallManifest
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall { ToolCallId = "tc1", Name = "toolA", RequiresClientExecution = true, ArgumentsJson = "{}" }
                },
                ToolCallResults = new List<AgentToolCallResult>
                {
                    new AgentToolCallResult { ToolCallId = "tc1", Name = "toolB", RequiresClientExecution = true, ResultJson = "{\"ok\":true}", ExecutionMs = 1 }
                }
            };

            var result = sut.ValidateToolCallManifest(manifest);
            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public void ValidateToolCallManifest_MissingResultJson_IsHardFail()
        {
            var sut = new AgentPipelineContextValidator();

            var manifest = new ToolCallManifest
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall { ToolCallId = "tc1", Name = "toolA", RequiresClientExecution = true, ArgumentsJson = "{}" }
                },
                ToolCallResults = new List<AgentToolCallResult>
                {
                    new AgentToolCallResult { ToolCallId = "tc1", Name = "toolA", RequiresClientExecution = true, ResultJson = null, ErrorMessage = "boom", ExecutionMs = 1 }
                }
            };

            var result = sut.ValidateToolCallManifest(manifest);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void ValidateToolCallManifest_HappyPath_Succeeds()
        {
            var sut = new AgentPipelineContextValidator();

            var manifest = new ToolCallManifest
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall { ToolCallId = "tc1", Name = "toolA", RequiresClientExecution = true, ArgumentsJson = "{}" },
                    new AgentToolCall { ToolCallId = "tc2", Name = "toolB", RequiresClientExecution = true, ArgumentsJson = "{}" }
                },
                ToolCallResults = new List<AgentToolCallResult>
                {
                    new AgentToolCallResult { ToolCallId = "tc1", Name = "toolA", RequiresClientExecution = true, ResultJson = "{\"ok\":true}", ExecutionMs = 1 },
                    new AgentToolCallResult { ToolCallId = "tc2", Name = "toolB", RequiresClientExecution = true, ResultJson = "{\"ok\":true}", ExecutionMs = 2 }
                }
            };

            var result = sut.ValidateToolCallManifest(manifest);
            Assert.That(result.Successful, Is.True);
        }

        [Test]
        public void ValidatePost_SessionRestorer_TurnIdMustDifferFromEnvelopeTurnId()
        {
            var sut = new AgentPipelineContextValidator();

            var ctx = new TestPipelineContext
            {
                Type = AgentPipelineContextTypes.FollowOn,
                Envelope = NewEnvelope(sessionId: "sess_1", turnId: "turn_env"),
                Session = new AgentSession { Id = "sess_1", Mode = "general" },
                ThisTurn = new AgentSessionTurn { Id = "turn_env" }
            };

            var result = sut.ValidatePostStep(ctx, PipelineSteps.SessionRestorer);
            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public void ValidatePost_ClientToolContinuationResolver_TurnIdMustEqualEnvelopeTurnId()
        {
            var sut = new AgentPipelineContextValidator();

            var ctx = new TestPipelineContext
            {
                Type = AgentPipelineContextTypes.ClientToolCallContinuation,
                Envelope = NewEnvelope(sessionId: "sess_1", turnId: "turn_env", toolResults: new List<ToolResultSubmission> { new ToolResultSubmission() }),
                Session = new AgentSession { Id = "sess_1", Mode = "general" },
                ThisTurn = new AgentSessionTurn { Id = "turn_other" },
                PromptKnowledgeProvider = new PromptKnowledgeProvider()
                {
                    ToolCallManifest = new ToolCallManifest()
                }
            };

            var result = sut.ValidatePostStep(ctx, PipelineSteps.ClientToolContinuationResolver);
            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public void ValidatePre_ResponseBuilder_Final_RequiresResponsePayloadAndText()
        {
            var sut = new AgentPipelineContextValidator();

            var ctx = new TestPipelineContext
            {
                Type = AgentPipelineContextTypes.FollowOn,
                Envelope = NewEnvelope(sessionId: "sess_1", turnId: "turn_1"),
                Session = new AgentSession { Id = "sess_1", Mode = "general" },
                ThisTurn = new AgentSessionTurn { Id = "turn_1" },
                ResponseType = ResponseTypes.Final,
                ResponsePayload = new ResponsePayload { PrimaryOutputText = null }
            };

            var result = sut.ValidatePreStep(ctx, PipelineSteps.ResponseBuilder);
            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public void ValidatePre_ResponseBuilder_ToolContinuation_RequiresManifest()
        {
            var sut = new AgentPipelineContextValidator();

            var ctx = new TestPipelineContext
            {
                Type = AgentPipelineContextTypes.FollowOn,
                Envelope = NewEnvelope(sessionId: "sess_1", turnId: "turn_1"),
                Session = new AgentSession { Id = "sess_1", Mode = "general" },
                ThisTurn = new AgentSessionTurn { Id = "turn_1" },
                ResponseType = ResponseTypes.ToolContinuation,
                PromptKnowledgeProvider = new PromptKnowledgeProvider { ToolCallManifest = null }
            };

            var result = sut.ValidatePreStep(ctx, PipelineSteps.ResponseBuilder);
            Assert.That(result.Successful, Is.False);
        }
    }
}
