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
    public class ApproveGoalAgentToolTests
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
        public async Task ApproveGoal_MissingIdentifier_ReturnsError()
        {
            var tool = new ApproveGoalAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject();

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("identifier is required."));
        }

        [Test]
        public async Task ApproveGoal_NotFound_ReturnsError()
        {
            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true))
                .ReturnsAsync((DetailedDesignReview)null);

            var tool = new ApproveGoalAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("DDR 'SYS-001' not found."));
        }

        [Test]
        public async Task ApproveGoal_EmptyGoal_ReturnsError()
        {
            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Goal = string.Empty
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true))
                .ReturnsAsync(ddr);

            var tool = new ApproveGoalAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Cannot approve goal because it is empty."));
        }

        [Test]
        public async Task ApproveGoal_Valid_SetsApproval()
        {
            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Goal = "Clarify DDR workflow",
                GoalApprovedBy = null,
                GoalApprovedTimestamp = null
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true))
                .ReturnsAsync(ddr);

            _ddrManager.Setup(m => m.UpdateDdrAsync(ddr, _context.Org, _context.User))
                .ReturnsAsync(InvokeResult.Success);

            var tool = new ApproveGoalAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(ddr.GoalApprovedBy, Is.Not.Null);
            Assert.That(ddr.GoalApprovedBy.Id, Is.EqualTo("user-id"));
            Assert.That(ddr.GoalApprovedTimestamp, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Result, Does.Contain("\"ok\":true"));
        }

        [Test]
        public async Task ApproveGoal_AlreadyApproved_ReturnsExistingEnvelope()
        {
            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Goal = "Clarify DDR workflow",
                GoalApprovedBy = EntityHeader.Create("other-user", "Other User"),
                GoalApprovedTimestamp = "2025-11-29T10:00:00Z"
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User, true))
                .ReturnsAsync(ddr);

            var tool = new ApproveGoalAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Does.Contain("\"ok\":true"));
            Assert.That(result.Result, Does.Contain("other-user"));
        }

        [Test]
        public void ApproveGoal_GetSchema_ReturnsObject()
        {
            var schema = ApproveGoalAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
