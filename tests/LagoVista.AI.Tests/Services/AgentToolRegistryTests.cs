using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentToolRegistryTests
    {
        private Mock<IAdminLogger> _logger;
        private AgentToolRegistry _sut;

        [SetUp]
        public void SetUp()
        {
            _logger = new Mock<IAdminLogger>();
            _sut = new AgentToolRegistry(_logger.Object);
        }

        #region Ctor Guards

        [Test]
        public void Ctor_NullLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AgentToolRegistry(null));
        }

        #endregion

        #region RegisterTool Contract Validation

        [Test]
        public void RegisterTool_MissingToolNameConst_ThrowsAndLogsError()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => _sut.RegisterTool<NoToolNameConstTool>());

            Assert.That(ex.Message, Does.Contain("must declare: public const string ToolName"));

            _logger.Verify(
                l => l.AddError(
                    "[AgentToolRegistry_RegisterTool__MissingToolNameConst]",
                    It.Is<string>(msg => msg.Contains("must declare: public const string ToolName"))),
                Times.Once);

            Assert.That(_sut.HasTool("tests_no_const"), Is.False);
        }

        [Test]
        public void RegisterTool_EmptyToolNameConst_ThrowsAndLogsError()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => _sut.RegisterTool<EmptyToolNameConstTool>());

            Assert.That(ex.Message, Does.Contain("declares an empty ToolName constant"));

            _logger.Verify(
                l => l.AddError(
                    "[AgentToolRegistry_RegisterTool__EmptyToolNameConst]",
                    It.Is<string>(msg => msg.Contains("declares an empty ToolName constant"))),
                Times.Once);

            Assert.That(_sut.HasTool(string.Empty), Is.False);
        }

        [Test]
        public void RegisterTool_MissingGetSchema_ThrowsAndLogsError()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => _sut.RegisterTool<MissingSchemaTool>());

            Assert.That(ex.Message, Does.Contain("must declare: public static object GetSchema"));

            _logger.Verify(
                l => l.AddError(
                    "[AgentToolRegistry_RegisterTool__MissingSchemaMethod]",
                    It.Is<string>(msg => msg.Contains("must declare: public static object GetSchema"))),
                Times.Once);

            Assert.That(_sut.HasTool(MissingSchemaTool.ToolName), Is.False);
        }

        [Test]
        public void RegisterTool_GetSchemaWrongReturnType_ThrowsAndLogsError()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => _sut.RegisterTool<WrongReturnTypeSchemaTool>());

            Assert.That(ex.Message, Does.Contain("must declare: public static object GetSchema"));

            _logger.Verify(
                l => l.AddError(
                    "[AgentToolRegistry_RegisterTool__MissingSchemaMethod]",
                    It.Is<string>(msg => msg.Contains("must declare: public static object GetSchema"))),
                Times.Once);

            Assert.That(_sut.HasTool(WrongReturnTypeSchemaTool.ToolName), Is.False);
        }


        [Test]
        public void RegisterTool_ToolWithInvalidToolNamePattern_ThrowsAndLogs()
        {
            // ToolName contains a dot, which violates ^[a-zA-Z0-9_-]+$
            var ex = Assert.Throws<InvalidOperationException>(
                () => _sut.RegisterTool<InvalidNameTool>());

            Assert.That(ex.Message, Does.Contain("does not match the required pattern"));

            _logger.Verify(l => l.AddError(
                    "[AgentToolRegistry_RegisterTool__InvalidToolNamePattern]",
                    It.Is<string>(msg => msg.Contains("invalid.name"))),
                Times.Once);
        }

        [Test]
        public void RegisterTool_GetSchemaWithParameters_ThrowsAndLogsError()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => _sut.RegisterTool<SchemaWithParametersTool>());

            Assert.That(ex.Message, Does.Contain("must declare: public static object GetSchema"));

            _logger.Verify(
                l => l.AddError(
                    "[AgentToolRegistry_RegisterTool__MissingSchemaMethod]",
                    It.Is<string>(msg => msg.Contains("must declare: public static object GetSchema"))),
                Times.Once);

            Assert.That(_sut.HasTool(SchemaWithParametersTool.ToolName), Is.False);
        }

        [Test]
        public void RegisterTool_DuplicateToolName_ThrowsAndLogsError_AndKeepsOriginal()
        {
            // First registration succeeds
            _sut.RegisterTool<ValidTool>();

            Assert.That(_sut.HasTool(ValidTool.ToolName), Is.True);
            Assert.That(_sut.GetToolType(ValidTool.ToolName), Is.EqualTo(typeof(ValidTool)));

            // Second registration with different type but same ToolName should throw
            var ex = Assert.Throws<InvalidOperationException>(
                () => _sut.RegisterTool<AnotherValidToolWithSameName>());

            Assert.That(ex.Message, Does.Contain("Duplicate IAgentTool name"));

            _logger.Verify(
                l => l.AddError(
                    "[AgentToolRegistry_RegisterTool__DuplicateToolName]",
                    It.Is<string>(msg => msg.Contains("Duplicate IAgentTool name"))),
                Times.Once);

            // Registry still points to first type
            Assert.That(_sut.HasTool(ValidTool.ToolName), Is.True);
            Assert.That(_sut.GetToolType(ValidTool.ToolName), Is.EqualTo(typeof(ValidTool)));
        }

        [Test]
        public void RegisterTool_ValidTool_RegistersTypeAndLogsTrace_NoErrors()
        {
            _sut.RegisterTool<ValidTool>();

            Assert.That(_sut.HasTool(ValidTool.ToolName), Is.True);
            Assert.That(_sut.GetToolType(ValidTool.ToolName), Is.EqualTo(typeof(ValidTool)));

            _logger.Verify(
                l => l.AddError(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never,
                "No errors expected for valid tool registration.");

            _logger.Verify(
                l => l.Trace(
                    It.Is<string>(msg => msg.Contains("[AgentToolRegistry_RegisterTool]") &&
                                         msg.Contains(ValidTool.ToolName) &&
                                         msg.Contains(typeof(ValidTool).FullName))),
                Times.Once);
        }

        #endregion

        #region Helper Tool Types

        private sealed class ValidTool : IAgentTool
        {
            public const string ToolName = "tests_valid_tool";

            public const string ToolUsageMetadata = "Valid Tool Meta Data";

            public string Name => ToolName;

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

        private sealed class AnotherValidToolWithSameName : IAgentTool
        {
            public const string ToolName = ValidTool.ToolName;
            public const string ToolUsageMetadata = "Valid Tool Meta Data";

            public string Name => ToolName;

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

        private sealed class NoToolNameConstTool : IAgentTool
        {
            // No ToolName const on purpose
            public string Name => "tests.no_const";

            public Task<InvokeResult<string>> ExecuteAsync(
                string argumentsJson,
                AgentToolExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(InvokeResult<string>.Create("{\"ok\":true}"));
            }
        }

        private sealed class EmptyToolNameConstTool : IAgentTool
        {
            public const string ToolName = "";

            public string Name => ToolName;

            public static object GetSchema()
            {
                return new { type = "function", name = ToolName };
            }

            public Task<InvokeResult<string>> ExecuteAsync(
                string argumentsJson,
                AgentToolExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(InvokeResult<string>.Create("{\"ok\":true}"));
            }
        }

        private sealed class MissingSchemaTool : IAgentTool
        {
            public const string ToolName = "tests_missing_schema";

            public string Name => ToolName;

            // Intentionally no GetSchema()

            public Task<InvokeResult<string>> ExecuteAsync(
                string argumentsJson,
                AgentToolExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(InvokeResult<string>.Create("{\"ok\":true}"));
            }
        }

        private sealed class WrongReturnTypeSchemaTool : IAgentTool
        {
            public const string ToolName = "tests_wrong_return_schema";

            public string Name => ToolName;

            public static string GetSchema()
            {
                return "{}";
            }

            public Task<InvokeResult<string>> ExecuteAsync(
                string argumentsJson,
                AgentToolExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(InvokeResult<string>.Create("{\"ok\":true}"));
            }
        }

        private sealed class SchemaWithParametersTool : IAgentTool
        {
            public const string ToolName = "tests_schema_with_params";

            public string Name => ToolName;

            public static object GetSchema(string someParam)
            {
                return new { type = "function", name = ToolName };
            }

            public Task<InvokeResult<string>> ExecuteAsync(
                string argumentsJson,
                AgentToolExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(InvokeResult<string>.Create("{\"ok\":true}"));
            }
        }

        #region Helper tool types

        private sealed class InvalidNameTool : IAgentTool
        {
            public const string ToolName = "invalid.name";

            public string Name => ToolName;

            public static object GetSchema()
            {
                // Schema shape doesn't matter here; we never get this far.
                return new { };
            }

            public Task<InvokeResult<string>> ExecuteAsync(
                string argumentsJson,
                AgentToolExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(InvokeResult<string>.Create("{}"));
            }
        }

        #endregion

        #endregion
    }
}
