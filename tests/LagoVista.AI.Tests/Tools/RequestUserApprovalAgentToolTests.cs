using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LagoVista.AI.AgentTools.Tests
{
    [TestFixture]
    public class RequestUserApprovalAgentToolTests
    {
        private Mock<IAdminLogger> _logger;
        private AgentToolExecutionContext _context;

        [SetUp]
        public void SetUp()
        {
            _logger = new Mock<IAdminLogger>();

            _context = new AgentToolExecutionContext
            {
                Org = EntityHeader.Create("org-id", "Org"),
                User = EntityHeader.Create("user-id", "User")
            };
        }

        [Test]
        public async Task ExecuteAsync_MissingArgumentsJson_ReturnsError()
        {
            var tool = new RequestUserApprovalAgentTool(_logger.Object);

            var result = await tool.ExecuteAsync(string.Empty, _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("argumentsJson must not be empty for request_user_approval."));
        }

        [Test]
        public async Task ExecuteAsync_MissingPrompt_ReturnsError()
        {
            var tool = new RequestUserApprovalAgentTool(_logger.Object);

            var payload = new JObject();

            var result = await tool.ExecuteAsync(payload.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("prompt is required for request_user_approval."));
        }

        [Test]
        public async Task ExecuteAsync_ValidPayload_ReturnsUserApprovalUnavailableEnvelope()
        {
            var tool = new RequestUserApprovalAgentTool(_logger.Object);

            var payload = new JObject
            {
                ["prompt"] = "Approve changing SYS-001 status to Approved?",
                ["context"] = new JObject
                {
                    ["ddr_identifier"] = "SYS-001",
                    ["chapter_id"] = "ch-1",
                    ["action"] = "Approve the DDR and capture approver metadata."
                }
            };

            var result = await tool.ExecuteAsync(payload.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);

            var envelope = JObject.Parse(result.Result);

            Assert.That(envelope.Value<bool>("ok"), Is.False);
            Assert.That((string)envelope["error"]?["code"], Is.EqualTo("UserApprovalUnavailable"));
        }


        [Test]
        public void RequestUserApproval_GetSchema_ReturnsObject()
        {
            var schema = RequestUserApprovalAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
