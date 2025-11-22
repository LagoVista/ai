//using System;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.IO;
//using System.Threading;
//using System.Threading.Tasks;
//using LagoVista.AI.Rag.Chunkers.Models;
//using LagoVista.AI.Rag.Chunkers.Services;
//using LagoVista.AI.Rag.ContractPacks.Ingestion.Services;
//using LagoVista.AI.Rag.Models;
//using LagoVista.Core.Utils.Types;
//using Moq;
//using NUnit.Framework;

//namespace LagoVista.AI.Rag.Tests.Chunking
//{
//    [TestFixture]
//    public class DefaultFileChunkingServiceTests
//    {
//        private string _root;
//        private string _filePath;
//        private string _relativePath;

//        private Mock<IChunkerServices> _mockChunkerServices;

//        [SetUp]
//        public void Setup()
//        {
//            _root = Path.Combine(Path.GetTempPath(), "rag-chunk-" + Guid.NewGuid().ToString("N"));
//            Directory.CreateDirectory(_root);

//            _relativePath = "Models/Device.cs";
//            _filePath = Path.Combine(_root, _relativePath.Replace('/', Path.DirectorySeparatorChar));

//            Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? _root);
//            File.WriteAllText(_filePath, "namespace Test { public class Device { } }");


//            _mockChunkerServices.Setup(mc => mc.DetectForFile(It.IsAny<string>(), It.IsAny<string>()))
//                       .Returns(new SourceKindResult
//                       {
//                           SubKind = CodeSubKind.Model,
//                           PrimaryTypeName = "Device",
//                           IsMixed = false,
//                           Summary = "This is a test summary for Device.",
//                           Reason = "Test stub: detected model type Device.",
//                           Evidence = Array.Empty<string>(),
//                           SymbolText = "public class Device { }"
//                       });
//        }

//        [TearDown]
//        public void Teardown()
//        {
//            if (Directory.Exists(_root))
//            {
//                try { Directory.Delete(_root, true); } catch { }
//            }
//        }

//        [Test]
//        public async Task BuildChunksAsync_Produces_Chunk_With_Header_Summary_And_Code()
//        {
//            var fileContext = new IndexFileContext
//            {
//                OrgId = "org-1",
//                ProjectId = "proj-1",
//                RepoId = "Repo1",
//                FullPath = _filePath,
//                RelativePath = _relativePath.Replace('\\', '/')
//            };

//            var service = new DefaultNormalizedChunkBuilderService(chunker);

//            var chunks = service.BuildChunksAsync(fileContext, CancellationToken.None);

//            Assert.That(chunks, Is.Not.Null);
//            Assert.That(chunks.Count, Is.EqualTo(1));

//            var chunk = chunks[0];
//            Assert.That(chunk.Identity, Is.Not.Null);
//            Assert.That(string.IsNullOrWhiteSpace(chunk.Identity.DocId), Is.False);
//            Assert.That(string.IsNullOrWhiteSpace(chunk.Identity.SectionKey), Is.False);
//            Assert.That(string.IsNullOrWhiteSpace(chunk.Identity.ChunkId), Is.False);

//            Assert.That(chunk.SubKind, Is.EqualTo("Model"));
//            Assert.That(chunk.Kind, Is.EqualTo("SourceCode"));

//            var text = chunk.NormalizedText;

//            Assert.That(text, Does.Contain("OrgId: org-1"));
//            Assert.That(text, Does.Contain("ProjectId: proj-1"));
//            Assert.That(text, Does.Contain("RepoId: Repo1"));
//            Assert.That(text, Does.Contain("Path: " + fileContext.RelativePath));
//            Assert.That(text, Does.Contain("SubKind: Model"));
//            Assert.That(text, Does.Contain("Symbol: Device"));

//            Assert.That(text, Does.Contain("Summary:"));
//            Assert.That(text, Does.Contain("This is a test summary for Device."));

//            Assert.That(text, Does.Contain("Code:"));
//            Assert.That(text, Does.Contain("public class Device"));
//        }

//        private DomainModelCatalog CreateEmptyCatalog()
//        {
//            return new DomainModelCatalog(
//                new ReadOnlyDictionary<string, DomainSummaryInfo>(new Dictionary<string, DomainSummaryInfo>()),
//                new ReadOnlyDictionary<string, ModelCatalogEntry>(new Dictionary<string, ModelCatalogEntry>()));
//        }

//        private IReadOnlyDictionary<string, string> CreateReadOnlyDictionary()
//        {
//            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
//        }

//        [Test]
//        public void BuildChunksAsync_Throws_If_File_Missing()
//        {
//            var fileContext = new IndexFileContext
//            {
//                OrgId = "org-1",
//                ProjectId = "proj-1",
//                RepoId = "Repo1",
//                FullPath = Path.Combine(_root, "Missing.cs"),
//                RelativePath = "Missing.cs"
//            };


//            var service = new DefaultNormalizedChunkBuilderService(mockChunker.Object);

//            Assert.Throws<FileNotFoundException>(() =>
//                service.BuildChunks(fileContext.FullPath, CreateEmptyCatalog(), CreateReadOnlyDictionary());
//        }
//    }
//}
