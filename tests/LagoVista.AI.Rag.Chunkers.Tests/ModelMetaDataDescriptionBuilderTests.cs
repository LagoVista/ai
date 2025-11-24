using LagoVista.AI.Models;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class ModelMetaDataDescriptionBuilderTests
    {
        private IndexFileContext GetIndexFileContext()
        {
            return new IndexFileContext()
            {

            };
        }


        [Test]
        public void Builds_Metadata_From_Device_Model_Source()
        {
            var modelPath = "./Content/AgentContextTest.txt";

            Assert.That(File.Exists(modelPath), Is.True, $"Model content file not found at {modelPath}");

            var source = File.ReadAllText(modelPath);
            var resources = ResxLabelScanner.GetSingleResourceDictionary(".");

            var metadata = ModelMetadataDescriptionBuilder.FromSource(GetIndexFileContext(), source,resources);

            Assert.Multiple(() =>
            {
                Assert.That(metadata, Is.Not.Null);
                Assert.That(metadata.ModelName, Is.EqualTo("AgentContextTestData"));
                Assert.That(metadata.Namespace, Is.EqualTo("LagoVista.AI.Models"));
                Assert.That(metadata.Domain, Is.EqualTo("AIAdmin"));

                Assert.That(metadata.Title, Is.EqualTo("Agent Context"));
                Assert.That(metadata.Description, Is.Not.Empty);
                Assert.That(metadata.Help, Is.Not.Empty);

                Assert.That(metadata.ListUIUrl, Is.EqualTo("/mlworkbench/agents"));
                Assert.That(metadata.EditUIUrl, Is.EqualTo("/mlworkbench/agent/{id}"));
                Assert.That(metadata.CreateUIUrl, Is.EqualTo("/mlworkbench/agent/add"));
                Assert.That(metadata.SaveUrl, Is.EqualTo("/api/ai/agentcontext"));
                Assert.That(metadata.GetListUrl, Is.EqualTo("/api/ai/agentcontexts"));
            });

            Assert.That(metadata.Fields, Is.Not.Null.And.Not.Empty);

            var iconField = metadata.Fields.Find(f => f.PropertyName == nameof(AgentContext.Icon));
            Assert.That(iconField, Is.Not.Null);
            Assert.That(iconField.Label, Is.EqualTo("Icon"));

            var vectorDbNameField = metadata.Fields.Find(f => f.PropertyName == nameof(AgentContext.VectorDatabaseCollectionName));
            Assert.That(vectorDbNameField, Is.Not.Null);
            Assert.That(vectorDbNameField.IsRequired, Is.True);

            // Basic form layout from AgentContextTestData
            Assert.That(metadata.Layouts, Is.Not.Null);
            Assert.That(metadata.Layouts.Form, Is.Not.Null);

            Assert.That(metadata.Layouts.Form.Col1Fields, Is.Not.Null.And.Not.Empty);
            Assert.That(metadata.Layouts.Form.Col2Fields, Is.Not.Null.And.Not.Empty);
            Assert.That(metadata.Layouts.Form.BottomFields, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Builds_Metadata_From_Agent_Content_With_Properties()
        {
            var modelPath = "./Content/AgentContextTest.txt";

            Assert.That(File.Exists(modelPath), Is.True, $"Model content file not found at {modelPath}");

            var source = File.ReadAllText(modelPath);
            var resources = ResxLabelScanner.GetSingleResourceDictionary(".");

            var metadata = ModelMetadataDescriptionBuilder.FromSource(GetIndexFileContext(), source, resources);

            Assert.Multiple(() =>
            {
                Assert.That(metadata, Is.Not.Null);
                Assert.That(metadata.ModelName, Is.EqualTo("AgentContextTestData"));
                Assert.That(metadata.Namespace, Is.EqualTo("LagoVista.AI.Models"));
                Assert.That(metadata.Domain, Is.EqualTo("AIAdmin"));

                Assert.That(metadata.Title, Is.EqualTo("Agent Context"));
                Assert.That(metadata.Description, Is.Not.Empty);
                Assert.That(metadata.Help, Is.Not.Empty);

                Assert.That(metadata.ListUIUrl, Is.EqualTo("/mlworkbench/agents"));
                Assert.That(metadata.EditUIUrl, Is.EqualTo("/mlworkbench/agent/{id}"));
                Assert.That(metadata.CreateUIUrl, Is.EqualTo("/mlworkbench/agent/add"));
                Assert.That(metadata.SaveUrl, Is.EqualTo("/api/ai/agentcontext"));
                Assert.That(metadata.GetListUrl, Is.EqualTo("/api/ai/agentcontexts"));
            });

            Assert.That(metadata.Fields, Is.Not.Null.And.Not.Empty);

            var azureField = metadata.Fields.Find(f => f.PropertyName == nameof(AgentContext.AzureAccountId));
            Assert.Multiple(() =>
            {
                Assert.That(azureField.PropertyName, Is.EqualTo(nameof(AgentContext.AzureAccountId)));
                Assert.That(azureField.FieldType, Is.EqualTo("Text"));
                Assert.That(azureField.Label, Is.EqualTo("Azure Storage Account Id"));
                Assert.That(azureField.Help, Is.EqualTo("Account Id of the Storage Account used to storage raw content that was indexed."));
                Assert.That(azureField.IsRequired, Is.EqualTo(true));
            });
        }

        [Test]
        public void Builds_Expanded_Layouts_From_Layout_Sample_Model()
        {
            var modelPath = "./Content/LayoutSampleModel.cs";

            Assert.That(File.Exists(modelPath), Is.True, $"Layout sample content file not found at {modelPath}");

            var source = File.ReadAllText(modelPath);
            var resources = ResxLabelScanner.GetSingleResourceDictionary(".");

            var metadata = ModelMetadataDescriptionBuilder.FromSource(GetIndexFileContext(), source,  resources);

            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata.Layouts, Is.Not.Null);

            var layouts = metadata.Layouts;

            Assert.Multiple(() =>
            {
                // Main form columns
                Assert.That(layouts.Form.Col1Fields, Is.EqualTo(new[] { "name" }));
                Assert.That(layouts.Form.Col2Fields, Is.EqualTo(new[] { "key" }));
                Assert.That(layouts.Form.BottomFields, Is.EqualTo(new[] { "description" }));

                // Advanced layout
                Assert.That(layouts.Advanced.Col1Fields, Is.EqualTo(new[] { "name" }));
                Assert.That(layouts.Advanced.Col2Fields, Is.EqualTo(new[] { "description" }));

                // Inline / Mobile / Simple / QuickCreate
                Assert.That(layouts.InlineFields, Is.EqualTo(new[] { "name" }));
                Assert.That(layouts.MobileFields, Is.EqualTo(new[] { "key" }));
                Assert.That(layouts.SimpleFields, Is.EqualTo(new[] { "name", "key" }));
                Assert.That(layouts.QuickCreateFields, Is.EqualTo(new[] { "name" }));

                // Additional actions
                Assert.That(layouts.AdditionalActions, Has.Count.EqualTo(1));
                var action = layouts.AdditionalActions[0];

                Assert.That(action.Key, Is.EqualTo("addContext"));
                Assert.That(action.Icon, Is.EqualTo("icon-plus"));
                Assert.That(action.ForCreate, Is.True);
                Assert.That(action.ForEdit, Is.True);

                Assert.That(action.Title, Is.EqualTo(resources["AgentContext_DefaultConversationContext"]));
                Assert.That(action.Help, Is.EqualTo(resources["AgentContext_ConversationContext_Description"]));
            });
        }
    }
}
