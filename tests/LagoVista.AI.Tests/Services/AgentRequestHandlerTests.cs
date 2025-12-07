using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentRequestHandlerTests
    {
        private Mock<IAgentOrchestrator> _orchestrator;
        private Mock<IAdminLogger> _adminLogger;
        private Mock<IServerToolSchemaProvider> _serverToolScheamaProvider;
        private Mock<IAgentModeCatalogService> _agentModeCatalogServiceMock;
        private AgentRequestHandler _sut;

        [SetUp]
        public void SetUp()
        {
            _orchestrator = new Mock<IAgentOrchestrator>();
            _adminLogger = new Mock<IAdminLogger>();
            _serverToolScheamaProvider = new Mock<IServerToolSchemaProvider>();
            _agentModeCatalogServiceMock = new Mock<IAgentModeCatalogService>();

            // Default: no server tools unless a test overrides this.
            _serverToolScheamaProvider
                .Setup(p => p.GetToolSchemas(It.IsAny<AgentExecuteRequest>()))
                .Returns(Array.Empty<object>());

            _serverToolScheamaProvider
                .Setup(p => p.GetToolSchemas(It.IsAny<IEnumerable<string>>()))
                .Returns(Array.Empty<object>());

            _serverToolScheamaProvider
                .Setup(p => p.GetToolSchema(It.IsAny<string>()))
                .Returns((object)null);

            // Default: no server tools from the mode catalog unless overridden.
            _agentModeCatalogServiceMock
                .Setup(m => m.GetToolsForMode(
                    It.IsAny<string>()))
                .Returns(new List<string>());

            _sut = new AgentRequestHandler(
                _orchestrator.Object,
                _adminLogger.Object,
                _serverToolScheamaProvider.Object,
                _agentModeCatalogServiceMock.Object);
        }

        #region Ctor Guards

        [Test]
        public void Ctor_NullOrchestrator_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentRequestHandler(
                    null,
                    _adminLogger.Object,
                    _serverToolScheamaProvider.Object,
                    _agentModeCatalogServiceMock.Object));
        }

        [Test]
        public void Ctor_NullAdminLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentRequestHandler(
                    _orchestrator.Object,
                    null,
                    _serverToolScheamaProvider.Object,
                    _agentModeCatalogServiceMock.Object));
        }

        [Test]
        public void Ctor_NullServerToolSchemaProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentRequestHandler(
                    _orchestrator.Object,
                    _adminLogger.Object,
                    null,
                    _agentModeCatalogServiceMock.Object));
        }

        [Test]
        public void Ctor_NullAgentModeCatalogService_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentRequestHandler(
                    _orchestrator.Object,
                    _adminLogger.Object,
                    _serverToolScheamaProvider.Object,
                    null));
        }

        #endregion

        #region HandleAsync Validation

        [Test]
        public async Task HandleAsync_NullRequest_ReturnsErrorAndDoesNotCallOrchestrator()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var result = await _sut.HandleAsync(null, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors.Count, Is.GreaterThan(0));
            Assert.That(result.Errors[0].Message, Is.EqualTo("AgentRequestEnvelope cannot be null."));

            _adminLogger.Verify(
                l => l.AddError("[AgentRequestHandler_HandleAsync__ValidateRequest]", "AgentRequestEnvelope cannot be null."),
                Times.Once);

            _orchestrator.Verify(
                o => o.BeginNewSessionAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()),
                Times.Never);

            _orchestrator.Verify(
                o => o.ExecuteTurnAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()),
                Times.Never);

            _serverToolScheamaProvider.Verify(p => p.GetToolSchemas(It.IsAny<AgentExecuteRequest>()), Times.Never);
            _serverToolScheamaProvider.Verify(p => p.GetToolSchemas(It.IsAny<IEnumerable<string>>()), Times.Never);
            _serverToolScheamaProvider.Verify(p => p.GetToolSchema(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task HandleAsync_MissingInstruction_ReturnsErrorAndDoesNotCallOrchestrator()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                Instruction = "  ",
                ConversationId = null
            };

            var result = await _sut.HandleAsync(request, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].Message, Is.EqualTo("Instruction is required."));

            _adminLogger.Verify(
                l => l.AddError("[AgentRequestHandler_HandleAsync__ValidateRequest]", "Instruction is required."),
                Times.Once);

            _orchestrator.Verify(
                o => o.BeginNewSessionAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()),
                Times.Never);

            _orchestrator.Verify(
                o => o.ExecuteTurnAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()),
                Times.Never);

            _serverToolScheamaProvider.Verify(p => p.GetToolSchemas(It.IsAny<AgentExecuteRequest>()), Times.Never);
            _serverToolScheamaProvider.Verify(p => p.GetToolSchemas(It.IsAny<IEnumerable<string>>()), Times.Never);
            _serverToolScheamaProvider.Verify(p => p.GetToolSchema(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region New Session Handling

        [Test]
        public async Task HandleAsync_NewSession_MissingAgentContext_ReturnsErrorAndDoesNotCallOrchestrator()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                ConversationId = null, // new session
                Instruction = "do something",
                AgentContext = null
            };

            var result = await _sut.HandleAsync(request, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].Message, Is.EqualTo("AgentContext is required for a new session."));

            _adminLogger.Verify(
                l => l.AddError(
                    "[AgentRequestHandler_HandleNewSessionAsync__ValidateRequest]",
                    "AgentContext is required for a new session."),
                Times.Once);

            _orchestrator.Verify(
                o => o.BeginNewSessionAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()),
                Times.Never);

            _serverToolScheamaProvider.Verify(p => p.GetToolSchemas(It.IsAny<AgentExecuteRequest>()), Times.Never);
            _serverToolScheamaProvider.Verify(p => p.GetToolSchemas(It.IsAny<IEnumerable<string>>()), Times.Never);
            _serverToolScheamaProvider.Verify(p => p.GetToolSchema(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task HandleAsync_NewSession_DelegatesToBeginNewSession_MergesServerTools_AndPassesThroughResponse()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                ConversationId = null, // new session
                Instruction = "do something",
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "ask",
                WorkspaceId = "ws-1",
                Repo = "repo-1",
                Language = "csharp",
                ActiveFiles = new List<ActiveFile>(),
                RagScopeFilter = new RagScopeFilter(),
                ToolsJson = null // client can be null; server will merge its tools
            };

            // Mode catalog: one server tool allowed: "testing.ping_pong"
            _agentModeCatalogServiceMock
                .Setup(m => m.GetToolsForMode(
                    "ask"))
                .Returns(new List<string> { "testing.ping_pong" });

            // Fake server tool schema (e.g., PingPongTool)
            var pingPongSchema = new
            {
                type = "function",
                name = "testing.ping_pong"
            };

            // Fake mode-change tool schema
            var changeModeSchema = new
            {
                type = "function",
                name = "agent_change_mode"
            };

            _serverToolScheamaProvider
                .Setup(p => p.GetToolSchemas(It.IsAny<IEnumerable<string>>()))
                .Returns(new object[] { pingPongSchema });

            _serverToolScheamaProvider
                .Setup(p => p.GetToolSchema("agent_change_mode"))
                .Returns(changeModeSchema);

            var orchestratorResponse = new AgentExecuteResponse
            {
                Text = "final answer",
                FullResponseUrl = "https://responses/1.json",
                ResponseContinuationId = "resp-1",
                Warnings = new List<string> { "warn-1" }
            };

            AgentExecuteRequest capturedRequest = null;

            _orchestrator
                .Setup(o => o.BeginNewSessionAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()))
                .Callback<AgentExecuteRequest, EntityHeader, EntityHeader, CancellationToken>((req, _, __, ___) => capturedRequest = req)
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(orchestratorResponse));

            var result = await _sut.HandleAsync(request, org, user);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.SameAs(orchestratorResponse));

            _orchestrator.Verify(
                o => o.BeginNewSessionAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()),
                Times.Once);

            _orchestrator.Verify(
                o => o.ExecuteTurnAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()),
                Times.Never);

            _serverToolScheamaProvider.Verify(p => p.GetToolSchemas(It.IsAny<IEnumerable<string>>()), Times.Once);
            _serverToolScheamaProvider.Verify(p => p.GetToolSchema("agent_change_mode"), Times.Once);

            Assert.That(capturedRequest, Is.Not.Null, "BeginNewSessionAsync should receive a non-null request.");
            Assert.That(capturedRequest.ToolsJson, Is.Not.Null.And.Not.Empty, "Server tools should be merged into ToolsJson.");

            var toolsArray = JArray.Parse(capturedRequest.ToolsJson);
            Assert.That(toolsArray.Count, Is.EqualTo(2), "Expected ping_pong + agent_change_mode to be merged.");

            var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in toolsArray)
            {
                toolNames.Add(t["name"]?.ToString());
            }

            Assert.That(toolNames.Contains("testing.ping_pong"), Is.True);
            Assert.That(toolNames.Contains("agent_change_mode"), Is.True);
        }

        [Test]
        public async Task HandleAsync_NewSession_MissingMode_DefaultsToGeneral_AndUsesModeCatalog()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                ConversationId = null,
                Instruction = "do something",
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = null, // should be defaulted to "general"
                WorkspaceId = "ws-1",
                Repo = "repo-1",
                Language = "csharp"
            };

            _agentModeCatalogServiceMock
                .Setup(m => m.GetToolsForMode(
                    "general"))
                .Returns(new List<string>());

            var orchestratorResponse = new AgentExecuteResponse
            {
                Text = "final answer"
            };

            AgentExecuteRequest capturedRequest = null;

            _orchestrator
                .Setup(o => o.BeginNewSessionAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()))
                .Callback<AgentExecuteRequest, EntityHeader, EntityHeader, CancellationToken>((req, _, __, ___) => capturedRequest = req)
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(orchestratorResponse));

            var result = await _sut.HandleAsync(request, org, user);

            Assert.That(result.Successful, Is.True);
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest.Mode, Is.EqualTo("general"));

            _agentModeCatalogServiceMock.Verify(
                m => m.GetToolsForMode("general"),
                Times.Once);
        }

        [Test]
        public async Task HandleAsync_NewSession_AddsAgentChangeMode_WhenNotReturnedByCatalog()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                ConversationId = null,
                Instruction = "do something",
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "ask",
                WorkspaceId = "ws-1",
                Repo = "repo-1",
                Language = "csharp"
            };

            // Catalog returns no tools.
            _agentModeCatalogServiceMock
                .Setup(m => m.GetToolsForMode("ask"))
                .Returns(new List<string>());

            // Only agent_change_mode is available via schema provider.
            var changeModeSchema = new
            {
                type = "function",
                name = "agent_change_mode"
            };

            _serverToolScheamaProvider
                .Setup(p => p.GetToolSchema("agent_change_mode"))
                .Returns(changeModeSchema);

            var orchestratorResponse = new AgentExecuteResponse
            {
                Text = "final answer"
            };

            AgentExecuteRequest capturedRequest = null;

            _orchestrator
                .Setup(o => o.BeginNewSessionAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()))
                .Callback<AgentExecuteRequest, EntityHeader, EntityHeader, CancellationToken>((req, _, __, ___) => capturedRequest = req)
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(orchestratorResponse));

            var result = await _sut.HandleAsync(request, org, user);

            Assert.That(result.Successful, Is.True);
            Assert.That(capturedRequest.ToolsJson, Is.Not.Null.And.Not.Empty);

            var toolsArray = JArray.Parse(capturedRequest.ToolsJson);
            Assert.That(toolsArray.Count, Is.EqualTo(1), "Expected only agent_change_mode tool.");

            var toolName = toolsArray[0]["name"]?.ToString();
            Assert.That(toolName, Is.EqualTo("agent_change_mode"));
        }

        #endregion

        #region Follow-up Turn Handling

        [Test]
        public async Task HandleAsync_FollowupTurn_DelegatesToExecuteTurn_AndPassesThroughResponse_WithMergingServerTools()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                ConversationId = "conv-1", // follow-up
                Instruction = "follow up",
                PreviousTurnId = "turn-1",
                Mode = "ask",
                WorkspaceId = "ws-1",
                Repo = "repo-1",
                Language = "csharp",
                ActiveFiles = new List<ActiveFile>(),
                RagScopeFilter = new RagScopeFilter(),
                ToolsJson = "[{\"name\":\"client.tool\",\"type\":\"function\"}]"
            };

            var orchestratorResponse = new AgentExecuteResponse
            {
                Text = "follow up answer",
                FullResponseUrl = "https://responses/2.json",
                ResponseContinuationId = "resp-2",
                Warnings = new List<string> { "warn-2" }
            };

            AgentExecuteRequest capturedRequest = null;

            _orchestrator
                .Setup(o => o.ExecuteTurnAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()))
                .Callback<AgentExecuteRequest, EntityHeader, EntityHeader, CancellationToken>((req, _, __, ___) => capturedRequest = req)
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(orchestratorResponse));

            var result = await _sut.HandleAsync(request, org, user);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.SameAs(orchestratorResponse));

            _orchestrator.Verify(
                o => o.ExecuteTurnAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()),
                Times.Once);

            _orchestrator.Verify(
                o => o.BeginNewSessionAsync(It.IsAny<AgentExecuteRequest>(), org, user, It.IsAny<CancellationToken>()),
                Times.Never);

            _serverToolScheamaProvider.Verify(p => p.GetToolSchemas(It.IsAny<AgentExecuteRequest>()), Times.Never);
            _serverToolScheamaProvider.Verify(p => p.GetToolSchemas(It.IsAny<IEnumerable<string>>()), Times.Once);
            _serverToolScheamaProvider.Verify(p => p.GetToolSchema(It.IsAny<string>()), Times.Once);

            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest.ToolsJson, Is.EqualTo(request.ToolsJson));
        }

        #endregion

        #region Helpers

        private static EntityHeader CreateOrg()
        {
            return new EntityHeader
            {
                Id = "org-1",
                Text = "Org 1"
            };
        }

        private static EntityHeader CreateUser()
        {
            return new EntityHeader
            {
                Id = "user-1",
                Text = "User 1"
            };
        }

        #endregion
    }
}
