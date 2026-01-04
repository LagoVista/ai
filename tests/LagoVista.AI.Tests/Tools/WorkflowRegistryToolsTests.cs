using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LagoVista.AI.Tests
{
    [TestFixture]
    public class ListWorkflowsToolTests
    {
        private Mock<IWorkflowDefinitionManager> _manager;
        private Mock<IAdminLogger> _logger;
        private ListWorkflowsTool _tool;

        [SetUp]
        public void SetUp()
        {
            _manager = new Mock<IWorkflowDefinitionManager>();
            _logger = new Mock<IAdminLogger>();
            _tool = new ListWorkflowsTool(_manager.Object, _logger.Object);
        }

        [Test]
        public async Task ExecuteAsync_ListWorkflows_FiltersDisabled()
        {
            var defs = new ListResponse<WorkflowDefinition>
            {
                Model = new List<WorkflowDefinition>
                {
                    new WorkflowDefinition
                    {
                        Id = "create_ddr",
                        Name = "Create DDR",
                        Description = "Create a new DDR",
                        Status = WorkflowStatus.Active,
                        Visibility = WorkflowVisibility.Public,
                        Version = "1.0.0"
                    },
                    new WorkflowDefinition
                    {
                        Id = "disabled_flow",
                        Name = "Disabled Flow",
                        Description = "Should not appear",
                        Status = WorkflowStatus.Disabled,
                        Visibility = WorkflowVisibility.Public,
                        Version = "1.0.0"
                    }
                }
            };

            _manager
                .Setup(mgr => mgr.GetWorkflowDefinitionsAsync(It.IsAny<ListRequest>(), EntityHeader.Create("ID", "TEXT"), EntityHeader.Create("ID", "TEXT")))
                .ReturnsAsync(defs);

            var result = await _tool.ExecuteAsync("{}", new AgentToolExecutionContext() { Org = EntityHeader.Create("ID", "TEXT"), User = EntityHeader.Create("ID", "TEXT") }, CancellationToken.None);

            Assert.That(result.Successful, Is.True);

            var parsed = JObject.Parse(result.Result);
            var workflows = (JArray)parsed["Workflows"];

            Assert.That(workflows.Count, Is.EqualTo(1));
            Assert.That((string)workflows[0]["WorkflowId"], Is.EqualTo("create_ddr"));
        }
    }

    [TestFixture]
    public class GetWorkflowManifestToolTests
    {
        private Mock<IWorkflowDefinitionManager> _manager;
        private Mock<IAdminLogger> _logger;
        private GetWorkflowManifestTool _tool;

        [SetUp]
        public void SetUp()
        {
            _manager = new Mock<IWorkflowDefinitionManager>();
            _logger = new Mock<IAdminLogger>();
            _tool = new GetWorkflowManifestTool(_manager.Object, _logger.Object);
        }

        [Test]
        public async Task ExecuteAsync_EmptyArgs_ReturnsError()
        {
            var result = await _tool.ExecuteAsync(string.Empty, null, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("non-empty arguments object"));
        }



        [Test]
        public async Task ExecuteAsync_WorkflowFound_ReturnsManifest()
        {
            var def = new WorkflowDefinition
            {
                Id = "create_ddr",
                Name = "Create DDR",
                Description = "Create a new DDR",
                Status = WorkflowStatus.Active,
                Visibility = WorkflowVisibility.Public,
                Version = "1.0.0"
            };

            _manager
                .Setup(mgr => mgr.GetWorkflowDefinitionAsync("create_ddr", It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()))
                .ReturnsAsync(def);

            var args = new JObject
            {
                ["workflowId"] = "create_ddr"
            };

            var result = await _tool.ExecuteAsync(args.ToString(), new AgentToolExecutionContext() { Org = EntityHeader.Create("ID", "TEXT"), User = EntityHeader.Create("ID", "TEXT") }, CancellationToken.None);

            Assert.That(result.Successful, Is.True);

            var parsed = JObject.Parse(result.Result);
            var workflow = parsed["Workflow"];

            // NOTE: 'id' is lower-case, 'Kind' is PascalCase
            Assert.That((string)workflow["id"], Is.EqualTo("create_ddr"));
            Assert.That((string)workflow["Name"], Is.EqualTo("Create DDR"));
        }


        [Test]
        public async Task ExecuteAsync_WorkflowMissing_ReturnsError()
        {
            _manager
                .Setup(mgr => mgr.GetWorkflowDefinitionAsync("missing", null, null))
                .ReturnsAsync((WorkflowDefinition)null);

            var args = new JObject
            {
                ["workflowId"] = "missing"
            };

            var result = await _tool.ExecuteAsync(args.ToString(), new AgentToolExecutionContext() { Org = EntityHeader.Create("ID", "TEXT"), User = EntityHeader.Create("ID", "TEXT") }, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("was not found"));
        }
    }

    [TestFixture]
    public class MatchWorkflowToolTests
    {
        private Mock<IWorkflowDefinitionManager> _manager;
        private Mock<IAdminLogger> _logger;
        private MatchWorkflowTool _tool;

        [SetUp]
        public void SetUp()
        {
            _manager = new Mock<IWorkflowDefinitionManager>();
            _logger = new Mock<IAdminLogger>();
            _tool = new MatchWorkflowTool(_manager.Object, _logger.Object);
        }

        [Test]
        public async Task ExecuteAsync_EmptyArgs_ReturnsError()
        {
            var result = await _tool.ExecuteAsync(string.Empty, new AgentToolExecutionContext() { Org = EntityHeader.Create("ID", "TEXT"), User = EntityHeader.Create("ID", "TEXT") }, CancellationToken.None);

        Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("non-empty arguments object"));
        }

        [Test]
        public async Task ExecuteAsync_MatchWorkflow_ReturnsExpectedMatch()
        {
            var defs = new ListResponse<WorkflowDefinition>
            {
                Model = new List<WorkflowDefinition>
                {
                    new WorkflowDefinition
                    {
                        Id = "create_ddr",
                        Name = "Create DDR",
                        Description = "Create a new DDR",
                        Status = WorkflowStatus.Active,
                        Visibility = WorkflowVisibility.Public,
                        Version = "1.0.0",
                        UserIntentPatterns = new List<string> { "create a new ddr" }
                    },
                    new WorkflowDefinition
                    {
                        Id = "refine_model",
                        Name = "Refine Model",
                        Description = "Refine a domain model",
                        Status = WorkflowStatus.Active,
                        Visibility = WorkflowVisibility.Public,
                        Version = "1.0.0",
                        UserIntentPatterns = new List<string> { "refine this model" }
                    }
                }
            };

            _manager
                .Setup(mgr => mgr.GetWorkflowDefinitionsAsync(It.IsAny<ListRequest>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()))
                .ReturnsAsync(defs);

            var args = new JObject
            {
                ["userMessage"] = "Can you create a new DDR for me?"
            };

            var result = await _tool.ExecuteAsync(args.ToString(), new AgentToolExecutionContext() { Org = EntityHeader.Create("ID","TEXT"), User = EntityHeader.Create("id","EXT") }, CancellationToken.None);

            Assert.That(result.Successful, Is.True);

            var parsed = JObject.Parse(result.Result);
            var matches = (JArray)parsed["Matches"];

            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That((string)matches[0]["WorkflowId"], Is.EqualTo("create_ddr"));
        }
    }
}
