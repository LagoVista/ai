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
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentToolExecutorTests
    {
        private Mock<IAgentToolRegistry> _registryMock;
        private Mock<IAdminLogger> _loggerMock;
        private AgentToolExecutor _executor;
        private AgentToolExecutionContext _context;

        [SetUp]
        public void SetUp()
        {
            _registryMock = new Mock<IAgentToolRegistry>(MockBehavior.Strict);
            _loggerMock = new Mock<IAdminLogger>(MockBehavior.Loose);

            _executor = new AgentToolExecutor(_registryMock.Object, _loggerMock.Object);

            _context = new AgentToolExecutionContext
            {
                AgentContext = new AgentContext { Id = "agent-1" },
                ConversationContext = new ConversationContext { Id = "conv-ctx-1" },
                Request = new AgentExecuteRequest { Mode = "ask", Instruction = "do something" },
                SessionId = "session-1",
                Org = EntityHeader.Create("org-1", "Org 1"),
                User = EntityHeader.Create("user-1", "User 1")
            };
        }

        [Test]
        public async Task ExecuteServerToolAsync_ToolNotRegistered_LeavesCallForClient()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "unregistered-tool",
                ArgumentsJson = "{\"foo\":\"bar\"}"
            };

            _registryMock.Setup(r => r.HasTool("unregistered-tool")).Returns(false);

            var updated = await _executor.ExecuteServerToolAsync(call, _context, CancellationToken.None);

            Assert.That(updated, Is.SameAs(call));
            Assert.That(updated.IsServerTool, Is.False);
            Assert.That(updated.WasExecuted, Is.False);
            Assert.That(updated.ResultJson, Is.Null);
            Assert.That(updated.ErrorMessage, Is.Null);

            _registryMock.Verify(r => r.HasTool("unregistered-tool"), Times.Once);
            _registryMock.Verify(r => r.GetTool(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteServerToolAsync_ToolRegistered_SuccessfulExecution_SetsResult()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "echo-tool",
                ArgumentsJson = "{\"msg\":\"hello\"}"
            };

            var toolMock = new Mock<IAgentTool>(MockBehavior.Strict);
            toolMock.Setup(t => t.Name).Returns("echo-tool");
            toolMock
                .Setup(t => t.ExecuteAsync(call.ArgumentsJson, It.IsAny<AgentToolExecutionContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<string>.Create("{\"ok\":true}"));

            _registryMock.Setup(r => r.HasTool("echo-tool")).Returns(true);
            _registryMock.Setup(r => r.GetTool("echo-tool")).Returns(InvokeResult<IAgentTool>.Create(toolMock.Object));

            var updated = await _executor.ExecuteServerToolAsync(call, _context, CancellationToken.None);

            Assert.That(updated.IsServerTool, Is.True);
            Assert.That(updated.WasExecuted, Is.True);
            Assert.That(updated.ResultJson, Is.EqualTo("{\"ok\":true}"));
            Assert.That(updated.ErrorMessage, Is.Null);

            _registryMock.Verify(r => r.HasTool("echo-tool"), Times.Once);
            _registryMock.Verify(r => r.GetTool("echo-tool"), Times.Once);
            toolMock.Verify(t => t.ExecuteAsync(call.ArgumentsJson, It.IsAny<AgentToolExecutionContext>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ExecuteServerToolAsync_ToolRegistered_FailedExecution_SetsError()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "failing-tool",
                ArgumentsJson = "{\"msg\":\"hello\"}"
            };

            var toolMock = new Mock<IAgentTool>(MockBehavior.Strict);
            toolMock.Setup(t => t.Name).Returns("failing-tool");
            toolMock
                .Setup(t => t.ExecuteAsync(call.ArgumentsJson, It.IsAny<AgentToolExecutionContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<string>.FromError("boom"));

            _registryMock.Setup(r => r.HasTool("failing-tool")).Returns(true);
            _registryMock.Setup(r => r.GetTool("failing-tool")).Returns(InvokeResult<IAgentTool>.Create(toolMock.Object));

            var updated = await _executor.ExecuteServerToolAsync(call, _context, CancellationToken.None);

            Assert.That(updated.IsServerTool, Is.True);
            Assert.That(updated.WasExecuted, Is.False);
            Assert.That(updated.ResultJson, Is.Null);
            Assert.That(updated.ErrorMessage, Is.EqualTo("boom"));

            _registryMock.Verify(r => r.HasTool("failing-tool"), Times.Once);
            _registryMock.Verify(r => r.GetTool("failing-tool"), Times.Once);
            toolMock.Verify(t => t.ExecuteAsync(call.ArgumentsJson, It.IsAny<AgentToolExecutionContext>(), It.IsAny<CancellationToken>()), Times.Once);
            _loggerMock.Verify(l => l.AddError(It.IsAny<string>(), It.Is<string>(m => m.Contains("boom")), It.IsAny<KeyValuePair<string, string>[]>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task ExecuteServerToolAsync_ToolThrowsException_SetsErrorMessage()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "throwing-tool",
                ArgumentsJson = "{}"
            };

            var toolMock = new Mock<IAgentTool>(MockBehavior.Strict);
            toolMock.Setup(t => t.Name).Returns("throwing-tool");
            toolMock
                .Setup(t => t.ExecuteAsync(call.ArgumentsJson, It.IsAny<AgentToolExecutionContext>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            _registryMock.Setup(r => r.HasTool("throwing-tool")).Returns(true);
            _registryMock.Setup(r => r.GetTool("throwing-tool")).Returns(InvokeResult<IAgentTool>.Create(toolMock.Object));

            var updated = await _executor.ExecuteServerToolAsync(call, _context, CancellationToken.None);

            Assert.That(updated.IsServerTool, Is.True);
            Assert.That(updated.WasExecuted, Is.False);
            Assert.That(updated.ResultJson, Is.Null);
            Assert.That(updated.ErrorMessage, Does.Contain("boom"));

            _loggerMock.Verify(l => l.AddException(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<KeyValuePair<string, string>[]>()), Times.AtLeastOnce);
        }
    }
}
