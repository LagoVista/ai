//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using LagoVista.AI.Interfaces;
//using LagoVista.AI.Models;
//using LagoVista.AI.Services;
//using LagoVista.Core.Validation;
//using LagoVista.IoT.Logging.Loggers;
//using LagoVista.IoT.Logging.Utils;
//using Moq;
//using NUnit.Framework;

//namespace LagoVista.AI.Tests.Services
//{
//    [TestFixture]
//    public class AgentKnowledgePackServiceTests
//    {
//        private Mock<IDdrConsumptionFieldProvider> _ddr;
//        private Mock<IServerToolUsageMetadataProvider> _usageProvider;
//        private Mock<IAgentToolBoxRepo> _tbRepo;

//        private AgentKnowledgePackService CreateSut()
//        {
//            return new AgentKnowledgePackService(_ddr.Object, _tbRepo.Object, _usageProvider.Object, new AdminLogger(new ConsoleLogWriter()));
//        }

//        [SetUp]
//        public void SetUp()
//        {
//            _ddr = new Mock<IDdrConsumptionFieldProvider>(MockBehavior.Strict);
//            _usageProvider = new Mock<IServerToolUsageMetadataProvider>(MockBehavior.Strict);
//            _tbRepo = new Mock<IAgentToolBoxRepo>(MockBehavior.Strict);
//        }

//        public IAgentPipelineContext GetPipelineContext()
//        {
//            return new AgentPipelineContext()
//            {
//                OrgId = "org",
//                AgentContext = BuildAgentContext(
//                    conversationContexts: new[] { BuildConversationContext("cc1") },
//                    modes: new[] { BuildMode("General") })
//            };
//        }

        

//        [Test]
//        public async Task CreateAsync_WhenMissingMode_ReturnsError()
//        {
//            var sut = CreateSut();

//            var result = await sut.CreateAsync("org", BuildAgentContext(), "cc1", null, CancellationToken.None);

//            Assert.That(result.Successful, Is.False);
//            _ddr.VerifyNoOtherCalls();
//            _usageProvider.VerifyNoOtherCalls();
//        }

//        [Test]
//        public async Task CreateAsync_WhenConversationContextNotFound_ReturnsError()
//        {
//            var agentContext = BuildAgentContext(
//                conversationContexts: new[] { BuildConversationContext("cc_other") },
//                modes: new[] { BuildMode("General") });

//            var sut = CreateSut();

//            var result = await sut.CreateAsync("org", agentContext, "cc_missing", "General", CancellationToken.None);

//            Assert.That(result.Successful, Is.False);
//            _ddr.VerifyNoOtherCalls();
//            _usageProvider.VerifyNoOtherCalls();
//        }

//        [Test]
//        public async Task CreateAsync_WhenModeNotFound_ReturnsError()
//        {
//            var agentContext = BuildAgentContext(
//                conversationContexts: new[] { BuildConversationContext("cc1") },
//                modes: new[] { BuildMode("Other") });

//            var sut = CreateSut();

//            var result = await sut.CreateAsync("org", agentContext, "cc1", "General", CancellationToken.None);

//            Assert.That(result.Successful, Is.False);
//            _ddr.VerifyNoOtherCalls();
//            _usageProvider.VerifyNoOtherCalls();
//        }

//        [Test]
//        public async Task CreateAsync_WhenDdrInstructionProviderFails_PropagatesError()
//        {
//            var agentContext = BuildAgentContext(
//                conversationContexts: new[] { BuildConversationContext("cc1") },
//                modes: new[] { BuildMode("General") },
//                agentInstructionDdrs: new[] { "SYS-009" });

//            _ddr
//                .Setup(m => m.GetAgentInstructionsAsync("org", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
//                .ReturnsAsync(InvokeResult<IDictionary<string, string>>.FromError("boom"));

//            var sut = CreateSut();

//            var result = await sut.CreateAsync("org", agentContext, "cc1", "General", CancellationToken.None);

//            Assert.That(result.Successful, Is.False);

//            _ddr.Verify(m => m.GetAgentInstructionsAsync(
//                    "org",
//                    It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "SYS-009" })),
//                    It.IsAny<CancellationToken>()),
//                Times.Once);

