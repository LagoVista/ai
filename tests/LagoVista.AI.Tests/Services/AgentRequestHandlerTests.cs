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
using LagoVista.UserAdmin.Interfaces.Managers;
using LagoVista.UserAdmin.Models.Orgs;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentRequestHandlerTests
    {
        private Mock<IAgentOrchestrator> _next;
        private Mock<IAdminLogger> _adminLogger;
        private Mock<IAgentStreamingContext> _streamingContext;
        private Mock<IOrganizationManager> _orgManager;
        private AgentRequestHandler _sut;

        [SetUp]
        public void SetUp()
        {
            _next = new Mock<IAgentOrchestrator>(MockBehavior.Strict);
            _adminLogger = new Mock<IAdminLogger>(MockBehavior.Loose);
            _streamingContext = new Mock<IAgentStreamingContext>(MockBehavior.Strict);
            _orgManager = new Mock<IOrganizationManager>(MockBehavior.Strict);

            _sut = new AgentRequestHandler(_next.Object, _adminLogger.Object, _orgManager.Object, _streamingContext.Object);

            _orgManager
                .Setup(om => om.GetOrganizationAsync(It.IsAny<string>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()))
                .ReturnsAsync((string id, EntityHeader org, EntityHeader user) => new Organization { DefaultAgentContext = new EntityHeader() { } });
        }

        #region Ctor Guards

        [Test]
        public void Ctor_NullNext_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentRequestHandler(null, _adminLogger.Object, _orgManager.Object, _streamingContext.Object));
        }

        [Test]
        public void Ctor_NullAdminLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentRequestHandler(_next.Object, null, _orgManager.Object, _streamingContext.Object));
        }

        #endregion

        #region HandleAsync Validation

        [Test]
        public async Task HandleAsync_NullRequest_ReturnsErrorAndDoesNotCallNext()
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

            _next.Verify(
                n => n.ExecuteAsync(It.IsAny<AgentPipelineContext>()),
                Times.Never);
        }

        [Test]
        public async Task HandleAsync_MissingInstruction_ReturnsErrorAndDoesNotCallNext()
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

            _next.Verify(
                n => n.ExecuteAsync(It.IsAny<AgentPipelineContext>()),
                Times.Never);
        }

        #endregion

        #region New Session Handling

        [Test]
        public async Task HandleAsync_NewSession_MissingAgentContext_ReturnsErrorAndDoesNotCallNext()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                ConversationId = null,
                Instruction = "do something",
                AgentContext = null
            };

            var result = await _sut.HandleAsync(request, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].Message, Is.EqualTo("AgentContext is required, this can either come from the request or be set as a default in the Owner settings."));

            _next.Verify(
                n => n.ExecuteAsync(It.IsAny<AgentPipelineContext>()),
                Times.Never);
        }

        #endregion

        #region Follow-up Turn Handling

        [Test]
        public async Task HandleAsync_FollowupTurn_DelegatesToNext_AndPassesThroughResponse()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                ConversationId = "conv-1",
                Instruction = "follow up",
                PreviousTurnId = "turn-1",
                Mode = "ask",
                WorkspaceId = "ws-1",
                Repo = "repo-1",
                Language = "csharp",
                ActiveFiles = new List<ActiveFile>(),
                RagScopeFilter = new RagScopeFilter(),
                ToolsJson = "[{\"name\":\"client.tool\",\"type\":\"function\"}]",
                AgentContext = new EntityHeader { Id = "agent-1" }
            };

            var downstreamResponse = new AgentExecuteResponse
            {
                Text = "follow up answer",
                FullResponseUrl = "https://responses/2.json",
                ResponseContinuationId = "resp-2",
                Warnings = new List<string> { "warn-2" }
            };

            AgentPipelineContext capturedCtx = null;

            _streamingContext
                .Setup(sc => sc.AddWorkflowAsync("Welcome Back to Aptix, let's get started...", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _next
                .Setup(n => n.ExecuteAsync(It.IsAny<AgentPipelineContext>()))
                .Callback<AgentPipelineContext>((ctx) =>
                {
                    capturedCtx = ctx;
                    ctx.Response = downstreamResponse;
                })
                .ReturnsAsync((AgentPipelineContext ctx) => InvokeResult<AgentPipelineContext>.Create(ctx));

            var result = await _sut.HandleAsync(request, org, user);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(result.Result, Is.SameAs(downstreamResponse));

            _next.Verify(
                n => n.ExecuteAsync(It.IsAny<AgentPipelineContext>()),
                Times.Once);

            Assert.That(capturedCtx, Is.Not.Null);
            Assert.That(capturedCtx.Request, Is.SameAs(request));
            Assert.That(capturedCtx.Request.ToolsJson, Is.EqualTo(request.ToolsJson));
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
