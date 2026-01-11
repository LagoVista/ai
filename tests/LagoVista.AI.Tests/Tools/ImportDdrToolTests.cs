using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Tools
{
    [TestFixture]
    public class ImportDdrToolTests
    {
        private static AgentToolExecutionContext BuildContext()
        {
            return new AgentToolExecutionContext
            {
                Org = EntityHeader.Create("org1", "Org 1"),
                User = EntityHeader.Create("user1", "User 1"),
                SessionId = "sess-1",
                Request = new Core.AI.Models.AgentExecuteRequest { SessionId
                = "conv-1" }
            };
        }

        private static string BuildMarkdown(string id, string title = "Test DDR", string status = "Approved")
        {
            return $@"# {id} — {title}

**ID:** {id}  
**Title:** {title}  
**Status:** {status}

## Approval Metadata
- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-21 06:45 EST (UTC-05:00)

---

## Body

Some content.";
        }

        private static string ToArgsJson(object args) => JsonConvert.SerializeObject(args);

        private static ImportDdrTool CreateTool(Mock<IDdrManager> ddrManager)
        {
            var logger = new Mock<IAdminLogger>(MockBehavior.Loose);
            return new ImportDdrTool(ddrManager.Object, logger.Object);
        }

        [Test]
        public async Task ExecuteAsync_WhenArgumentsEmpty_ReturnsError()
        {
            var mgr = new Mock<IDdrManager>(MockBehavior.Strict);
            var tool = CreateTool(mgr);

            var res = await tool.ExecuteAsync(string.Empty, BuildContext(), CancellationToken.None);

            Assert.That(res.Successful, Is.False);
            Assert.That(res.ErrorMessage, Does.Contain("requires a non-empty arguments").IgnoreCase);
            mgr.VerifyNoOtherCalls();
        }

        [Test]
        public async Task ExecuteAsync_WhenDryRun_ReturnsPreview_AndDoesNotPersist()
        {
            var mgr = new Mock<IDdrManager>(MockBehavior.Strict);
            mgr.Setup(m => m.GetDdrByTlaIdentiferAsync("AGN-023", It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>(), false))
               .ReturnsAsync((DetailedDesignReview)null);

            var tool = CreateTool(mgr);

            var args = new
            {
                Markdown = BuildMarkdown("AGN-023", "DDR Ingestion Contract"),
                // New fields you added in your implementation
                DdrType = "Generation",
                NeedsHumanConfirmation = false,
                HumanSummary = "Two sentence human summary.",
                CondensedDdrContent = "Condensed content.",
                RagIndexCard = "AGN-023 Generation Approved 2025-12-21: DDR ingestion and derived field generation contract.",

                DryRun = true,
                Confirmed = false
            };

            var res = await tool.ExecuteAsync(ToArgsJson(args), BuildContext(), CancellationToken.None);

            Assert.That(res.Successful, Is.True, res.ErrorMessage);

            dynamic payload = JsonConvert.DeserializeObject(res.Result);
            Assert.That((bool)payload.Success, Is.True);
            Assert.That((bool)payload.DryRun, Is.True);
            Assert.That((string)payload.Identifier, Is.EqualTo("AGN-023"));

            mgr.Verify(m => m.GetDdrByTlaIdentiferAsync("AGN-023", It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>(), false), Times.Once);
            mgr.VerifyNoOtherCalls();
        }

        [Test]
        public async Task ExecuteAsync_WhenDryRun_AndDdrAlreadyExists_ReturnsError()
        {
            var mgr = new Mock<IDdrManager>(MockBehavior.Strict);
            mgr.Setup(m => m.GetDdrByTlaIdentiferAsync("AGN-999", It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>(), false))
               .ReturnsAsync(new DetailedDesignReview { DdrIdentifier = "AGN-999", Name = "Existing" });

            var tool = CreateTool(mgr);

            var args = new
            {
                Markdown = BuildMarkdown("AGN-999", "Existing DDR"),
                Type = "Generation",
                NeedsHumanConfirmation = true,
                HumanSummary = "Summary.",
                CondensedDdrContent = "Condensed.",
                RagIndexCard = "AGN-999 Generation Approved 2025-12-21: Routes DDR.",
                DryRun = true,
                Confirmed = false
            };

            var res = await tool.ExecuteAsync(ToArgsJson(args), BuildContext(), CancellationToken.None);

            Assert.That(res.Successful, Is.False);
            Assert.That(res.ErrorMessage, Does.Contain("already exists").IgnoreCase);

            mgr.Verify(m => m.GetDdrByTlaIdentiferAsync("AGN-999", It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>(), false), Times.Once);
            mgr.VerifyNoOtherCalls();
        }

        [Test]
        public async Task ExecuteAsync_WhenConfirmedTrue_PersistsDDR()
        {
            var mgr = new Mock<IDdrManager>(MockBehavior.Strict);

            // Confirmed path should still check existence first.
            mgr.Setup(m => m.GetDdrByTlaIdentiferAsync("AGN-050", It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>(), false))
               .ReturnsAsync(null as DetailedDesignReview);

            mgr.Setup(m => m.AddDdrAsync(It.IsAny<DetailedDesignReview>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()))
               .ReturnsAsync(InvokeResult.Success);

            var tool = CreateTool(mgr);

            var args = new
            {
                Markdown = BuildMarkdown("AGN-050", "Confirmed DDR"),
                DdrType = "Instruction",
                NeedsHumanConfirmation = false,
                HumanSummary = "Summary.",
                CondensedDdrContent = "Condensed.",
                RagIndexCard = "AGN-050 Instruction Approved 2025-12-21: Routes DDR.",

                // Keep this aligned with your updated implementation.
                // If your implementation still accepts ModeInstructionDdrs as a string, pass a string.
                AgentInstructions = new string[]
                    { "MUST do X.","MUST NOT do Y." },

                DryRun = false,
                Confirmed = true
            };

            var res = await tool.ExecuteAsync(ToArgsJson(args), BuildContext(), CancellationToken.None);

            Assert.That(res.Successful, Is.True, res.ErrorMessage);

            mgr.Verify(m => m.GetDdrByTlaIdentiferAsync(
                "AGN-050",
                It.IsAny<EntityHeader>(),
                It.IsAny<EntityHeader>(),
                false),
            Times.Once);

            mgr.Verify(m => m.AddDdrAsync(
                    It.Is<DetailedDesignReview>(d => d.DdrIdentifier == "AGN-050" && d.Name == "Confirmed DDR"),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>()),
                Times.Once);

            mgr.VerifyNoOtherCalls();
        }

        [Test]
        public async Task ExecuteAsync_WhenConfirmedTrue_AndDdrAlreadyExists_ReturnsError_AndDoesNotPersist()
        {
            var mgr = new Mock<IDdrManager>(MockBehavior.Strict);

            // Confirmed path should still check existence first.
            mgr.Setup(m => m.GetDdrByTlaIdentiferAsync("AGN-777", It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>(), false))
               .ReturnsAsync(new DetailedDesignReview { DdrIdentifier = "AGN-777", Name = "Already There" });

            // If the tool incorrectly tries to persist, we want to catch it.
            mgr.Setup(m => m.AddDdrAsync(It.IsAny<DetailedDesignReview>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()))
               .ReturnsAsync(InvokeResult.Success);

            var tool = CreateTool(mgr);

            var args = new
            {
                Markdown = BuildMarkdown("AGN-777", "Duplicate DDR"),
                DdrType = "Generation",
                NeedsHumanConfirmation = false,
                HumanSummary = "Summary.",
                CondensedDdrContent = "Condensed.",
                RagIndexCard = "AGN-777 Generation Approved 2025-12-21: Routes DDR.",
                DryRun = false,
                Confirmed = true
            };

            var res = await tool.ExecuteAsync(ToArgsJson(args), BuildContext(), CancellationToken.None);

            Assert.That(res.Successful, Is.False);
            Assert.That(res.ErrorMessage, Does.Contain("already exists").IgnoreCase);

            mgr.Verify(m => m.GetDdrByTlaIdentiferAsync("AGN-777", It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>(), false), Times.Once);
            mgr.Verify(m => m.AddDdrAsync(It.IsAny<DetailedDesignReview>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()), Times.Never);
            mgr.VerifyNoOtherCalls();
        }


        [Test]
        public async Task ExecuteAsync_WhenMarkdownMissing_ReturnsError()
        {
            var mgr = new Mock<IDdrManager>(MockBehavior.Strict);
            var tool = CreateTool(mgr);

            var args = new
            {
                Markdown = (string)null,
                Type = "Generation",
                DryRun = true
            };

            var res = await tool.ExecuteAsync(ToArgsJson(args), BuildContext(), CancellationToken.None);

            Assert.That(res.Successful, Is.False);
            Assert.That(res.ErrorMessage, Does.Contain("requires 'markdown'").IgnoreCase);
            mgr.VerifyNoOtherCalls();
        }

        [Test]
        public async Task ExecuteAsync_WhenIdentifierCannotBeParsed_ReturnsError()
        {
            var mgr = new Mock<IDdrManager>(MockBehavior.Strict);
            var tool = CreateTool(mgr);

            var md = "# Bad DDR\n\n**Title:** Missing ID\n";

            var args = new
            {
                Markdown = md,
                Type = "Generation",
                NeedsHumanConfirmation = true,
                HumanSummary = "Summary.",
                CondensedDdrContent = "Condensed.",
                RagIndexCard = "Unknown.",
                DryRun = true
            };

            var res = await tool.ExecuteAsync(ToArgsJson(args), BuildContext(), CancellationToken.None);

            Assert.That(res.Successful, Is.False);
            Assert.That(res.ErrorMessage, Does.Contain("could not parse 'identifier'").IgnoreCase);
            mgr.VerifyNoOtherCalls();
        }
    }
}
