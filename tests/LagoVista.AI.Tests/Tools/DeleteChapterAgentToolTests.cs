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
    public class DeleteChapterAgentToolTests
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
        public async Task DeleteChapter_MissingIdentifier_ReturnsError()
        {
            var tool = new DeleteChapterAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["chapter_id"] = "ch-1"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("identifier is required."));
        }

        [Test]
        public async Task DeleteChapter_MissingChapterId_ReturnsError()
        {
            var tool = new DeleteChapterAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("chapter_id is required."));
        }

        [Test]
        public async Task DeleteChapter_DdrNotFound_ReturnsError()
        {
            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true))
                .ReturnsAsync((DetailedDesignReview)null);

            var tool = new DeleteChapterAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["chapter_id"] = "ch-1"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("DDR 'SYS-001' not found."));
        }

        [Test]
        public async Task DeleteChapter_ChapterNotFound_ReturnsError()
        {
            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Chapters = new List<DdrChapter>()
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true))
                .ReturnsAsync(ddr);

            var tool = new DeleteChapterAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["chapter_id"] = "missing"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Chapter 'missing' not found."));
        }

        [Test]
        public async Task DeleteChapter_Valid_DeletesChapter()
        {
            var chapter1 = new DdrChapter { Id = "ch-1", Title = "Chapter 1" };
            var chapter2 = new DdrChapter { Id = "ch-2", Title = "Chapter 2" };

            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Chapters = new List<DdrChapter> { chapter1, chapter2 }
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true))
                .ReturnsAsync(ddr);

            _ddrManager.Setup(m => m.UpdateDdrAsync(ddr, _context.Org, _context.User))
                .ReturnsAsync(InvokeResult.Success);

            var tool = new DeleteChapterAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["chapter_id"] = "ch-1"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(ddr.Chapters, Has.Count.EqualTo(1));
            Assert.That(ddr.Chapters[0].Id, Is.EqualTo("ch-2"));
            Assert.That(result.Result, Does.Contain("\"ok\":true"));
        }

        [Test]
        public void DeleteChapter_GetSchema_ReturnsObject()
        {
            var schema = DeleteChapterAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
