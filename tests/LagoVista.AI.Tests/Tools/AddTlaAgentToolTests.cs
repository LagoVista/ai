using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
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
    public class AddTlaAgentToolTests
    {
        private Mock<IDdrManager> _ddrManager;
        private Mock<IAdminLogger> _logger;
        private AgentToolExecutionContext _context;

        [SetUp]
        public void SetUp()
        {
            _ddrManager = new Mock<IDdrManager>();
            _logger = new Mock<IAdminLogger>();

            _context = new AgentToolExecutionContext
            {
                Org = EntityHeader.Create("org-id", "Org"),
                User = EntityHeader.Create("user-id", "User")
            };
        }

        [Test]
        public async Task AddTla_MissingTla_ReturnsError()
        {
            var tool = new AddTlaAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["title"] = "System",
                ["summary"] = "System work"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("tla is required."));
        }

        [Test]
        public async Task AddTla_Duplicate_ReturnsError()
        {
            var existing = new List<DdrTla>
            {
                new DdrTla { Tla = "SYS", Title = "System", Summary = "System work" }
            };

            _ddrManager.Setup(m => m.GetTlaCatalogAsync(_context.Org, _context.User))
                .ReturnsAsync(existing);

            var tool = new AddTlaAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["tla"] = "sys",
                ["title"] = "System",
                ["summary"] = "Duplicate"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("TLA 'SYS' already exists."));
        }

        [Test]
        public async Task AddTla_Valid_AddsTla()
        {
            _ddrManager.Setup(m => m.GetTlaCatalogAsync(_context.Org, _context.User))
                .ReturnsAsync(new List<DdrTla>());

            _ddrManager.Setup(m => m.AddTlaCatalog(
                    It.IsAny<DdrTla>(),
                    _context.Org,
                    _context.User))
                .ReturnsAsync(InvokeResult.Success);

            var tool = new AddTlaAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["tla"] = "sys",
                ["title"] = "System",
                ["summary"] = "System work"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Does.Contain("\"ok\":true"));

            _ddrManager.Verify(m => m.AddTlaCatalog(
                It.Is<DdrTla>(t => t.Tla == "SYS"),
                _context.Org,
                _context.User), Times.Once);
        }

        [Test]
        public void AddTla_GetSchema_ReturnsObject()
        {
            var schema = AddTlaAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
