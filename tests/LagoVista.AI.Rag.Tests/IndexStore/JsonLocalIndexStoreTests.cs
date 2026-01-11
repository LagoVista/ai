using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Indexing.Models;
using LagoVista.AI.Rag.ContractPacks.IndexStore.Services;
using LagoVista.AI.Rag.Models;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests.IndexStore
{
    [TestFixture]
    public class JsonLocalIndexStoreTests
    {
        private string _rootFolder;
        private string _repoId;

        [SetUp]
        public void Setup()
        {
            _rootFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootFolder);
            _repoId = "TestRepo";
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_rootFolder))
            {
                try
                {
                    Directory.Delete(_rootFolder, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup; ignore IO contention in tests.
                }
            }
        }

        private IngestionConfig GetConfig()
        {
            return new IngestionConfig()
            {
                Ingestion = new FileIngestionConfig()
                {
                    SourceRoot = _rootFolder
                }
            };
        }


        [Test]
        public async Task LoadAsync_WhenFileMissing_ReturnsEmptyStoreWithRepoId()
        {
            var store = new JsonLocalIndexStore();

            var result = await store.LoadAsync(GetConfig(), _repoId, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.RepoId, Is.EqualTo(_repoId));
            Assert.That(result.Records, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task SaveAsync_ThenLoadAsync_RoundTripsRecords()
        {
            var jsonStore = new JsonLocalIndexStore();

            var local = new LocalIndexStore
            {
                RepoId = null,
                ProjectRoot = "./src"
            };

            var record = local.GetOrAdd("Models/Device.cs", "doc-device-001");
            record.ContentHash = "hash-1";
            record.ActiveContentHash = "hash-1";
            record.SubKind = "Model";
            record.LastIndexedUtc = new DateTime(2025, 01, 01, 12, 0, 0, DateTimeKind.Utc);
            record.FlagForReview = false;
            record.Reindex = null;
            record.Facets.Add(new FacetValue
            {
                Type = "Kind",
                Value = "SourceCode"
            });

            await jsonStore.SaveAsync(GetConfig(), _repoId, local, CancellationToken.None);

            // Now load it back
            var loaded = await jsonStore.LoadAsync(GetConfig(), _repoId, CancellationToken.None);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.RepoId, Is.EqualTo(_repoId));
            Assert.That(loaded.Records, Is.Not.Null);
            Assert.That(loaded.Count, Is.EqualTo(1));

            Assert.That(loaded.TryGet("Models/Device.cs", out var loadedRecord), Is.True);
            Assert.That(loadedRecord, Is.Not.Null);
            Assert.That(loadedRecord.DocId, Is.EqualTo("doc-device-001"));
            Assert.That(loadedRecord.ContentHash, Is.EqualTo("hash-1"));
            Assert.That(loadedRecord.ActiveContentHash, Is.EqualTo("hash-1"));
            Assert.That(loadedRecord.SubKind, Is.EqualTo("Model"));
            Assert.That(loadedRecord.FlagForReview, Is.False);
            Assert.That(loadedRecord.Facets.Count, Is.EqualTo(1));
            Assert.That(loadedRecord.Facets[0].Type, Is.EqualTo("Kind"));
            Assert.That(loadedRecord.Facets[0].Value, Is.EqualTo("SourceCode"));
        }

        [Test]
        public void GetAll_Returns_All_Records()
        {
            var local = new LocalIndexStore
            {
                RepoId = _repoId,
                ProjectRoot = "./src"
            };

            local.GetOrAdd("Models/Device.cs", "doc-device-001");
            local.GetOrAdd("Managers/DeviceManager.cs", "doc-device-manager-001");

            var store = new JsonLocalIndexStore();

            var all = store.GetAll(local);

            Assert.That(all, Is.Not.Null);
            Assert.That(all.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task SaveAsync_Creates_File_On_Disk()
        {
            var jsonStore = new JsonLocalIndexStore();

            var local = new LocalIndexStore
            {
                RepoId = _repoId,
                ProjectRoot = "./src"
            };

            local.GetOrAdd("Models/Device.cs", "doc-device-001");

            await jsonStore.SaveAsync(GetConfig(), _repoId, local, CancellationToken.None);

            var expectedPath = Path.Combine(_rootFolder, _repoId, _repoId + ".local-index.json").Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

            Assert.That(File.Exists(expectedPath), Is.True);
        }
    }
}
