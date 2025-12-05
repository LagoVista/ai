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
    public class ApproveChapterAgentToolTests
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
        public async Task ApproveChapter_MissingIdentifier_ReturnsError()
        {
            var tool = new ApproveChapterAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["chapter_id"] = "ch-1"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("identifier is required."));
        }

        [Test]
        public async Task ApproveChapter_MissingChapterId_ReturnsError()
        {
            var tool = new ApproveChapterAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("chapter_id is required."));
        }

        [Test]
        public async Task ApproveChapter_DdrNotFound_ReturnsError()
        {
            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User))
                .ReturnsAsync((DetailedDesignReview)null);

            var tool = new ApproveChapterAgentTool(_ddrManager.Object, _logger.Object);

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
        public async Task ApproveChapter_ChapterNotFound_ReturnsError()
        {
            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Chapters = new List<DdrChapter>()
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User))
                .ReturnsAsync(ddr);

            var tool = new ApproveChapterAgentTool(_ddrManager.Object, _logger.Object);

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
        public async Task ApproveChapter_AlreadyApproved_ReturnsExistingEnvelope()
        {
            var chapter = new DdrChapter
            {
                Id = "ch-1",
                Title = "Chapter 1",
                Summary = "Summary",
                Details = "Details",
                ApprovedBy = EntityHeader.Create("other-user", "Other User"),
                ApprovedTimestamp = "2025-11-29T10:00:00Z"
            };

            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Chapters = new List<DdrChapter> { chapter }
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User))
                .ReturnsAsync(ddr);

            var tool = new ApproveChapterAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["chapter_id"] = "ch-1"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Does.Contain("\"ok\":true"));
            Assert.That(result.Result, Does.Contain("other-user"));
        }

        [Test]
        public async Task ApproveChapter_Valid_SetsApproval()
        {
            var chapter = new DdrChapter
            {
                Id = "ch-1",
                Title = "Chapter 1",
                Summary = "Summary",
                Details = "Details",
                ApprovedBy = null,
                ApprovedTimestamp = null
            };

            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Chapters = new List<DdrChapter> { chapter }
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User))
                .ReturnsAsync(ddr);

            _ddrManager.Setup(m => m.UpdateDdrAsync(ddr, _context.Org, _context.User))
                .ReturnsAsync(InvokeResult.Success);

            var tool = new ApproveChapterAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["chapter_id"] = "ch-1"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(chapter.ApprovedBy, Is.Not.Null);
            Assert.That(chapter.ApprovedBy.Id, Is.EqualTo("user-id"));
            Assert.That(chapter.ApprovedTimestamp, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Result, Does.Contain("\"ok\":true"));
        }

        [Test]
        public void ApproveChapter_GetSchema_ReturnsObject()
        {
            var schema = ApproveChapterAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
