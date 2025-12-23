using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Tools
{
    [TestFixture]
    public class HelloWorldToolTests
    {
        private IAdminLogger _logger = new AdminLogger(new ConsoleLogWriter());
        private HelloWorldTool _tool;

        [SetUp]
        public void SetUp()
        {
            _tool = new HelloWorldTool(_logger);
        }

        private static AgentToolExecutionContext CreateContext()
        {
            return new AgentToolExecutionContext
            {
                SessionId = "session-123",
                Request = new AgentExecuteRequest
                {
                    SessionId = "conversation-456"
                }
            };
        }

        [Test]
        public async Task ExecuteAsync_WithValidName_ReturnsSuccessAndGreeting()
        {
            // Arrange
            var args = new { name = "Kevin" };
            var argsJson = JsonConvert.SerializeObject(args);
            var context = CreateContext();

            // Act
            var result = await _tool.ExecuteAsync(
                argsJson,
                context,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null.And.Not.Empty);

            // Use a strongly-typed DTO so we match the C# casing.
            var payload = JsonConvert.DeserializeObject<HelloWorldResultDto>(result.Result);
            Assert.That(payload, Is.Not.Null);

            Assert.That(payload.Message, Does.Contain("Hello, Kevin!"));
            Assert.That(payload.SessionId, Is.EqualTo("session-123"));
        }

        private sealed class HelloWorldResultDto
        {
            public string Message { get; set; }
            public string SessionId { get; set; }
        }

        [Test]
        public async Task ExecuteAsync_WithEmptyArgumentsJson_ReturnsError()
        {
            // Arrange
            var context = CreateContext();

            // Act
            var result = await _tool.ExecuteAsync(
                "   ",
                context,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage,
                Does.Contain("requires a non-empty arguments object"));
        }

        [Test]
        public async Task ExecuteAsync_WithMissingName_ReturnsError()
        {
            // Arrange
            var args = new { }; // no name
            var argsJson = JsonConvert.SerializeObject(args);
            var context = CreateContext();

            // Act
            var result = await _tool.ExecuteAsync(
                argsJson,
                context,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage,
                Does.Contain("requires a non-empty 'name' string"));
        }

        [Test]
        public void HelloWorldTool_RegistersSuccessfully()
        {
            // Arrange
            var registry = new AgentToolRegistry(_logger);

            // Act + Assert
            Assert.DoesNotThrow(() =>
            {
                registry.RegisterTool<HelloWorldTool>();
            });
        }

        [Test]
        public void GetSchema_ReturnsValidShape()
        {
            // Act
            var schemaObj = HelloWorldTool.GetSchema();
            var json = JsonConvert.SerializeObject(schemaObj);
            dynamic schema = JsonConvert.DeserializeObject<dynamic>(json);

            // Assert
            Assert.That((string)schema.type, Is.EqualTo("function"));
            Assert.That((string)schema.name, Is.EqualTo(HelloWorldTool.ToolName));

            Assert.That((string)schema.description,
                Does.Contain("greeting").Or.Contain("greeting message"));

            Assert.That((string)schema.parameters.type, Is.EqualTo("object"));

            // properties.name.type == "string"
            Assert.That((string)schema.parameters.properties.name.type,
                Is.EqualTo("string"));

            // required contains "name"
            bool hasNameRequired = false;
            foreach (var r in schema.parameters.required)
            {
                if ((string)r == "name")
                {
                    hasNameRequired = true;
                    break;
                }
            }

            Assert.That(hasNameRequired, Is.True,
                "Schema.required must contain 'name'.");
        }
    }
}
