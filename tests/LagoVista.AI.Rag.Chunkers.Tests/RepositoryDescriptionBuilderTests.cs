using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class RepositoryDescriptionBuilderTests
    {
        private string SourceText;

        [SetUp]
        public void Setup()
        {
            var path = "./Content/AgentContextTestRepository.txt";
            Assert.That(System.IO.File.Exists(path), Is.True, $"Repository content file not found at {path}");
            SourceText = System.IO.File.ReadAllText(path);
        }

        [Test]
        public void Builds_Basic_Repository_Metadata_Correctly()
        {
            var description = RepositoryDescriptionBuilder.CreateRepositoryDescription(SourceText);

            Assert.Multiple(() =>
            {
                Assert.That(description, Is.Not.Null, "Description should not be null.");
                Assert.That(description.ClassName, Is.EqualTo("AgentContextRepo"));
                Assert.That(description.Namespace, Is.EqualTo("LagoVista.AI.CloudRepos"));

                Assert.That(description.BaseTypeName, Is.EqualTo("DocumentDBRepoBase"));
                Assert.That(description.PrimaryEntity, Is.EqualTo("AgentContext"));
                Assert.That(description.RepositoryKind, Is.EqualTo(RepositoryKind.DocumentDb));
            });
        }

        [Test]
        public void Captures_Implemented_Interfaces_And_RepositoryKind()
        {
            var description = RepositoryDescriptionBuilder.CreateRepositoryDescription(SourceText);

            Assert.That(description.ImplementedInterfaces, Is.Not.Null);
            Assert.That(description.ImplementedInterfaces, Is.Not.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(description.ImplementedInterfaces, Does.Contain("IAgentConextRepo"));

                // RepositoryKind should be inferred from the base type
                Assert.That(description.RepositoryKind, Is.EqualTo(RepositoryKind.DocumentDb));

                // StorageProfile is optional and currently not inferred by the builder
                Assert.That(description.StorageProfile, Is.Null, "StorageProfile is not currently inferred and should be null.");
            });
        }

        [Test]
        public void Discovers_Repository_Methods_And_MethodKinds()
        {
            var description = RepositoryDescriptionBuilder.CreateRepositoryDescription(SourceText);

            Assert.That(description.Methods, Is.Not.Null);
            Assert.That(description.Methods.Count, Is.EqualTo(6), "Expected 6 repository methods.");

            var addMethod = description.Methods.Single(m => m.MethodName == "AddAgentContextAsync");
            var deleteMethod = description.Methods.Single(m => m.MethodName == "DeleteAgentContextAsync");
            var getMethod = description.Methods.Single(m => m.MethodName == "GetAgentContextAsync");
            var getSummariesMethod = description.Methods.Single(m => m.MethodName == "GetAgentContextSummariesForOrgAsync");
            var queryKeyMethod = description.Methods.Single(m => m.MethodName == "QueryKeyInUseAsync");
            var updateMethod = description.Methods.Single(m => m.MethodName == "UpdateAgentContextAsync");

            Assert.Multiple(() =>
            {
                // Method kinds
                Assert.That(addMethod.MethodKind, Is.EqualTo(RepositoryMethodKind.Insert));
                Assert.That(deleteMethod.MethodKind, Is.EqualTo(RepositoryMethodKind.Delete));
                Assert.That(updateMethod.MethodKind, Is.EqualTo(RepositoryMethodKind.Update));

                Assert.That(getMethod.MethodKind, Is.EqualTo(RepositoryMethodKind.GetById));
                Assert.That(getSummariesMethod.MethodKind, Is.EqualTo(RepositoryMethodKind.Query));
          
                // Visibility / significance flags
                foreach (var method in description.Methods)
                {
                    Assert.That(method.IsPublic, Is.True, $"Method {method.MethodName} should be public.");
                    Assert.That(method.IsProtectedOrInternal, Is.False, $"Method {method.MethodName} should not be protected/internal.");
                    Assert.That(method.IsPrivate, Is.False, $"Method {method.MethodName} should not be private.");
                    Assert.That(method.IsSignificant, Is.True, $"Method {method.MethodName} should be marked significant.");
                }
            });
        }

        [Test]
        public void Records_Method_Line_Numbers_And_BodyText()
        {
            var description = RepositoryDescriptionBuilder.CreateRepositoryDescription(SourceText);

            Assert.That(description.Methods, Is.Not.Null);

            foreach (var method in description.Methods)
            {
                Assert.That(method.LineStart, Is.Not.Null, $"Method {method.MethodName} should have a LineStart.");
                Assert.That(method.LineEnd, Is.Not.Null, $"Method {method.MethodName} should have a LineEnd.");
                Assert.That(method.LineEnd, Is.GreaterThanOrEqualTo(method.LineStart), $"Method {method.MethodName} has an invalid line range.");
                Assert.That(method.BodyText, Is.Not.Null.And.Not.Empty, $"Method {method.MethodName} should have BodyText captured.");
            }

            var addMethod = description.Methods.Single(m => m.MethodName == "AddAgentContextAsync");
            var deleteMethod = description.Methods.Single(m => m.MethodName == "DeleteAgentContextAsync");
            var queryKeyMethod = description.Methods.Single(m => m.MethodName == "QueryKeyInUseAsync");
            var updateMethod = description.Methods.Single(m => m.MethodName == "UpdateAgentContextAsync");

            Assert.Multiple(() =>
            {
                Assert.That(addMethod.BodyText, Does.Contain("CreateDocumentAsync"));
                Assert.That(deleteMethod.BodyText, Does.Contain("DeleteDocumentAsync"));
                Assert.That(queryKeyMethod.BodyText, Does.Contain("QueryAsync"));
                Assert.That(updateMethod.BodyText, Does.Contain("UpsertDocumentAsync"));
            });
        }
    }
}
