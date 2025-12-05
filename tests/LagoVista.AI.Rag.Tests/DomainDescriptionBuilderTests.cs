using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    [TestFixture]
    public class DomainDescriptionBuilderTests
    {
        [Test]
        public async Task BuildAsync_ValidDocument_BuildsExpectedDescriptionAndSummarySection()
        {
            // Arrange
            const string documentText =
                "# Devices\n" +
                "Handles all device provisioning, registration, and lifecycle operations.\n" +
                "\n" +
                "Additional narrative line 1.\n" +
                "Additional narrative line 2.\n";

            var fileContext = new IndexFileContext
            {
                FullPath = "C:/src/LagoVista.AI.Rag/Domains/Devices.md",
                RelativePath = "Domains/Devices.md",
                RepoId = "TestRepo",
                Language = "markdown"
            };

            var deviceClass = new ModelClassEntry(
                domainKey: "devices",
                className: "Device",
                qualifiedClassName: "LagoVista.Devices.Models.Device",
                title: "Registered Device",
                description: "Represents a device in the system",
                helpText: "Help text for Device",
                relativePath: "src/devices/models/Device.cs");

            var deviceGroupClass = new ModelClassEntry(
                domainKey: "devices",
                className: "DeviceGroup",
                qualifiedClassName: "LagoVista.Devices.Models.DeviceGroup",
                title: "Device Group",
                description: "Logical grouping of devices",
                helpText: "Help text for DeviceGroup",
                relativePath: "src/devices/models/DeviceGroup.cs");

            var catalogMock = new Mock<IDomainCatalogService>();
            catalogMock
                .Setup(svc => svc.GetClassesForDomain("devices"))
                .Returns(new List<ModelClassEntry> { deviceClass, deviceGroupClass });

            var loggerMock = new Mock<IAdminLogger>();

            var builder = new DomainDescriptionBuilder(loggerMock.Object);

            // Act
            var result = await builder.BuildAsync(
                fileContext,
                documentText,
                catalogMock.Object,
                resourceDictionary: null);

            // Assert
            Assert.That(result, Is.Not.Null, "Expected a non-null InvokeResult.");
            Assert.That(result.Successful, Is.True, "Expected builder to succeed for a valid domain document.");
            Assert.That(result.Result, Is.Not.Null, "Expected a non-null IRagDescription result.");

            var domainDescription = result.Result as DomainDescriptionRag;
            Assert.That(domainDescription, Is.Not.Null, "Expected result to be a DomainDescriptionRag instance.");

            Assert.That(domainDescription.DomainName, Is.EqualTo("Devices"), "DomainName should come from the H1 heading.");
            Assert.That(
                domainDescription.DomainSummary,
                Is.EqualTo("Handles all device provisioning, registration, and lifecycle operations."),
                "DomainSummary should be the first non-empty line after the heading.");

            Assert.That(domainDescription.Classes, Has.Count.EqualTo(2), "Expected two model classes from the catalog.");
            Assert.That(domainDescription.Classes[0].ClassName, Is.EqualTo("Device"));
            Assert.That(domainDescription.Classes[1].ClassName, Is.EqualTo("DeviceGroup"));

            var sections = domainDescription.BuildSummarySections();
            Assert.That(sections, Is.Not.Null);
            Assert.That(sections.Count(), Is.EqualTo(1), "DomainDescription must produce exactly one SummarySection.");

            var section = sections.First();
            Assert.That(section.SectionKey, Is.EqualTo("domain-devices"), "SectionKey must follow domain-{normalizedDomainName} pattern.");
            Assert.That(section.PartIndex, Is.EqualTo(1));
            Assert.That(section.PartTotal, Is.EqualTo(1));
            Assert.That(section.SymbolType, Is.EqualTo("Domain"));
            Assert.That(section.Symbol, Is.EqualTo("Devices"));

            Assert.That(section.FinderSnippet, Does.Contain("Domain: Devices"));
            Assert.That(section.FinderSnippet, Does.Contain("DomainSummary: Handles all device provisioning, registration, and lifecycle operations."));
            Assert.That(section.FinderSnippet, Does.Contain("Kind: Domain"));
            Assert.That(section.FinderSnippet, Does.Contain("Artifact: Devices"));
            Assert.That(section.FinderSnippet, Does.Contain("Purpose: Describes the scope and responsibilities of the Devices domain."));

            // Backing artifact checks
            Assert.That(section.BackingArtifact, Does.Contain("## Model Classes in This Domain"));
            Assert.That(section.BackingArtifact, Does.Contain("### Device"));
            Assert.That(section.BackingArtifact, Does.Contain("### DeviceGroup"));
            Assert.That(section.BackingArtifact, Does.Contain("Qualified Name: LagoVista.Devices.Models.Device"));
            Assert.That(section.BackingArtifact, Does.Contain("Qualified Name: LagoVista.Devices.Models.DeviceGroup"));

        }

        [Test]
        public async Task BuildAsync_MissingHeading_UsesFileNameAsDomainName()
        {
            // Arrange
            const string documentText =
                "Handles all device provisioning, registration, and lifecycle operations.\n" +
                "More narrative.\n";

            var fileContext = new IndexFileContext
            {
                FullPath = "C:/src/LagoVista.AI.Rag/Domains/Business.md",
                RelativePath = "Domains/Business.md",
                RepoId = "TestRepo",
                Language = "markdown"
            };

            var catalogMock = new Mock<IDomainCatalogService>();
            catalogMock
                .Setup(svc => svc.GetClassesForDomain("business"))
                .Returns(new List<ModelClassEntry>());

            var loggerMock = new Mock<IAdminLogger>();
            var builder = new DomainDescriptionBuilder(loggerMock.Object);

            // Act
            var result = await builder.BuildAsync(
                fileContext,
                documentText,
                catalogMock.Object,
                resourceDictionary: null);

            // Assert
            Assert.That(result.Successful, Is.True, "Builder should succeed even without an explicit heading.");

            var domainDescription = result.Result as DomainDescriptionRag;
            Assert.That(domainDescription, Is.Not.Null);
            Assert.That(domainDescription.DomainName, Is.EqualTo("Business"), "DomainName should fall back to file name without extension.");

            var sections = domainDescription.BuildSummarySections();
            Assert.That(sections.Count, Is.EqualTo(1));
            Assert.That(sections.First().SectionKey, Is.EqualTo("domain-business"));
        }
    }
}
