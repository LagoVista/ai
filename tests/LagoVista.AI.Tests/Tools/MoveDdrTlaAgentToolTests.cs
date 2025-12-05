using System.Collections.Generic;
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
    public class MoveDdrTlaAgentToolTests
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
        public async Task MoveDdr_MissingIdentifier_ReturnsError()
        {
            var tool = new MoveDdrTlaAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["new_tla"] = "AGN"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("identifier is required."));
        }

        [Test]
        public async Task MoveDdr_MissingNewTla_ReturnsError()
        {
            var tool = new MoveDdrTlaAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("new_tla is required."));
        }

        [Test]
        public async Task MoveDdr_UnknownTla_ReturnsError()
        {
            _ddrManager.Setup(m => m.GetTlaCatalogAsync(_context.Org, _context.User))
                .ReturnsAsync(new List<DdrTla>());

            var tool = new MoveDdrTlaAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["new_tla"] = "XXX"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Unknown TLA 'XXX'."));
        }

        [Test]
        public async Task MoveDdr_NoOp_WhenSameTla()
        {
            var catalog = new List<DdrTla>
            {
                new DdrTla { Tla = "SYS" }
            };

            _ddrManager.Setup(m => m.GetTlaCatalogAsync(_context.Org, _context.User))
                .ReturnsAsync(catalog);

            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User))
                .ReturnsAsync(ddr);

            var tool = new MoveDdrTlaAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["new_tla"] = "SYS"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Does.Contain("\"old_identifier\":\"SYS-001\""));
            Assert.That(result.Result, Does.Contain("\"new_identifier\":\"SYS-001\""));
        }

        [Test]
        public async Task MoveDdr_Valid_MovesToNewTla()
        {
            var catalog = new List<DdrTla>
            {
                new DdrTla { Tla = "SYS" },
                new DdrTla { Tla = "AGN" }
            };

            _ddrManager.Setup(m => m.GetTlaCatalogAsync(_context.Org, _context.User))
                .ReturnsAsync(catalog);

            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User))
                .ReturnsAsync(ddr);

            _ddrManager.Setup(m => m.AllocateTlaIndex("AGN", _context.Org, _context.User))
                .ReturnsAsync(InvokeResult<int>.Create(2));

            _ddrManager.Setup(m => m.UpdateDdrAsync(ddr, _context.Org, _context.User))
                .ReturnsAsync(InvokeResult.Success);

            var tool = new MoveDdrTlaAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["new_tla"] = "AGN"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(ddr.Tla, Is.EqualTo("AGN"));
            Assert.That(ddr.Index, Is.EqualTo(2));
            Assert.That(result.Result, Does.Contain("\"old_identifier\":\"SYS-001\""));
            Assert.That(result.Result, Does.Contain("\"new_identifier\":\"AGN-002\""));
        }

        [Test]
        public void MoveDdr_GetSchema_ReturnsObject()
        {
            var schema = MoveDdrTlaAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
