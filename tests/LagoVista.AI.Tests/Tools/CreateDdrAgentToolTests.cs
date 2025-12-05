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
    public class CreateDdrAgentToolTests
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
        public async Task CreateDdr_MissingSummary_ReturnsError()
        {
            var tool = new CreateDdrAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["tla"] = "SYS",
                ["title"] = "System DDR"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("summary is required."));
        }

        [Test]
        public async Task CreateDdr_UnknownTla_ReturnsError()
        {
            _ddrManager.Setup(m => m.GetTlaCatalogAsync(_context.Org, _context.User))
                .ReturnsAsync(new List<DdrTla>());

            var tool = new CreateDdrAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["tla"] = "XXX",
                ["title"] = "Some DDR",
                ["summary"] = "Summary"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Unknown TLA 'XXX'."));
        }

        [Test]
        public async Task CreateDdr_Valid_SucceedsAndReturnsIdentifier()
        {
            _ddrManager.Setup(m => m.GetTlaCatalogAsync(_context.Org, _context.User))
                .ReturnsAsync(new List<DdrTla> { new DdrTla { Tla = "SYS", Title = "System", Summary = "System" } });

            _ddrManager.Setup(m => m.AllocateTlaIndex("SYS", _context.Org, _context.User))
                .ReturnsAsync(InvokeResult<int>.Create(1));

            _ddrManager.Setup(m => m.AddDdrAsync(It.IsAny<DetailedDesignReview>(), _context.Org, _context.User))
                .ReturnsAsync(InvokeResult.Success);

            var tool = new CreateDdrAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["tla"] = "SYS",
                ["title"] = "System DDR",
                ["summary"] = "Summary"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Does.Contain("SYS-001"));
        }

        [Test]
        public void CreateDdr_GetSchema_ReturnsObject()
        {
            var schema = CreateDdrAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
