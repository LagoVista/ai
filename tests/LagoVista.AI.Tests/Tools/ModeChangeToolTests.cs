using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Tools
{
    [TestFixture]
    public class ModeChangeToolTests
    {
        private Mock<IAgentSessionManager> _sessionManagerMock;
        private Mock<IAdminLogger> _loggerMock;
        private ModeChangeTool _tool;

        [SetUp]
        public void SetUp()
        {
            _sessionManagerMock = new Mock<IAgentSessionManager>(MockBehavior.Strict);
            _loggerMock = new Mock<IAdminLogger>(MockBehavior.Loose);

            _tool = new ModeChangeTool(_sessionManagerMock.Object, _loggerMock.Object);
        }

        private static Mock<IAgentPipelineContext> CreateContextMock(
            AgentSession session,
            string timeStamp = "2025-12-26T13:00:00.000Z",
            CancellationToken token = default)
        {
            var ctxMock = new Mock<IAgentPipelineContext>(MockBehavior.Strict);

            ctxMock.SetupGet(c => c.Session).Returns(session);
            ctxMock.SetupGet(c => c.TimeStamp).Returns(timeStamp);

            // These aren’t used by ModeChangeTool, but are part of the interface.
            // Keep them safely wired so future refactors don’t explode tests.
            ctxMock.SetupGet(c => c.CancellationToken).Returns(token);

            // Everything else: we intentionally do NOT set up, so if ModeChangeTool starts
            // touching more of the context unexpectedly, the test will fail loudly.
            return ctxMock;
        }

        private sealed class ModeChangeResultDto
        {
            public bool Success { get; set; }
            public string Mode { get; set; }
            public bool Branch { get; set; }
            public string Reason { get; set; }
        }

        [Test]
        public async Task ExecuteAsync_ValidArgs_UpdatesSession_AddsHistory_ReturnsSuccessPayload()
        {
            // Arrange
            var session = new AgentSession
            {
                Id = "sess_1",
                Mode = "Chat",
                ModeReason = "Initial",
                ModeSetTimestamp = "2025-12-25T10:00:00.000Z",
                LastUpdatedDate = "2025-12-25T10:00:00.000Z",
                ModeHistory = new List<ModeHistory>()
            };

            var ctxMock = CreateContextMock(session, timeStamp: "2025-12-26T13:00:00.000Z");
            var ctx = ctxMock.Object;

            var args = new
            {
                mode = "DDRAuthoring",
                branch = true,
                reason = "User requested DDR authoring."
            };

            var argsJson = JsonConvert.SerializeObject(args);

            // Act
            var result = await _tool.ExecuteAsync(argsJson, ctx);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null.And.Not.Empty);

            // Session mutated
            Assert.That(session.Mode, Is.EqualTo("DDRAuthoring"));
            Assert.That(session.ModeReason, Is.EqualTo("User requested DDR authoring."));
            Assert.That(session.ModeSetTimestamp, Is.EqualTo("2025-12-26T13:00:00.000Z"));
            Assert.That(session.LastUpdatedDate, Is.EqualTo("2025-12-26T13:00:00.000Z"));

            // History appended
            Assert.That(session.ModeHistory, Is.Not.Null);
            Assert.That(session.ModeHistory.Count, Is.EqualTo(1));

            var history = session.ModeHistory[0];
            Assert.That(history.PreviousMode, Is.EqualTo("Chat"));
            Assert.That(history.NewMode, Is.EqualTo("DDRAuthoring"));
            Assert.That(history.Reason, Is.EqualTo("User requested DDR authoring."));
            Assert.That(history.TimeStamp, Is.EqualTo("2025-12-26T13:00:00.000Z"));

            // Payload
            var payload = JsonConvert.DeserializeObject<ModeChangeResultDto>(result.Result);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Success, Is.True);
            Assert.That(payload.Mode, Is.EqualTo("DDRAuthoring"));
            Assert.That(payload.Branch, Is.True);
            Assert.That(payload.Reason, Is.EqualTo("User requested DDR authoring."));

            // Log success path
            _loggerMock.Verify(l => l.Trace(It.Is<string>(s =>
                    s.Contains("[ModeChangeTool_ExecuteAsync]") &&
                    s.Contains("Changed mode via tool") &&
                    s.Contains("Chat") &&
                    s.Contains("DDRAuthoring"))),
                Times.Once);

            _loggerMock.Verify(l => l.AddException(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);

            // Context usage: it should only need Session + TimeStamp
            ctxMock.VerifyGet(c => c.Session, Times.AtLeastOnce);
            ctxMock.VerifyGet(c => c.TimeStamp, Times.AtLeastOnce);
            ctxMock.VerifyNoOtherCalls();

            // This tool overload should not call session manager
            _sessionManagerMock.VerifyNoOtherCalls();
        }

        [Test]
        public async Task ExecuteAsync_EmptyArgs_ReturnsError_DoesNotMutateSessionOrHistory()
        {
            // Arrange
            var session = new AgentSession
            {
                Mode = "Chat",
                ModeReason = "Initial",
                ModeSetTimestamp = "A",
                LastUpdatedDate = "B",
                ModeHistory = new List<ModeHistory>()
            };

            var ctxMock = CreateContextMock(session);
            var ctx = ctxMock.Object;

            // Act
            var result = await _tool.ExecuteAsync("", ctx);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("requires a non-empty arguments object"));

            Assert.That(session.Mode, Is.EqualTo("Chat"));
            Assert.That(session.ModeReason, Is.EqualTo("Initial"));
            Assert.That(session.ModeSetTimestamp, Is.EqualTo("A"));
            Assert.That(session.LastUpdatedDate, Is.EqualTo("B"));
            Assert.That(session.ModeHistory.Count, Is.EqualTo(0));

            _loggerMock.Verify(l => l.Trace(It.IsAny<string>()), Times.Never);
            _loggerMock.Verify(l => l.AddException(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);

            // Should short-circuit before touching ctx.Session
            ctxMock.VerifyNoOtherCalls();
            _sessionManagerMock.VerifyNoOtherCalls();
        }

        [Test]
        public async Task ExecuteAsync_MissingMode_ReturnsError_DoesNotMutateSessionOrHistory()
        {
            // Arrange
            var session = new AgentSession
            {
                Mode = "Chat",
                ModeHistory = new List<ModeHistory>()
            };

            var ctxMock = CreateContextMock(session);
            var ctx = ctxMock.Object;

            var args = new
            {
                mode = "",
                branch = false,
                reason = "Some reason."
            };
            var argsJson = JsonConvert.SerializeObject(args);

            // Act
            var result = await _tool.ExecuteAsync(argsJson, ctx);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("non-empty 'mode'"));

            Assert.That(session.Mode, Is.EqualTo("Chat"));
            Assert.That(session.ModeHistory.Count, Is.EqualTo(0));

            _loggerMock.Verify(l => l.Trace(It.IsAny<string>()), Times.Never);
            _loggerMock.Verify(l => l.AddException(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);

            ctxMock.VerifyGet(c => c.TimeStamp, Times.Never);
            ctxMock.VerifyGet(c => c.Session, Times.Never);
            ctxMock.VerifyNoOtherCalls();

            _sessionManagerMock.VerifyNoOtherCalls();
        }

        [Test]
        public async Task ExecuteAsync_MissingBranch_ReturnsError_DoesNotMutateSessionOrHistory()
        {
            // Arrange
            var session = new AgentSession
            {
                Mode = "Chat",
                ModeHistory = new List<ModeHistory>()
            };

            var ctxMock = CreateContextMock(session);
            var ctx = ctxMock.Object;

            // branch omitted -> null
            var argsJson = @"{""mode"":""DDRAuthoring"",""reason"":""Because.""}";

            // Act
            var result = await _tool.ExecuteAsync(argsJson, ctx);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("requires a 'branch' boolean flag"));

            Assert.That(session.Mode, Is.EqualTo("Chat"));
            Assert.That(session.ModeHistory.Count, Is.EqualTo(0));

            _loggerMock.Verify(l => l.Trace(It.IsAny<string>()), Times.Never);
            _loggerMock.Verify(l => l.AddException(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);

            ctxMock.VerifyGet(c => c.TimeStamp, Times.Never);
            ctxMock.VerifyGet(c => c.Session, Times.Never);
            ctxMock.VerifyNoOtherCalls();

            _sessionManagerMock.VerifyNoOtherCalls();
        }

        [Test]
        public async Task ExecuteAsync_MissingReason_ReturnsError_DoesNotMutateSessionOrHistory()
        {
            // Arrange
            var session = new AgentSession
            {
                Mode = "Chat",
                ModeHistory = new List<ModeHistory>()
            };

            var ctxMock = CreateContextMock(session);
            var ctx = ctxMock.Object;

            var args = new
            {
                mode = "DDRAuthoring",
                branch = false,
                reason = ""
            };
            var argsJson = JsonConvert.SerializeObject(args);

            // Act
            var result = await _tool.ExecuteAsync(argsJson, ctx);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("non-empty 'reason'"));

            Assert.That(session.Mode, Is.EqualTo("Chat"));
            Assert.That(session.ModeHistory.Count, Is.EqualTo(0));

            _loggerMock.Verify(l => l.Trace(It.IsAny<string>()), Times.Never);
            _loggerMock.Verify(l => l.AddException(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);

            ctxMock.VerifyGet(c => c.TimeStamp, Times.Never);
            ctxMock.VerifyGet(c => c.Session, Times.Never);
            ctxMock.VerifyNoOtherCalls();

            _sessionManagerMock.VerifyNoOtherCalls();
        }

        [Test]
        public async Task ExecuteAsync_InvalidJson_ReturnsError_LogsException_DoesNotMutateSessionOrHistory()
        {
            // Arrange
            var session = new AgentSession
            {
                Mode = "Chat",
                ModeHistory = new List<ModeHistory>()
            };

            var ctxMock = CreateContextMock(session);
            var ctx = ctxMock.Object;

            var argsJson = "{ not valid json";

            // Act
            var result = await _tool.ExecuteAsync(argsJson, ctx);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("failed to change the session mode"));

            Assert.That(session.Mode, Is.EqualTo("Chat"));
            Assert.That(session.ModeHistory.Count, Is.EqualTo(0));

            _loggerMock.Verify(l => l.AddException(
                    "[ModeChangeTool_ExecuteAsync__Exception]",
                    It.IsAny<Exception>()),
                Times.Once);

            _loggerMock.Verify(l => l.Trace(It.IsAny<string>()), Times.Never);

            // It will have accessed Session/TimeStamp only if it got past validation;
            // in invalid JSON case it will enter try/catch, so it likely accessed neither.
            // We won’t over-constrain those reads here.
            ctxMock.VerifyNoOtherCalls();

            _sessionManagerMock.VerifyNoOtherCalls();
        }
    }
}
