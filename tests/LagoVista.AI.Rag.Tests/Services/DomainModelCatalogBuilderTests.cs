using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Services;
using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.Core.Models.UIMetaData;
using System.Net.Http;
using LagoVista.Core.Utils.Types;

namespace LagoVista.AI.Rag.Tests.Services
{
    [TestFixture]
    public class DomainModelCatalogBuilderTests
    {
        private string _root;
        private string _repoId;
        private string _csFilePath;
        private string _relativePath;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _repoId = "Repo1";
            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(Path.Combine(_root, _repoId));

            _relativePath = "DomainAndModel.cs";
            _csFilePath = Path.Combine(_root, _repoId, _relativePath);

            File.WriteAllText(_csFilePath, "// dummy C# content for testing");
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        //[Test]
        //public async Task BuildAsync_Builds_Domain_And_Model_Catalog()
        //{
        //    var files = new List<DiscoveredFile>
        //    {
        //        new DiscoveredFile
        //        {
        //            RepoId = _repoId,
        //            FullPath = _csFilePath,
        //            RelativePath = _relativePath,
        //            SizeBytes = 10,
        //            IsBinary = false
        //        }
        //    };

        //    var chunker = new FakeChunkerServices();
        //    var builder = new DomainModelCatalogBuilder(chunker);

        //    var catalog = await builder.BuildAsync(_repoId, files, CancellationToken.None);

        //    Assert.That(catalog, Is.Not.Null);

        //    // Domain should be present
        //    Assert.That(catalog.DomainsByKey.ContainsKey("Devices"), Is.True);
        //    var domain = catalog.DomainsByKey["Devices"];
        //    Assert.That(domain.Title, Is.EqualTo("Devices Domain"));

        //    // Model should be present
        //    Assert.That(catalog.ModelsByQualifiedName.ContainsKey("Acme.Project.Devices.Device"), Is.True);
        //    var modelEntry = catalog.ModelsByQualifiedName["Acme.Project.Devices.Device"];
        //    Assert.That(modelEntry.RepoId, Is.EqualTo(_repoId));
        //    Assert.That(modelEntry.RelativePath, Is.EqualTo(_relativePath));
        //    Assert.That(modelEntry.Structure.Domain, Is.EqualTo("Devices"));
        //    Assert.That(modelEntry.Structure.ModelName, Is.EqualTo("Device"));
        //}

        [Test]
        public async Task BuildAsync_Ignores_Non_Cs_Files()
        {
            var nonCsPath = Path.Combine(_root, _repoId, "readme.txt");
            File.WriteAllText(nonCsPath, "hello");

            var files = new List<DiscoveredFile>
            {
                new DiscoveredFile
                {
                    RepoId = _repoId,
                    FullPath = nonCsPath,
                    RelativePath = "readme.txt",
                    SizeBytes = 5,
                    IsBinary = false
                }
            };

            var detector = new FakeSubKindDetector();
            var chunker = new FakeChunkerServices();
            var builder = new DomainModelCatalogBuilder(chunker);

            var catalog = await builder.BuildAsync(_repoId, files, CancellationToken.None);

            Assert.That(catalog.DomainsByKey.Count, Is.EqualTo(0));
            Assert.That(catalog.ModelsByQualifiedName.Count, Is.EqualTo(0));
        }

        private sealed class FakeSubKindDetector : IChunkerServices
        {
            public ModelMetadataDescription BuildMetadataDescriptionForModel(string sourceText, string relativePath, IReadOnlyDictionary<string, string> resources)
            {
                throw new NotImplementedException();
            }

            public ModelStructureDescription BuildStructuredDescriptionForModel(string sourceText, string relativePath, IReadOnlyDictionary<string, string> resources)
            {
                throw new NotImplementedException();
            }

            public string BuildSummaryForMethod(MethodSummaryContext ctx)
            {
                throw new NotImplementedException();
            }

