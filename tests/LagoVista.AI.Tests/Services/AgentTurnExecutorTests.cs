using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.AI.Interfaces;
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
        private Mock<IAgentExecutionService> _agentExecutionService;
        private Mock<IAgentTurnTranscriptStore> _transcriptStore;
        private Mock<IAdminLogger> _adminLogger;
        private AgentTurnExecutor _sut;

        [SetUp]
        public void SetUp()
        {
            _agentExecutionService = new Mock<IAgentExecutionService>();
            _transcriptStore = new Mock<IAgentTurnTranscriptStore>();
            _adminLogger = new Mock<IAdminLogger>();

            _sut = new AgentTurnExecutor(
                _agentExecutionService.Object,
                _transcriptStore.Object,
                _adminLogger.Object);
        }

        #region Ctor Guards

        [Test]
        public void Ctor_NullAgentExecutionService_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentTurnExecutor(null, _transcriptStore.Object, _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullTranscriptStore_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentTurnExecutor(_agentExecutionService.Object, null, _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullAdminLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentTurnExecutor(_agentExecutionService.Object, _transcriptStore.Object, null));
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
        public async Task ExecuteAsync_WhenMissingSession_ReturnsError()
        {
            var ctx = BuildContext();
            ctx.Session = null;

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("AGENT_TURN_MISSING_SESSION"));
        }

        #endregion

        #region ExecuteAsync - New Session

        [Test]
        public async Task ExecuteAsync_NewSession_Success_WritesResponseTranscript_AndSetsResponseFields()
        {
            var ctx = BuildContext();
            ctx.Request.ConversationId = null; // new session signal

            var execResponse = new AgentExecuteResponse
            {
                Text = "hello world",
                ResponseContinuationId = "resp-123"
            };

            _agentExecutionService
                .Setup(s => s.ExecuteAsync(ctx.Request, ctx.Org, ctx.User, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(execResponse));

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(ctx.Org.Id, ctx.Session.Id, ctx.Turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("https://www.test.ai/response")));

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(ctx.Response, Is.SameAs(execResponse));

            Assert.That(execResponse.FullResponseUrl, Is.EqualTo("https://www.test.ai/response"));
            Assert.That(execResponse.ConversationId, Is.EqualTo(ctx.Session.Id));
            Assert.That(execResponse.TurnId, Is.EqualTo(ctx.Turn.Id));

            _agentExecutionService.Verify(s => s.ExecuteAsync(ctx.Request, ctx.Org, ctx.User, It.IsAny<CancellationToken>()), Times.Once);
            _transcriptStore.Verify(t => t.SaveTurnResponseAsync(ctx.Org.Id, ctx.Session.Id, ctx.Turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_NewSession_ExecFails_ReturnsError_AndDoesNotWriteResponseTranscript()
        {
            var ctx = BuildContext();
            ctx.Request.ConversationId = null;

            _agentExecutionService
                .Setup(s => s.ExecuteAsync(ctx.Request, ctx.Org, ctx.User, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.FromError("exec-failed"));

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);

            _transcriptStore.Verify(
                t => t.SaveTurnResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_NewSession_SaveResponseFails_LogsErrorAndReturnsError()
        {
            var ctx = BuildContext();
            ctx.Request.ConversationId = null;

            var execResponse = new AgentExecuteResponse { Text = "answer" };

            _agentExecutionService
                .Setup(s => s.ExecuteAsync(ctx.Request, ctx.Org, ctx.User, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(execResponse));

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(ctx.Org.Id, ctx.Session.Id, ctx.Turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.FromError("failed-to-save-response"));

            var result = await _sut.ExecuteAsync(ctx);

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
            var ctx = BuildContext();
            ctx.Request.ConversationId = "conv-1";
            ctx.Request.ResponseContinuationId = "resp-cont-999";

            string capturedResponseJson = null;

            var execResponse = new AgentExecuteResponse
            {
                Text = "follow-up-answer",
                ResponseContinuationId = "resp-456"
            };

            _agentExecutionService
                .Setup(s => s.ExecuteAsync(ctx.Request, ctx.Org, ctx.User, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(execResponse));

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(ctx.Org.Id, ctx.Session.Id, ctx.Turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, string, CancellationToken>((o, sid, tid, json, ct) => capturedResponseJson = json)
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("https://www.test.ai/response")));

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(capturedResponseJson, Is.Not.Null.And.Not.Empty);

            dynamic envelope = JsonConvert.DeserializeObject(capturedResponseJson);
            Assert.That((string)envelope.ResponseId, Is.EqualTo("resp-cont-999"));
        }

        [Test]
        public async Task ExecuteAsync_Followup_SaveResponseFails_LogsErrorAndReturnsError()
        {
            var ctx = BuildContext();
            ctx.Request.ConversationId = "conv-1";

            var execResponse = new AgentExecuteResponse { Text = "answer" };

            _agentExecutionService
                .Setup(s => s.ExecuteAsync(ctx.Request, ctx.Org, ctx.User, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(execResponse));

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(ctx.Org.Id, ctx.Session.Id, ctx.Turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.FromError("failed-to-save-response"));

            var result = await _sut.ExecuteAsync(ctx);

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
                ConversationId = "conversation-1",
                PreviousOpenAIResponseId = null
            };

            var request = new AgentExecuteRequest
            {
                AgentContext = session.AgentContext,
                ConversationContext = session.ConversationContext,
                Mode = "ask",
                Instruction = "do something",
                ConversationId = turn.ConversationId,
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
                Session = session,
                Turn = turn,
                Request = request
            };
        }

        #endregion
    }
}
