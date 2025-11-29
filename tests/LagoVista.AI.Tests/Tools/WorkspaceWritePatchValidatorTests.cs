using System.Collections.Generic;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.Validation;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Tools
{
    [TestFixture]
    public class WorkspaceWritePatchValidatorTests
    {
        private WorkspaceWritePatchValidator _validator;

        [SetUp]
        public void SetUp()
        {
            _validator = new WorkspaceWritePatchValidator();
        }

        [Test]
        public void Validate_ReturnsError_WhenArgsNull()
        {
            var result = _validator.Validate(null);
            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Arguments payload was null."));
        }

        [Test]
        public void Validate_ReturnsError_WhenNoFiles()
        {
            var args = new WorkspaceWritePatchArgs
            {
                Files = new List<FilePatchArgs>()
            };

            var result = _validator.Validate(args);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("At least one file patch must be provided in files[]."));
        }

        [Test]
        public void Validate_ReturnsError_OnDuplicateDocPaths()
        {
            var args = new WorkspaceWritePatchArgs
            {
                Files = new List<FilePatchArgs>
                {
                    new FilePatchArgs
                    {
                        DocPath = "repo/file1.cs",
                        OriginalSha256 = new string('a', 64),
                        Changes = new List<ChangeArgs>
                        {
                            new ChangeArgs { Operation = "insert", AfterLine = 0, NewLines = new List<string>{ "using X;" } }
                        }
                    },
                    new FilePatchArgs
                    {
                        DocPath = "repo/file1.cs", // duplicate
                        OriginalSha256 = new string('b', 64),
                        Changes = new List<ChangeArgs>
                        {
                            new ChangeArgs { Operation = "delete", StartLine = 1, EndLine = 1, ExpectedOriginalLines = new List<string>{ "line" } }
                        }
                    }
                }
            };

            var result = _validator.Validate(args);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.StartWith("Each docPath may appear at most once per batch."));
        }

        [Test]
        public void Validate_Succeeds_ForMinimalValidInsertChange()
        {
            var args = new WorkspaceWritePatchArgs
            {
                Files = new List<FilePatchArgs>
                {
                    new FilePatchArgs
                    {
                        DocPath = "repo/valid.cs",
                        OriginalSha256 = new string('c', 64),
                        Changes = new List<ChangeArgs>
                        {
                            new ChangeArgs
                            {
                                Operation = "insert",
                                AfterLine = 0,
                                NewLines = new List<string>{ "using System;" }
                            }
                        }
                    }
                }
            };

            var result = _validator.Validate(args);

            Assert.That(result.Successful, Is.True);
        }

        [Test]
        public void Validate_ReturnsError_ForReplaceMissingNewLines()
        {
            var args = new WorkspaceWritePatchArgs
            {
                Files = new List<FilePatchArgs>
                {
                    new FilePatchArgs
                    {
                        DocPath = "repo/replace.cs",
                        OriginalSha256 = new string('d', 64),
                        Changes = new List<ChangeArgs>
                        {
                            new ChangeArgs
                            {
                                Operation = "replace",
                                StartLine = 2,
                                EndLine = 2,
                                // missing NewLines
                            }
                        }
                    }
                }
            };

            var result = _validator.Validate(args);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("must include at least one new line in newLines"));
        }

        [Test]
        public void Validate_ReturnsError_WhenDeleteHasInvalidLineRange()
        {
            var args = new WorkspaceWritePatchArgs
            {
                Files = new List<FilePatchArgs>
                {
                    new FilePatchArgs
                    {
                        DocPath = "repo/delete.cs",
                        OriginalSha256 = new string('e', 64),
                        Changes = new List<ChangeArgs>
                        {
                            new ChangeArgs
                            {
                                Operation = "delete",
                                StartLine = -1,
                                EndLine = 0,
                                ExpectedOriginalLines = new List<string>{ "bad" }
                            }
                        }
                    }
                }
            };

            var result = _validator.Validate(args);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("valid startLine and endLine"));
        }
    }
}
