using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LagoVista.AI.Rag.Chunkers.Models; // DomainSummaryInfo
using LagoVista.AI.Rag.Services;        // DomainCatalogService
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    /// <summary>
    /// Tests for the private Roslyn-based domain extraction helper on DomainCatalogService.
    ///
    /// These tests invoke the private static ExtractDomainsFromSnippet method via reflection
    /// to avoid expanding the public surface area.
    /// </summary>
    [TestFixture]
    public class DomainCatalogServiceDomainExtractionTests
    {
        private static IReadOnlyList<DomainSummaryInfo> Extract(string source)
        {
            var method = typeof(DomainCatalogService).GetMethod(
                "ExtractDomainsFromSnippet",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null, "Expected ExtractDomainsFromSnippet private method to exist.");

            var result = method.Invoke(null, new object[] { source }) as IReadOnlyList<DomainSummaryInfo>;
            Assert.That(result, Is.Not.Null, "Expected non-null result from ExtractDomainsFromSnippet.");

            return result;
        }

        [Test]
        public void Extract_BasicDomainDescriptor_ReturnsSingleSummary()
        {
            const string source = @"
using LagoVista.Core.Attributes;
using LagoVista.Core.Models.UIMetaData;

namespace Sample
{
    [DomainDescriptor]
    public class SampleDomain
    {
        [DomainDescription(""SampleDomainKey"")]
        public static DomainDescription Domain
        {
            get
            {
                return new DomainDescription
                {
                    Name = ""Sample Domain"",
                    Description = ""Sample Domain Description""
                };
            }
        }
    }
}
";

            var summaries = Extract(source);

            Assert.That(summaries.Count, Is.EqualTo(1));
            var summary = summaries[0];

            Assert.That(summary.DomainKey, Is.EqualTo("SampleDomainKey"));
            Assert.That(summary.Title, Is.EqualTo("Sample Domain"));
            Assert.That(summary.Description, Is.EqualTo("Sample Domain Description"));
        }

        [Test]
        public void Extract_UsesConstString_ForDomainKey_AndKeyName()
        {
            const string source = @"
using LagoVista.Core.Attributes;
using LagoVista.Core.Models.UIMetaData;

namespace Sample
{
    [DomainDescriptor]
    public class AnotherDomain
    {
        public const string DOMAIN_KEY = ""AnotherKey"";

        [DomainDescription(DOMAIN_KEY)]
        public static DomainDescription Domain
        {
            get
            {
                return new DomainDescription
                {
                    Name = ""Another Domain"",
                    Description = ""Another Domain Description""
                };
            }
        }
    }
}
";

            var summaries = Extract(source);

            Assert.That(summaries.Count, Is.EqualTo(1));
            var summary = summaries[0];

            Assert.That(summary.DomainKey, Is.EqualTo("AnotherKey"));
            Assert.That(summary.DomainKeyName, Is.EqualTo("DOMAIN_KEY"));
        }

        [Test]
        public void Extract_FallsBackToDomainKey_WhenTitleMissing()
        {
            const string source = @"
using LagoVista.Core.Attributes;
using LagoVista.Core.Models.UIMetaData;

namespace Sample
{
    [DomainDescriptor]
    public class KeyOnlyDomain
    {
        [DomainDescription(""KeyOnly"")]
        public static DomainDescription Domain
        {
            get
            {
                return new DomainDescription
                {
                    Description = ""Has description only""
                };
            }
        }
    }
}
";

            var summaries = Extract(source);

            Assert.That(summaries.Count, Is.EqualTo(1));
            var summary = summaries[0];

            // Name not set in initializer, so it should fall back to DomainKey.
            Assert.That(summary.Title, Is.EqualTo("KeyOnly"));
            Assert.That(summary.DomainKey, Is.EqualTo("KeyOnly"));
        }

        [Test]
        public void Extract_Ignores_Class_Without_DomainDescriptor_Attribute()
        {
            const string source = @"
using LagoVista.Core.Attributes;
using LagoVista.Core.Models.UIMetaData;

namespace Sample
{
    public class NoDomainDescriptor
    {
        [DomainDescription(""Key"")]
        public static DomainDescription Domain
        {
            get
            {
                return new DomainDescription
                {
                    Name = ""Should Not Be Seen"",
                    Description = ""Should Not Be Seen""
                };
            }
        }
    }
}
";

            var summaries = Extract(source);

            Assert.That(summaries.Count, Is.EqualTo(0));
        }

        [Test]
        public void Extract_Ignores_NonStatic_Or_NonPublic_Properties()
        {
            const string source = @"
using LagoVista.Core.Attributes;
using LagoVista.Core.Models.UIMetaData;

namespace Sample
{
    [DomainDescriptor]
    public class InvalidDomain
    {
        // Not static
        [DomainDescription(""Key1"")]
        public DomainDescription InstanceDomain
        {
            get
            {
                return new DomainDescription
                {
                    Name = ""Instance Domain"",
                    Description = ""Should Be Ignored""
                };
            }
        }

        // Static but not public
        [DomainDescription(""Key2"")]
        static DomainDescription PrivateDomain
        {
            get
            {
                return new DomainDescription
                {
                    Name = ""Private Domain"",
                    Description = ""Should Be Ignored""
                };
            }
        }
    }
}
";

            var summaries = Extract(source);

            Assert.That(summaries.Count, Is.EqualTo(0));
        }

        [Test]
        public void Extract_RealWorld_AIDomain_File_ProducesExpectedSummary()
        {
            // Arrange
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Content", "AIDomain.txt");
            Assert.That(File.Exists(path), Is.True, $"Expected real-world domain file at '{path}'.");

            var source = File.ReadAllText(path);

            // Act
            var summaries = Extract(source);

            // Assert
            Assert.That(summaries.Count, Is.EqualTo(1), "Real-world AIDomain should produce a single summary.");

            var summary = summaries[0];

            Assert.That(summary.DomainKey, Is.EqualTo("AI Admin"));
            Assert.That(summary.Title, Is.EqualTo("AI Admin"));
            Assert.That(summary.Description, Is.EqualTo("A set of classes that contains meta data for managing machine learning models."));
        }
    }
}
