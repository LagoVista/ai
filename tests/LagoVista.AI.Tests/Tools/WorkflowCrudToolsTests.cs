using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LagoVista.AI.Tests
{
    [TestFixture]
    public class CreateWorkflowToolTests
    {
        private Mock<IWorkflowDefinitionManager> _manager;
        private Mock<IAdminLogger> _logger;
        private CreateWorkflowTool _tool;

        [SetUp]
        public void SetUp()
        {
            _manager = new Mock<IWorkflowDefinitionManager>();
            _logger = new Mock<IAdminLogger>();
            _tool = new CreateWorkflowTool(_manager.Object, _logger.Object);
        }

        [Test]
        public async Task ExecuteAsync_ValidWorkflow_CreatesAndReturnsOk()
        {
            _manager
                .Setup(mgr => mgr.QueryWorkflowIdInUseAsync("create_ddr", It.IsAny<EntityHeader>()))
                .ReturnsAsync(false);

            _manager
                .Setup(mgr => mgr.AddWorkflowDefinitionAsync(It.IsAny<WorkflowDefinition>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()))
                .ReturnsAsync(InvokeResult.Success);

            var args = new JObject
            {
                ["workflow"] = new JObject
                {
                    ["Id"] = "create_ddr",
                    ["Name"] = "Create DDR",
                    ["Description"] = "Create a new DDR",
                    ["Version"] = "1.0.0"
                }
            };

            var context = new AgentToolExecutionContext
            {
                Org = EntityHeader.Create("ORG", "Org"),
                User = EntityHeader.Create("USER", "User")
            };

            var result = await _tool.ExecuteAsync(args.ToString(), context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);

            var parsed = JObject.Parse(result.Result);
            Assert.That((bool)parsed["Ok"], Is.True);

            var workflow = parsed["Workflow"];
            Assert.That((string)workflow["id"], Is.EqualTo("create_ddr"));
            Assert.That((string)workflow["Name"], Is.EqualTo("Create DDR"));
        }
    }

    [TestFixture]
    public class UpdateWorkflowToolTests
    {
        private Mock<IWorkflowDefinitionManager> _manager;
        private Mock<IAdminLogger> _logger;
        private UpdateWorkflowTool _tool;

        [SetUp]
        public void SetUp()
        {
            _manager = new Mock<IWorkflowDefinitionManager>();
            _logger = new Mock<IAdminLogger>();
            _tool = new UpdateWorkflowTool(_manager.Object, _logger.Object);
        }

        [Test]
        public async Task ExecuteAsync_ValidWorkflow_UpdatesAndReturnsOk()
        {
            _manager
                .Setup(mgr => mgr.UpdateWorkflowDefinitionAsync(It.IsAny<WorkflowDefinition>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()))
                .ReturnsAsync(InvokeResult.Success);

            var args = new JObject
            {
                ["workflow"] = new JObject
                {
                    ["Id"] = "create_ddr",
                    ["Name"] = "Create DDR Updated",
                    ["Description"] = "Updated description",
                    ["Version"] = "1.1.0"
                }
            };

            var context = new AgentToolExecutionContext
            {
                Org = EntityHeader.Create("ORG", "Org"),
                User = EntityHeader.Create("USER", "User")
            };

            var result = await _tool.ExecuteAsync(args.ToString(), context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);

            var parsed = JObject.Parse(result.Result);
            Assert.That((bool)parsed["Ok"], Is.True);

            var workflow = parsed["Workflow"];
            Assert.That((string)workflow["id"], Is.EqualTo("create_ddr"));
            Assert.That((string)workflow["Name"], Is.EqualTo("Create DDR Updated"));
        }
    }

    [TestFixture]
    public class DeleteWorkflowToolTests
    {
        private Mock<IWorkflowDefinitionManager> _manager;
        private Mock<IAdminLogger> _logger;
        private DeleteWorkflowTool _tool;

        [SetUp]
        public void SetUp()
        {
            _manager = new Mock<IWorkflowDefinitionManager>();
            _logger = new Mock<IAdminLogger>();
            _tool = new DeleteWorkflowTool(_manager.Object, _logger.Object);
        }

        [Test]
        public async Task ExecuteAsync_ValidId_DeletesAndReturnsOk()
        {
            _manager
                .Setup(mgr => mgr.DeleteWorkflowDefinitionAsync("create_ddr", It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()))
                .ReturnsAsync(InvokeResult.Success);

            var args = new JObject
            {
                ["workflowId"] = "create_ddr"
            };

            var context = new AgentToolExecutionContext
            {
                Org = EntityHeader.Create("ORG", "Org"),
                User = EntityHeader.Create("USER", "User")
            };

            var result = await _tool.ExecuteAsync(args.ToString(), context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);

            var parsed = JObject.Parse(result.Result);
            Assert.That((bool)parsed["Ok"], Is.True);
        }

        [Test]
        public async Task ExecuteAsync_MissingId_ReturnsValidationEnvelope()
        {
            var args = new JObject();

            var result = await _tool.ExecuteAsync(args.ToString(), null, CancellationToken.None);

            Assert.That(result.Successful, Is.True);

            var parsed = JObject.Parse(result.Result);
            Assert.That((bool)parsed["Ok"], Is.False);

            var errors = (JArray)parsed["Errors"];
            Assert.That(errors.Count, Is.GreaterThan(0));
        }
    }
}
