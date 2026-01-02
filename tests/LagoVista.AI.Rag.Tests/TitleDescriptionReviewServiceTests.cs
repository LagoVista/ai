using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    [TestFixture]
    public class TitleDescriptionReviewServiceTests
    {
        [Test]
        public async Task ReviewAsync_SuccessfulResponse_MapsRefinedValues()
        {
            var llmClient = new Mock<ITitleDescriptionLlmClient>();
            var logger = new Mock<IAdminLogger>();

            llmClient.Setup(c =>
                    c.ReviewAsync(
                        It.IsAny<TitleDescriptionReviewRequest>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TitleDescriptionReviewResult
                {
                    RequiresAttention = false,
                    Title = "New Title",
                    Description = "New Description"
                });

            var svc = new TitleDescriptionReviewService(llmClient.Object, logger.Object);

            var result = await svc.ReviewAsync(
                SummaryObjectKind.Model,
                "AgentContext",
                "Old Title",
                "Old Description",
                null,
                "field context",
                CancellationToken.None);

            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.RequiresAttention, Is.False);
            Assert.That(result.RefinedTitle, Is.EqualTo("New Title"));
            Assert.That(result.RefinedDescription, Is.EqualTo("New Description"));
            Assert.That(result.RefinedHelp, Is.Null);
        }

        [Test]
        public async Task ReviewAsync_RequiresAttention_KeepsOriginalValues()
        {
            var llmClient = new Mock<ITitleDescriptionLlmClient>();
            var logger = new Mock<IAdminLogger>();

            llmClient.Setup(c =>
                    c.ReviewAsync(
                        It.IsAny<TitleDescriptionReviewRequest>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TitleDescriptionReviewResult
                {
                    RequiresAttention = true,
                    // No refined values set – service should fall back to originals
                });

            var svc = new TitleDescriptionReviewService(llmClient.Object, logger.Object);

            var result = await svc.ReviewAsync(
                SummaryObjectKind.Model,
                "AgentContext",
                "Old Title",
                "Old Description",
                null,
                "field context",
                CancellationToken.None);

            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.RequiresAttention, Is.True);
            Assert.That(result.RefinedTitle, Is.EqualTo("Old Title"));
            Assert.That(result.RefinedDescription, Is.EqualTo("Old Description"));
        }

        [Test]
        public async Task ReviewAsync_InvalidJson_IsFailure()
        {
            var llmClient = new Mock<ITitleDescriptionLlmClient>();
            var logger = new Mock<IAdminLogger>();

            llmClient.Setup(c =>
                    c.ReviewAsync(
                        It.IsAny<TitleDescriptionReviewRequest>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TitleDescriptionReviewResult
                {
                    IsError = true,
                    FailureReason = "It just failed"
                });

            var svc = new TitleDescriptionReviewService(llmClient.Object, logger.Object);

            var result = await svc.ReviewAsync(
                SummaryObjectKind.Model,
                "AgentContext",
                "Old Title",
                "Old Description",
                null,
                null,
                CancellationToken.None);

            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.FailureReason, Is.Not.Null.And.Not.Empty);
        }

        // --------------------------------------------------------------------
        // New tests
        // --------------------------------------------------------------------

        [Test]
        public async Task ReviewAsync_DetectsChanges_WhenLlmOmitsHasChanges()
        {
            var llmClient = new Mock<ITitleDescriptionLlmClient>();
            var logger = new Mock<IAdminLogger>();

            // LLM returns different values, but forgets to set HasChanges = true
            llmClient.Setup(c =>
                    c.ReviewAsync(
                        It.IsAny<TitleDescriptionReviewRequest>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TitleDescriptionReviewResult
                {
                    Title = "Refined Title",
                    Description = "Refined Description",
                    Help = "Refined Help",
                    HasChanges = false,              // simulate bug in LLM result
                    RequiresAttention = false
                });

            var svc = new TitleDescriptionReviewService(llmClient.Object, logger.Object);

            var result = await svc.ReviewAsync(
                SummaryObjectKind.Model,
                "AgentContext",
                "Old Title",
                "Old Description",
                "Old Help",
                "field context",
                CancellationToken.None);

            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.RefinedTitle, Is.EqualTo("Refined Title"));
            Assert.That(result.RefinedDescription, Is.EqualTo("Refined Description"));
            Assert.That(result.RefinedHelp, Is.EqualTo("Refined Help"));

            // Service should infer HasChanges based on value differences
            Assert.That(result.HasChanges, Is.True);
            Assert.That(result.RequiresAttention, Is.False);
        }

        [Test]
        public async Task ReviewAsync_EmptyTitleOrDescription_ForcesRequiresAttention_AndKeepsOriginals()
        {
            var llmClient = new Mock<ITitleDescriptionLlmClient>();
            var logger = new Mock<IAdminLogger>();

            // LLM returns empty strings – guard rail should kick in
            llmClient.Setup(c =>
                    c.ReviewAsync(
                        It.IsAny<TitleDescriptionReviewRequest>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TitleDescriptionReviewResult
                {
                    Title = string.Empty,
                    Description = string.Empty,
                    Help = null,
                    HasChanges = true,          // even if LLM thinks it changed things
                    RequiresAttention = false   // service should override this
                });

            var svc = new TitleDescriptionReviewService(llmClient.Object, logger.Object);

            var result = await svc.ReviewAsync(
                SummaryObjectKind.Model,
                "AgentContext",
                "Old Title",
                "Old Description",
                "Old Help",
                "field context",
                CancellationToken.None);

            // Guard rail results:
            Assert.That(result.RefinedTitle, Is.EqualTo("Old Title"));
            Assert.That(result.RefinedDescription, Is.EqualTo("Old Description"));
            Assert.That(result.RefinedHelp, Is.EqualTo("Old Help"));
            Assert.That(result.HasChanges, Is.False);
            Assert.That(result.RequiresAttention, Is.True);
            Assert.That(result.Warnings, Has.Some.Contains("LLM returned empty title and/or description"));
        }

        [Test]
        public async Task ReviewAsync_WarningsFromLlm_AppearInWarningsAndNotes()
        {
            var llmClient = new Mock<ITitleDescriptionLlmClient>();
            var logger = new Mock<IAdminLogger>();

            var llmResult = new TitleDescriptionReviewResult
            {
                Title = "New Title",
                Description = "New Description",
                Help = null,
                HasChanges = true,
                RequiresAttention = true
            };
            llmResult.Warnings.Add("Field X may be ambiguous.");
            llmResult.Warnings.Add("Consider reviewing the domain description.");

            llmClient.Setup(c =>
                    c.ReviewAsync(
                        It.IsAny<TitleDescriptionReviewRequest>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(llmResult);

            var svc = new TitleDescriptionReviewService(llmClient.Object, logger.Object);

            var result = await svc.ReviewAsync(
                SummaryObjectKind.Model,
                "AgentContext",
                "Old Title",
                "Old Description",
                null,
                "field context",
                CancellationToken.None);

            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.RequiresAttention, Is.True);
            Assert.That(result.RefinedTitle, Is.EqualTo("New Title"));
            Assert.That(result.RefinedDescription, Is.EqualTo("New Description"));

            // Warnings should be preserved
            Assert.That(result.Warnings, Has.Count.EqualTo(2));
            Assert.That(result.Warnings[0], Does.Contain("Field X may be ambiguous"));
            Assert.That(result.Warnings[1], Does.Contain("Consider reviewing the domain description"));

            // AdditionalConfiguration should be a joined summary
            Assert.That(result.Notes, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Notes, Does.Contain("Field X may be ambiguous"));
        }

        [Test]
        public async Task ReviewAsync_PopulatesContextBlob_WithExternalContext()
        {
            var llmClient = new Mock<ITitleDescriptionLlmClient>();
            var logger = new Mock<IAdminLogger>();

            TitleDescriptionReviewRequest capturedRequest = null;

            llmClient.Setup(c =>
                    c.ReviewAsync(
                        It.IsAny<TitleDescriptionReviewRequest>(),
                        It.IsAny<CancellationToken>()))
                .Callback<TitleDescriptionReviewRequest, CancellationToken>((req, _) =>
                {
                    capturedRequest = req;
                })
                .ReturnsAsync(new TitleDescriptionReviewResult
                {
                    Title = "New Title",
                    Description = "New Description",
                    HasChanges = true
                });

            var svc = new TitleDescriptionReviewService(llmClient.Object, logger.Object);

            const string externalContext = "field1:title,field2:description";

            var result = await svc.ReviewAsync(
                SummaryObjectKind.Model,
                "AgentContext",
                "Old Title",
                "Old Description",
                null,
                externalContext,
                CancellationToken.None);

            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest.ContextBlob, Is.Not.Null.And.Not.Empty);

            // Basic sanity: core values and external context should be present
            Assert.That(capturedRequest.ContextBlob, Does.Contain("Old Title"));
            Assert.That(capturedRequest.ContextBlob, Does.Contain("Old Description"));
            Assert.That(capturedRequest.ContextBlob, Does.Contain(externalContext));
        }
    }
}
