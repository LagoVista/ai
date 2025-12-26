using System;
using System.Linq;
using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.Core.Models.UIMetaData;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class DomainDescriptorSummaryExtractorTests
    {
        [Test]
        public void Extract_Should_Find_Domain_With_Const_Key_And_Initializer()
        {
            var source = @"
using System;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models.UIMetaData;

namespace LagoVista.AI.Models
{
    [DomainDescriptor]
    public class AIDomain
    {
        public const string AIAdmin = ""AI Admin"";

        [DomainDescription(AIAdmin)]
        public static DomainDescription AIAdminDescription
        {
            get
            {
                return new DomainDescription
                {
                    Name = ""AI Admin"",
                    Description = ""A set of classes that contains meta data for managing machine learning models."",
                    DomainType = DomainDescription.DomainTypes.BusinessObject
                };
            }
        }
    }
}
";

            var result = DomainDescriptorSummaryExtractor.Extract(source).Result;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));

            var summary = result.Single();

            Assert.Multiple(() =>
            {
                Assert.That(summary.DomainKey, Is.EqualTo("AI Admin"));
                Assert.That(summary.DomainKeyName, Is.EqualTo("AIAdmin"));
                Assert.That(summary.Title, Is.EqualTo("AI Admin"));
                Assert.That(summary.Description, Does.Contain("meta data for managing machine learning models"));
                Assert.That(summary.DomainType, Is.EqualTo(DomainDescription.DomainTypes.BusinessObject));
                Assert.That(summary.SourceTypeName, Is.EqualTo("LagoVista.AI.Models.AIDomain"));
                Assert.That(summary.SourcePropertyName, Is.EqualTo("AIAdminDescription"));
            });
        }

        [Test]
        public void Extract_Should_Handle_Literal_Attribute_Value_Without_Const()
        {
            var source = @"
using System;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models.UIMetaData;

namespace LagoVista.Notifications.Models
{
    [DomainDescriptor]
    public class NotificationDomain
    {
        [DomainDescription(""Notifications"")]
        public static DomainDescription NotificationDescription
        {
            get
            {
                return new DomainDescription
                {
                    Name = ""Notifications"",
                    Description = ""Handles outbound user notifications and messaging."",
                    DomainType = DomainDescription.DomainTypes.DTO
                };
            }
        }
    }
}
";

            var result = DomainDescriptorSummaryExtractor.Extract(source).Result;

            Assert.That(result.Count, Is.EqualTo(1));
            var summary = result.Single();

            Assert.Multiple(() =>
            {
                Assert.That(summary.DomainKey, Is.EqualTo("Notifications"));
                Assert.That(summary.Title, Is.EqualTo("Notifications"));
                Assert.That(summary.Description, Does.Contain("outbound user notifications"));
                // DomainType is explicitly set to System in the initializer
                Assert.That(summary.DomainType, Is.EqualTo(DomainDescription.DomainTypes.Dto));
                Assert.That(summary.SourceTypeName, Is.EqualTo("LagoVista.Notifications.Models.NotificationDomain"));
                Assert.That(summary.SourcePropertyName, Is.EqualTo("NotificationDescription"));
            });
        }


        [Test]
        public void Extract_Should_Fallback_To_Attribute_Or_Property_When_Name_Missing()
        {
            var source = @"
using System;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models.UIMetaData;

namespace LagoVista.AI.Models
{
    [DomainDescriptor]
    public class ExperimentalDomain
    {
        [DomainDescription(""Experimental"")]
        public static DomainDescription ExperimentalDescription
        {
            get
            {
                return new DomainDescription
                {
                    // Name is intentionally omitted here
                    Description = ""short""
                    // DomainType is also omitted, so we should default to Unknown
                };
            }
        }
    }
}
";

            var result = DomainDescriptorSummaryExtractor.Extract(source).Result;

            Assert.That(result.Count, Is.EqualTo(1));
            var summary = result.Single();

            Assert.Multiple(() =>
            {
                // Title falls back to attribute value ("Experimental")
                Assert.That(summary.Title, Is.EqualTo("Experimental"));
                Assert.That(summary.DomainKey, Is.EqualTo("Experimental"));

                // DomainType is not set in the initializer, so extractor defaults to NotReady
                Assert.That(summary.DomainType, Is.EqualTo(DomainDescription.DomainTypes.BusinessObject));
            });

            var issues = summary.ValidateBasicQuality();
            Assert.That(issues.Count, Is.GreaterThan(0));
        }

    }
}
