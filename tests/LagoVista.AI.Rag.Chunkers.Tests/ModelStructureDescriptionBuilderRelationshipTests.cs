using System.Collections.Generic;
using System.Linq;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class ModelStructureDescriptionBuilderRelationshipTests
    {
        private static IReadOnlyDictionary<string, string> BuildResources()
        {
            // Keys are what ExtractResourceKey() produces (segment after ".Names.")
            return new Dictionary<string, string>
            {
                ["Model_Title"] = "Test Model",
                ["Model_Description"] = "Test description",
                ["Model_Help"] = "Help text"
            };
        }

        [Test]
        public void Builds_OneToOne_Relationship_From_EntityHeader_Property()
        {
            var source = @"
using LagoVista.Core.Models;
using LagoVista.Core.Attributes;

namespace MyApp.Models
{
    [EntityDescription(""AIAdmin"", FakeResources.Names.Model_Title, FakeResources.Names.Model_Description, FakeResources.Names.Model_Help,
        EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(FakeResources))]
    public class RelationshipModel
    {
        [FormField(LabelResource: FakeResources.Names.Model_Title, FieldType: FieldTypes.EntityHeader, ResourceType: typeof(FakeResources))]
        public EntityHeader<Device> Device { get; set; }
    }

    public class Device : EntityBase
    {
    }

    public static class FakeResources
    {
        public static class Names
        {
            public const string Model_Title = ""Model_Title"";
            public const string Model_Description = ""Model_Description"";
            public const string Model_Help = ""Model_Help"";
        }
    }
}
";

            var resources = BuildResources();

            var description = ModelStructureDescriptionBuilder.FromSource(
                source,
                "src/Models/RelationshipModel.cs",
                resources);

            Assert.Multiple(() =>
            {
                // EntityHeaderRefs
                Assert.That(description.EntityHeaderRefs, Has.Count.EqualTo(1));
                var headerRef = description.EntityHeaderRefs[0];
                Assert.That(headerRef.Key, Is.EqualTo("device"));
                Assert.That(headerRef.PropertyName, Is.EqualTo("Device"));
                Assert.That(headerRef.TargetType, Is.EqualTo("Device"));
                Assert.That(headerRef.IsCollection, Is.False);

                // Relationships
                Assert.That(description.Relationships, Has.Count.EqualTo(1));
                var rel = description.Relationships[0];
                Assert.That(rel.Name, Is.EqualTo("RelationshipModelToDevice"));
                Assert.That(rel.FromModel, Is.EqualTo("MyApp.Models.RelationshipModel"));
                Assert.That(rel.ToModel, Is.EqualTo("Device"));
                Assert.That(rel.Cardinality, Is.EqualTo("OneToOne"));
                Assert.That(rel.SourceProperty, Is.EqualTo("Device"));
                Assert.That(rel.TargetProperty, Is.Null);
            });
        }

        [Test]
        public void Builds_OneToMany_Relationship_From_EntityHeader_List_Property()
        {
            var source = @"
using System.Collections.Generic;
using LagoVista.Core.Models;
using LagoVista.Core.Attributes;

namespace MyApp.Models
{
    [EntityDescription(""AIAdmin"", FakeResources.Names.Model_Title, FakeResources.Names.Model_Description, FakeResources.Names.Model_Help,
        EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(FakeResources))]
    public class RelationshipModelWithCollection
    {
        [FormField(LabelResource: FakeResources.Names.Model_Title, FieldType: FieldTypes.EntityHeader, ResourceType: typeof(FakeResources))]
        public List<EntityHeader<Customer>> Customers { get; set; }
    }

    public class Customer : EntityBase
    {
    }

    public static class FakeResources
    {
        public static class Names
        {
            public const string Model_Title = ""Model_Title"";
            public const string Model_Description = ""Model_Description"";
            public const string Model_Help = ""Model_Help"";
        }
    }
}
";

            var resources = BuildResources();

            var description = ModelStructureDescriptionBuilder.FromSource(
                source,
                "src/Models/RelationshipModelWithCollection.cs",
                resources);

            Assert.Multiple(() =>
            {
                // EntityHeaderRefs
                Assert.That(description.EntityHeaderRefs, Has.Count.EqualTo(1));
                var headerRef = description.EntityHeaderRefs[0];
                Assert.That(headerRef.Key, Is.EqualTo("customers"));
                Assert.That(headerRef.PropertyName, Is.EqualTo("Customers"));
                Assert.That(headerRef.TargetType, Is.EqualTo("Customer"));
                Assert.That(headerRef.IsCollection, Is.True);

                // Relationships
                Assert.That(description.Relationships, Has.Count.EqualTo(1));
                var rel = description.Relationships[0];
                Assert.That(rel.Name, Is.EqualTo("RelationshipModelWithCollectionToCustomer"));
                Assert.That(rel.FromModel, Is.EqualTo("MyApp.Models.RelationshipModelWithCollection"));
                Assert.That(rel.ToModel, Is.EqualTo("Customer"));
                Assert.That(rel.Cardinality, Is.EqualTo("OneToMany"));
                Assert.That(rel.SourceProperty, Is.EqualTo("Customers"));
                Assert.That(rel.TargetProperty, Is.Null);
            });
        }

        [Test]
        public void Builds_EntityHeader_Relationships_From_Model()
        {
            // Note: this is raw C# source passed into the Roslyn-based builder.
            // Types like AIResources, AIDomain, EntityHeader, etc. do NOT need
            // to exist at runtime. They are just plain text to the parser.
            var source = @"
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Attributes;
using LagoVista.Core;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    public enum LlmProviders { OpenAI }

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
    public class AgentContextTestData : EntityBase
    {
        [FormField(
            LabelResource: AIResources.Names.AgentContext_LlmProvider,
            FieldType: FieldTypes.Picker,
            EnumType: typeof(LlmProviders),
            ResourceType: typeof(AIResources))]
        public EntityHeader<LlmProviders> LlmProvider { get; set; }

        [FormField(
            LabelResource: AIResources.Names.AgentContext_DefaultConversationContext,
            FieldType: FieldTypes.EntityHeaderPicker,
            ResourceType: typeof(AIResources))]
        public EntityHeader DefaultConversationContext { get; set; }
    }
}
";

            // Minimal resource dictionary to satisfy EntityDescription lookups
            var resources = new Dictionary<string, string>
            {
                ["AiAgentContext_Title"] = "AI Agent Context",
                ["AiAgentContext_Description"] = "AI Agent Context Description"
            };

            var description = ModelStructureDescriptionBuilder.FromSource(
                source,
                "src/Models/AgentContextTestData.cs",
                resources);

            Assert.Multiple(() =>
            {
                // Expect two EntityHeader-based properties
                Assert.That(description.EntityHeaderRefs, Has.Count.EqualTo(2),
                    "Should detect two EntityHeader references.");
                Assert.That(description.Relationships, Has.Count.EqualTo(2),
                    "Each EntityHeader should produce one relationship.");

                // ----- LlmProvider: EntityHeader<LlmProviders> (One-to-One) -----
                var llmRef = description.EntityHeaderRefs
                    .Single(r => r.PropertyName == "LlmProvider");

                Assert.That(llmRef.Key, Is.EqualTo("llmProvider"));
                Assert.That(llmRef.TargetType, Is.EqualTo("LlmProviders"));
                Assert.That(llmRef.IsCollection, Is.False);

                var llmRel = description.Relationships
                    .Single(r => r.SourceProperty == "LlmProvider");

                Assert.That(llmRel.FromModel, Is.EqualTo(description.QualifiedName));
                Assert.That(llmRel.ToModel, Is.EqualTo("LlmProviders"));
                Assert.That(llmRel.Cardinality, Is.EqualTo("OneToOne"));

                // ----- DefaultConversationContext: plain EntityHeader (One-to-One) -----
                var defaultRef = description.EntityHeaderRefs
                    .Single(r => r.PropertyName == "DefaultConversationContext");

                Assert.That(defaultRef.Key, Is.EqualTo("defaultConversationContext"));
                Assert.That(defaultRef.TargetType, Is.Null);
                Assert.That(defaultRef.IsCollection, Is.False);

                var defaultRel = description.Relationships
                    .Single(r => r.SourceProperty == "DefaultConversationContext");

                Assert.That(defaultRel.FromModel, Is.EqualTo(description.QualifiedName));
                Assert.That(defaultRel.ToModel, Is.EqualTo("EntityHeader"));
                Assert.That(defaultRel.Cardinality, Is.EqualTo("OneToOne"));
            });
        }

        [Test]
        public void Builds_Single_ChildView_Composition_From_Model()
        {
            var source = @"
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Attributes;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    public static class AIResources
    {
        public static class Names
        {
            public const string AgentContext_Title = ""AgentContext_Title"";
            public const string AgentContext_Description = ""AgentContext_Description"";
            public const string Common_Name = ""Common_Name"";
            public const string AgentContext_Address = ""AgentContext_Address"";
            public const string Common_Street = ""Common_Street"";
        }
    }

    public static class AIDomain
    {
        public const string AIAdmin = ""AIAdmin"";
    }

    [EntityDescription(
        AIDomain.AIAdmin,
        AIResources.Names.AgentContext_Title,
        AIResources.Names.AgentContext_Description,
        AIResources.Names.AgentContext_Description,
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
    public class ParentModel : EntityBase
    {
        [FormField(LabelResource: AIResources.Names.Common_Name,
                   FieldType: FieldTypes.Text,
                   ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_Address,
                   FieldType: FieldTypes.ChildView,
                   ResourceType: typeof(AIResources))]
        public Address Address { get; set; }
    }

    public class Address
    {
        [FormField(LabelResource: AIResources.Names.Common_Street,
                   FieldType: FieldTypes.Text,
                   ResourceType: typeof(AIResources))]
        public string Street { get; set; }
    }
}
";

            var resources = new Dictionary<string, string>
            {
                ["AgentContext_Title"] = "Agent Context",
                ["AgentContext_Description"] = "Agent Context Description",
                ["Common_Name"] = "Name",
                ["AgentContext_Address"] = "Address",
                ["Common_Street"] = "Street"
            };

            var description = ModelStructureDescriptionBuilder.FromSource(
                source,
                "src/Models/ParentModel.cs",
                resources);

            Assert.Multiple(() =>
            {
                // We should still have the scalar Name property.
                var nameProp = description.Properties.Single(p => p.Name == "Name");
                Assert.That(nameProp.ClrType, Is.EqualTo("string"));
                Assert.That(nameProp.IsCollection, Is.False);

                // There should be a property for Address.
                var addressProp = description.Properties.Single(p => p.Name == "Address");
                Assert.That(addressProp.ClrType, Is.EqualTo("Address"));
                Assert.That(addressProp.IsCollection, Is.False);

                // And there should be a ChildObject entry keyed off Address.
                Assert.That(description.ChildObjects, Has.Count.EqualTo(1));
                var child = description.ChildObjects.Single();

                Assert.That(child.PropertyName, Is.EqualTo("Address"));
                Assert.That(child.Key, Is.EqualTo("address"));   // camelCase convention
                Assert.That(child.ClrType, Is.EqualTo("Address"));
                Assert.That(child.IsCollection, Is.False);

                // Relationship-wise, we’re treating this as composition.
                // Depending on how we wire it, we either:
                //  - don’t add a Relationship entry, OR
                //  - add one with Cardinality = ""Composition"".
                //
                // For now we’ll assert the conservative expectation:
                Assert.That(description.Relationships.Any(r => r.SourceProperty == "Address"), Is.False);
            });
        }

        [Test]
        public void Builds_ChildList_Composition_From_Model()
        {
            var source = @"
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Attributes;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    public static class AIResources
    {
        public static class Names
        {
            public const string AgentContext_Title = ""AgentContext_Title"";
            public const string AgentContext_Description = ""AgentContext_Description"";
            public const string Common_Name = ""Common_Name"";
            public const string AgentContext_Addresses = ""AgentContext_Addresses"";
            public const string Common_Street = ""Common_Street"";
        }
    }

    public static class AIDomain
    {
        public const string AIAdmin = ""AIAdmin"";
    }

    [EntityDescription(
        AIDomain.AIAdmin,
        AIResources.Names.AgentContext_Title,
        AIResources.Names.AgentContext_Description,
        AIResources.Names.AgentContext_Description,
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
    public class ParentModel : EntityBase
    {
        [FormField(LabelResource: AIResources.Names.Common_Name,
                   FieldType: FieldTypes.Text,
                   ResourceType: typeof(AIResources))]
        public string Name { get; set; }

        [FormField(LabelResource: AIResources.Names.AgentContext_Addresses,
                   FieldType: FieldTypes.ChildListInline,
                   ResourceType: typeof(AIResources))]
        public List<Address> Addresses { get; set; }
    }

    public class Address
    {
        [FormField(LabelResource: AIResources.Names.Common_Street,
                   FieldType: FieldTypes.Text,
                   ResourceType: typeof(AIResources))]
        public string Street { get; set; }
    }
}
";

            var resources = new Dictionary<string, string>
            {
                ["AgentContext_Title"] = "Agent Context",
                ["AgentContext_Description"] = "Agent Context Description",
                ["Common_Name"] = "Name",
                ["AgentContext_Addresses"] = "Addresses",
                ["Common_Street"] = "Street"
            };

            var description = ModelStructureDescriptionBuilder.FromSource(
                source,
                "src/Models/ParentModel.cs",
                resources);

            Assert.Multiple(() =>
            {
                // Addresses should appear as a collection property.
                var addressesProp = description.Properties.Single(p => p.Name == "Addresses");
                Assert.That(addressesProp.ClrType, Is.EqualTo("Address"));
                Assert.That(addressesProp.IsCollection, Is.True);

                // And there should be a ChildObject entry describing the composition.
                Assert.That(description.ChildObjects, Has.Count.EqualTo(1));
                var child = description.ChildObjects.Single();

                Assert.That(child.PropertyName, Is.EqualTo("Addresses"));
                Assert.That(child.Key, Is.EqualTo("addresses"));
                Assert.That(child.ClrType, Is.EqualTo("Address"));
                Assert.That(child.IsCollection, Is.True);

                // Again, treat this as composition (no external relationship).
                Assert.That(description.Relationships.Any(r => r.SourceProperty == "Addresses"), Is.False);
            });
        }
    }
}
