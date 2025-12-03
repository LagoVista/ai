using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    [TestFixture]
    public class JsonTitleDescriptionRefinementCatalogStoreTests
    {
        private static string CreateTempPath()
        {
            return Path.GetTempPath();
        }

        [Test]
        public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptyCatalog()
        {
            var path = CreateTempPath();

            var config = new IngestionConfig() { Ingestion = new FileIngestionConfig() { SourceRoot = path }, DomainCatalogPath = "rag-content" };
            var fullPath = Path.Combine(config.Ingestion.SourceRoot, config.DomainCatalogPath, JsonTitleDescriptionRefinementCatalogStore.CatalogFileName);

            if (File.Exists(fullPath)) File.Delete(fullPath);
            var logger = new Mock<IAdminLogger>();
            var store = new JsonTitleDescriptionRefinementCatalogStore(config, logger.Object);

            var catalog = await store.LoadAsync(CancellationToken.None);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Refined, Is.Empty);
            Assert.That(catalog.Failures, Is.Empty);
            Assert.That(catalog.Warnings, Is.Empty);
            Assert.That(catalog.Skipped, Is.Empty);
        }

        [Test]
        public async Task SaveAsync_ThenLoadAsync_RoundTripsCatalogAndAddsFooter()
        {
            var path = CreateTempPath();
            var logger = new Mock<IAdminLogger>();

            var config = new IngestionConfig() { Ingestion = new FileIngestionConfig() { SourceRoot = CreateTempPath() }, DomainCatalogPath = "rag-content" };

            var store = new JsonTitleDescriptionRefinementCatalogStore(config, logger.Object);

            var catalog = new TitleDescriptionCatalog();
            catalog.Refined.Add(new TitleDescriptionCatalogEntry
            {
                Kind = CatalogEntryKind.Model,
                RepoId = "repo1",
                File = "file.cs",
                FileHash = "hash1",
                SymbolName = "AgentContext",
                IndexVersion = "1",
                Timestamp = DateTime.UtcNow,
                OriginalTitle = "Old",
                OriginalDescription = "Old Desc",
                RefinedTitle = "New",
                RefinedDescription = "New Desc"
            });

            var fullPath = Path.Combine(config.Ingestion.SourceRoot, config.DomainCatalogPath, JsonTitleDescriptionRefinementCatalogStore.CatalogFileName);

            await store.SaveAsync(catalog, CancellationToken.None);

            Assert.That(File.Exists(fullPath), Is.True);

            var text = await File.ReadAllTextAsync(fullPath);
            Assert.That(text, Does.Contain("----- IDX-066 SUMMARY -----"));
            Assert.That(text, Does.Contain("Refined Models: 1"));

            var loaded = await store.LoadAsync(CancellationToken.None);
            Assert.That(loaded.Refined, Has.Count.EqualTo(1));
            Assert.That(loaded.Refined[0].SymbolName, Is.EqualTo("AgentContext"));
            Assert.That(loaded.Refined[0].RefinedTitle, Is.EqualTo("New"));
        }
    }
}
