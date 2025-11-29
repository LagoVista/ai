using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentToolFactoryTests
    {
        private Mock<IAdminLogger> _logger;
      
        [SetUp]
        public void SetUp()
        {
            _logger = new Mock<IAdminLogger>();
        }

        #region Ctor Guards

        [Test]
        public void Ctor_NullServiceProvider_Throws()
        {
            var registry = new AgentToolRegistry(_logger.Object);

            Assert.Throws<ArgumentNullException>(
                () => new AgentToolFactory(null, registry, _logger.Object));
        }

        [Test]
        public void Ctor_NullRegistry_Throws()
        {
            var services = new ServiceCollection();
            var sp = services.BuildServiceProvider();

            Assert.Throws<ArgumentNullException>(
                () => new AgentToolFactory(sp, null, _logger.Object));
        }

        [Test]
        public void Ctor_NullLogger_Throws()
        {
            var services = new ServiceCollection();
            var sp = services.BuildServiceProvider();
            var registry = new AgentToolRegistry(new Mock<IAdminLogger>().Object);

            Assert.Throws<ArgumentNullException>(
                () => new AgentToolFactory(sp, registry, null));
        }

        #endregion

        #region GetTool Scenarios

        [Test]
        public void GetTool_EmptyName_ReturnsErrorAndLogs()
        {
            var services = new ServiceCollection();
            var sp = services.BuildServiceProvider();

            var registry = new AgentToolRegistry(_logger.Object);
            var factory = new AgentToolFactory(sp, registry, _logger.Object);

            var result = factory.GetTool("   ");

            Assert.That(result.Successful, Is.False);
           Assert.That(result.ErrorMessage, Does.Contain("Tool name is required."));

            _logger.Verify(
                l => l.AddError(
                    "[AgentToolRegistry_GetTool__EmptyName]",
                    It.Is<string>(msg => msg.Contains("Tool name is required."))),
                Times.Once);
        }

        [Test]
        public void GetTool_NotRegistered_ReturnsNotFoundErrorAndLogs()
        {
            var services = new ServiceCollection();
            var sp = services.BuildServiceProvider();

            var registry = new AgentToolRegistry(_logger.Object);
            var factory = new AgentToolFactory(sp, registry, _logger.Object);

            var result = factory.GetTool("tests.unknown");

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("is not registered in AgentToolRegistry"));

            _logger.Verify(
                l => l.AddError(
                    "[AgentToolRegistry_GetTool__NotFound]",
                    It.Is<string>(msg => msg.Contains("Tool 'tests.unknown' is not registered"))),
                Times.Once);
        }



        [Test]
        public void GetTool_RegisteredInDI_ResolvesAndReturnsInstance()
        {
            var services = new ServiceCollection();
            services.AddTransient<FakeTool>();
            var sp = services.BuildServiceProvider();

            var registry = new AgentToolRegistry(_logger.Object);
            registry.RegisterTool<FakeTool>();

            var factory = new AgentToolFactory(sp, registry, _logger.Object);

            var result = factory.GetTool(FakeTool.ToolName);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);
            Assert.That(result.Result, Is.InstanceOf<FakeTool>());
        }

        [Test]
        public void GetTool_RegisteredInDI_ConstructorThrows_ReturnsExceptionErrorAndLogs()
        {
            var services = new ServiceCollection();
            services.AddTransient<ExplodingTool>();
            var sp = services.BuildServiceProvider();

            var registry = new AgentToolRegistry(_logger.Object);
            registry.RegisterTool<ExplodingTool>();

            var factory = new AgentToolFactory(sp, registry, _logger.Object);

            var result = factory.GetTool(ExplodingTool.ToolName);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Exception while creating tool 'tests_exploding_tool'"));

            _logger.Verify(
                l => l.AddException(
                    "[AgentToolRegistry_GetTool__Exception]",
                    It.IsAny<Exception>()),
                Times.Once);
        }

        #endregion

        #region Helper Tool Types

        /// <summary>
        /// Simple fake tool type used only for DI/factory tests.
        /// </summary>
        private sealed class FakeTool : IAgentTool
        {
            public string Name => FakeTool.ToolName;

            public const string ToolName = "tests_fake_tool";

            public const string ToolUsageMetadata = "fake tool meta data";

            public static object GetSchema()
            {
                return new
                {
                    type = "function",
                    name = ToolName,
                    parameters = new { type = "object" }
                };
            }

            public Task<InvokeResult<string>> ExecuteAsync(
                string argumentsJson,
                AgentToolExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(InvokeResult<string>.Create("ok"));
            }
        }

        private sealed class ExplodingTool : IAgentTool
        {
            public const string ToolName = "tests_exploding_tool";

            public string Name => ToolName;

            public const string ToolUsageMetadata = "Exploding tool meta data";


            public ExplodingTool(IAdminLogger logger)
            {
                // Throws during construction to simulate DI failure.
                throw new InvalidOperationException("Boom from ExplodingTool ctor.");
            }

            public static object GetSchema()
            {
                return new
                {
                    type = "function",
                    name = ToolName,
                    parameters = new { type = "object" }
                };
            }

            public Task<InvokeResult<string>> ExecuteAsync(
                string argumentsJson,
                AgentToolExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(InvokeResult<string>.Create("{\"ok\":true}"));
            }
        }

        #endregion
    }
}
