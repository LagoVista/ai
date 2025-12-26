using System;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Tools
{
    [TestFixture]
    public class ModeChangeToolTests
    {
        private Mock<IAdminLogger> _loggerMock;
        private Mock<IAgentSessionManager> _sessionManagerMock;
        private ModeChangeTool _tool;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<IAdminLogger>();
            _sessionManagerMock = new Mock<IAgentSessionManager>();

            _tool = new ModeChangeTool(_sessionManagerMock.Object, _loggerMock.Object);
        }

        private static AgentPipelineContext CreatePipelineContext(
            string initialMode = "Chat",
            DateTime? timestamp = null)
        {
            var ctx = new AgentPipelineContext();

            ctx.AttachSession(new AgentSession
            {
                Mode = initialMode,
                ModeReason = "Initial reason",
            }, new AgentSessionTurn() { });

            // Tool expects ModeHistory exists and can Add(...)
            // If your AgentSession constructor initializes it, this is harmless; if not, it's required.
            ctx.Session.ModeHistory ??= new System.Collections.Generic.List<ModeHistory>();

            return ctx;
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
            var ctx = CreatePipelineContext(initialMode: "Chat", timestamp: new DateTime(2025, 12, 26, 13, 0, 0, DateTimeKind.Utc));

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

            // Session updated
            Assert.That(ctx.Session.Mode, Is.EqualTo("DDRAuthoring"));
            Assert.That(ctx.Session.ModeReason, Is.EqualTo("User requested DDR authoring."));
            Assert.That(ctx.Session.ModeSetTimestamp, Is.EqualTo(ctx.TimeStamp));
            Assert.That(ctx.Session.LastUpdatedDate, Is.EqualTo(ctx.TimeStamp));

            // Mode history appended
            Assert.That(ctx.Session.ModeHistory, Is.Not.Null);
            Assert.That(ctx.Session.ModeHistory.Count, Is.EqualTo(1));

            var history = ctx.Session.ModeHistory[0];
            Assert.That(history.PreviousMode, Is.EqualTo("Chat"));
            Assert.That(history.NewMode, Is.EqualTo("DDRAuthoring"));
            Assert.That(history.Reason, Is.EqualTo("User requested DDR authoring."));
            Assert.That(history.TimeStamp, Is.EqualTo(ctx.TimeStamp));

            // Payload correct
            var payload = JsonConvert.DeserializeObject<ModeChangeResultDto>(result.Result);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Success, Is.True);
            Assert.That(payload.Mode, Is.EqualTo("DDRAuthoring"));
            Assert.That(payload.Branch, Is.True);
            Assert.That(payload.Reason, Is.EqualTo("User requested DDR authoring."));

            // Logging
            _loggerMock.Verify(l => l.Trace(It.Is<string>(s =>
                    s.Contains("ModeChangeTool_ExecuteAsync") &&
                    s.Contains("Changed mode via tool") &&
                    s.Contains("Chat") &&
                    s.Contains("DDRAuthoring"))),
                Times.Once);

            _loggerMock.Verify(l => l.AddException(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_EmptyArgumentsJson_ReturnsError_DoesNotMutateSession()
        {
            // Arrange
            var ctx = CreatePipelineContext(initialMode: "Chat");
            var beforeMode = ctx.Session.Mode;
            var beforeReason = ctx.Session.ModeReason;
            var beforeSetTs = ctx.Session.ModeSetTimestamp;
            var beforeUpdated = ctx.Session.LastUpdatedDate;
            var beforeHistoryCount = ctx.Session.ModeHistory.Count;

            // Act
            var result = await _tool.ExecuteAsync("", ctx);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("requires a non-empty arguments object"));

            Assert.That(ctx.Session.Mode, Is.EqualTo(beforeMode));
            Assert.That(ctx.Session.ModeReason, Is.EqualTo(beforeReason));
            Assert.That(ctx.Session.ModeSetTimestamp, Is.EqualTo(beforeSetTs));
            Assert.That(ctx.Session.LastUpdatedDate, Is.EqualTo(beforeUpdated));
            Assert.That(ctx.Session.ModeHistory.Count, Is.EqualTo(beforeHistoryCount));

            _loggerMock.Verify(l => l.Trace(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_MissingMode_ReturnsError_DoesNotMutateSessionOrHistory()
        {
            // Arrange
            var ctx = CreatePipelineContext(initialMode: "Chat");
            var beforeHistoryCount = ctx.Session.ModeHistory.Count;

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

            Assert.That(ctx.Session.Mode, Is.EqualTo("Chat"));
            Assert.That(ctx.Session.ModeHistory.Count, Is.EqualTo(beforeHistoryCount));

            _loggerMock.Verify(l => l.Trace(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_MissingBranch_ReturnsError_DoesNotMutateSessionOrHistory()
        {
            // Arrange
            var ctx = CreatePipelineContext(initialMode: "Chat");
            var beforeHistoryCount = ctx.Session.ModeHistory.Count;

            // branch omitted -> null
            var argsJson = @"{""mode"":""DDRAuthoring"",""reason"":""Because.""}";

            // Act
            var result = await _tool.ExecuteAsync(argsJson, ctx);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("requires a 'branch' boolean flag"));

            Assert.That(ctx.Session.Mode, Is.EqualTo("Chat"));
            Assert.That(ctx.Session.ModeHistory.Count, Is.EqualTo(beforeHistoryCount));

            _loggerMock.Verify(l => l.Trace(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_MissingReason_ReturnsError_DoesNotMutateSessionOrHistory()
        {
            // Arrange
            var ctx = CreatePipelineContext(initialMode: "Chat");
            var beforeHistoryCount = ctx.Session.ModeHistory.Count;

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

            Assert.That(ctx.Session.Mode, Is.EqualTo("Chat"));
            Assert.That(ctx.Session.ModeHistory.Count, Is.EqualTo(beforeHistoryCount));

            _loggerMock.Verify(l => l.Trace(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_InvalidJson_ReturnsError_LogsException()
        {
            // Arrange
            var ctx = CreatePipelineContext(initialMode: "Chat");
            var beforeHistoryCount = ctx.Session.ModeHistory.Count;

            var argsJson = "{ not valid json";

            // Act
            var result = await _tool.ExecuteAsync(argsJson, ctx);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("failed to change the session mode"));

            Assert.That(ctx.Session.Mode, Is.EqualTo("Chat"));
            Assert.That(ctx.Session.ModeHistory.Count, Is.EqualTo(beforeHistoryCount));

            _loggerMock.Verify(l => l.AddException(
                    "[ModeChangeTool_ExecuteAsync__Exception]",
                    It.IsAny<Exception>()),
                Times.Once);

            _loggerMock.Verify(l => l.Trace(It.IsAny<string>()), Times.Never);
        }
    }
}
