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
    public class DdrChapterAgentToolsTests
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
        public async Task AddChapter_MissingIdentifier_ReturnsError()
        {
            var tool = new AddChapterAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["title"] = "Chapter 1",
                ["summary"] = "Summary"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("identifier is required."));
        }

        [Test]
        public async Task AddChapter_Valid_CreatesChapter()
        {
            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Chapters = new List<DdrChapter>()
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true))
                       .ReturnsAsync(ddr);

            _ddrManager.Setup(m => m.UpdateDdrAsync(ddr, _context.Org, _context.User))
                       .ReturnsAsync(InvokeResult.Success);

            var tool = new AddChapterAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["title"] = "Chapter 1",
                ["summary"] = "Summary"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Does.Contain("\"ok\":true"));
            Assert.That(ddr.Chapters.Count, Is.EqualTo(1));
        }

        [Test]
        public void AddChapter_GetSchema_ReturnsObject()
        {
            var schema = AddChapterAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }

        [Test]
        public async Task AddChapters_MissingChapters_ReturnsError()
        {
            var tool = new AddChaptersAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("chapters array is required and must be non-empty."));
        }

        [Test]
        public async Task AddChapters_Valid_AddsAllChapters()
        {
            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Chapters = new List<DdrChapter>()
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true))
                       .ReturnsAsync(ddr);

            _ddrManager.Setup(m => m.UpdateDdrAsync(ddr, _context.Org, _context.User))
                       .ReturnsAsync(InvokeResult.Success);

            var tool = new AddChaptersAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["chapters"] = new JArray(
                    new JObject { ["title"] = "T1", ["summary"] = "S1" },
                    new JObject { ["title"] = "T2", ["summary"] = "S2" }
                )
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Does.Contain("\"ok\":true"));
            Assert.That(ddr.Chapters.Count, Is.EqualTo(2));
        }

        [Test]
        public void AddChapters_GetSchema_ReturnsObject()
        {
            var schema = AddChaptersAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
