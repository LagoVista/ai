using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Services;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.Core.Models.UIMetaData;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class DomainModelCatalogBuilderTests
    {
        private const string RepoId = "test-repo";

        [Test]
        public void Ctor_NullChunkerServices_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DomainModelCatalogBuilder(null, null));
        }

        [Test]
        public void BuildAsync_NullRepoId_Throws()
        {
            var chunkerMock = new Mock<IChunkerServices>();
            var descriptorMock = new Mock<ICodeDescriptionService>(MockBehavior.Strict);

            var sut = new DomainModelCatalogBuilder(chunkerMock.Object, descriptorMock.Object);

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await sut.BuildAsync(null, new List<DiscoveredFile>()));
        }

        [Test]
        public void BuildAsync_NullFiles_Throws()
        {
            var chunkerMock = new Mock<IChunkerServices>();
            var descriptorMock = new Mock<ICodeDescriptionService>(MockBehavior.Strict);

            var sut = new DomainModelCatalogBuilder(chunkerMock.Object, descriptorMock.Object);

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await sut.BuildAsync(RepoId, null));
        }

        [Test]
        public async Task BuildAsync_Skips_NonCs_And_MissingFiles()
        {
            var chunkerMock = new Mock<IChunkerServices>(MockBehavior.Strict);
            var descriptorMock = new Mock<ICodeDescriptionService>(MockBehavior.Strict);

            var sut = new DomainModelCatalogBuilder(chunkerMock.Object, descriptorMock.Object);

            var tempDir = CreateTempDirectory();

            try
            {
                var txtPath = Path.Combine(tempDir, "readme.txt");
                File.WriteAllText(txtPath, "// not C#");

                var missingCsPath = Path.Combine(tempDir, "Missing.cs");

                var files = new List<DiscoveredFile>
                {
                    new DiscoveredFile { FullPath = txtPath, RelativePath = "readme.txt" },
                    new DiscoveredFile { FullPath = missingCsPath, RelativePath = "Missing.cs" }
                };

                var catalog = await sut.BuildAsync(RepoId, files);

                var domains = GetDomainsDictionary(catalog);
                var models = GetModelsDictionary(catalog);

                Assert.That(domains.Count, Is.EqualTo(0));
                Assert.That(models.Count, Is.EqualTo(0));

                chunkerMock.Verify(c => c.DetectForFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                chunkerMock.Verify(c => c.ExtractDomains(It.IsAny<string>()), Times.Never);
                descriptorMock.Verify(d => d.BuildModelStructureDescription(
                    It.IsAny<string>()), Times.Never);
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Test]
        public async Task BuildAsync_Populates_Domains_And_Models()
        {
            var chunkerMock = new Mock<IChunkerServices>();
            var descriptorMock = new Mock<ICodeDescriptionService>(MockBehavior.Strict);

            var sut = new DomainModelCatalogBuilder(chunkerMock.Object, descriptorMock.Object);

            var tempDir = CreateTempDirectory();

            try
            {
                var fullPath = Path.Combine(tempDir, "Device.cs");
                const string sourceText = "public class Device { }";
                File.WriteAllText(fullPath, sourceText);

                var files = new List<DiscoveredFile>
                {
                    new DiscoveredFile
                    {
                        FullPath = fullPath,
                        RelativePath = "Models/Device.cs"
                    }
                };

                var detectionResult = new SourceKindResult
                {
                    SubKind = CodeSubKind.Model,
                    PrimaryTypeName = "Device",
                    SymbolText = null
                };

                chunkerMock
                    .Setup(c => c.DetectForFile(sourceText, "Models/Device.cs"))
                    .Returns(detectionResult);

                var domainInfo = new DomainSummaryInfo("Devices", "Devices Domain", "Domain Description", DomainDescription.DomainTypes.Service, "DomainType", "Domainproperty");

                chunkerMock
                    .Setup(c => c.ExtractDomains(sourceText))
                    .Returns(new[] { domainInfo });

                var modelStructure = new ModelStructureDescription
                {
                    ModelName = "Device",
                    Namespace = "LagoVista.Devices",
                    QualifiedName = "LagoVista.Devices.Device",
                    BusinessDomainKey = "Devices"
                };

                descriptorMock
                    .Setup(c => c.BuildModelStructureDescription(
                        sourceText))
                    .Returns(modelStructure);

                var catalog = await sut.BuildAsync(RepoId, files);

                var domains = GetDomainsDictionary(catalog);
                var models = GetModelsDictionary(catalog);

                Assert.That(domains.ContainsKey("Devices"), Is.True, "Expected Devices domain in catalog.");
                var devicesDomain = domains["Devices"];
                Assert.That(devicesDomain.DomainKey, Is.EqualTo("Devices"));

                Assert.That(models.ContainsKey("LagoVista.Devices.Device"), Is.True, "Expected Device model entry.");

                var modelEntry = models["LagoVista.Devices.Device"];
                Assert.That(modelEntry.RepoId, Is.EqualTo(RepoId));
                Assert.That(modelEntry.RelativePath, Is.EqualTo("Models/Device.cs"));
                Assert.That(modelEntry.SubKind, Is.EqualTo(CodeSubKind.Model));
                Assert.That(modelEntry.Structure, Is.Not.Null);
                Assert.That(modelEntry.Structure.QualifiedName, Is.EqualTo("LagoVista.Devices.Device"));

                chunkerMock.Verify(c => c.DetectForFile(sourceText, "Models/Device.cs"), Times.Once);
                chunkerMock.Verify(c => c.ExtractDomains(sourceText), Times.Once);
                descriptorMock.Verify(d => d.BuildModelStructureDescription(
                    sourceText), Times.Once);
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Test]
        public void BuildAsync_Honors_CancellationToken()
        {
            var chunkerMock = new Mock<IChunkerServices>(MockBehavior.Strict);
            var descriptorMock = new Mock<ICodeDescriptionService>(MockBehavior.Strict);

            var sut = new DomainModelCatalogBuilder(chunkerMock.Object, descriptorMock.Object);

            var tempDir = CreateTempDirectory();

            try
            {
                var fullPath = Path.Combine(tempDir, "Device.cs");
                File.WriteAllText(fullPath, "public class Device { }");

                var files = new List<DiscoveredFile>
                {
                    new DiscoveredFile
                    {
                        FullPath = fullPath,
                        RelativePath = "Models/Device.cs"
                    }
                };

                using var cts = new CancellationTokenSource();
                cts.Cancel();

                Assert.ThrowsAsync<OperationCanceledException>(async () =>
                    await sut.BuildAsync(RepoId, files, cts.Token));

                chunkerMock.Verify(c => c.DetectForFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                chunkerMock.Verify(c => c.ExtractDomains(It.IsAny<string>()), Times.Never);
                descriptorMock.Verify(c => c.BuildModelStructureDescription(
                    It.IsAny<string>()), Times.Never);
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        private static string CreateTempDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "DomainModelCatalogBuilderTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // best-effort cleanup only
            }
        }

        private static IReadOnlyDictionary<string, DomainSummaryInfo> GetDomainsDictionary(DomainModelCatalog catalog)
        {
            var dictType = typeof(IReadOnlyDictionary<string, DomainSummaryInfo>);

            var prop = typeof(DomainModelCatalog)
                .GetProperties()
                .FirstOrDefault(p => dictType.IsAssignableFrom(p.PropertyType));

            Assert.That(prop, Is.Not.Null, "Could not locate domains dictionary property on DomainModelCatalog.");

            return (IReadOnlyDictionary<string, DomainSummaryInfo>)prop.GetValue(catalog)!;
        }

        private static IReadOnlyDictionary<string, ModelCatalogEntry> GetModelsDictionary(DomainModelCatalog catalog)
        {
            var dictType = typeof(IReadOnlyDictionary<string, ModelCatalogEntry>);

            var prop = typeof(DomainModelCatalog)
                .GetProperties()
                .FirstOrDefault(p => dictType.IsAssignableFrom(p.PropertyType));

            Assert.That(prop, Is.Not.Null, "Could not locate models dictionary property on DomainModelCatalog.");

            return (IReadOnlyDictionary<string, ModelCatalogEntry>)prop.GetValue(catalog)!;
        }
    }
}
