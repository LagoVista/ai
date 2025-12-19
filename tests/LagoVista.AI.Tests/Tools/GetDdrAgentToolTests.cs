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
    public class GetDdrAgentToolTests
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
        public async Task GetDdr_MissingIdentifier_ReturnsError()
        {
            var tool = new GetDdrAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject();

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("identifier is required."));
        }

        [Test]
        public async Task GetDdr_NotFound_ReturnsError()
        {
            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true ))
                .ReturnsAsync((DetailedDesignReview)null);

            var tool = new GetDdrAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("DDR 'SYS-001' not found."));
        }

        [Test]
        public async Task GetDdr_Valid_ReturnsEnvelopeWithChapters()
        {
            var chapter = new DdrChapter
            {
                Id = "ch-1",
                Title = "Chapter 1",
                Summary = "Summary",
                Details = "Details"
            };

            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Name = "Test DDR",
                Description = "Test description",
                Status = "Draft",
                StatusTimestamp = "2025-11-29T10:00:00Z",
                Goal = "Goal",
                Chapters = new List<DdrChapter> { chapter }
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true ))
                .ReturnsAsync(ddr);

            var tool = new GetDdrAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Does.Contain("\"ok\":true"));
            Assert.That(result.Result, Does.Contain("SYS-001"));
            Assert.That(result.Result, Does.Contain("\"chapters\""));
        }

        [Test]
        public void GetDdr_GetSchema_ReturnsObject()
        {
            var schema = GetDdrAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
