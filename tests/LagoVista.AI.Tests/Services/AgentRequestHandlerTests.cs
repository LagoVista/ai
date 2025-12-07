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
        private AgentRequestHandler _sut;

        [SetUp]
        public void SetUp()
        {
            _orchestrator = new Mock<IAgentOrchestrator>();
            _adminLogger = new Mock<IAdminLogger>();
            

            _sut = new AgentRequestHandler(_orchestrator.Object, _adminLogger.Object);
        }

        #region Ctor Guards

        [Test]
        public void Ctor_NullOrchestrator_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentRequestHandler(null, _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullAdminLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentRequestHandler(_orchestrator.Object, null));
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
        }


        #endregion

        #region Follow-up Turn Handling

        [Test]
        public async Task HandleAsync_FollowupTurn_DelegatesToExecuteTurn_AndPassesThroughResponse_WithoutMergingServerTools()
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
