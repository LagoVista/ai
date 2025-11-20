using System.Collections.Generic;
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
    }
}