//            _ddr.VerifyNoOtherCalls();
//            _usageProvider.VerifyNoOtherCalls();
//        }

//        [Test]
//        public async Task CreateAsync_WhenDdrReferenceProviderFails_PropagatesError()
//        {
//            var agentContext = BuildAgentContext(
//                conversationContexts: new[] { BuildConversationContext("cc1") },
//                modes: new[] { BuildMode("General") },
//                referenceDdrs: new[] { "AGN-035" });

//            _ddr
//                .Setup(m => m.GetAgentInstructionsAsync("org", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
//                .ReturnsAsync(InvokeResult<IDictionary<string, string>>.Create(new Dictionary<string, string>()));

//            _ddr
//                .Setup(m => m.GetReferentialSummariesAsync("org", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
//                .ReturnsAsync(InvokeResult<IDictionary<string, string>>.FromError("nope"));

//            var sut = CreateSut();

//            var result = await sut.CreateAsync("org", agentContext, "cc1", "General", CancellationToken.None);

//            Assert.That(result.Successful, Is.False);

//            _ddr.Verify(m => m.GetAgentInstructionsAsync(
//                    "org",
//                    It.Is<IEnumerable<string>>(ids => ids.Count() == 0),
//                    It.IsAny<CancellationToken>()),
//                Times.Once);

//            _ddr.Verify(m => m.GetReferentialSummariesAsync(
//                    "org",
//                    It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "AGN-035" })),
//                    It.IsAny<CancellationToken>()),
//                Times.Once);

//            _ddr.VerifyNoOtherCalls();
//            _usageProvider.VerifyNoOtherCalls();
//        }

//        [Test]
//        public async Task CreateAsync_HappyPath_AssemblesAndDedupes_InPrecedenceOrder_AndAddsToolUsage()
//        {
//            // Agent: A,B + tools t1,t2
//            // Conversation: B,C + tools t2,t3
//            // ModeKey: C,D + tools t3,t4
//            var agentContext = BuildAgentContext(
//                welcomeAgent: "wa",
//                conversationContexts: new[]
//                {
//                    BuildConversationContext(
//                        "cc1",
//                        welcome: "wc",
//                        instructionDdrs: new[] { "B", "C" },
//                        referenceDdrs: new[] { "R2" },
//                        toolIds: new[] { "t2", "t3" })
//                },
//                modes: new[]
//                {
//                    BuildMode(
//                        "General",
//                        welcome: "wm",
//                        instructionDdrs: new[] { "C", "D" },
//                        referenceDdrs: new[] { "R3" },
//                        toolIds: new[] { "t3", "t4" })
//                },
//                agentInstructionDdrs: new[] { "A", "B" },
//                referenceDdrs: new[] { "R1" },
//                toolIds: new[] { "t1", "t2" });

//            _ddr
//                .Setup(m => m.GetAgentInstructionsAsync("org", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
//                .ReturnsAsync((string org, IEnumerable<string> ids, CancellationToken ct) =>
//                {
//                    var dict = ids.ToDictionary(id => id, id => "I:" + id, StringComparer.OrdinalIgnoreCase);
//                    return InvokeResult<IDictionary<string, string>>.Create(dict);
//                });

//            _ddr
//                .Setup(m => m.GetReferentialSummariesAsync("org", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
//                .ReturnsAsync((string org, IEnumerable<string> ids, CancellationToken ct) =>
//                {
//                    var dict = ids.ToDictionary(id => id, id => "R:" + id, StringComparer.OrdinalIgnoreCase);
//                    return InvokeResult<IDictionary<string, string>>.Create(dict);
//                });

//            // Tool usage metadata (strict mock: must be set up for all tools)
//            _usageProvider.Setup(m => m.GetToolUsageMetadata("t1")).Returns("U:t1");
//            _usageProvider.Setup(m => m.GetToolUsageMetadata("t2")).Returns("U:t2");
//            _usageProvider.Setup(m => m.GetToolUsageMetadata("t3")).Returns("U:t3");
//            _usageProvider.Setup(m => m.GetToolUsageMetadata("t4")).Returns("U:t4");

//            var sut = CreateSut();

