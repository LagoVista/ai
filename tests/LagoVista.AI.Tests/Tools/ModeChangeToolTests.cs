using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
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
            _sessionManagerMock = new Mock<IAgentSessionManager>();
            _loggerMock = new Mock<IAdminLogger>();
            _tool = new ModeChangeTool(_sessionManagerMock.Object, _loggerMock.Object);
        }

        private static AgentToolExecutionContext CreateContext()
        {
            return new AgentToolExecutionContext
            {
                SessionId = "session-123",
                Org = EntityHeader.Create("org-1", "Org One"),
                User = EntityHeader.Create("user-1", "User One"),
                Request = new AgentExecuteRequest
                {
                    ConversationId = "conversation-456"
                }
            };
        }

        private sealed class ModeChangeResultDto
        {
            public bool Success { get; set; }
            public string Mode { get; set; }
            public bool Branch { get; set; }
            public string Reason { get; set; }
        }

        [Test]
        public async Task ExecuteAsync_ValidArgs_ChangesModeAndReturnsSuccess()
        {
            // Arrange
            var args = new
            {
                mode = "DDRAuthoring",
                branch = true,
                reason = "User requested DDR authoring."
            };
            var argsJson = JsonConvert.SerializeObject(args);
            var context = CreateContext();

            _sessionManagerMock.Setup(mct => mct.SetSessionModeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>()))
                .ReturnsAsync(InvokeResult.Success);

            // Act
            var result = await _tool.ExecuteAsync(argsJson, context, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null.And.Not.Empty);

            _sessionManagerMock.Verify(m => m.SetSessionModeAsync(
                    "session-123",
                    "DDRAuthoring",
                    "User requested DDR authoring.",
                    context.Org,
                    context.User),
                Times.Once);

            var payload = JsonConvert.DeserializeObject<ModeChangeResultDto>(result.Result);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Success, Is.True);
            Assert.That(payload.Mode, Is.EqualTo("DDRAuthoring"));
            Assert.That(payload.Branch, Is.True);
            Assert.That(payload.Reason, Is.EqualTo("User requested DDR authoring."));
        }

        [Test]
        public async Task ExecuteAsync_MissingMode_ReturnsErrorAndDoesNotCallSessionManager()
        {
            // Arrange
            var args = new
            {
                mode = "",
                branch = false,
                reason = "Some reason."
            };
            var argsJson = JsonConvert.SerializeObject(args);
            var context = CreateContext();

            // Act
            var result = await _tool.ExecuteAsync(argsJson, context, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("non-empty 'mode'"));

            _sessionManagerMock.Verify(m => m.SetSessionModeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>()),
                Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_MissingReason_ReturnsErrorAndDoesNotCallSessionManager()
        {
            // Arrange
            var args = new
            {
                mode = "DDRAuthoring",
                branch = false,
                reason = ""
            };
            var argsJson = JsonConvert.SerializeObject(args);
            var context = CreateContext();

            // Act
            var result = await _tool.ExecuteAsync(argsJson, context, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("non-empty 'reason'"));

            _sessionManagerMock.Verify(m => m.SetSessionModeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>()),
                Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_SessionManagerThrows_ReturnsErrorAndLogsException()
        {
            // Arrange
            var args = new
            {
                mode = "DDRAuthoring",
                branch = false,
                reason = "User requested DDR authoring."
            };
            var argsJson = JsonConvert.SerializeObject(args);
            var context = CreateContext();

            _sessionManagerMock
                .Setup(m => m.SetSessionModeAsync(
                    context.SessionId,
                    args.mode,
                    args.reason,
                    context.Org,
                    context.User))
                .ThrowsAsync(new System.Exception("boom"));

            // Act
            var result = await _tool.ExecuteAsync(argsJson, context, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("failed to change the session mode"));

            _sessionManagerMock.Verify(m => m.SetSessionModeAsync(
                    context.SessionId,
                    args.mode,
                    args.reason,
                    context.Org,
                    context.User),
                Times.Once);

            _loggerMock.Verify(l => l.AddException(
                    "[ModeChangeTool_ExecuteAsync__Exception]",
                    It.IsAny<System.Exception>()),
                Times.Once);
        }
    }
}
