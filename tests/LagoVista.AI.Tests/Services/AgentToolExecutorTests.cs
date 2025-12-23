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
                async () => await _sut.ExecuteServerToolAsync(null, new AgentPipelineContext()));
        }

        [Test]
        public async Task ExecuteServerToolAsync_EmptyName_SetsErrorAndDoesNotCallFactoryOrRegistry()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "  "
            };

            var context = new AgentPipelineContext();

            var result = await _sut.ExecuteServerToolAsync(call, context);

            // InvokeResult reflects error
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Tool call name is empty."));

            // Call object was mutated
            Assert.That(call.IsServerTool, Is.False);
            Assert.That(call.WasExecuted, Is.False);
            Assert.That(call.ErrorMessage, Is.EqualTo("Tool call name is empty."));
            Assert.That(call.RequiresClientExecution, Is.False);

            _toolRegistry.Verify(r => r.HasTool(It.IsAny<string>()), Times.Never);
            _toolFactory.Verify(f => f.GetTool(It.IsAny<string>()), Times.Never);

            _logger.Verify(
                l => l.AddError("[AgentToolExecutor_ExecuteServerToolAsync__EmptyName]", "Tool call name is empty."),
                Times.Once);
        }

        [Test]
        public async Task ExecuteServerToolAsync_UnknownTool_LeavesForClientAndReturnsErrorResult()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "testing_ping_pong"
            };

            var context = new AgentPipelineContext();

            _toolRegistry.Setup(r => r.HasTool(call.Name)).Returns(false);

            var result = await _sut.ExecuteServerToolAsync(call, context);

            // InvokeResult is an error (per current implementation)
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not registered as a server tool"));

            // Call is still marked as client-only / not executed
            Assert.That(call.IsServerTool, Is.False);
            Assert.That(call.WasExecuted, Is.False);
            Assert.That(call.RequiresClientExecution, Is.False); // NEW
            Assert.That(call.ErrorMessage, Is.Null);             // as today, or set it if you decide to


            _toolFactory.Verify(f => f.GetTool(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteServerToolAsync_FactoryError_SetsErrorAndDoesNotExecute()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "testing_ping_pong",
                ArgumentsJson = "{\"message\":\"hi\"}"
            };

            var context = new AgentPipelineContext();

            _toolRegistry.Setup(r => r.HasTool(call.Name)).Returns(true);

            _toolFactory
                .Setup(f => f.GetTool(call.Name))
                .Returns(InvokeResult<IAgentTool>.FromError("Factory failed", "AGENT_TOOL_CREATE_FAILED"));

            var result = await _sut.ExecuteServerToolAsync(call, context);

            // InvokeResult is error from factory
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Factory failed"));

            // Call flags
            Assert.That(call.IsServerTool, Is.False);          // no tool instance
            Assert.That(call.WasExecuted, Is.False);
            Assert.That(call.RequiresClientExecution, Is.False); // NEW
            Assert.That(call.ErrorMessage, Is.EqualTo("Factory failed"));

            _toolFactory.Verify(f => f.GetTool(call.Name), Times.Once);
        }

        [Test]
        public async Task ExecuteServerToolAsync_ToolExecutesSuccessfully_PopulatesResultAndReturnsSuccess()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "testing_ping_pong",
                ArgumentsJson = "{\"message\":\"hi\"}"
            };

            var context = new AgentPipelineContext();

            _toolRegistry.Setup(r => r.HasTool(call.Name)).Returns(true);

            var toolMock = new Mock<IAgentTool>();
            toolMock.Setup(t => t.IsToolFullyExecutedOnServer).Returns(true);
            toolMock
                .Setup(t => t.ExecuteAsync(call.ArgumentsJson, context))
                .ReturnsAsync(InvokeResult<string>.Create("{\"reply\":\"pong: hi\"}"));

            _toolFactory
                .Setup(f => f.GetTool(call.Name))
                .Returns(InvokeResult<IAgentTool>.Create(toolMock.Object));

            var result = await _sut.ExecuteServerToolAsync(call, context);

            // InvokeResult success
            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.SameAs(call));

            // Call mutated
            Assert.That(call.IsServerTool, Is.True);
            Assert.That(call.WasExecuted, Is.True);
            Assert.That(call.RequiresClientExecution, Is.False); // NEW
            Assert.That(call.ErrorMessage, Is.Null);
            Assert.That(call.ResultJson, Is.EqualTo("{\"reply\":\"pong: hi\"}"));

            _toolFactory.Verify(f => f.GetTool(call.Name), Times.Once);
            toolMock.Verify(
                t => t.ExecuteAsync(call.ArgumentsJson, context),
                Times.Once);
        }

        [Test]
        public async Task ExecuteServerToolAsync_ToolExecutionFails_SetsErrorLogsAndReturnsError()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "testing_ping_pong",
                ArgumentsJson = "{\"message\":\"hi\"}"
            };

            var context = new AgentPipelineContext();

            _toolRegistry.Setup(r => r.HasTool(call.Name)).Returns(true);

            var toolMock = new Mock<IAgentTool>();
            toolMock.Setup(t => t.IsToolFullyExecutedOnServer).Returns(true);
            toolMock
                 .Setup(t => t.ExecuteAsync(call.ArgumentsJson, context))
                .ReturnsAsync(InvokeResult<string>.FromError("Tool failed"));



            _toolFactory
                .Setup(f => f.GetTool(call.Name))
                .Returns(InvokeResult<IAgentTool>.Create(toolMock.Object));

            var result = await _sut.ExecuteServerToolAsync(call, context);

            // InvokeResult is error from tool execution
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Tool failed"));

            // Call mutated
            Assert.That(call.IsServerTool, Is.True);
            Assert.That(call.WasExecuted, Is.False);
            Assert.That(call.RequiresClientExecution, Is.False); // NEW
            Assert.That(call.ErrorMessage, Is.EqualTo("Tool failed"));
            Assert.That(call.ResultJson, Is.Null);


            _logger.Verify(
                l => l.AddError(
                    "[AgentToolExecutor_ExecuteServerToolAsync__ToolFailed]",
                    It.Is<string>(msg => msg.Contains("Tool 'testing_ping_pong' execution failed: Tool failed"))),
                Times.Once);
        }

        [Test]
        public async Task ExecuteServerToolAsync_ClientFinalTool_SetsRequiresClientExecutionTrue()
        {
            var call = new AgentToolCall
            {
                CallId = "call-1",
                Name = "apply_file_patch",
                ArgumentsJson = "{\"patchId\":\"123\"}"
            };

            var context = new AgentPipelineContext();

            _toolRegistry.Setup(r => r.HasTool(call.Name)).Returns(true);

            var toolMock = new Mock<IAgentTool>();

            // This is the key: tool is NOT fully executed on the server.
            toolMock.Setup(t => t.IsToolFullyExecutedOnServer).Returns(false);

            // Whatever your current signature is, adapt this:
            toolMock
                .Setup(t => t.ExecuteAsync(call.ArgumentsJson, context))
                .ReturnsAsync(InvokeResult<string>.Create("{\"normalizedPatchId\":\"123\"}"));

            _toolFactory
                .Setup(f => f.GetTool(call.Name))
                .Returns(InvokeResult<IAgentTool>.Create(toolMock.Object));

            var result = await _sut.ExecuteServerToolAsync(call, context);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.SameAs(call));

            Assert.That(call.IsServerTool, Is.True);
            Assert.That(call.WasExecuted, Is.True);

            // NEW invariant:
            Assert.That(call.RequiresClientExecution, Is.True);
            Assert.That(call.ErrorMessage, Is.Null);
            Assert.That(call.ResultJson, Is.EqualTo("{\"normalizedPatchId\":\"123\"}"));
        }

    }
}
