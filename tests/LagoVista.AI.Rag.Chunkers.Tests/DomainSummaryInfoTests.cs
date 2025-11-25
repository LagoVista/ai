using System;
using System.Linq;
using NUnit.Framework;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class DomainSummaryInfoTests
    {
        [Test]
        public void BuildSummarySection_Should_Include_Core_Fields()
        {
            var info = new DomainSummaryInfo(
                domainKey: "AI Admin",
                domainKeyName: null, 
                title: "AI Admin",
                description: "A set of classes that contains meta data for managing machine learning models.",
                domainType: DomainDescription.DomainTypes.BusinessObject,
                sourceTypeName: "LagoVista.AI.Models.AIDomain",
                sourcePropertyName: "AIAdminDescription");

            var section = info.BuildSections(null).Single();

            Assert.Multiple(() =>
            {
                Assert.That(section, Is.Not.Null);

                // New core properties
                Assert.That(section.SectionKey, Is.EqualTo("domain-ai-admin"));
                //Assert.That(section.SectionType, Is.EqualTo("Overview"));
                Assert.That(section.Symbol, Is.EqualTo("LagoVista.AI.Models.AIDomain"));
                Assert.That(section.SymbolType, Is.EqualTo("Domain"));

                // Title + normalized text
                //Assert.That(section.Title, Is.EqualTo("AI Admin - Domain Overview"));
                Assert.That(section.SectionNormalizedText, Does.Contain("Domain Title: AI Admin"));
                Assert.That(section.SectionNormalizedText, Does.Contain("Domain Description:"));
                Assert.That(section.SectionNormalizedText,
                    Does.Contain("meta data for managing machine learning models"));
            });
        }

        [Test]
        public void ValidateBasicQuality_Should_Flag_Empty_Description()
        {
            var info = new DomainSummaryInfo(
                domainKey: "Experimental",
                domainKeyName: null,
                title: "Experimental",
                description: string.Empty,
                domainType: DomainDescription.DomainTypes.BusinessObject,
                sourceTypeName: "LagoVista.AI.Models.ExperimentalDomain",
                sourcePropertyName: "ExperimentalDescription");

            var issues = info.ValidateBasicQuality();

            Assert.Multiple(() =>
            {
                Assert.That(issues, Is.Not.Null);
                Assert.That(issues.Count, Is.GreaterThanOrEqualTo(1));
                Assert.That(issues.First(), Does.Contain("empty description").IgnoreCase);
            });
        }

        [Test]
        public void ValidateBasicQuality_Should_Flag_Very_Short_Description()
        {
            var info = new DomainSummaryInfo(
                domainKey: "Experimental",
                domainKeyName: null, 
                title: "Ex",
                description: "short",
                domainType: DomainDescription.DomainTypes.BusinessObject,
                sourceTypeName: "LagoVista.AI.Models.ExperimentalDomain",
                sourcePropertyName: "ExperimentalDescription");

            var issues = info.ValidateBasicQuality();

            Assert.That(issues.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(string.Join(";", issues), Does.Contain("very short").IgnoreCase);
        }
    }
}
