using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Services;
using LagoVista.AI.Rag.Models;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests.Chunking
{
    [TestFixture]
    public class DefaultNormalizedChunkBuilderServiceTests
    {
        private string _root;
        private string _filePath;
        private string _relativePath;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "rag-chunk-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);

            _relativePath = "Models/Device.cs";
            _filePath = Path.Combine(_root, _relativePath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? _root);
            File.WriteAllText(_filePath, "namespace Test { public class Device { } public class DeviceManager { } }");
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_root))
            {
                try
                {
                    Directory.Delete(_root, true);
                }
                catch
                {
                    // best-effort cleanup only
                }
            }
        }

        [Test]
        public async Task Single_DetectionResult_Produces_Header_Summary_And_Code()
        {
            var fileContext = new IndexFileContext
            {
                OrgId = "org-1",
                ProjectId = "proj-1",
                RepoId = "Repo1",
                FullPath = _filePath,
                RelativePath = _relativePath.Replace('\\', '/')
            };

            var chunker = new FakeChunkerServicesSingle();
            var builder = new DefaultNormalizedChunkBuilderService(chunker);

            var chunks = await builder.BuildChunksAsync(fileContext, CancellationToken.None);

            Assert.That(chunks, Is.Not.Null);
            Assert.That(chunks.Count, Is.EqualTo(1));

            var chunk = chunks[0];
            Assert.That(chunk.Identity, Is.Not.Null);
            Assert.That(string.IsNullOrWhiteSpace(chunk.Identity.DocId), Is.False);
            Assert.That(string.IsNullOrWhiteSpace(chunk.Identity.SectionKey), Is.False);
            Assert.That(string.IsNullOrWhiteSpace(chunk.Identity.ChunkId), Is.False);

            Assert.That(chunk.Kind, Is.EqualTo("SourceCode"));
            Assert.That(chunk.SubKind, Is.EqualTo("Model"));

            var text = chunk.NormalizedText;

            // Header
            Assert.That(text, Does.Contain("OrgId: org-1"));
            Assert.That(text, Does.Contain("ProjectId: proj-1"));
            Assert.That(text, Does.Contain("RepoId: Repo1"));
            Assert.That(text, Does.Contain("Path: " + fileContext.RelativePath));
            Assert.That(text, Does.Contain("SubKind: Model"));
            Assert.That(text, Does.Contain("Symbol: Device"));

            // Summary
            Assert.That(text, Does.Contain("Summary:"));
            Assert.That(text, Does.Contain("This is a test summary for Device."));

            // Code
            Assert.That(text, Does.Contain("Code:"));
            Assert.That(text, Does.Contain("public class Device"));
        }

        [Test]
        public async Task Multiple_DetectionResults_Produce_Multiple_Chunks_With_Correct_Symbols()
        {
            var fileContext = new IndexFileContext
            {
                OrgId = "org-2",
                ProjectId = "proj-2",
                RepoId = "Repo2",
                FullPath = _filePath,
                RelativePath = _relativePath.Replace('\\', '/')
            };

            var chunker = new FakeChunkerServicesMultiple();
            var builder = new DefaultNormalizedChunkBuilderService(chunker);

            var chunks = await builder.BuildChunksAsync(fileContext, CancellationToken.None);

            Assert.That(chunks, Is.Not.Null);
            Assert.That(chunks.Count, Is.EqualTo(2));

            var deviceChunk = chunks[0];
            var managerChunk = chunks[1];

            Assert.That(deviceChunk.Identity.Symbol, Is.EqualTo("Device"));
            Assert.That(managerChunk.Identity.Symbol, Is.EqualTo("DeviceManager"));

            Assert.That(deviceChunk.NormalizedText, Does.Contain("Symbol: Device"));
            Assert.That(managerChunk.NormalizedText, Does.Contain("Symbol: DeviceManager"));

            Assert.That(deviceChunk.SubKind, Is.EqualTo("Model"));
            Assert.That(managerChunk.SubKind, Is.EqualTo("Manager"));
        }

        [Test]
        public async Task When_Summary_Is_Empty_Summary_Section_Is_Omitted_But_Code_Remains()
        {
            var fileContext = new IndexFileContext
            {
                OrgId = "org-3",
                ProjectId = "proj-3",
                RepoId = "Repo3",
                FullPath = _filePath,
                RelativePath = _relativePath.Replace('\\', '/')
            };

            var chunker = new FakeChunkerServicesNoSummary();
            var builder = new DefaultNormalizedChunkBuilderService(chunker);

            var chunks = await builder.BuildChunksAsync(fileContext, CancellationToken.None);

            Assert.That(chunks, Is.Not.Null);
            Assert.That(chunks.Count, Is.EqualTo(1));

            var chunk = chunks[0];
            var text = chunk.NormalizedText;

            // Header still present
            Assert.That(text, Does.Contain("OrgId: org-3"));
            Assert.That(text, Does.Contain("Symbol: Device"));

            // No Summary section
            Assert.That(text, Does.Not.Contain("Summary:"));

            // But Code section remains
            Assert.That(text, Does.Contain("Code:"));
            Assert.That(text, Does.Contain("public class Device"));
        }

        private sealed class FakeChunkerServicesSingle : IChunkerServices
        {
            public IReadOnlyList<SubKindDetectionResult> DetectForFile(string sourceText, string relativePath)
            {
                return new[]
                {
                    new SubKindDetectionResult
                    {
                        Path = relativePath,
                        SubKind = CodeSubKind.Model,
                        PrimaryTypeName = "Device",
                        IsMixed = false,
                        Summary = "This is a test summary for Device.",
                        Reason = "Test stub: detected model type Device.",
                        Evidence = Array.Empty<string>(),
                        SymbolText = "public class Device { }"
                    }
                };
            }

            public ModelMetadataDescription BuildMetadataDescriptionForModel(string sourceText, string relativePath, IReadOnlyDictionary<string, string> resources) => throw new NotImplementedException();
            public ModelStructureDescription BuildStructuredDescriptionForModel(string sourceText, string relativePath, IReadOnlyDictionary<string, string> resources) => throw new NotImplementedException();
            public string BuildSummaryForMethod(MethodSummaryContext ctx) => throw new NotImplementedException();
            public IReadOnlyList<DomainSummaryInfo> ExtractDomains(string source, string filePath) => throw new NotImplementedException();
            public Task<TitleDescriptionReviewResult> ReviewTitleAndDescriptionAsync(SummaryObjectKind kind, string symbolName, string title, string description, string llmUrl, string llmApiKey, System.Net.Http.HttpClient httpClient = null, string model = "gpt-4.1-mini", CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private sealed class FakeChunkerServicesMultiple : IChunkerServices
        {
            public IReadOnlyList<SubKindDetectionResult> DetectForFile(string sourceText, string relativePath)
            {
                return new[]
                {
                    new SubKindDetectionResult
                    {
                        Path = relativePath,
                        SubKind = CodeSubKind.Model,
                        PrimaryTypeName = "Device",
                        IsMixed = true,
                        Summary = "Model summary for Device.",
                        Reason = "Detected model type Device.",
                        Evidence = Array.Empty<string>(),
                        SymbolText = "public class Device { }"
                    },
                    new SubKindDetectionResult
                    {
                        Path = relativePath,
                        SubKind = CodeSubKind.Manager,
                        PrimaryTypeName = "DeviceManager",
                        IsMixed = true,
                        Summary = "Manager summary for DeviceManager.",
                        Reason = "Detected manager type DeviceManager.",
                        Evidence = Array.Empty<string>(),
                        SymbolText = "public class DeviceManager { }"
                    }
                };
            }

            public ModelMetadataDescription BuildMetadataDescriptionForModel(string sourceText, string relativePath, IReadOnlyDictionary<string, string> resources) => throw new NotImplementedException();
            public ModelStructureDescription BuildStructuredDescriptionForModel(string sourceText, string relativePath, IReadOnlyDictionary<string, string> resources) => throw new NotImplementedException();
            public string BuildSummaryForMethod(MethodSummaryContext ctx) => throw new NotImplementedException();
            public IReadOnlyList<DomainSummaryInfo> ExtractDomains(string source, string filePath) => throw new NotImplementedException();
            public Task<TitleDescriptionReviewResult> ReviewTitleAndDescriptionAsync(SummaryObjectKind kind, string symbolName, string title, string description, string llmUrl, string llmApiKey, System.Net.Http.HttpClient httpClient = null, string model = "gpt-4.1-mini", CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private sealed class FakeChunkerServicesNoSummary : IChunkerServices
        {
            public IReadOnlyList<SubKindDetectionResult> DetectForFile(string sourceText, string relativePath)
            {
                return new[]
                {
                    new SubKindDetectionResult
                    {
                        Path = relativePath,
                        SubKind = CodeSubKind.Model,
                        PrimaryTypeName = "Device",
                        IsMixed = false,
                        Summary = null,
                        Reason = "Detected model type Device without summary.",
                        Evidence = Array.Empty<string>(),
                        SymbolText = "public class Device { }"
                    }
                };
            }

            public ModelMetadataDescription BuildMetadataDescriptionForModel(string sourceText, string relativePath, IReadOnlyDictionary<string, string> resources) => throw new NotImplementedException();
            public ModelStructureDescription BuildStructuredDescriptionForModel(string sourceText, string relativePath, IReadOnlyDictionary<string, string> resources) => throw new NotImplementedException();
            public string BuildSummaryForMethod(MethodSummaryContext ctx) => throw new NotImplementedException();
            public IReadOnlyList<DomainSummaryInfo> ExtractDomains(string source, string filePath) => throw new NotImplementedException();
            public Task<TitleDescriptionReviewResult> ReviewTitleAndDescriptionAsync(SummaryObjectKind kind, string symbolName, string title, string description, string llmUrl, string llmApiKey, System.Net.Http.HttpClient httpClient = null, string model = "gpt-4.1-mini", CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }
    }
}
