using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    [TestFixture]
    public class RoslynModelMetadataSourceTests
    {
        private static string CreateTempCsFile(string content)
        {
            var fileName = $"ModelMetaSrc-{Guid.NewGuid():N}.cs";
            var path = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(path, content);
            return path;
        }

        [Test]
        public async Task GetModelsAsync_FindsEntityDescription_And_FormField()
        {
            // Note: AIDomain.AIAdmin is a const string; EntityDescription expects the constant
            // value, Roslyn just needs the syntax, not actual compilation.
            var code =
                "using LagoVista.Core.Attributes;\r\n" +
                "using LagoVista.Core.Models.UIMetaData;\r\n" +
                "\r\n" +
                "namespace LagoVista.AI.Models\r\n" +
                "{\r\n" +
                "    public class AIDomain\r\n" +
                "    {\r\n" +
                "        public const string AIAdmin = \"AI Admin\";\r\n" +
                "    }\r\n" +
                "\r\n" +
                "    public static class AIResources\r\n" +
                "    {\r\n" +
                "        public static class Names\r\n" +
                "        {\r\n" +
                "            public const string AiAgentContext_Title = \"AiAgentContext_Title\";\r\n" +
                "            public const string AiAgentContext_Description = \"AiAgentContext_Description\";\r\n" +
                "            public const string Common_Icon = \"Common_Icon\";\r\n" +
                "        }\r\n" +
                "    }\r\n" +
                "\r\n" +
                "    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AiAgentContext_Title, AIResources.Names.AiAgentContext_Description, AIResources.Names.AiAgentContext_Description, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources))]\r\n" +
                "    public class AgentContext\r\n" +
                "    {\r\n" +
                "        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon)]\r\n" +
                "        public string Icon { get; set; }\r\n" +
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

            var resx = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                {
                    "AIResources",
                    new Dictionary<string, string>
                    {
                        { "AiAgentContext_Title", "Agent Context" },
                        { "AiAgentContext_Description", "Context for AI agents." },
                        { "Common_Icon", "Icon" }
                    }
                }
            };

            var logger = new Mock<IAdminLogger>();
            var src = new RoslynModelMetadataSource(logger.Object);

            var models = await src.GetModelsAsync(discovered, resx, CancellationToken.None);

            Assert.That(models, Has.Count.EqualTo(1));
            var model = models[0];

            Assert.That(model.ClassName, Is.EqualTo("AgentContext"));
            Assert.That(model.DomainKey, Is.EqualTo("AIAdmin"));
            Assert.That(model.Title, Is.EqualTo("Agent Context"));
            Assert.That(model.Description, Is.EqualTo("Context for AI agents."));
            Assert.That(model.Help, Is.EqualTo("Context for AI agents."));
            Assert.That(model.Fields, Has.Count.EqualTo(1));

            var field = model.Fields[0];
            Assert.That(field.PropertyName, Is.EqualTo("Icon"));
            Assert.That(field.Label, Is.EqualTo("Icon"));
            Assert.That(model.HasErrors, Is.False, string.Join("; ", model.Errors));
        }

        [Test]
        public async Task GetModelsAsync_MissingLabelResource_FlagsError()
        {
            var code =
                "using LagoVista.Core.Attributes;\r\n" +
                "using LagoVista.Core.Models.UIMetaData;\r\n" +
                "\r\n" +
                "namespace LagoVista.AI.Models\r\n" +
                "{\r\n" +
                "    public class AIDomain\r\n" +
                "    {\r\n" +
                "        public const string AIAdmin = \"AI Admin\";\r\n" +
                "    }\r\n" +
                "\r\n" +
                "    public static class AIResources\r\n" +
                "    {\r\n" +
                "        public static class Names\r\n" +
                "        {\r\n" +
                "            public const string AiAgentContext_Title = \"AiAgentContext_Title\";\r\n" +
                "            public const string AiAgentContext_Description = \"AiAgentContext_Description\";\r\n" +
                "        }\r\n" +
                "    }\r\n" +
                "\r\n" +
                "    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AiAgentContext_Title, AIResources.Names.AiAgentContext_Description, AIResources.Names.AiAgentContext_Description, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources))]\r\n" +
                "    public class AgentContext\r\n" +
                "    {\r\n" +
                "        [FormField(FieldType: FieldTypes.Icon)]\r\n" +
                "        public string Icon { get; set; }\r\n" +
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

            var resx = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                {
                    "AIResources",
                    new Dictionary<string, string>
                    {
                        { "AiAgentContext_Title", "Agent Context" },
                        { "AiAgentContext_Description", "Context for AI agents." }
                    }
                }
            };

            var logger = new Mock<IAdminLogger>();
            var src = new RoslynModelMetadataSource(logger.Object);

            var models = await src.GetModelsAsync(discovered, resx, CancellationToken.None);

            Assert.That(models, Has.Count.EqualTo(1));
            var model = models[0];

            Assert.That(model.HasErrors, Is.True);
            Assert.That(string.Join("; ", model.Errors), Does.Contain("missing a LabelResource"));
        }
    }
}
