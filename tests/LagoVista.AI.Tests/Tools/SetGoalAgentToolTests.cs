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
    public class SetGoalAgentToolTests
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
        public async Task SetGoal_MissingIdentifier_ReturnsError()
        {
            var tool = new SetGoalAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["goal"] = "Do something important"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("identifier is required."));
        }

        [Test]
        public async Task SetGoal_MissingGoal_ReturnsError()
        {
            var tool = new SetGoalAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("goal is required."));
        }

        [Test]
        public async Task SetGoal_Valid_SetsGoal()
        {
            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Goal = null,
                GoalApprovedTimestamp = null
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User))
                .ReturnsAsync(ddr);

            _ddrManager.Setup(m => m.UpdateDdrAsync(ddr, _context.Org, _context.User))
                .ReturnsAsync(InvokeResult.Success);

            var tool = new SetGoalAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["goal"] = "Clarify how agent tools are registered and used."
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(ddr.Goal, Is.EqualTo("Clarify how agent tools are registered and used."));
        }

        [Test]
        public void SetGoal_GetSchema_ReturnsObject()
        {
            var schema = SetGoalAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
