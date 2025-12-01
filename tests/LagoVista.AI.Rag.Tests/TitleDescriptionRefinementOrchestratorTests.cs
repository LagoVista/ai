using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    [TestFixture]
    public class TitleDescriptionRefinementOrchestratorTests
    {
        [Test]
        public async Task RunAsync_ModelFailure_SkipsDomainPass()
        {
            var reviewService = new Mock<ITitleDescriptionReviewService>();
            var modelSource = new Mock<IModelMetadataSource>();
            var domainSource = new Mock<IDomainMetadataSource>();
            var catalogStore = new Mock<ITitleDescriptionRefinementCatalogStore>();
            var hashService = new Mock<IContentHashService>();
            var logger = new Mock<IAdminLogger>();
            var resxUpdateService = new Mock<IResxUpdateService>();
            var domainDescriptorUpdateService = new Mock<IDomainDescriptorUpdateService>();

            var files = new List<DiscoveredFile>();
            var resources = new Dictionary<string, IReadOnlyDictionary<string, string>>();

            var catalog = new TitleDescriptionCatalog();
            catalogStore.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(catalog);
            catalogStore.Setup(s => s.SaveAsync(It.IsAny<TitleDescriptionCatalog>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);

            var model = new ModelMetadata
            {
                RepoId = "repo1",
                FullPath = "path.cs",
                ClassName = "AgentContext",
                Title = "Title",
                Description = "Desc",
                Help = null,
                TitleResourceKey = "TitleKey",
                DescriptionResourceKey = "DescKey",
                HelpResourceKey = null
            };

            modelSource.Setup(m => m.GetModelsAsync(files, resources, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<ModelMetadata> { model });

            hashService.Setup(h => h.ComputeFileHashAsync("path.cs")).ReturnsAsync("hash1");

            reviewService.Setup(r => r.ReviewAsync(
                    SummaryObjectKind.Model,
                    "AgentContext",
                    "Title",
                    "Desc",
                    null,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TitleDescriptionReviewResult
                {
                    IsError = true,
                    FailureReason = "boom"
                });

            var orchestrator = new TitleDescriptionRefinementOrchestrator(
                reviewService.Object,
                modelSource.Object,
                domainSource.Object,
                catalogStore.Object,
                hashService.Object,
                resxUpdateService.Object,
                domainDescriptorUpdateService.Object,
                logger.Object);

            await orchestrator.RunAsync(files, resources, CancellationToken.None);

            Assert.That(catalog.Failures, Has.Count.EqualTo(1));
            domainSource.Verify(d => d.GetDomainsAsync(It.IsAny<IReadOnlyList<DiscoveredFile>>(), It.IsAny<IReadOnlyList<ModelMetadata>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task RunAsync_NoModelFailures_RunsDomainPass()
        {
            var reviewService = new Mock<ITitleDescriptionReviewService>();
            var modelSource = new Mock<IModelMetadataSource>();
            var domainSource = new Mock<IDomainMetadataSource>();
            var catalogStore = new Mock<ITitleDescriptionRefinementCatalogStore>();
            var hashService = new Mock<IContentHashService>();
            var logger = new Mock<IAdminLogger>();
            var resxUpdateService = new Mock<IResxUpdateService>();
            var domainDescriptorUpdateService = new Mock<IDomainDescriptorUpdateService>();

            var files = new List<DiscoveredFile>();

            // ✅ Provide resources so TitleKey / DescKey can be resolved to a RESX path.
            var resources = new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        {
            @"C:\fake\Strings.resx",
            new Dictionary<string, string>
            {
                { "TitleKey", "Title" },
                { "DescKey",  "Desc"  }
            }
        }
    };

            var catalog = new TitleDescriptionCatalog();
            catalogStore.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(catalog);
            catalogStore.Setup(s => s.SaveAsync(It.IsAny<TitleDescriptionCatalog>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);

            var model = new ModelMetadata
            {
                RepoId = "repo1",
                FullPath = "path.cs",
                ClassName = "AgentContext",
                Title = "Title",
                Description = "Desc",
                Help = null,
                TitleResourceKey = "TitleKey",
                DescriptionResourceKey = "DescKey",
                HelpResourceKey = null
            };

            modelSource.Setup(m => m.GetModelsAsync(files, resources, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<ModelMetadata> { model });

            var domain = new DomainMetadata
            {
                RepoId = "repo1",
                FullPath = "domain.cs",
                ClassName = "AIDomain",
                DomainKey = "AI Admin",
                Name = "AI Admin",
                Description = "Domain desc"
            };

            domainSource.Setup(d => d.GetDomainsAsync(files, It.IsAny<IReadOnlyList<ModelMetadata>>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new List<DomainMetadata> { domain });

            hashService.Setup(h => h.ComputeFileHashAsync("path.cs")).ReturnsAsync("hash1");
            hashService.Setup(h => h.ComputeFileHashAsync("domain.cs")).ReturnsAsync("hash2");

            // ✅ Model review: successful, with changes.
            reviewService.Setup(r => r.ReviewAsync(
                    SummaryObjectKind.Model,
                    "AgentContext",
                    "Title",
                    "Desc",
                    null,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TitleDescriptionReviewResult
                {
                    IsError = false,
                    HasChanges = true,

                    // Depending on your implementation you may also want to set the refined values:
                    Title = "New Title",
                    Description = "New Desc",
                    Help = null
                });

            // ✅ Domain review: successful, no changes required (so it goes down the "skipped" path
            // but still records a DomainCatalogEntry).
            reviewService.Setup(r => r.ReviewAsync(
                    SummaryObjectKind.Domain,
                    "AIDomain",
                    "AI Admin",
                    "Domain desc",
                    null,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TitleDescriptionReviewResult
                {
                    IsError = false,
                    HasChanges = false,
                    Title = "AI Admin",
                    Description = "Domain desc"
                });

            // ✅ Make sure RESX writes don't throw (otherwise they'd be recorded as failures).
            resxUpdateService.Setup(s => s.ApplyUpdatesAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var orchestrator = new TitleDescriptionRefinementOrchestrator(
                reviewService.Object,
                modelSource.Object,
                domainSource.Object,
                catalogStore.Object,
                hashService.Object,
                resxUpdateService.Object,
                domainDescriptorUpdateService.Object,
                logger.Object);

            await orchestrator.RunAsync(files, resources, CancellationToken.None);

            Assert.That(catalog.Failures.Any(e => e.Kind == CatalogEntryKind.Model), Is.False,
                "There should be no model failures in the happy-path test.");

            Assert.That(catalog.Refined, Has.Count.EqualTo(1));
            Assert.That(catalog.Domains, Has.Count.EqualTo(1));
        }

    }
}
