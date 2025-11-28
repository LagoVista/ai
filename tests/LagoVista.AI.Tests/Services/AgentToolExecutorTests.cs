using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentToolExecutorTests
    {
        private Mock<IAgentToolFactory> _toolFactory;
        private Mock<IAgentToolRegistry> _toolRegistry;
        private Mock<IAdminLogger> _logger;
        private AgentToolExecutor _sut;

        [SetUp]
        public void SetUp()
        {
            _toolFactory = new Mock<IAgentToolFactory>();
            _toolRegistry = new Mock<IAgentToolRegistry>();
            _logger = new Mock<IAdminLogger>();

            _sut = new AgentToolExecutor(_toolFactory.Object, _toolRegistry.Object, _logger.Object);
        }

        [Test]
        public void Ctor_NullFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentToolExecutor(null, _toolRegistry.Object, _logger.Object));
        }

        [Test]
        public void Ctor_NullRegistry_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentToolExecutor(_toolFactory.Object, null, _logger.Object));
        }

        [Test]
        public void Ctor_NullLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentToolExecutor(_toolFactory.Object, _toolRegistry.Object, null));
        }

        [Test]
        public void ExecuteServerToolAsync_NullCall_Throws()
        {
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await _sut.ExecuteServerToolAsync(null, new AgentToolExecutionContext(), CancellationToken.None));
        }

        [Test]
        public async Task ExecuteServerToolAsync_EmptyName_SetsErrorAndDoesNotCallFactoryOrRegistry()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "  "
            };

            var context = new AgentToolExecutionContext();

            var result = await _sut.ExecuteServerToolAsync(call, context, CancellationToken.None);

            Assert.That(result.IsServerTool, Is.False);
            Assert.That(result.WasExecuted, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Tool call name is empty."));

            _toolRegistry.Verify(r => r.HasTool(It.IsAny<string>()), Times.Never);
            _toolFactory.Verify(f => f.GetTool(It.IsAny<string>()), Times.Never);

            _logger.Verify(
                l => l.AddError("[AgentToolExecutor_ExecuteServerToolAsync__EmptyName]", "Tool call name is empty."),
                Times.Once);
        }

        [Test]
        public async Task ExecuteServerToolAsync_UnknownTool_LeavesForClient()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "testing.ping_pong"
            };

            var context = new AgentToolExecutionContext();

            _toolRegistry.Setup(r => r.HasTool("testing.ping_pong")).Returns(false);

            var result = await _sut.ExecuteServerToolAsync(call, context, CancellationToken.None);

            Assert.That(result.IsServerTool, Is.False);
            Assert.That(result.WasExecuted, Is.False);
            Assert.That(result.ErrorMessage, Is.Null);

            _toolFactory.Verify(f => f.GetTool(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteServerToolAsync_FactoryError_SetsErrorAndDoesNotExecute()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "testing.ping_pong",
                ArgumentsJson = "{\"message\":\"hi\"}"
            };

            var context = new AgentToolExecutionContext();

            _toolRegistry.Setup(r => r.HasTool(call.Name)).Returns(true);

            _toolFactory
                .Setup(f => f.GetTool(call.Name))
                .Returns(InvokeResult<IAgentTool>.FromError("Factory failed", "AGENT_TOOL_CREATE_FAILED"));

            var result = await _sut.ExecuteServerToolAsync(call, context, CancellationToken.None);

            Assert.That(result.IsServerTool, Is.True);
            Assert.That(result.WasExecuted, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Factory failed"));

            _toolFactory.Verify(f => f.GetTool(call.Name), Times.Once);
        }

        [Test]
        public async Task ExecuteServerToolAsync_ToolExecutesSuccessfully_PopulatesResult()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "testing.ping_pong",
                ArgumentsJson = "{\"message\":\"hi\"}"
            };

            var context = new AgentToolExecutionContext();

            _toolRegistry.Setup(r => r.HasTool(call.Name)).Returns(true);

            var toolMock = new Mock<IAgentTool>();
            toolMock
                .Setup(t => t.ExecuteAsync(call.ArgumentsJson, context, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<string>.Create("{\"reply\":\"pong: hi\"}"));

            _toolFactory
                .Setup(f => f.GetTool(call.Name))
                .Returns(InvokeResult<IAgentTool>.Create(toolMock.Object));

            var result = await _sut.ExecuteServerToolAsync(call, context, CancellationToken.None);

            Assert.That(result.IsServerTool, Is.True);
            Assert.That(result.WasExecuted, Is.True);
            Assert.That(result.ErrorMessage, Is.Null);
            Assert.That(result.ResultJson, Is.EqualTo("{\"reply\":\"pong: hi\"}"));

            _toolFactory.Verify(f => f.GetTool(call.Name), Times.Once);
            toolMock.Verify(t => t.ExecuteAsync(call.ArgumentsJson, context, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ExecuteServerToolAsync_ToolExecutionFails_SetsErrorAndLogs()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "testing.ping_pong",
                ArgumentsJson = "{\"message\":\"hi\"}"
            };

            var context = new AgentToolExecutionContext();

            _toolRegistry.Setup(r => r.HasTool(call.Name)).Returns(true);

            var toolMock = new Mock<IAgentTool>();
            toolMock
                .Setup(t => t.ExecuteAsync(call.ArgumentsJson, context, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<string>.FromError("Tool failed"));

            _toolFactory
                .Setup(f => f.GetTool(call.Name))
                .Returns(InvokeResult<IAgentTool>.Create(toolMock.Object));

            var result = await _sut.ExecuteServerToolAsync(call, context, CancellationToken.None);

            Assert.That(result.IsServerTool, Is.True);
            Assert.That(result.WasExecuted, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Tool failed"));
            Assert.That(result.ResultJson, Is.Null);

            _logger.Verify(
                l => l.AddError(
                    "[AgentToolExecutor_ExecuteServerToolAsync__ToolFailed]",
                    It.Is<string>(msg => msg.Contains("Tool 'testing.ping_pong' execution failed: Tool failed"))),
                Times.Once);
        }
    }
}
