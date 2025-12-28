using System;
using System.Collections.Generic;
using System.Linq;
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
    public class DefaultServerToolSchemaProviderTests
    {
        private Mock<IAdminLogger> _logger;

        private AgentToolRegistry _registry;
        private DefaultServerToolSchemaProvider _sut;

        [SetUp]
        public void SetUp()
        {
            _logger = new Mock<IAdminLogger>();

            _registry = new AgentToolRegistry(_logger.Object);
            _sut = new DefaultServerToolSchemaProvider(_registry, _logger.Object);
        }

        #region Ctor Guards

        [Test]
        public void Ctor_NullRegistry_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new DefaultServerToolSchemaProvider(null, _logger.Object));
        }

        [Test]
        public void Ctor_NullLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new DefaultServerToolSchemaProvider(_registry, null));
        }

        #endregion

        #region GetToolSchemas

        [Test]
        public void GetToolSchemas_NoRegisteredTools_ReturnsEmptyList()
        {
            // Arrange
            var toolList = new[] { "tool1" };

            // Act
            var schemas = _sut.GetToolSchemas(toolList.ToList());

            // Assert
            Assert.That(schemas, Is.Not.Null);
            Assert.That(schemas.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetToolSchemas_SingleRegisteredTool_ReturnsSchemaFromTool()
        {
            // Arrange: register a valid tool that has a simple, testable schema.
            _registry.RegisterTool<SchemaReturningTool>();

            // Request the *actual* tool name that the registry uses as the key.
            var toolList = new[] { SchemaReturningTool.ToolName };

            // Act
            var schemas = _sut.GetToolSchemas(toolList.ToList());

            // Assert
            Assert.That(schemas, Is.Not.Null);
            Assert.That(schemas.Count, Is.EqualTo(1));

            Assert.That(schemas[0].Name, Is.EqualTo(SchemaReturningTool.ToolName));
        }

        [Test]
        public void GetToolSchemas_MultipleRegisteredTools_ReturnsAllSchemas()
        {
            // Arrange
            _registry.RegisterTool<SchemaReturningTool>();
            _registry.RegisterTool<AnotherSchemaReturningTool>();

            // Request both tool names explicitly.
            var toolList = new[]
            {
                SchemaReturningTool.ToolName,
                AnotherSchemaReturningTool.ToolName
            };

            // Act
            var schemas = _sut.GetToolSchemas(toolList.ToList());

            // Assert
            Assert.That(schemas, Is.Not.Null);
            Assert.That(schemas.Count, Is.EqualTo(2));

            // Order is not guaranteed, so use a set-style assertion.
            var schemaStrings = new HashSet<string>();
            foreach (var s in schemas)
            {
                schemaStrings.Add(s.Name);
            }

            Assert.That(schemaStrings, Does.Contain(SchemaReturningTool.ToolName));
            Assert.That(schemaStrings, Does.Contain(AnotherSchemaReturningTool.ToolName));
        }

        #endregion

        #region Helper Tool Types

        /// <summary>
        /// Minimal tool that satisfies the AgentToolRegistry contracts and returns a simple string schema.
        /// </summary>
        private sealed class SchemaReturningTool : IAgentTool
        {
            public const string ToolName = "tests_schema_tool";
            public const string ToolSummary = "A first test tool for schema retrieval.";

            public string Name => ToolName;

            public const string ToolUsageMetadata = "Valid Tool Meta Data";

            public bool IsToolFullyExecutedOnServer => true;

            public static OpenAiToolDefinition GetSchema()
            {
                return ToolSchema.Function(ToolName, "Creates a friendly greeting message using the user's name.", p =>
                {
                    p.String("name", "The user's name to include in the greeting.", required: true);
                });
            }
            
            public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

            public Task<InvokeResult<string>> ExecuteAsync(
                string argumentsJson,
                AgentToolExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(InvokeResult<string>.Create("{\"ok\":true}"));
            }
        }

        /// <summary>
        /// Second minimal tool so we can verify multiple schemas are returned.
        /// </summary>
        private sealed class AnotherSchemaReturningTool : IAgentTool
        {
            public const string ToolName = "tests_schema_tool_2";

            public const string ToolSummary = "A second test tool for schema retrieval.";

            public bool IsToolFullyExecutedOnServer => true;

            public const string ToolUsageMetadata = "Valid Tool Meta Data";

            public string Name => ToolName;

            public static OpenAiToolDefinition GetSchema()
            {
                return ToolSchema.Function(ToolName, "Creates a friendly greeting message using the user's name.", p =>
                {
                    p.String("name", "The user's name to include in the greeting.", required: true);
                });
            }
            public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context) => ExecuteAsync(argumentsJson, context.ToToolContext(), context.CancellationToken);

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
