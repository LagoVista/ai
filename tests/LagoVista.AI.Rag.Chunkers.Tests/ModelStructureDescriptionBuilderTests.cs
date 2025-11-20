using System.IO;
using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Resources;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class ModelStructureDescriptionBuilderTests
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
        public void Includes_EntityBase_Properties_For_EntityBase_Derived_Model()
        {
            // This source uses EntityBase but we don't need it to compile;
            // the builder only does syntax analysis.
            var source = @"
using LagoVista.Core;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.AI.Models.Resources;

namespace LagoVista.AI.Models
{
    [EntityDescription(
        AIDomain.AIAdmin,
        AIResources.Names.AiAgentContext_Title,
        AIResources.Names.AiAgentContext_Description,
        AIResources.Names.AiAgentContext_Description,
        EntityDescriptionAttribute.EntityTypes.CoreIoTModel,
        typeof(AIResources),
        GetUrl: ""/api/ai/agentcontext/{id}"",
        GetListUrl: ""/api/ai/agentcontexts"",
        FactoryUrl: ""/api/ai/agentcontext/factory"",
        SaveUrl: ""/api/ai/agentcontext"",
        DeleteUrl: ""/api/ai/agentcontext/{id}"",
        ListUIUrl: ""/mlworkbench/agents"",
        EditUIUrl: ""/mlworkbench/agent/{id}"",
        CreateUIUrl: ""/mlworkbench/agent/add"",
        Icon: ""icon-ae-database-3"")]
    public class SampleEntityBaseModel : EntityBase
    {
        [FormField(
            LabelResource: AIResources.Names.Common_Name,
            FieldType: FieldTypes.Text,
            ResourceType: typeof(AIResources))]
        public string CustomProperty { get; set; }
    }
}
";

            // Resource keys are taken from the segment after ".Names."
            var resources = new Dictionary<string, string>
            {
                { "AiAgentContext_Title", "Agent Context" },
                { "AiAgentContext_Description", "Agent Context description" },
                { "Common_Name", "Name" }
            };

            var description = ModelStructureDescriptionBuilder.FromSource(
                source,
                "src/Models/SampleEntityBaseModel.cs",
                resources);

            // All properties
            var allProps = description.Properties.ToList();
            Assert.That(allProps, Is.Not.Empty, "Expected some properties in the description.");

            // EntityBase properties should be present with Group == 'EntityBase'
            var entityBaseProps = allProps
                .Where(p => p.Group == "EntityBase")
                .Select(p => p.Name)
                .ToList();

            Assert.That(entityBaseProps, Is.Not.Empty,
                "Expected at least one EntityBase property to be added to the model structure.");

            // These are very standard EntityBase members in your universe;
            // adjust if your EntityBase differs.
            Assert.That(entityBaseProps, Does.Contain("Name"),
                "Expected EntityBase property 'Name' to be included.");
            Assert.That(entityBaseProps, Does.Contain("Key"),
                "Expected EntityBase property 'Key' to be included.");
        }

        [Test]
        public void Does_Not_Duplicate_EntityBase_Properties_When_Model_Overrides()
        {
            var source = @"
using LagoVista.Core;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.AI.Models.Resources;

namespace LagoVista.AI.Models
{
    [EntityDescription(
        AIDomain.AIAdmin,
        AIResources.Names.AiAgentContext_Title,
        AIResources.Names.AiAgentContext_Description,
        AIResources.Names.AiAgentContext_Description,
        EntityDescriptionAttribute.EntityTypes.CoreIoTModel,
        typeof(AIResources),
        GetUrl: ""/api/ai/agentcontext/{id}"",
        GetListUrl: ""/api/ai/agentcontexts"",
        FactoryUrl: ""/api/ai/agentcontext/factory"",
        SaveUrl: ""/api/ai/agentcontext"",
        DeleteUrl: ""/api/ai/agentcontext/{id}"",
        ListUIUrl: ""/mlworkbench/agents"",
        EditUIUrl: ""/mlworkbench/agent/{id}"",
        CreateUIUrl: ""/mlworkbench/agent/add"",
        Icon: ""icon-ae-database-3"")]
    public class SampleEntityBaseModelWithName : EntityBase
    {
        [FormField(
            LabelResource: AIResources.Names.Common_Name,
            FieldType: FieldTypes.Text,
            ResourceType: typeof(AIResources))]
        public string Name { get; set; }
    }
}
";

            var resources = new Dictionary<string, string>
            {
                { "AiAgentContext_Title", "Agent Context" },
                { "AiAgentContext_Description", "Agent Context description" },
                { "Common_Name", "Name" }
            };

            var description = ModelStructureDescriptionBuilder.FromSource(
                source,
                "src/Models/SampleEntityBaseModelWithName.cs",
                resources);

            var allProps = description.Properties.ToList();
            Assert.That(allProps, Is.Not.Empty, "Expected some properties in the description.");

            // We should only have ONE 'Name' property total (the concrete one),
            // even though EntityBase also has a Name.
            var nameProps = allProps.Where(p => p.Name == "Name").ToList();
            Assert.That(nameProps.Count, Is.EqualTo(1),
                "ModelStructureDescription should not contain duplicate 'Name' properties from EntityBase and the derived type.");

            // And that single Name should *not* be tagged as an EntityBase property.
            Assert.That(nameProps[0].Group, Is.Not.EqualTo("EntityBase"),
                "Overridden 'Name' on the derived model should not be grouped under EntityBase.");

            // Sanity: we should still have *some* EntityBase properties present (e.g., Key, Id, etc.)
            var entityBaseProps = allProps.Where(p => p.Group == "EntityBase").ToList();
            Assert.That(entityBaseProps, Is.Not.Empty,
                "Expected other EntityBase properties to still be included when not overridden.");
        }

    }
}
