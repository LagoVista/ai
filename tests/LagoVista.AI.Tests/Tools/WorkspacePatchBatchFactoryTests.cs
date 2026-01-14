using System.Collections.Generic;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Tools
{
    [TestFixture]
    public class WorkspacePatchBatchFactoryTests
    {
        private WorkspacePatchBatchFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new WorkspacePatchBatchFactory();
        }

        private string FakeId() => "id123";

        [Test]
        public void BuildBatch_AssignsIds_AndMapsFields()
        {
            var args = new WorkspaceWritePatchArgs
            {
                BatchLabel = "Test Batch",
                BatchKey = "batch-key",
                Files = new List<FilePatchArgs>
                {
                    new FilePatchArgs
                    {
                        DocPath = "repo/service.cs",
                        FileKey = "svckey",
                        FileLabel = "Service",
                        OriginalSha256 = new string('a', 64),
                        Changes = new List<ChangeArgs>
                        {
                            new ChangeArgs
                            {
                                ChangeKey = "ck1",
                                Operation = "insert",
                                AfterLine = 0,
                                NewLines = new List<string>{ "using X;" }
                            }
                        }
                    }
                }
            };

            var batch = _factory.BuildBatch(args,new Mock<IAgentPipelineContext>().Object, FakeId);

            Assert.That(batch.BatchId, Is.EqualTo("id123"));
            Assert.That(batch.BatchKey, Is.EqualTo("batch-key"));
            Assert.That(batch.BatchLabel, Is.EqualTo("Test Batch"));
            Assert.That(batch.Files.Count, Is.EqualTo(1));

            var file = batch.Files[0];
            Assert.That(file.FilePatchId, Is.EqualTo("id123"));
            Assert.That(file.DocPath, Is.EqualTo("repo/service.cs"));
            Assert.That(file.FileKey, Is.EqualTo("svckey"));
            Assert.That(file.OriginalSha256.Length, Is.EqualTo(64));

            var change = file.Changes[0];
            Assert.That(change.ChangeId, Is.EqualTo("id123"));
            Assert.That(change.Operation, Is.EqualTo("insert"));
            Assert.That(change.AfterLine, Is.EqualTo(0));
            Assert.That(change.NewLines[0], Is.EqualTo("using X;"));
        }

        [Test]
        public void BuildResponse_MapsIds_Labels_AndChanges()
        {
            var batch = new WorkspacePatchBatch
            {
                BatchId = "b1",
                BatchKey = "bk1",
                BatchLabel = "BatchLabel",
                Files = new List<WorkspaceFilePatch>
                {
                    new WorkspaceFilePatch
                    {
                        FilePatchId = "f1",
                        FileKey = "srv",
                        FileLabel = "Service Label",
                        DocPath = "repo/a.cs",
                        OriginalSha256 = new string('a', 64),
                        Changes = new List<WorkspaceLineChange>
                        {
                            new WorkspaceLineChange
                            {
                                ChangeId = "c1",
                                ChangeKey = "ck1",
                                Operation = "replace",
                                Description = "test change",
                                StartLine = 1,
                                EndLine = 1,
                                NewLines = new List<string>{ "public class A {}" },
                                ExpectedOriginalLines = new List<string>{ "public class A_old {}" }
                            }
                        }
                    }
                }
            };

            var result = _factory.BuildResponse(batch, new Mock<IAgentPipelineContext>().Object);

            Assert.That(result.Success, Is.True);
            Assert.That(result.BatchId, Is.EqualTo("b1"));
            Assert.That(result.Files.Count, Is.EqualTo(1));

            var file = result.Files[0];
            Assert.That(file.FilePatchId, Is.EqualTo("f1"));
            Assert.That(file.DocPath, Is.EqualTo("repo/a.cs"));

            var change = file.Changes[0];
            Assert.That(change.ChangeId, Is.EqualTo("c1"));
            Assert.That(change.Operation, Is.EqualTo("replace"));
            Assert.That(change.Description, Is.EqualTo("test change"));
        }
    }
}
