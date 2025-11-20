using LagoVista.AI.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class ModelMetaDataDescriptionBuilderTests
    {

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
                Assert.That(description.FactoryUrl, Is.EqualTo("/api/ai/agentcontext/factory"));
                Assert.That(description.DeleteUrl, Is.EqualTo("/api/ai/agentcontext/{id}"));
                Assert.That(description.GetUrl, Is.EqualTo("/api/ai/agentcontext/{id}"));
            });

            Assert.That(description.Properties, Is.Not.Null);

            var nameProp = description.Properties.Find(p => p.Name == "Name");
            Assert.That(nameProp, Is.Not.Null, "Name property should be present in Properties.");

            var keyProp = description.Properties.Find(p => p.Name == "Key");
            Assert.That(keyProp, Is.Not.Null, "Key property should be present in Properties.");

            var azureAccountIdProperty = description.Properties.Find(p => p.Name == nameof(AgentContext.AzureAccountId));
            Assert.That(keyProp, Is.Not.Null, "AzureAccountId property should be present in Properties.");
        }

        [Test]
        public void Builds_Metadata_From_Device_Model_Source()
        {
            var modelPath = "./Content/SampleDeviceModel.cs";
            var resourcePath = "./Content/resources.resx";

            Assert.That(File.Exists(modelPath), Is.True, $"Model content file not found at {modelPath}");
            Assert.That(File.Exists(resourcePath), Is.True, $"Resource content file not found at {resourcePath}");

            var source = File.ReadAllText(modelPath);
            var resources = ResxLabelScanner.GetSingleResourceDictionary(".");

            var metadata = ModelMetadataDescriptionBuilder.FromSource(
                source,
                "src/Models/Device.cs",
                resources);

            Assert.Multiple(() =>
            {
                Assert.That(metadata, Is.Not.Null);
                Assert.That(metadata.ModelName, Is.EqualTo("AgentContextTestData"));
                Assert.That(metadata.Namespace, Is.EqualTo("LagoVista.AI.Models"));
                Assert.That(metadata.Domain, Is.EqualTo("AIAdmin"));

                // These should line up with the same resources used by the structure builder.
                Assert.That(metadata.Title, Is.EqualTo("Agent Context"));
                Assert.That(metadata.Description, Is.EqualTo("An agent context is how a generative AI should be implemented to answer questions and create context.  It consists of a vector database, a content database, LLM provide and model as well as basic information about how prompts should be created such as user, role and system contexts."));
                Assert.That(metadata.Help, Is.EqualTo("An agent context is how a generative AI should be implemented to answer questions and create context.  It consists of a vector database, a content database, LLM provide and model as well as basic information about how prompts should be created such as user, role and system contexts."));

                Assert.That(metadata.ListUIUrl, Is.EqualTo("/mlworkbench/agents"));
                Assert.That(metadata.EditUIUrl, Is.EqualTo("/mlworkbench/agent/{id}"));
                Assert.That(metadata.CreateUIUrl, Is.EqualTo("/mlworkbench/agent/add"));
                Assert.That(metadata.SaveUrl, Is.EqualTo("/api/ai/agentcontext"));
                Assert.That(metadata.GetListUrl, Is.EqualTo("/api/ai/agentcontexts"));
            });

            Assert.That(metadata.Fields, Is.Not.Null);
            Assert.That(metadata.Fields.Count, Is.GreaterThan(0), "Expected at least one field in metadata.Fields.");

            // A couple of spot-checks
            var iconField = metadata.Fields.FirstOrDefault(f => f.PropertyName == nameof(AgentContextTestData.Icon));
            Assert.That(iconField, Is.Not.Null, "Icon should be present in metadata.Fields.");

            var vectorDbNameField = metadata.Fields.FirstOrDefault(f => f.PropertyName == nameof(AgentContextTestData.VectorDatabaseCollectionName));
            Assert.That(vectorDbNameField, Is.Not.Null, "VectorDatabaseCollectionName should be present in metadata.Fields.");
        }

        [Test]
        public void Metadata_Fields_Are_Derived_From_FormField_Properties_Only()
        {
            var modelPath = "./Content/SampleDeviceModel.cs";
            var resourcePath = "./Content/resources.resx";

            Assert.That(File.Exists(modelPath), Is.True, $"Model content file not found at {modelPath}");
            Assert.That(File.Exists(resourcePath), Is.True, $"Resource content file not found at {resourcePath}");

            var source = File.ReadAllText(modelPath);
            var resources = ResxLabelScanner.GetSingleResourceDictionary(".");

            var metadata = ModelMetadataDescriptionBuilder.FromSource(
                source,
                "src/Models/Device.cs",
                resources);

            Assert.That(metadata.Fields, Is.Not.Null);

            // Properties with [FormField] should be present
            Assert.That(
                metadata.Fields.Any(f => f.PropertyName == nameof(AgentContextTestData.AzureAccountId)),
                Is.True,
                "AzureAccountId should be included because it has a FormField attribute.");

            // Backing secret-id properties (no [FormField]) should not be present
            Assert.That(
                metadata.Fields.Any(f => f.PropertyName == nameof(AgentContextTestData.VectorDatabaseApiKeySecretId)),
                Is.False,
                "VectorDatabaseApiKeySecretId should not be included because it has no FormField attribute.");

            Assert.That(
                metadata.Fields.Any(f => f.PropertyName == nameof(AgentContextTestData.AzureApiTokenSecretId)),
                Is.False,
                "AzureApiTokenSecretId should not be included because it has no FormField attribute.");

            Assert.That(
                metadata.Fields.Any(f => f.PropertyName == nameof(AgentContextTestData.LlmApiKeySecretId)),
                Is.False,
                "LlmApiKeySecretId should not be included because it has no FormField attribute.");
        }

        [Test]
        public void Extracts_Form_Layouts_From_IFormDescriptor_Interfaces()
        {
            var modelPath = "./Content/SampleDeviceModel.cs";
            var resources = ResxLabelScanner.GetSingleResourceDictionary(".");

            var source = File.ReadAllText(modelPath);

            var metadata = ModelMetadataDescriptionBuilder.FromSource(
                source,
                "src/Models/Device.cs",
                resources);

            Assert.That(metadata.Layouts, Is.Not.Null);

            Assert.That(metadata.Layouts.Form.Col1Fields, Is.EqualTo(new[]
            {
        "name",
        "key",
        "icon",
        "vectorDatabaseCollectionName",
        "vectorDatabaseUri",
        "vectorDatabaseApiKey",
        "azureAccountId",
        "azureApiToken",
        "blobContainerName",
        "description"
    }));

            Assert.That(metadata.Layouts.Form.Col2Fields, Is.EqualTo(new[]
            {
        "llmProvider",
        "llmApiKey",
        "embeddingModel",
        "defaultConversationContext",
        "conversationContexts"
    }));

            Assert.That(metadata.Layouts.Form.BottomFields, Is.EqualTo(new[]
            {
        "description"
    }));
        }

    }
}
