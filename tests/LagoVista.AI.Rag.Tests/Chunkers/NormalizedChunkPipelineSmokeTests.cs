using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Services;
using LagoVista.AI.Rag.Models;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests.Chunkers
{
    /// <summary>
    /// Smoke tests that exercise the full pipeline for 7.2:
    ///   - Determine SubKind via SubKindDetector
    ///   - Flow results through DefaultNormalizedChunkBuilderService
    ///   - Inspect the final NormalizedText that would be embedded
    ///
    /// These use the real-world AgentContext* sample files under ./Content.
    /// </summary>
    [TestFixture]
    public class NormalizedChunkPipelineSmokeTests
    {
        private DefaultNormalizedChunkBuilderService _builder;

        [SetUp]
        public void Setup()
        {
            // ChunkerServicesPipeline wires DetectForFile to the real SubKindDetector.
            _builder = new DefaultNormalizedChunkBuilderService(new ChunkerServicesPipeline());
        }

        [Test]
        public async Task Pipeline_Smoke_AgentContext_Model()
        {
            var path = "./Content/AgentContextTest.txt";
            Assert.That(File.Exists(path), Is.True, $"Model content file not found at {path}");

            var fullPath = Path.GetFullPath(path);

            var ctx = new IndexFileContext
            {
                OrgId = "org-smoke",
                ProjectId = "proj-smoke",
                RepoId = "repo-smoke",
                FullPath = fullPath,
                RelativePath = "Models/AgentContext.cs"
            };

            var chunks = await _builder.BuildChunksAsync(ctx, CancellationToken.None);

            Assert.That(chunks, Is.Not.Null);
            Assert.That(chunks.Count, Is.GreaterThan(0), "Expected at least one chunk.");

            DumpChunks(ctx.RelativePath, chunks);
        }

        [Test]
        public async Task Pipeline_Smoke_AgentContext_Manager()
        {
            var path = "./Content/AgentContextTestManager.txt";
            Assert.That(File.Exists(path), Is.True, $"Manager content file not found at {path}");

            var fullPath = Path.GetFullPath(path);

            var ctx = new IndexFileContext
            {
                OrgId = "org-smoke",
                ProjectId = "proj-smoke",
                RepoId = "repo-smoke",
                FullPath = fullPath,
                RelativePath = "Managers/AgentContextManager.cs"
            };

            var chunks = await _builder.BuildChunksAsync(ctx, CancellationToken.None);

            Assert.That(chunks, Is.Not.Null);
            Assert.That(chunks.Count, Is.GreaterThan(0), "Expected at least one chunk.");

            DumpChunks(ctx.RelativePath, chunks);
        }

        [Test]
        public async Task Pipeline_Smoke_AgentContext_Repository()
        {
            var path = "./Content/AgentContextTestRepository.txt";
            Assert.That(File.Exists(path), Is.True, $"Repository content file not found at {path}");

            var fullPath = Path.GetFullPath(path);

            var ctx = new IndexFileContext
            {
                OrgId = "org-smoke",
                ProjectId = "proj-smoke",
                RepoId = "repo-smoke",
                FullPath = fullPath,
                RelativePath = "Repos/AgentContextRepository.cs"
            };

            var chunks = await _builder.BuildChunksAsync(ctx, CancellationToken.None);

            Assert.That(chunks, Is.Not.Null);
            Assert.That(chunks.Count, Is.GreaterThan(0), "Expected at least one chunk.");

            DumpChunks(ctx.RelativePath, chunks);
        }

        [Test]
        public async Task Pipeline_Smoke_AgentContext_Controller()
        {
            var path = "./Content/AgentContextTestController.txt";
            Assert.That(File.Exists(path), Is.True, $"Controller content file not found at {path}");

            var fullPath = Path.GetFullPath(path);

            var ctx = new IndexFileContext
            {
                OrgId = "org-smoke",
                ProjectId = "proj-smoke",
                RepoId = "repo-smoke",
                FullPath = fullPath,
                RelativePath = "Controllers/AgentContextController.cs"
            };

            var chunks = await _builder.BuildChunksAsync(ctx, CancellationToken.None);

            Assert.That(chunks, Is.Not.Null);
            Assert.That(chunks.Count, Is.GreaterThan(0), "Expected at least one chunk.");

            DumpChunks(ctx.RelativePath, chunks);
        }

        private static void DumpChunks(string relativePath, System.Collections.Generic.IReadOnlyList<NormalizedChunk> chunks)
        {
            var index = 1;
            foreach (var chunk in chunks)
            {
                Console.WriteLine($"==== {relativePath} chunk {index++} ====");
                Console.WriteLine(chunk.NormalizedText);
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Minimal pipeline implementation of IChunkerServices for 7.2 tests:
        /// - DetectForFile is wired to the real SubKindDetector so we exercise
        ///   the actual SubKind detection logic.
        /// - Other methods are placeholders for now and can be fleshed out when
        ///   we start exercising the richer summary/metadata paths.
        /// </summary>
        private sealed class ChunkerServicesPipeline : IChunkerServices
        {
            public System.Collections.Generic.IReadOnlyList<SubKindDetectionResult> DetectForFile(string sourceText, string relativePath)
            {
                return SubKindDetector.DetectForFile(sourceText, relativePath);
            }

            public ModelMetadataDescription BuildMetadataDescriptionForModel(string sourceText, string relativePath, System.Collections.Generic.IReadOnlyDictionary<string, string> resources)
            {
                throw new NotImplementedException("BuildMetadataDescriptionForModel is not used in 7.2 smoke tests.");
            }

            public ModelStructureDescription BuildStructuredDescriptionForModel(string sourceText, string relativePath, System.Collections.Generic.IReadOnlyDictionary<string, string> resources)
            {
                throw new NotImplementedException("BuildStructuredDescriptionForModel is not used in 7.2 smoke tests.");
            }

            public string BuildSummaryForMethod(MethodSummaryContext ctx)
            {
                // For now, just delegate to the simple summary builder.
                return MethodSummaryBuilder.BuildSummary(ctx);
            }

            public System.Collections.Generic.IReadOnlyList<DomainSummaryInfo> ExtractDomains(string source, string filePath)
            {
                // Not required for 7.2 raw C# chunking tests.
                return Array.Empty<DomainSummaryInfo>();
            }

            public string BuildModelSummary(ModelMetadataDescription metadata)
            {
                // Delegate to the shared helper; not used in these tests yet but keeps the contract complete.
                return ModelMetadataSummaryBuilder.BuildSummary(metadata);
            }

            public Task<TitleDescriptionReviewResult> ReviewTitleAndDescriptionAsync(
                SummaryObjectKind kind,
                string symbolName,
                string title,
                string description,
                string llmUrl,
                string llmApiKey,
                System.Net.Http.HttpClient httpClient = null,
                string model = "gpt-4.1-mini",
                CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException("ReviewTitleAndDescriptionAsync is not used in 7.2 smoke tests.");
            }
        }
    }
}
