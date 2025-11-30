using System.Collections.Generic;
using LagoVista.AI.Rag.Scoring;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    [TestFixture]
    public class SummarySectionScoringServiceTests
    {
        private SummarySectionScoringService CreateService(IEnumerable<GlobalModelDescriptor> models = null)
        {
            var options = new SummarySectionScoringOptions
            {
                DomainVerbs = new List<string> { "provision", "register", "embed", "index" },
                RoleKeywords = new List<string> { "manager", "service", "tool", "primitive" }
            };

            models = models ?? new[]
            {
                new GlobalModelDescriptor { Name = "Device", Domain = "IoT", Description = "Represents a physical IoT device." },
                new GlobalModelDescriptor { Name = "Alert", Domain = "IoT", Description = "Represents a triggered alert." }
            };

            return new SummarySectionScoringService(models, options);
        }

        [Test]
        public void Score_GoodSummary_HasHighCompositeScore()
        {
            var service = CreateService();

            var request = new SummarySectionScoreRequest
            {
                SnippetId = "snippet-1",
                SubtypeKind = SummarySectionSubtypeKind.SummarySection,
                Text = "This summary describes the Device entity in the IoT domain. " +
                       "It represents a physical device and is used by the DeviceManager to register and provision devices."
            };

            var result = service.Score(request);

            Assert.That(result.CompositeScore, Is.GreaterThan(70));
            Assert.That(result.Category, Is.EqualTo(SummarySectionScoreCategory.Good).Or.EqualTo(SummarySectionScoreCategory.Excellent));
            Assert.That(result.MatchedModels, Has.Count.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Score_VeryShortSummary_HasLowStructuralClarity()
        {
            var service = CreateService();

            var request = new SummarySectionScoreRequest
            {
                SnippetId = "snippet-2",
                SubtypeKind = SummarySectionSubtypeKind.SummarySection,
                Text = "Device stuff."
            };

            var result = service.Score(request);

            Assert.That(result.DimensionScores[ScoreDimension.StructuralClarity], Is.LessThan(80));
            Assert.That(result.Flags, Does.Contain("VeryShortText"));
        }

        [Test]
        public void Score_EmptyText_IsRejected()
        {
            var service = CreateService();

            var request = new SummarySectionScoreRequest
            {
                SnippetId = "snippet-3",
                SubtypeKind = SummarySectionSubtypeKind.SummarySection,
                Text = "   "
            };

            var result = service.Score(request);

            Assert.That(result.CompositeScore, Is.EqualTo(0));
            Assert.That(result.Category, Is.EqualTo(SummarySectionScoreCategory.Reject));
            Assert.That(result.Flags, Does.Contain("EmptyText"));
        }

        [Test]
        public void Score_IsDeterministic_ForSameInput()
        {
            var service = CreateService();

            var request = new SummarySectionScoreRequest
            {
                SnippetId = "snippet-4",
                SubtypeKind = SummarySectionSubtypeKind.SummarySection,
                Text = "The Alert entity represents a triggered alert that is raised by devices and processed by the alert pipeline."
            };

            var first = service.Score(request);
            var second = service.Score(request);

            Assert.That(first.CompositeScore, Is.EqualTo(second.CompositeScore));
            Assert.That(first.Category, Is.EqualTo(second.Category));
        }
    }
}