            public RagChunkPlan ChunkCSharpWithRoslyn(string text, string relPath, string blobPath, int maxTokensPerChunk = 6500, int overlapLines = 6)
            {
                throw new NotImplementedException();
            }


            public SourceKindResult DetectForFile(string sourceText, string relativePath)
            {
                // We return two results: one we intend to be treated as a domain snippet,
                // and one as a model snippet. The builder does not branch on SubKind, so
                // we simply distinguish them via SymbolText content.
                return new SourceKindResult
                {
                    Path = relativePath,
                    SubKind = default(CodeSubKind),
                    PrimaryTypeName = "DevicesDomain",
                    IsMixed = true,
                    Reason = "Fake domain for tests",
                    SymbolText = "DOMAIN_SNIPPET"
                };
            }

            public int EstimateTokens(string s)
            {
                throw new NotImplementedException();
            }

            public IReadOnlyList<DomainSummaryInfo> ExtractDomains(string source, string filePath)
            {
                throw new NotImplementedException();
            }

            public Task<TitleDescriptionReviewResult> ReviewTitleAndDescriptionAsync(SummaryObjectKind kind, string symbolName, string title, string description, string llmUrl, string llmApiKey, HttpClient httpClient = null, string model = "gpt-4.1-mini", CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class FakeChunkerServices : IChunkerServices
        {
            public IReadOnlyList<DomainSummaryInfo> ExtractDomains(string source, string filePath)
            {
                if (source != null && source.Contains("DOMAIN_SNIPPET"))
                {
                    var domain = new DomainSummaryInfo(
                        domainKey: "Devices",
                        title: "Devices Domain",
                        description: "Device management domain",
                        domainType: default(DomainDescription.DomainTypes),
                        sourceTypeName: "Acme.Project.Domains.DevicesDomain",
                        sourcePropertyName: "Devices");

                    return new List<DomainSummaryInfo> { domain };
                }

                return Array.Empty<DomainSummaryInfo>();
            }

            public ModelStructureDescription BuildStructuredDescriptionForModel(
                string sourceText,
                string relativePath,
                IReadOnlyDictionary<string, string> resources)
            {
                if (sourceText != null && sourceText.Contains("MODEL_SNIPPET"))
                {
                    return new ModelStructureDescription
                    {
                        ModelName = "Device",
                        Namespace = "Acme.Project.Devices",
                        QualifiedName = "Acme.Project.Devices.Device",
                        Domain = "Devices",
                        Title = "Device",
                        Description = "Device model description"
                    };
                }

                return null;
            }

            // Not used in these tests but required by the interface.
            public ModelMetadataDescription BuildMetadataDescriptionForModel(
                string sourceText,
                string relativePath,
                IReadOnlyDictionary<string, string> resources)
            {
                throw new NotImplementedException();
            }

            public string BuildSummaryForMethod(MethodSummaryContext ctx)
            {
                throw new NotImplementedException();
            }

            public SourceKindResult DetectForFile(string sourceText, string relativePath)
            {
                return new SourceKindResult
                {
                    Path = relativePath,
                    SubKind = default(CodeSubKind),
                    PrimaryTypeName = "DevicesDomain",
                    IsMixed = true,
                    Reason = "Fake domain for tests",
                    SymbolText = "DOMAIN_SNIPPET"
                };

            }

            public Task<TitleDescriptionReviewResult> ReviewTitleAndDescriptionAsync(SummaryObjectKind kind, string symbolName, string title, string description, string llmUrl, string llmApiKey, HttpClient httpClient = null, string model = "gpt-4.1-mini", CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public int EstimateTokens(string s)
            {
                throw new NotImplementedException();
            }

            public RagChunkPlan ChunkCSharpWithRoslyn(string text, string relPath, string blobPath, int maxTokensPerChunk = 6500, int overlapLines = 6)
            {
                throw new NotImplementedException();
            }
        }
    }
}