//            var result = await sut.CreateAsync("org", agentContext, "cc1", "General", CancellationToken.None);

//            Assert.That(result.Successful, Is.True);
//            Assert.That(result.Result, Is.Not.Null);

//            var pack = result.Result;

//            Assert.That(pack.AgentWelcomeMessage, Is.EqualTo("wa"));
//            Assert.That(pack.ConversationWelcomeMessage, Is.EqualTo("wc"));
//            Assert.That(pack.ModeWelcomeMessage, Is.EqualTo("wm"));

////            Assert.That(pack.AvailableToolsEnabledToolNames, Is.EqualTo(new[] { "t1", "t2", "t3", "t4" }));

//            Assert.That(pack.KindCatalog.ContainsKey(KnowledgeKind.Instruction), Is.True);
//            Assert.That(pack.KindCatalog.ContainsKey(KnowledgeKind.Reference), Is.True);
//  //          Assert.That(pack.KindCatalog.ContainsKey(KnowledgeKind.Tool), Is.True);

//            var instructionLane = pack.KindCatalog[KnowledgeKind.Instruction].SessionKnowledge;
//            var referenceLane = pack.KindCatalog[KnowledgeKind.Reference].SessionKnowledge;
//    //        var toolLane = pack.KindCatalog[KnowledgeKind.Tool].SessionKnowledge;

//            var instructionItems = instructionLane.Items.Where(i => i.Kind == KnowledgeKind.Instruction).ToList();
//            var referenceItems = referenceLane.Items.Where(i => i.Kind == KnowledgeKind.Reference).ToList();
//      //      var toolItems = toolLane.Items.Where(i => i.Kind == KnowledgeKind.Tool).ToList();

//            Assert.That(instructionItems.Select(i => i.Id).ToArray(), Is.EqualTo(new[] { "A", "B", "C", "D" }));
//            Assert.That(instructionItems.Select(i => i.Content).All(c => c.StartsWith("I:", StringComparison.Ordinal)), Is.True);

//            Assert.That(referenceItems.Select(i => i.Id).ToArray(), Is.EqualTo(new[] { "R1", "R2", "R3" }));
//            Assert.That(referenceItems.Select(i => i.Content).All(c => c.StartsWith("R:", StringComparison.Ordinal)), Is.True);

//        //    Assert.That(toolItems.Select(i => i.Id).ToArray(), Is.EqualTo(new[] { "t1", "t2", "t3", "t4" }));
//            //Assert.That(toolItems.Select(i => i.Content).ToArray(), Is.EqualTo(new[] { "U:t1", "U:t2", "U:t3", "U:t4" }));
            
//            _ddr.Verify(m => m.GetAgentInstructionsAsync(
//                    "org",
//                    It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "A", "B", "C", "D" })),
//                    It.IsAny<CancellationToken>()),
//                Times.Once);

//            _ddr.Verify(m => m.GetReferentialSummariesAsync(
//                    "org",
//                    It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "R1", "R2", "R3" })),
//                    It.IsAny<CancellationToken>()),
//                Times.Once);

//            _usageProvider.Verify(m => m.GetToolUsageMetadata("t1"), Times.Once);
//            _usageProvider.Verify(m => m.GetToolUsageMetadata("t2"), Times.Once);
//            _usageProvider.Verify(m => m.GetToolUsageMetadata("t3"), Times.Once);
//            _usageProvider.Verify(m => m.GetToolUsageMetadata("t4"), Times.Once);

//            _ddr.VerifyNoOtherCalls();
//            _usageProvider.VerifyNoOtherCalls();
//        }

//        [Test]
//        public async Task CreateAsync_WhenConversationContextIdNull_UsesDefaultConversationContext()
//        {
//            var ccDefault = BuildConversationContext("cc_default", instructionDdrs: new[] { "X" });
//            var agentContext = BuildAgentContext(
//                conversationContexts: new[] { ccDefault, BuildConversationContext("cc_other") },
//                modes: new[] { BuildMode("General") },
//                defaultConversationId: "cc_default");

