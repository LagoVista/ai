using LagoVista.AI.Rag.ContractPacks.Infrastructure.Services;
using LagoVista.AI.Rag.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Tests.Services
{
    [TestFixture]
    public class FileDiscoveryServiceTests
    {
        private string _root;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(Path.Combine(_root, "Repo1", "bin"));

            File.WriteAllText(Path.Combine(_root, "Repo1", "test1.cs"), "// hello world");
            File.WriteAllText(Path.Combine(_root, "Repo1", "test2.resx"), "<root></root>");
            File.WriteAllBytes(Path.Combine(_root, "Repo1", "bin", "test.dll"), new byte[200]);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [Test]
        public async Task DiscoverAsync_FindsFiles()
        {
            var config = new IngestionConfig
            {
                Ingestion = new FileIngestionConfig
                {
                    SourceRoot = _root,
                    Repositories = new List<string> { "Repo1" },
                    Include = new List<string> { "**/*.cs", "**/*.resx" },
                    Exclude = new List<string> { "bin/**" }
                }
            };

            var svc = new FileDiscoveryService();

            var files = await svc.DiscoverAsync(config, "Repo1", "", CancellationToken.None);

            Assert.That(files.Count, Is.EqualTo(2));
            Assert.That(files.Any(f => f.RelativePath == "test1.cs"), Is.True);
            Assert.That(files.Any(f => f.RelativePath == "test2.resx"), Is.True);
        }

        [Test]
        public async Task DiscoverAsync_Excludes_Binary_Files()
        {
            var config = new IngestionConfig
            {
                Ingestion = new FileIngestionConfig
                {
                    SourceRoot = _root,
                    Repositories = new List<string> { "Repo1" },
                    Include = new List<string> { "**/*" },
                    Exclude = new List<string>()
                }
            };

            var svc = new FileDiscoveryService();
            var files = await svc.DiscoverAsync(config, "Repo1", CancellationToken.None);

            Assert.That(files.Any(f => f.RelativePath.Contains("test.dll")), Is.False);
        }

        [Test]
        public void DiscoverAsync_InvalidRepo_Throws()
        {
            var config = new IngestionConfig
            {
                Ingestion = new FileIngestionConfig
                {
                    SourceRoot = _root,
                    Repositories = new List<string> { "Repo1" }
                }
            };

            var svc = new FileDiscoveryService();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.DiscoverAsync(config, "BadRepo","", CancellationToken.None));
        }
    }
}
