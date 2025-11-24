using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Services;
using LagoVista.AI.Rag.Models;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests.Ingestion
{
    [TestFixture]
    public class IndexFileContextBuilderTests
    {
        private string _root;
        private string _repoId;
        private string _repoFolder;
        private string _fileRelativePath;
        private string _fileFullPath;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "rag-index-ctx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);

            _repoId = "Repo1";
            _repoFolder = Path.Combine(_root, _repoId);
            Directory.CreateDirectory(_repoFolder);

            _fileRelativePath = "Models/Device.cs";
            _fileFullPath = Path.Combine(_repoFolder, _fileRelativePath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(_fileFullPath) ?? _repoFolder);
            File.WriteAllText(_fileFullPath, "public class Device { }" + Environment.NewLine);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_root))
            {
                try { Directory.Delete(_root, recursive: true); }
                catch { }
            }
        }

        [Test]
        public async Task BuildAsync_Creates_Context_And_Updates_LocalIndex_For_New_File()
        {
            var config = new IngestionConfig
            {
                OrgId = "org-1",
                ContentRepo = new ContentRepo { AccountId = "proj-1" },
                Ingestion = new FileIngestionConfig { SourceRoot = _root }
            };

            var localIndex = new LocalIndexStore { RepoId = _repoId, ProjectRoot = "./src" };

            var planned = new PlannedFileIngestion
            {
                RepoRoot = _repoId,
                ProjectId = "proj-1",
                CanonicalPath = _fileRelativePath,
                Reindex = "full",
                IsActive = true
            };

            var repoInfo = new GitRepoInfo()
            {

            };

            var builder = new IndexFileContextBuilder(new Mock<IIndexIdServices>().Object);

            var ctx = await builder.BuildAsync(config, repoInfo, _repoId, planned, localIndex, CancellationToken.None);

            Assert.That(ctx, Is.Not.Null);
            Assert.That(ctx.FullPath, Is.EqualTo(_fileFullPath));
            Assert.That(ctx.RelativePath, Is.EqualTo(_fileRelativePath.Replace('\\', '/')));
            Assert.That(ctx.Language, Is.EqualTo("csharp"));

            Assert.That(ctx.DocumentIdentity, Is.Not.Null);
            Assert.That(string.IsNullOrWhiteSpace(ctx.DocumentIdentity.DocId), Is.False);

            Assert.That(localIndex.TryGet(_fileRelativePath, out var record), Is.True);
            Assert.That(record.DocId, Is.EqualTo(ctx.DocumentIdentity.DocId));
            Assert.That(string.IsNullOrWhiteSpace(record.ActiveContentHash), Is.False);
        }

        [Test]
        public void BuildAsync_Throws_When_File_Missing()
        {
            var config = new IngestionConfig
            {
                OrgId = "org-1",
                Ingestion = new FileIngestionConfig { SourceRoot = _root }
            };

            var localIndex = new LocalIndexStore { RepoId = _repoId };

            var planned = new PlannedFileIngestion
            {
                CanonicalPath = "Models/Missing.cs",
                Reindex = "full",
                IsActive = true
            };

            var repoInfo = new GitRepoInfo()
            {

            };



            var builder = new IndexFileContextBuilder(new Mock<IIndexIdServices>().Object);

            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await builder.BuildAsync(config, repoInfo, _repoId, planned, localIndex, CancellationToken.None));
        }
    }
}
