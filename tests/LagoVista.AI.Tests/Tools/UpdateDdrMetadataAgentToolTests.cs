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
    public class UpdateDdrMetadataAgentToolTests
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
        public async Task UpdateMetadata_MissingIdentifier_ReturnsError()
        {
            var tool = new UpdateDdrMetadataAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["title"] = "New Title"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("identifier is required."));
        }

        [Test]
        public async Task UpdateMetadata_NoTitleOrSummary_ReturnsError()
        {
            var tool = new UpdateDdrMetadataAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("At least one of title or summary must be provided."));
        }

        [Test]
        public async Task UpdateMetadata_Valid_UpdatesFields()
        {
            var ddr = new DetailedDesignReview
            {
                Tla = "SYS",
                Index = 1,
                Name = "Old Title",
                Description = "Old Summary"
            };

            _ddrManager.Setup(m => m.GetDdrByTlaIdentiferAsync("SYS-001", _context.Org, _context.User))
                .ReturnsAsync(ddr);

            _ddrManager.Setup(m => m.UpdateDdrAsync(ddr, _context.Org, _context.User))
                .ReturnsAsync(InvokeResult.Success);

            var tool = new UpdateDdrMetadataAgentTool(_ddrManager.Object, _logger.Object);

            var args = new JObject
            {
                ["identifier"] = "SYS-001",
                ["title"] = "New Title",
                ["summary"] = "New Summary"
            };

            var result = await tool.ExecuteAsync(args.ToString(), _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(ddr.Name, Is.EqualTo("New Title"));
            Assert.That(ddr.Description, Is.EqualTo("New Summary"));
        }

        [Test]
        public void UpdateMetadata_GetSchema_ReturnsObject()
        {
            var schema = UpdateDdrMetadataAgentTool.GetSchema();
            Assert.That(schema, Is.Not.Null);
        }
    }
}
