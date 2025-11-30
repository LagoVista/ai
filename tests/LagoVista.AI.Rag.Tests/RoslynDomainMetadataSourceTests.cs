using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    [TestFixture]
    public class RoslynDomainMetadataSourceTests
    {
        private static string CreateTempCsFile(string content)
        {
            var fileName = $"DomainMetaSrc-{Guid.NewGuid():N}.cs";
            var path = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(path, content);
            return path;
        }

        [Test]
        public async Task GetDomainsAsync_FindsDomainDescriptor_And_AssociatesModels()
        {
            var code =
                "using LagoVista.Core.Attributes;\r\n" +
                "using LagoVista.Core.Models.UIMetaData;\r\n" +
                "\r\n" +
                "namespace LagoVista.AI.Models\r\n" +
                "{\r\n" +
                "    [DomainDescriptor]\r\n" +
                "    public class AIDomain\r\n" +
                "    {\r\n" +
                "        public const string AIAdmin = \"AI Admin\";\r\n" +
                "\r\n" +
                "        [DomainDescription(AIAdmin)]\r\n" +
                "        public static DomainDescription AIAdminDescription\r\n" +
                "        {\r\n" +
                "            get\r\n" +
                "            {\r\n" +
                "                return new DomainDescription\r\n" +
                "                {\r\n" +
                "                    Name = \"AI Admin\",\r\n" +
                "                    Description = \"Admin domain for AI-related metadata.\"\r\n" +
                "                };\r\n" +
                "            }\r\n" +
                "        }\r\n" +
                "    }\r\n" +
                "}\r\n";

            var path = CreateTempCsFile(code);

            var discovered = new List<DiscoveredFile>
            {
                new DiscoveredFile
                {
                    RepoId = "repo1",
                    FullPath = path,
                    RelativePath = Path.GetFileName(path),
                    IsBinary = false,
                    SizeBytes = new FileInfo(path).Length
                }
            };

            var models = new List<ModelMetadata>
            {
                new ModelMetadata
                {
                    RepoId = "repo1",
                    FullPath = path,
                    ClassName = "AgentContext",
                    DomainKey = "AIAdmin",
                    Title = "Agent Context",
                    Description = "Context for AI agents."
                }
            };

            var logger = new Mock<IAdminLogger>();
            var src = new RoslynDomainMetadataSource(logger.Object);

            var domains = await src.GetDomainsAsync(discovered, models, CancellationToken.None);

            Assert.That(domains, Has.Count.EqualTo(1));
            var domain = domains[0];

            Assert.That(domain.ClassName, Is.EqualTo("AIDomain"));
            Assert.That(domain.DomainKey, Is.EqualTo("AIAdmin"));
            Assert.That(domain.Name, Is.EqualTo("AI Admin"));
            Assert.That(domain.Description, Is.EqualTo("Admin domain for AI-related metadata."));
            Assert.That(domain.Entities, Has.Count.EqualTo(1));

            var entity = domain.Entities[0];
            Assert.That(entity.SymbolName, Is.EqualTo("AgentContext"));
            Assert.That(entity.Title, Is.EqualTo("Agent Context"));
            Assert.That(entity.Description, Is.EqualTo("Context for AI agents."));
            Assert.That(domain.HasErrors, Is.False, string.Join("; ", domain.Errors));
        }

        [Test]
        public async Task GetDomainsAsync_MultipleDomainDescriptionMembers_FlagsError()
        {
            var code =
                "using LagoVista.Core.Attributes;\r\n" +
                "\r\n" +
                "namespace LagoVista.AI.Models\r\n" +
                "{\r\n" +
                "    [DomainDescriptor]\r\n" +
                "    public class AIDomain\r\n" +
                "    {\r\n" +
                "        public const string AIAdmin = \"AI Admin\";\r\n" +
                "        public const string AIOps = \"AI Ops\";\r\n" +
                "\r\n" +
                "        [DomainDescription(AIAdmin)]\r\n" +
                "        public static DomainDescription AIAdminDescription\r\n" +
                "        {\r\n" +
                "            get\r\n" +
                "            {\r\n" +
                "                return new DomainDescription { Name = \"AI Admin\", Description = \"Admin domain.\" };\r\n" +
                "            }\r\n" +
                "        }\r\n" +
                "\r\n" +
                "        [DomainDescription(AIOps)]\r\n" +
                "        public static DomainDescription AIOpsDescription\r\n" +
                "        {\r\n" +
                "            get\r\n" +
                "            {\r\n" +
                "                return new DomainDescription { Name = \"AI Ops\", Description = \"Ops domain.\" };\r\n" +
                "            }\r\n" +
                "        }\r\n" +
                "    }\r\n" +
                "}\r\n";

            var path = CreateTempCsFile(code);

            var discovered = new List<DiscoveredFile>
            {
                new DiscoveredFile
                {
                    RepoId = "repo1",
                    FullPath = path,
                    RelativePath = Path.GetFileName(path),
                    IsBinary = false,
                    SizeBytes = new FileInfo(path).Length
                }
            };

            var models = new List<ModelMetadata>();

            var logger = new Mock<IAdminLogger>();
            var src = new RoslynDomainMetadataSource(logger.Object);

            var domains = await src.GetDomainsAsync(discovered, models, CancellationToken.None);

            Assert.That(domains, Has.Count.EqualTo(1));
            var domain = domains[0];

            Assert.That(domain.HasErrors, Is.True);
            Assert.That(string.Join("; ", domain.Errors), Does.Contain("Multiple [DomainDescription] members"));
        }
    }
}
