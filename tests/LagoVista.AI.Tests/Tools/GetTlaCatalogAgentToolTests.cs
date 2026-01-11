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
using NUnit.Framework;

namespace LagoVista.AI.AgentTools.Tests
{
    [TestFixture]
    public class GetTlaCatalogAgentToolTests
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
        public async Task GetTlaCatalog_Empty_ReturnsOkWithEmptyArray()
        {
            _ddrManager.Setup(m => m.GetTlaCatalogAsync(_context.Org, _context.User))
                .ReturnsAsync(new List<DdrTla>());

            var tool = new GetTlaCatalogAgentTool(_ddrManager.Object, _logger.Object);

            var result = await tool.ExecuteAsync("{}", _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Does.Contain("\"ok\":true"));
        }

        [Test]
        public async Task GetTlaCatalog_WithItems_ReturnsThoseItems()
        {
            var catalog = new List<DdrTla>
            {
                new DdrTla { Tla = "SYS", Title = "System", Summary = "System work" },
                new DdrTla { Tla = "AGN", Title = "Agent", Summary = "Agent and reasoning" }
            };

            _ddrManager.Setup(m => m.GetTlaCatalogAsync(_context.Org, _context.User))
                .ReturnsAsync(catalog);

            var tool = new GetTlaCatalogAgentTool(_ddrManager.Object, _logger.Object);

            var result = await tool.ExecuteAsync("{}", _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Does.Contain("\"SYS\""));
            Assert.That(result.Result, Does.Contain("\"AGN\""));
        }

        [Test]
        public void GetTlaCatalog_GetSchema_ReturnsObject()
        {
            var schema = GetTlaCatalogAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
