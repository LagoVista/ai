using System.Collections.Generic;
using LagoVista.AI.Rag.Scoring;
using LagoVista.Core.PlatformSupport;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    [TestFixture]
    public class SummarySectionScoreHandlerTests
    {
        private LogOnlySummarySectionScoreHandler CreateHandler(double minPublishScore, out Mock<IAdminLogger> loggerMock)
        {
            loggerMock = new Mock<IAdminLogger>();
            var options = new SummarySectionScoreHandlerOptions
            {
                MinPublishScore = minPublishScore
            };

            return new LogOnlySummarySectionScoreHandler(loggerMock.Object, options);
        }

        [Test]
        public void Handle_ScoreAboveThreshold_PublishesWithoutWarning()
        {
            var handler = CreateHandler(60.0, out var loggerMock);

            var request = new SummarySectionScoreRequest
            {
                SnippetId = "snippet-1",
                SubtypeKind = SummarySectionSubtypeKind.SummarySection,
                Text = "Some reasonably good summary text."
            };

            var scoreResult = new SummarySectionScoreResult
            {
                SnippetId = request.SnippetId,
                SubtypeKind = request.SubtypeKind,
                CompositeScore = 75.0,
                Category = SummarySectionScoreCategory.Good,
                Flags = new List<string>(),
                Reasons = new List<string>()
            };

            var handlingResult = handler.Handle(request, scoreResult);

            Assert.That(handlingResult.ShouldPublish, Is.True);
            Assert.That(handlingResult.Disposition, Is.EqualTo("Accepted"));
            Assert.That(handlingResult.FinalText, Is.EqualTo(request.Text));

            loggerMock.Verify(l => l.AddCustomEvent(
                It.IsAny<LogLevel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<KeyValuePair<string, string>[]>()),
                Times.Never);
        }

        [Test]
        public void Handle_ScoreBelowThreshold_RejectsAndLogsWarning()
        {
            var handler = CreateHandler(60.0, out var loggerMock);

            var request = new SummarySectionScoreRequest
            {
                SnippetId = "snippet-2",
                SubtypeKind = SummarySectionSubtypeKind.SummarySection,
                Text = "Weak summary."
            };

            var scoreResult = new SummarySectionScoreResult
            {
                SnippetId = request.SnippetId,
                SubtypeKind = request.SubtypeKind,
                CompositeScore = 45.0,
                Category = SummarySectionScoreCategory.Poor,
                Flags = new List<string> { "LowCoverage" },
                Reasons = new List<string> { "Text may not provide enough context." }
            };

            var handlingResult = handler.Handle(request, scoreResult);

            Assert.That(handlingResult.ShouldPublish, Is.False);
            Assert.That(handlingResult.Disposition, Is.EqualTo("RejectedLowScore"));
            Assert.That(handlingResult.FinalCompositeScore, Is.EqualTo(45.0));

            loggerMock.Verify(l => l.AddCustomEvent(
                LogLevel.Warning,
                "SummarySectionScoreHandler.LowScore",
                It.IsAny<string>(),
                It.IsAny<KeyValuePair<string, string>[]>()),
                Times.Once);
        }
    }
}
