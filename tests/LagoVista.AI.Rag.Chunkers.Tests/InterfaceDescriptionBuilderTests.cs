using System;
using System.Linq;
using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class InterfaceDescriptionBuilderTests
    {
        private string SourceText;

        private IndexFileContext GetIndexFileContext()
        {
            return new IndexFileContext()
            {
                DocumentIdentity = new DocumentIdentity()
                {

                } ,
                GitRepoInfo = new GitRepoInfo()
                {

                }
            };
        }

        [SetUp]
        public void Setup()
        {
            var path = "./Content/IAgentContextManager.txt";
            Assert.That(System.IO.File.Exists(path), Is.True, $"Interface content file not found at {path}");
            SourceText = System.IO.File.ReadAllText(path);
        }

        [Test]
        public void Builds_Basic_Interface_Metadata_Correctly()
        {
            var description = InterfaceDescriptionBuilder.CreateInterfaceDescription(GetIndexFileContext(), SourceText);

            Assert.Multiple(() =>
            {
                Assert.That(description, Is.Not.Null);

                Assert.That(description.InterfaceName, Is.EqualTo("IAgentContextManager"));
                Assert.That(description.Namespace, Is.EqualTo("LagoVista.AI"));
                Assert.That(description.FullName, Is.EqualTo("LagoVista.AI.IAgentContextManager"));

                Assert.That(description.IsGeneric, Is.False);
                Assert.That(description.GenericArity, Is.EqualTo(0));

                Assert.That(description.BaseInterfaces, Is.Not.Null);
                Assert.That(description.BaseInterfaces, Is.Empty, "IAgentContextManager should not extend any interfaces.");

                // From naming heuristic: I + AgentContext + Manager => AgentContext
                Assert.That(description.PrimaryEntity, Is.EqualTo("AgentContext"));

                Assert.That(description.Role, Is.EqualTo("ManagerContract"));

                Assert.That(description.LineStart, Is.Not.Null);
                Assert.That(description.LineEnd, Is.Not.Null);
                Assert.That(description.LineEnd, Is.GreaterThanOrEqualTo(description.LineStart));
            });
        }

        [Test]
        public void Discovers_Methods_And_Method_Surface()
        {
            var description = InterfaceDescriptionBuilder.CreateInterfaceDescription(GetIndexFileContext(), SourceText);

            Assert.That(description.Methods, Is.Not.Null);
            Assert.That(description.Methods.Count, Is.EqualTo(6), "Expected 6 methods on IAgentContextManager.");

            var addMethod = description.Methods.Single(m => m.Name == "AddAgentContextAsync");
            var updateMethod = description.Methods.Single(m => m.Name == "UpdateAgentContextAsync");
            var getMethod = description.Methods.Single(m => m.Name == "GetAgentContextAsync");
            var getWithSecrets = description.Methods.Single(m => m.Name == "GetAgentContextWithSecretsAsync");
            var deleteMethod = description.Methods.Single(m => m.Name == "DeleteAgentContextAsync");
            var listMethod = description.Methods.Single(m => m.Name == "GetAgentContextsForOrgAsync");

            Assert.Multiple(() =>
            {
                // Return types (syntax-based)
                Assert.That(addMethod.ReturnType, Is.EqualTo("Task<InvokeResult>"));
                Assert.That(updateMethod.ReturnType, Is.EqualTo("Task<InvokeResult>"));
                Assert.That(getMethod.ReturnType, Is.EqualTo("Task<Models.AgentContext>"));
                Assert.That(getWithSecrets.ReturnType, Is.EqualTo("Task<Models.AgentContext>"));
                Assert.That(deleteMethod.ReturnType, Is.EqualTo("Task<InvokeResult>"));
                Assert.That(listMethod.ReturnType, Is.EqualTo("Task<ListResponse<AgentContextSummary>>"));

                // All methods should be async because they return Task / Task<T>
                foreach (var method in description.Methods)
                {
                    Assert.That(method.IsAsync, Is.True, $"Method {method.Name} should be marked async based on Task return type.");
                }
            });

            // Parameter surface for a couple of representative methods
            Assert.Multiple(() =>
            {
                Assert.That(addMethod.Parameters.Count, Is.EqualTo(3));
                Assert.That(addMethod.Parameters[0].Name, Is.EqualTo("agentContext"));
                Assert.That(addMethod.Parameters[0].Type, Is.EqualTo("AgentContext"));
                Assert.That(addMethod.Parameters[1].Type, Is.EqualTo("EntityHeader"));
                Assert.That(addMethod.Parameters[2].Type, Is.EqualTo("EntityHeader"));

                Assert.That(listMethod.Parameters.Count, Is.EqualTo(3));
                Assert.That(listMethod.Parameters[0].Type, Is.EqualTo("EntityHeader"));
                Assert.That(listMethod.Parameters[1].Type, Is.EqualTo("EntityHeader"));
                Assert.That(listMethod.Parameters[2].Type, Is.EqualTo("ListRequest"));

                // No optional parameters in this interface
                foreach (var param in description.Methods.SelectMany(m => m.Parameters))
                {
                    Assert.That(param.IsOptional, Is.False, $"Parameter {param.Name} should not be optional.");
                    Assert.That(param.DefaultValue, Is.Null, $"Parameter {param.Name} should not have a default value.");
                }
            });
        }

        [Test]
        public void Records_Method_Line_Numbers()
        {
            var description = InterfaceDescriptionBuilder.CreateInterfaceDescription(GetIndexFileContext(), SourceText);

            Assert.That(description.Methods, Is.Not.Null);

            foreach (var method in description.Methods)
            {
                Assert.That(method.LineStart, Is.Not.Null, $"Method {method.Name} should have a LineStart.");
                Assert.That(method.LineEnd, Is.Not.Null, $"Method {method.Name} should have a LineEnd.");
                Assert.That(method.LineEnd, Is.GreaterThanOrEqualTo(method.LineStart), $"Method {method.Name} has an invalid line range.");
            }
        }
    }
}