//            _ddr
//                .Setup(m => m.GetAgentInstructionsAsync("org", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
//                .ReturnsAsync((string org, IEnumerable<string> ids, CancellationToken ct) =>
//                {
//                    var dict = ids.ToDictionary(id => id, id => "I:" + id, StringComparer.OrdinalIgnoreCase);
//                    return InvokeResult<IDictionary<string, string>>.Create(dict);
//                });

//            _ddr
//                .Setup(m => m.GetReferentialSummariesAsync("org", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
//                .ReturnsAsync(InvokeResult<IDictionary<string, string>>.Create(new Dictionary<string, string>()));

//            var sut = CreateSut();

//            var result = await sut.CreateAsync("org", agentContext, null, "General", CancellationToken.None);

//            Assert.That(result.Successful, Is.True);

//            var instructionLane = result.Result.KindCatalog[KnowledgeKind.Instruction].SessionKnowledge;

//            Assert.That(instructionLane.Items.Any(i => i.Kind == KnowledgeKind.Instruction && i.Id == "X"), Is.True);

//            _ddr.Verify(m => m.GetAgentInstructionsAsync(
//                    "org",
//                    It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "X" })),
//                    It.IsAny<CancellationToken>()),
//                Times.Once);

//            _ddr.Verify(m => m.GetReferentialSummariesAsync(
//                    "org",
//                    It.Is<IEnumerable<string>>(ids => ids.Count() == 0),
//                    It.IsAny<CancellationToken>()),
//                Times.Once);

//            _ddr.VerifyNoOtherCalls();
//            _usageProvider.VerifyNoOtherCalls();
//        }

//        private static AgentContext BuildAgentContext(
//            IEnumerable<AgentContextRole> conversationContexts = null,
//            IEnumerable<AgentMode> modes = null,
//            string welcomeAgent = null,
//            IEnumerable<string> agentInstructionDdrs = null,
//            IEnumerable<string> referenceDdrs = null,
//            IEnumerable<string> toolIds = null,
//            string defaultConversationId = null)
//        {
//            return new AgentContext
//            {
//                WelcomeMessage = welcomeAgent,
//                AgentInstructionDdrs = (agentInstructionDdrs ?? Array.Empty<string>()).ToArray(),
//                ReferenceDdrs = (referenceDdrs ?? Array.Empty<string>()).ToArray(),
//                AssociatedToolIds = (toolIds ?? Array.Empty<string>()).ToArray(),
//                ConversationContexts = conversationContexts == null ? new List<AgentContextRole>() : conversationContexts.ToList(),
//                AgentModes = modes == null ? new List<AgentMode>() : modes.ToList(),
//                DefaultRole = defaultConversationId == null
//                    ? null
//                    : LagoVista.Core.Models.EntityHeader.Create(defaultConversationId, defaultConversationId)
//            };
//        }

//        private static AgentContextRole BuildConversationContext(
//            string id,
//            string welcome = null,
//            IEnumerable<string> instructionDdrs = null,
//            IEnumerable<string> referenceDdrs = null,
//            IEnumerable<string> toolIds = null)
//        {
//            return new AgentContextRole
//            {
//                Id = id,
//                Kind = id,
//                WelcomeMessage = welcome,
//                AgentInstructionDdrs = (instructionDdrs ?? Array.Empty<string>()).ToArray(),
//                ReferenceDdrs = (referenceDdrs ?? Array.Empty<string>()).ToArray(),
//                AssociatedToolIds = (toolIds ?? Array.Empty<string>()).ToArray(),
//                SystemPrompts = new List<string> { "sys" }
//            };
//        }

//        private static AgentMode BuildMode(
//            string key,
//            string welcome = null,
//            IEnumerable<string> instructionDdrs = null,
//            IEnumerable<string> referenceDdrs = null,
//            IEnumerable<string> toolIds = null)
//        {
//            return new AgentMode
//            {
//                Key = key,
//                WelcomeMessage = welcome,
//                AgentInstructionDdrs = (instructionDdrs ?? Array.Empty<string>()).ToArray(),
//                ReferenceDdrs = (referenceDdrs ?? Array.Empty<string>()).ToArray(),
//                AssociatedToolIds = (toolIds ?? Array.Empty<string>()).ToArray()
//            };
//        }
//    }
//}
