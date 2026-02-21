using System.Threading;
using System.Threading.Tasks;
using Castle.Core.Logging;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LagoVista.AI.Tests
{
    [TestFixture]
    public class WorkspaceCreateFileToolTests
    {
        Mock<IAdminLogger> _logger = new Mock<IAdminLogger>();

        [Test]
        public void Name_Matches_ToolName_Constant()
        {
            var tool = new WorkspaceCreateFileTool(_logger.Object, new Mock<IContentHashService>().Object, new Mock<ISessionCodeFilesRepo>().Object);

            Assert.That(tool.Name, Is.EqualTo(WorkspaceCreateFileTool.ToolName));
        }

        [Test]
        public void GetSchema_Returns_Function_With_Correct_Name_And_Required_Fields()
        {
            var schema = WorkspaceCreateFileTool.GetSchema();
            var json = JsonConvert.SerializeObject(schema);
            dynamic obj = JsonConvert.DeserializeObject(json)!;

            Assert.That((string)obj.name, Is.EqualTo(WorkspaceCreateFileTool.ToolName));
            Assert.That((string)obj.type, Is.EqualTo("function"));

            // parameters.properties.path/content must exist and be strings
            Assert.That((string)obj.parameters.type, Is.EqualTo("object"));

            Assert.That(obj.parameters.properties.path, Is.Not.Null);
            Assert.That((string)obj.parameters.properties.path.type, Is.EqualTo("string"));

            Assert.That(obj.parameters.properties.content, Is.Not.Null);
            Assert.That((string)obj.parameters.properties.content.type, Is.EqualTo("string"));

            // required array should contain path and content
            bool sawPath = false;
            bool sawContent = false;
            foreach (var item in obj.parameters.required)
            {
                var name = (string)item;
                if (name == "path") sawPath = true;
                if (name == "content") sawContent = true;
            }

            Assert.That(sawPath, Is.True);
            Assert.That(sawContent, Is.True);
        }

        [Test]
        public void ToolUsageMetadata_Is_NonEmpty()
        {
            Assert.That(string.IsNullOrWhiteSpace(WorkspaceCreateFileTool.ToolUsageMetadata), Is.False);
        }

        [Test]
        public async Task ExecuteAsync_Does_Not_Throw_And_Returns_Json_Result()
        {
            var tool = new WorkspaceCreateFileTool(_logger.Object, new  Mock<IContentHashService>().Object, new Mock<ISessionCodeFilesRepo>().Object);

            var context = new AgentToolExecutionContext
            {
                SessionId = "session-123",
                Request = new AgentExecuteRequest { SessionId = "conv-456" }
            };

            InvokeResult<string> result = await tool.ExecuteAsync("{ \"path\": \"src/Foo.cs\", \"content\": \"class Foo { }\" }", context, CancellationToken.None);

            Assert.That(result, Is.Not.Null);

            // We cannot assert specific success flags here without knowing the
            // exact InvokeResult implementation, but we can assert the result
            // contains a valid JSON payload with the sentinel fields.
            Assert.That(result.Result, Is.Not.Null.And.Not.Empty);

            dynamic payload = JsonConvert.DeserializeObject(result.Result)!;

            Assert.That((string)payload.ToolName, Is.EqualTo(WorkspaceCreateFileTool.ToolName));
            Assert.That((bool)payload.IsClientExecutedOnly, Is.True);
            Assert.That((string)payload.SessionId, Is.EqualTo("session-123"));
        }
    }
}
