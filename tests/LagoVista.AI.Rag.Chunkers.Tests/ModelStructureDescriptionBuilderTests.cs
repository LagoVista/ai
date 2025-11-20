using System.IO;
using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Models;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class ModelStructureDescriptionBuilderTests
    {
        private static string GetContentPath(params string[] parts)
        {
            // TestDirectory points at .../tests/LagoVista.AI.Rag.Chunkers.Tests/bin/Debug/netX
            // We walk up to the repo root, then back down into tests/.../Content.
            var baseDir = TestContext.CurrentContext.TestDirectory;
            var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..")); // up from bin/Debug/netX
            var contentRoot = Path.Combine(repoRoot, "tests", "LagoVista.AI.Rag.Chunkers.Tests", "Content");

            foreach (var part in parts)
            {
                contentRoot = Path.Combine(contentRoot, part);
            }

            return contentRoot;
        }

        [Test]
        public void Builds_Structure_From_Device_Model_Source()
        {
            var modelPath = "./Content/SampleDeviceModel.cs";
            var resourcePath = "./Content/resources.resx";

            Assert.That(File.Exists(modelPath), Is.True, $"Model content file not found at {modelPath}");
            Assert.That(File.Exists(resourcePath), Is.True, $"Resource content file not found at {resourcePath}");

            var source = File.ReadAllText(modelPath);
            var resources = ResxLabelScanner.GetSingleResourceDictionary(".");

            // FromSource is a static method
            var description = ModelStructureDescriptionBuilder.FromSource(source, "src/Models/Device.cs", resources);

            Assert.Multiple(() =>
            {
                Assert.That(description, Is.Not.Null);
                Assert.That(description.ModelName, Is.EqualTo("AgentContextTestData"));
                Assert.That(description.Namespace, Is.EqualTo("LagoVista.AI.Models"));
                Assert.That(description.QualifiedName, Is.EqualTo("LagoVista.AI.Models.AgentContextTestData"));
                Assert.That(description.Domain, Is.EqualTo("AIAdmin"));
                Assert.That(description.Title, Is.EqualTo("Agent Context"));
                Assert.That(description.Description, Is.EqualTo("An agent context is how a generative AI should be implemented to answer questions and create context.  It consists of a vector database, a content database, LLM provide and model as well as basic information about how prompts should be created such as user, role and system contexts."));
                Assert.That(description.Help, Is.EqualTo("An agent context is how a generative AI should be implemented to answer questions and create context.  It consists of a vector database, a content database, LLM provide and model as well as basic information about how prompts should be created such as user, role and system contexts."));
                Assert.That(description.ListUIUrl, Is.EqualTo("/mlworkbench/agents"));
                Assert.That(description.EditUIUrl, Is.EqualTo("/mlworkbench/agent/{id}"));
                Assert.That(description.CreateUIUrl, Is.EqualTo("/mlworkbench/agent/add"));
                Assert.That(description.SaveUrl, Is.EqualTo("/api/ai/agentcontext"));
                Assert.That(description.GetListUrl, Is.EqualTo("/api/ai/agentcontexts"));
            });

            Assert.That(description.Properties, Is.Not.Null);
            
            var nameProp = description.Properties.Find(p => p.Name == "Name");
            Assert.That(nameProp, Is.Not.Null, "Name property should be present in Properties.");

            var keyProp = description.Properties.Find(p => p.Name == "Key");
            Assert.That(keyProp, Is.Not.Null, "Key property should be present in Properties.");

            var azureAccountIdProperty = description.Properties.Find(p => p.Name == nameof(AgentContext.AzureAccountId));
            Assert.That(keyProp, Is.Not.Null, "AzureAccountId property should be present in Properties.");
        }
    }
}
