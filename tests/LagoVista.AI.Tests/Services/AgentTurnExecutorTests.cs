using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentTurnExecutorTests
    {
        private Mock<IAgentExecutionService> _agentExecutionStep;
        private Mock<IAgentTurnTranscriptStore> _transcriptStore;
        private Mock<IAdminLogger> _adminLogger;

        private AgentTurnExecutor _sut;

        [SetUp]
        public void SetUp()
        {
            _agentExecutionStep = new Mock<IAgentExecutionService>(MockBehavior.Strict);
            _transcriptStore = new Mock<IAgentTurnTranscriptStore>(MockBehavior.Strict);
            _adminLogger = new Mock<IAdminLogger>(MockBehavior.Loose);

            _sut = new AgentTurnExecutor(
                _agentExecutionStep.Object,
                _transcriptStore.Object,
                _adminLogger.Object);
        }

        #region Ctor Guards

        [Test]
        public void Ctor_NullAgentExecutionStep_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentTurnExecutor(null, _transcriptStore.Object, _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullTranscriptStore_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentTurnExecutor(_agentExecutionStep.Object, null, _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullAdminLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentTurnExecutor(_agentExecutionStep.Object, _transcriptStore.Object, null));
        }

        #endregion

        #region ExecuteAsync - Context Guards

        [Test]
        public async Task ExecuteAsync_WhenCtxNull_ReturnsError()
        {
            var result = await _sut.ExecuteAsync(null);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("AGENT_TURN_NULL_CONTEXT"));
        }

        [Test]
        public async Task ExecuteAsync_WhenMissingRequest_ReturnsError()
        {
            var ctx = BuildContext();
            ctx.Request = null;

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("AGENT_TURN_MISSING_REQUEST"));
        }

        [Test]
        public async Task ExecuteAsync_WhenMissingOrg_ReturnsError()
        {
            var ctx = BuildContext();
            ctx.Org = null;

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("AGENT_TURN_MISSING_ORG"));
        }

        [Test]
        public async Task ExecuteAsync_WhenMissingUser_ReturnsError()
        {
            var ctx = BuildContext();
            ctx.User = null;

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("AGENT_TURN_MISSING_USER"));
        }

        [Test]
        public async Task ExecuteAsync_WhenMissingSession_ReturnsError()
        {
            var ctx = BuildContext();
            ctx.Session = null;

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("AGENT_TURN_MISSING_SESSION"));
        }

        [Test]
        public async Task ExecuteAsync_WhenMissingTurn_ReturnsError()
        {
            var ctx = BuildContext();
            ctx.Turn = null;

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("AGENT_TURN_MISSING_TURN"));
        }

        [Test]
        public async Task ExecuteAsync_WhenMissingSessionId_ReturnsError()
        {
            var ctx = BuildContext();
            ctx.Session.Id = "   ";

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("AGENT_TURN_MISSING_SESSION_ID"));
        }

        [Test]
        public async Task ExecuteAsync_WhenMissingTurnId_ReturnsError()
        {
            var ctx = BuildContext();
            ctx.Turn.Id = null;

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("AGENT_TURN_MISSING_TURN_ID"));
        }

        #endregion

        #region ExecuteAsync - New Session

        [Test]
        public async Task ExecuteAsync_NewSession_Success_WritesResponseTranscript_AndSetsResponseFields()
        {
            // Arrange
            var ctx = BuildContext();
            ctx.Request.SessionId = null; // new session signal

            var execResponse = new AgentExecuteResponse
            {
                Text = "hello world",
                ResponseContinuationId = "resp-123"
            };

            _agentExecutionStep
                .Setup(s => s.ExecuteAsync(ctx))
                .Callback<AgentPipelineContext>((c) => c.Response = execResponse)
                .ReturnsAsync((AgentPipelineContext c) => InvokeResult<AgentPipelineContext>.Create(c));

            string capturedResponseJson = null;

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(ctx.Org.Id, ctx.Session.Id, ctx.Turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, string, CancellationToken>((o, sid, tid, json, ct) => capturedResponseJson = json)
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("https://www.test.ai/response")));

            // Act
            var result = await _sut.ExecuteAsync(ctx);

            // Assert
            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(result.Result.Response, Is.SameAs(execResponse));
            Assert.That(ctx.Response, Is.SameAs(execResponse));

            Assert.That(execResponse.FullResponseUrl, Is.EqualTo("https://www.test.ai/response"));
            Assert.That(execResponse.SessionId, Is.EqualTo(ctx.Session.Id));
            Assert.That(execResponse.TurnId, Is.EqualTo(ctx.Turn.Id));

            Assert.That(capturedResponseJson, Is.Not.Null.And.Not.Empty);
            dynamic envelope = JsonConvert.DeserializeObject(capturedResponseJson);
            Assert.That((string)envelope.SessionId, Is.EqualTo(ctx.Session.Id));
            Assert.That((string)envelope.SessionId, Is.EqualTo(ctx.Session.Id));
            Assert.That((string)envelope.TurnId, Is.EqualTo(ctx.Turn.Id));
            Assert.That(envelope.Response, Is.Not.Null);

            _agentExecutionStep.Verify(s => s.ExecuteAsync(ctx), Times.Once);
            _transcriptStore.Verify(t => t.SaveTurnResponseAsync(ctx.Org.Id, ctx.Session.Id, ctx.Turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_NewSession_ExecFails_ReturnsError_AndDoesNotWriteResponseTranscript()
        {
            // Arrange
            var ctx = BuildContext();
            ctx.Request.SessionId = null;

            _agentExecutionStep
                .Setup(s => s.ExecuteAsync(ctx))
                .ReturnsAsync(InvokeResult<AgentPipelineContext>.FromError("exec-failed"));

            // Act
            var result = await _sut.ExecuteAsync(ctx);

            // Assert
            Assert.That(result.Successful, Is.False);

            _transcriptStore.Verify(
                t => t.SaveTurnResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_NewSession_ExecDoesNotSetResponse_ReturnsError_AndDoesNotWriteTranscript()
        {
            // Arrange
            var ctx = BuildContext();
            ctx.Request.SessionId = null;

            _agentExecutionStep
                .Setup(s => s.ExecuteAsync(ctx))
                .ReturnsAsync(InvokeResult<AgentPipelineContext>.Create(ctx)); // Response not set

            // Act
            var result = await _sut.ExecuteAsync(ctx);

            // Assert
            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("AGENT_TURN_MISSING_RESPONSE"));

            _transcriptStore.Verify(
                t => t.SaveTurnResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_NewSession_SaveResponseFails_LogsErrorAndReturnsError()
        {
            // Arrange
            var ctx = BuildContext();
            ctx.Request.SessionId = null;

            var execResponse = new AgentExecuteResponse { Text = "answer" };

            _agentExecutionStep
                .Setup(s => s.ExecuteAsync(ctx))
                .Callback<AgentPipelineContext>((c) => c.Response = execResponse)
                .ReturnsAsync((AgentPipelineContext c ) => InvokeResult<AgentPipelineContext>.Create(c));

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(ctx.Org.Id, ctx.Session.Id, ctx.Turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.FromError("failed-to-save-response"));

            // Act
            var result = await _sut.ExecuteAsync(ctx);

            // Assert
            Assert.That(result.Successful, Is.False);

            _adminLogger.Verify(l => l.AddError(
                    "[AgentTurnExecutor_ExecuteNewSessionTurnAsync__Transcript]",
                    "Failed to store turn response transcript."),
                Times.Once);
        }

        #endregion

        #region ExecuteAsync - Followup

        [Test]
        public async Task ExecuteAsync_Followup_Success_WritesResponseEnvelopeIncludingResponseId()
        {
            // Arrange
            var ctx = BuildContext();
            ctx.Request.SessionId = "conv-1";
            ctx.Request.ResponseContinuationId = "resp-cont-999";

            var execResponse = new AgentExecuteResponse
            {
                Text = "follow-up-answer",
                ResponseContinuationId = "resp-456"
            };

            _agentExecutionStep
                .Setup(s => s.ExecuteAsync(ctx))
                .Callback<AgentPipelineContext>((c) => c.Response = execResponse)
                .ReturnsAsync((AgentPipelineContext c) => InvokeResult<AgentPipelineContext>.Create(c));

            string capturedResponseJson = null;

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(ctx.Org.Id, ctx.Session.Id, ctx.Turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, string, CancellationToken>((o, sid, tid, json, ct) => capturedResponseJson = json)
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("https://www.test.ai/response")));

            // Act
            var result = await _sut.ExecuteAsync(ctx);

            // Assert
            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(capturedResponseJson, Is.Not.Null.And.Not.Empty);

            dynamic envelope = JsonConvert.DeserializeObject(capturedResponseJson);
            Assert.That((string)envelope.ResponseId, Is.EqualTo("resp-cont-999"));

            Assert.That(execResponse.FullResponseUrl, Is.EqualTo("https://www.test.ai/response"));
            Assert.That(execResponse.SessionId, Is.EqualTo(ctx.Session.Id));
            Assert.That(execResponse.TurnId, Is.EqualTo(ctx.Turn.Id));
        }

        [Test]
        public async Task ExecuteAsync_Followup_SaveResponseFails_LogsErrorAndReturnsError()
        {
            // Arrange
            var ctx = BuildContext();
            ctx.Request.SessionId = "conv-1";
            ctx.Request.ResponseContinuationId = "resp-cont-999";

            var execResponse = new AgentExecuteResponse { Text = "answer" };

            _agentExecutionStep
                .Setup(s => s.ExecuteAsync(ctx))
                .Callback<AgentPipelineContext>((c) => c.Response = execResponse)
                .ReturnsAsync((AgentPipelineContext c) => InvokeResult<AgentPipelineContext>.Create(c));

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(ctx.Org.Id, ctx.Session.Id, ctx.Turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.FromError("failed-to-save-response"));

            // Act
            var result = await _sut.ExecuteAsync(ctx);

            // Assert
            Assert.That(result.Successful, Is.False);

            _adminLogger.Verify(l => l.AddError(
                    "[AgentTurnExecutor_ExecuteFollowupTurnAsync__Transcript]",
                    "Failed to store turn response transcript."),
                Times.Once);
        }

        #endregion

        #region Helpers

        private static AgentPipelineContext BuildContext()
        {
            var org = new EntityHeader { Id = "org-1", Text = "Org 1" };
            var user = new EntityHeader { Id = "user-1", Text = "User 1" };

            var session = new AgentSession
            {
                Id = "session-1",
                AgentContext = new EntityHeader(),
                ConversationContext = new EntityHeader(),
                Repo = "repo-name",
                DefaultLanguage = "csharp",
                WorkspaceId = "workspace-1"
            };

            var turn = new AgentSessionTurn
            {
                Id = "turn-1",
                SessionId = "conversation-1",
                PreviousOpenAIResponseId = null
            };

            var request = new AgentExecuteRequest
            {
                AgentContext = session.AgentContext,
                ConversationContext = session.ConversationContext,
                Mode = "ask",
                Instruction = "do something",
                SessionId = turn.SessionId,
                Repo = session.Repo,
                Language = session.DefaultLanguage,
                WorkspaceId = session.WorkspaceId,
                ActiveFiles = new List<ActiveFile>(),
                RagScopeFilter = new RagScopeFilter()
            };

            return new AgentPipelineContext
            {
                CorrelationId = "corr-1",
                Org = org,
                User = user,
                AgentContext = new AgentContext(),
                ConversationContext = new ConversationContext(),
                Session = session,
                Turn = turn,
                Request = request
            };
        }

        #endregion
    }
}
