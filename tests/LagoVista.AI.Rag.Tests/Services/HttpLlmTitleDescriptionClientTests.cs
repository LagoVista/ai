using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.AI.Services;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests.Services
{
    [TestFixture]
    public class HttpLlmTitleDescriptionClientTests
    {
        private Mock<IStructuredTextLlmService> _structuredTextLlmService;
        private Mock<IAdminLogger> _logger;

        [SetUp]
        public void SetUp()
        {
            _structuredTextLlmService = new Mock<IStructuredTextLlmService>();
            _logger = new Mock<IAdminLogger>();
        }

        private static TitleDescriptionReviewRequest CreateSampleRequest()
        {
            return new TitleDescriptionReviewRequest
            {
                Kind = SummaryObjectKind.Model,
                SymbolName = "SampleModel",
                Title = "Original Title",
                Description = "Original description.",
                Help = "Original help.",
                Model = HttpStructuredTextLlmService.DefaultModel,
                DomainKey = "AIAdmin",
                DomainName = "AI Admin",
                DomainDescription = "Admin domain for AI metadata.",
                Fields = new System.Collections.Generic.List<ModelFieldMetadata>()
            };
        }

        private static TitleDescriptionReviewResult CreateReviewResult()
        {
            var result = new TitleDescriptionReviewResult
            {
                Title = "Refined Title",
                Description = "Refined Description",
                Help = "Refined Help",
                HasChanges = true,
                RequiresAttention = false
            };

            result.Warnings.Add("First warning");
            result.Warnings.Add("Second warning");

            return result;
        }

        [Test]
        public async Task ReviewAsync_Success_ReturnsMappedResult_AndPassesExpectedArgs()
        {
            // Arrange
            var request = CreateSampleRequest();
            var expected = CreateReviewResult();

            string capturedSystemPrompt = null;
            string capturedInputText = null;
            string capturedModel = null;
            string capturedOperationName = null;
            string capturedCorrelationId = null;

            var invokeResult = new InvokeResult<TitleDescriptionReviewResult>
            {
                Result = expected
            };

            _structuredTextLlmService
                .Setup(s => s.ExecuteAsync<TitleDescriptionReviewResult>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback((string systemPrompt,
                           string inputText,
                           string model,
                           string operationName,
                           string correlationId,
                           CancellationToken token) =>
                {
                    capturedSystemPrompt = systemPrompt;
                    capturedInputText = inputText;
                    capturedModel = model;
                    capturedOperationName = operationName;
                    capturedCorrelationId = correlationId;
                })
                .ReturnsAsync(invokeResult);

            var client = new HttpLlmTitleDescriptionClient(
                _structuredTextLlmService.Object,
                _logger.Object);

            // Act
            var result = await client.ReviewAsync(request);

            // Assert
            Assert.Multiple(() =>
            {
                // Typed result
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Title, Is.EqualTo("Refined Title"));
                Assert.That(result.Description, Is.EqualTo("Refined Description"));
                Assert.That(result.Help, Is.EqualTo("Refined Help"));
                Assert.That(result.HasChanges, Is.True);
                Assert.That(result.RequiresAttention, Is.False);
                Assert.That(result.Warnings, Has.Count.EqualTo(2));
                Assert.That(result.Warnings[0], Is.EqualTo("First warning"));

                // Argument flow into IStructuredTextLlmService
                Assert.That(capturedSystemPrompt, Is.Not.Null.And.Not.Empty);
                Assert.That(capturedSystemPrompt, Does.Contain("expert technical editor"));

                Assert.That(capturedInputText, Is.Not.Null.And.Not.Empty);
                Assert.That(capturedInputText, Does.Contain("SampleModel"));
                Assert.That(capturedInputText, Does.Contain("Original Title"));

                Assert.That(capturedModel, Is.EqualTo(request.Model));
                Assert.That(capturedOperationName, Is.EqualTo("TitleDescriptionReview"));
                Assert.That(capturedCorrelationId, Is.EqualTo(request.SymbolName));
            });

            _structuredTextLlmService.Verify(s => s.ExecuteAsync<TitleDescriptionReviewResult>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public void ReviewAsync_FailedInvokeResult_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = CreateSampleRequest();

            var failed = new InvokeResult<TitleDescriptionReviewResult>();
            failed.AddUserError("LLM call failed.");

            _structuredTextLlmService
                .Setup(s => s.ExecuteAsync<TitleDescriptionReviewResult>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(failed);

            var client = new HttpLlmTitleDescriptionClient(
                _structuredTextLlmService.Object,
                _logger.Object);

            // Act + Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.ReviewAsync(request));
        }

        [Test]
        public void ReviewAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            var client = new HttpLlmTitleDescriptionClient(
                _structuredTextLlmService.Object,
                _logger.Object);

            // Act + Assert
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await client.ReviewAsync(null));
        }
    }
}
