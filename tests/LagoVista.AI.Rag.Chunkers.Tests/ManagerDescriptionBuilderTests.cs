using System;
using System.Linq;
using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class ManagerDescriptionBuilderTests
    {
        String SourceText;

        [SetUp]
        public void Setup()
        {
            var managerPath = "./Content/AgentContextTestManager.txt";
            Assert.That(System.IO.File.Exists(managerPath), Is.True, $"Manager content file not found at {managerPath}");
            SourceText = System.IO.File.ReadAllText(managerPath);
        }


        [Test]
        public void Builds_Basic_Manager_Metadata_Correctly()
        {
            var description = ManagerDescriptionBuilder.CreateManagerDescription(SourceText);

            Assert.Multiple(() =>
            {
                Assert.That(description, Is.Not.Null);
                Assert.That(description.ClassName, Is.EqualTo("AgentContextTestManager"));
                Assert.That(description.Namespace, Is.EqualTo("LagoVista.AI.Managers"));
                Assert.That(description.ManagerType, Is.EqualTo(ManagerType.ManagerOverview));
                Assert.That(description.PrimaryEntity, Is.EqualTo("AgentContextTest"));
            });
        }

        [Test]
        public void Captures_Constructors_And_Dependency_Interfaces()
        {
            var description = ManagerDescriptionBuilder.CreateManagerDescription(SourceText);

            Assert.That(description.Constructors, Is.Not.Null);
            Assert.That(description.Constructors.Count, Is.EqualTo(1));

            var ctor = description.Constructors.Single();

            Assert.Multiple(() =>
            {
                Assert.That(ctor.SignatureText, Does.Contain("AgentContextTestManager"));
                Assert.That(ctor.SignatureText, Does.Contain("IAgentConextRepo"));
                Assert.That(ctor.BodyText, Does.Contain("_repo = repo"));
                Assert.That(ctor.LineStart, Is.Not.Null);
                Assert.That(ctor.LineEnd, Is.Not.Null);
                Assert.That(ctor.LineEnd, Is.GreaterThanOrEqualTo(ctor.LineStart));
            });

            Assert.That(description.DependencyInterfaces, Is.Not.Null);
            Assert.That(description.DependencyInterfaces, Does.Contain("IAgentConextRepo"));
            Assert.That(description.DependencyInterfaces, Does.Contain("ISecureStorage"));
            Assert.That(description.DependencyInterfaces, Does.Contain("IAdminLogger"));
            Assert.That(description.DependencyInterfaces, Does.Contain("IAppConfig"));
            Assert.That(description.DependencyInterfaces, Does.Contain("IDependencyManager"));
            Assert.That(description.DependencyInterfaces, Does.Contain("ISecurity"));
        }

        [Test]
        public void Discovers_Manager_Methods_And_MethodKinds()
        {
            var description = ManagerDescriptionBuilder.CreateManagerDescription(SourceText);

            Assert.That(description.Methods, Is.Not.Null);
            Assert.That(description.Methods.Count, Is.EqualTo(7));

            var addMethod = description.Methods.Single(m => m.MethodName == "AddAgentContextTestAsync");
            var deleteMethod = description.Methods.Single(m => m.MethodName == "DeleteAgentContextTestAsync");
            var getMethod = description.Methods.Single(m => m.MethodName == "GetAgentContextTestAsync");
            var getWithSecrets = description.Methods.Single(m => m.MethodName == "GetAgentContextTestWithSecretsAsync");
            var getVectorWithSecrets = description.Methods.Single(m => m.MethodName == "GetVectorDatabaseAsyncWithSecrets");
            var listMethod = description.Methods.Single(m => m.MethodName == "GetAgentContextTestsForOrgAsync");
            var updateMethod = description.Methods.Single(m => m.MethodName == "UpdateAgentContextTestAsync");

            Assert.Multiple(() =>
            {
                Assert.That(addMethod.MethodKind, Is.EqualTo(ManagerMethodKind.Create));
                Assert.That(deleteMethod.MethodKind, Is.EqualTo(ManagerMethodKind.Delete));
                Assert.That(updateMethod.MethodKind, Is.EqualTo(ManagerMethodKind.Update));

                Assert.That(getMethod.MethodKind, Is.EqualTo(ManagerMethodKind.Query));
                Assert.That(getWithSecrets.MethodKind, Is.EqualTo(ManagerMethodKind.Query));
                Assert.That(getVectorWithSecrets.MethodKind, Is.EqualTo(ManagerMethodKind.Query));
                Assert.That(listMethod.MethodKind, Is.EqualTo(ManagerMethodKind.Query));

                Assert.That(addMethod.IsPublic, Is.True);
                Assert.That(deleteMethod.IsPublic, Is.True);
                Assert.That(getMethod.IsPublic, Is.True);
                Assert.That(updateMethod.IsPublic, Is.True);

                Assert.That(addMethod.BodyText, Does.Contain("ValidationCheck"));
                Assert.That(deleteMethod.BodyText, Does.Contain("DeleteAgentContextTestAsync"));
            });
        }

        [Test]
        public void Records_Method_Line_Numbers()
        {
            var description = ManagerDescriptionBuilder.CreateManagerDescription(SourceText);

            foreach (var method in description.Methods)
            {
                Assert.That(method.LineStart, Is.Not.Null, $"Method {method.MethodName} should have a LineStart.");
                Assert.That(method.LineEnd, Is.Not.Null, $"Method {method.MethodName} should have a LineEnd.");
                Assert.That(method.LineEnd, Is.GreaterThanOrEqualTo(method.LineStart), $"Method {method.MethodName} has invalid line range.");
            }
        }
    }
}
