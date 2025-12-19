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
    public class SetDdrStatusAgentToolTests
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
        public async Task SetStatus_MissingIdentifier_ReturnsError()
        {
            var tool = new SetDdrStatusAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["status"] = "InProgress"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("identifier is required."));
        }

        [Test]
        public async Task SetStatus_MissingStatus_ReturnsError()
        {
            var tool = new SetDdrStatusAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("status is required."));
        }

        [Test]
        public async Task SetStatus_InvalidStatus_ReturnsError()
        {
            var tool = new SetDdrStatusAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["status"] = "NotARealStatus"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Invalid status 'NotARealStatus'."));
        }

        [Test]
        public async Task SetStatus_DdrNotFound_ReturnsError()
        {
            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true))
                .ReturnsAsync((DetailedDesignReview)null);

            var tool = new SetDdrStatusAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["status"] = "Draft"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("DDR 'SYS-001' not found."));
        }

        [Test]
        public async Task SetStatus_Valid_UpdatesStatus()
        {
            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Status = "Draft",
                StatusTimestamp = null
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true))
                .ReturnsAsync(ddr);

            _ddrManager.Setup(m => m.UpdateDdrAsync(ddr, _context.Org, _context.User))
                .ReturnsAsync(InvokeResult.Success);

            var tool = new SetDdrStatusAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["status"] = "InProgress"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(ddr.Status, Is.EqualTo("InProgress"));
            Assert.That(ddr.StatusTimestamp, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Result, Does.Contain("\"ok\":true"));
            Assert.That(result.Result, Does.Contain("InProgress"));
        }

        [Test]
        public void SetStatus_GetSchema_ReturnsObject()
        {
            var schema = SetDdrStatusAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
