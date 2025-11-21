using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Services;
using LagoVista.AI.Rag.Models;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests.Ingestion
{
    [TestFixture]
    public class DefaultIngestionPlannerTests
    {
        private const string RepoId = "Repo1";

        private DefaultIngestionPlanner _planner;

        [SetUp]
        public void Setup()
        {
            _planner = new DefaultIngestionPlanner();
        }

        [Test]
        public async Task NewFile_IsScheduledForFullIndex()
        {
            var discovered = new List<string>
            {
                "Models/Device.cs"
            };

            var localIndex = new LocalIndexStore
            {
                RepoId = RepoId,
                ProjectRoot = "./src"
            };

            var plan = await _planner.BuildPlanAsync(
                RepoId,
                discovered,
                localIndex,
                CancellationToken.None);

            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.RepoRoot, Is.EqualTo(RepoId));
            Assert.That(plan.ProjectId, Is.EqualTo("./src"));

            Assert.That(plan.FilesToIndex.Count, Is.EqualTo(1));
            var item = plan.FilesToIndex[0];

            Assert.That(item.CanonicalPath, Is.EqualTo("Models/Device.cs"));
            Assert.That(item.DocId, Is.Null);
            Assert.That(item.Reindex, Is.EqualTo("full"));
            Assert.That(item.IsActive, Is.True);
            Assert.That(plan.DocsToDelete.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ExistingFile_DefaultsToChunkReindex()
        {
            var discovered = new List<string>
            {
                "Models/Device.cs"
            };

            var localIndex = new LocalIndexStore
            {
                RepoId = RepoId,
                ProjectRoot = "./src"
            };

            var record = localIndex.GetOrAdd("Models/Device.cs", "doc-1");
            record.ContentHash = "hash-1";
            record.ActiveContentHash = "hash-1";
            // record.Reindex null/empty => planner should treat this as "chunk"
            // IsActive will be false, since ContentHash == ActiveContentHash

            var plan = await _planner.BuildPlanAsync(
                RepoId,
                discovered,
                localIndex,
                CancellationToken.None);

            Assert.That(plan.FilesToIndex.Count, Is.EqualTo(1));
            var item = plan.FilesToIndex[0];

            Assert.That(item.CanonicalPath, Is.EqualTo("Models/Device.cs"));
            Assert.That(item.DocId, Is.EqualTo("doc-1"));
            Assert.That(item.Reindex, Is.EqualTo("chunk"));
            Assert.That(item.IsActive, Is.EqualTo(record.IsActive));
            Assert.That(plan.DocsToDelete.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ExistingFile_RespectsPerFileReindexFlag()
        {
            var discovered = new List<string>
            {
                "Models/Device.cs"
            };

            var localIndex = new LocalIndexStore
            {
                RepoId = RepoId,
                ProjectRoot = "./src"
            };

            var record = localIndex.GetOrAdd("Models/Device.cs", "doc-1");
            record.ContentHash = "hash-1";
            record.ActiveContentHash = "hash-2"; // changed
            record.Reindex = "full";

            var plan = await _planner.BuildPlanAsync(
                RepoId,
                discovered,
                localIndex,
                CancellationToken.None);

            Assert.That(plan.FilesToIndex.Count, Is.EqualTo(1));
            var item = plan.FilesToIndex[0];

            Assert.That(item.CanonicalPath, Is.EqualTo("Models/Device.cs"));
            Assert.That(item.DocId, Is.EqualTo("doc-1"));
            Assert.That(item.Reindex, Is.EqualTo("full"));
            Assert.That(item.IsActive, Is.EqualTo(record.IsActive));
            Assert.That(plan.DocsToDelete.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task MissingFile_IsScheduledForDeletion()
        {
            var discovered = new List<string>(); // nothing discovered

            var localIndex = new LocalIndexStore
            {
                RepoId = RepoId,
                ProjectRoot = "./src"
            };

            var record = localIndex.GetOrAdd("Models/Old.cs", "doc-old-1");
            record.ContentHash = "hash-1";
            record.ActiveContentHash = "hash-1";

            var plan = await _planner.BuildPlanAsync(
                RepoId,
                discovered,
                localIndex,
                CancellationToken.None);

            Assert.That(plan.FilesToIndex.Count, Is.EqualTo(0));
            Assert.That(plan.DocsToDelete.Count, Is.EqualTo(1));

            var deletion = plan.DocsToDelete[0];
            Assert.That(deletion.CanonicalPath, Is.EqualTo(record.FilePath));
            Assert.That(deletion.DocId, Is.EqualTo("doc-old-1"));
            Assert.That(deletion.RepoRoot, Is.EqualTo(RepoId));
            Assert.That(deletion.ProjectId, Is.EqualTo("./src"));
        }

        [Test]
        public void BuildPlanAsync_ThrowsOnNullArguments()
        {
            var localIndex = new LocalIndexStore();

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _planner.BuildPlanAsync(RepoId, null, localIndex, CancellationToken.None));

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _planner.BuildPlanAsync(RepoId, Array.Empty<string>(), null, CancellationToken.None));
        }
    }
}
