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
    public class ListDdrsAgentToolTests
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
        public async Task ListDdrs_NullResponse_ReturnsError()
        {
            // No setup: GetDdrsAsync will return null by default
            var tool = new ListDdrsAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject();

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Unexpected null response from GetDdrsAsync."));
        }

        [Test]
        public async Task ListDdrs_ExecutesWithEmptyArguments_UsesDefaults()
        {
            var tool = new ListDdrsAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject();

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Unexpected null response from GetDdrsAsync."));
        }

        [Test]
        public void ListDdrs_GetSchema_ReturnsObject()
        {
            var schema = ListDdrsAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
