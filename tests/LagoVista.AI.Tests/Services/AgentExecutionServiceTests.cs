using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentExecutionServiceTests
    {
        private Mock<IAgentContextManager> _agentContextManager;
        private Mock<IAdminLogger> _adminLogger;
        private Mock<IAgentReasoner> _reasoner;
        private Mock<IRagContextBuilder> _ragContextBuilder;
        private Mock<IAgentModeCatalogService> _catalogService;

        private AgentExecutionService _sut;

        [SetUp]
        public void SetUp()
        {
            _agentContextManager = new Mock<IAgentContextManager>(MockBehavior.Strict);
            _adminLogger = new Mock<IAdminLogger>(MockBehavior.Loose);
            _reasoner = new Mock<IAgentReasoner>(MockBehavior.Strict);
            _ragContextBuilder = new Mock<IRagContextBuilder>(MockBehavior.Strict);
            _catalogService = new Mock<IAgentModeCatalogService>();

            _sut = new AgentExecutionService(
                _agentContextManager.Object,
                _reasoner.Object,
                _ragContextBuilder.Object,
                _catalogService.Object,
                _adminLogger.Object);
        }

        #region Ctor Guards

        [Test]
        public void Ctor_NullAgentContextManager_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentExecutionService(null, _reasoner.Object, _ragContextBuilder.Object, _catalogService.Object, _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullReasoner_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentExecutionService(_agentContextManager.Object, null, _ragContextBuilder.Object, _catalogService.Object, _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullRagContextBuilder_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentExecutionService(_agentContextManager.Object, _reasoner.Object, null, _catalogService.Object, _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullAdminLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentExecutionService(_agentContextManager.Object, _reasoner.Object, _ragContextBuilder.Object, _catalogService.Object, null));
        }

        #endregion

        #region ExecuteAsync Validation

        [Test]
        public async Task ExecuteAsync_NullRequest_ReturnsErrorAndDoesNotCallDeps()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var result = await _sut.ExecuteAsync(null, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors.Count, Is.GreaterThan(0));
            Assert.That(result.Errors[0].Message, Is.EqualTo("AgentExecuteRequest cannot be null."));

            _adminLogger.Verify(l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__ValidateRequest]",
                    "AgentExecuteRequest cannot be null."),
                Times.Once);

            _agentContextManager.Verify(m => m.GetAgentContextWithSecretsAsync(It.IsAny<string>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()), Times.Never);
            _ragContextBuilder.Verify(b => b.BuildContextSectionAsync(It.IsAny<AgentContext>(), It.IsAny<string>(), It.IsAny<RagScopeFilter>()), Times.Never);
            _reasoner.Verify(r => r.ExecuteAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<ConversationContext>(),
                    It.IsAny<AgentExecuteRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_MissingAgentContext_ReturnsError()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = null,
                Mode = "ask",
                Instruction = "do something"
            };

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].Message, Is.EqualTo("AgentContext is required."));

            _adminLogger.Verify(l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__ValidateRequest]",
                    "AgentContext is required."),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_MissingMode_ReturnsError()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = " ",
                Instruction = "do something"
            };

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].Message, Is.EqualTo("Mode is required (e.g. 'ask' or 'edit')."));

            _adminLogger.Verify(l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__ValidateRequest]",
                    "Mode is required (e.g. 'ask' or 'edit')."),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_MissingInstruction_ReturnsError()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "ask",
                Instruction = "  "
            };

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].Message, Is.EqualTo("Instruction is required."));

            _adminLogger.Verify(l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__ValidateRequest]",
                    "Instruction is required."),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_UnsupportedMode_ReturnsErrorAndLogs()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "weirdmode",
                Instruction = "do something"
            };

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].Message, Is.EqualTo("Unsupported mode 'weirdmode'."));

            _adminLogger.Verify(l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__UnsupportedMode]",
                    It.Is<string>(msg => msg.StartsWith("Unsupported mode 'weirdmode'."))),
                Times.Once);
        }

        #endregion

        #region Edit Mode

        [Test]
        public async Task ExecuteAsync_EditMode_ReturnsErrorAgentExecuteResponse_WithRequiredFields()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "edit",
                Instruction = "modify this code"
            };

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.True, "InvokeResult should be successful even for not-implemented edit mode.");
            Assert.That(result.Result, Is.Not.Null);
            Assert.That(result.Result.Kind, Is.EqualTo("error"));
            Assert.That(result.Result.ErrorCode, Is.EqualTo("AGENT_EXEC_EDIT_NOT_IMPLEMENTED"));
            Assert.That(result.Result.ErrorMessage, Is.EqualTo("Edit mode is not implemented yet."));

            _adminLogger.Verify(l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__EditNotImplemented]",
                    It.Is<string>(msg => msg.Contains("Edit mode is not implemented yet."))),
                Times.Once);

            _agentContextManager.Verify(m => m.GetAgentContextWithSecretsAsync(It.IsAny<string>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()), Times.Never);
            _ragContextBuilder.Verify(b => b.BuildContextSectionAsync(It.IsAny<AgentContext>(), It.IsAny<string>(), It.IsAny<RagScopeFilter>()), Times.Never);
            _reasoner.Verify(r => r.ExecuteAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<ConversationContext>(),
                    It.IsAny<AgentExecuteRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion

        #region Ask Mode Success

        [Test]
        public async Task ExecuteAsync_AskMode_Success_PopulatesRequiredFieldsFromReasonerResponse()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "ask",
                Instruction = "what is the meaning of life?",
                ConversationId = null,
                ConversationContext = null,
                Repo = "repo-1",
                Language = "csharp",
                RagScopeFilter = new RagScopeFilter()
            };

            var agentContext = CreateAgentContextWithDefaultConversation();
            var chosenConversationContext = agentContext.ConversationContexts[0];

            _agentContextManager
                .Setup(m => m.GetAgentContextWithSecretsAsync(request.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            _ragContextBuilder
                .Setup(b => b.BuildContextSectionAsync(agentContext, request.Instruction, request.RagScopeFilter))
                .ReturnsAsync(InvokeResult<string>.Create("RAG-BLOCK"));

            var reasonerResponse = new AgentExecuteResponse
            {
                Text = "42",
                FullResponseUrl = "https://responses/meaning-of-life.json",
                ResponseContinuationId = "resp-meaning-of-life",
                Kind = "answer",
                Warnings = new List<string> { "some warning" }
            };

            AgentContext capturedAgentContext = null;
            ConversationContext capturedConversationContext = null;
            AgentExecuteRequest capturedRequest = null;
            string capturedRagBlock = null;
            string capturedSessionId = null;
            EntityHeader capturedOrg = null;
            EntityHeader capturedUser = null;

            _reasoner
                .Setup(r => r.ExecuteAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<ConversationContext>(),
                    It.IsAny<AgentExecuteRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<CancellationToken>()))
                .Callback<AgentContext, ConversationContext, AgentExecuteRequest, string, string, EntityHeader, EntityHeader, CancellationToken>(
                    (ctx, convCtx, req, rag, sessionId, o, u, ct) =>
                    {
                        capturedAgentContext = ctx;
                        capturedConversationContext = convCtx;
                        capturedRequest = req;
                        capturedRagBlock = rag;
                        capturedSessionId = sessionId;
                        capturedOrg = o;
                        capturedUser = u;
                    })
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(reasonerResponse));

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.SameAs(reasonerResponse));

            Assert.That(capturedAgentContext, Is.SameAs(agentContext));
            Assert.That(capturedConversationContext, Is.SameAs(chosenConversationContext));
            Assert.That(capturedRequest, Is.SameAs(request));
            Assert.That(capturedRagBlock, Is.EqualTo("RAG-BLOCK"));
            Assert.That(capturedSessionId, Is.Not.Null.And.Not.Empty);
            Assert.That(capturedOrg, Is.SameAs(org));
            Assert.That(capturedUser, Is.SameAs(user));

            Assert.That(result.Result.Text, Is.EqualTo("42"));
            Assert.That(result.Result.FullResponseUrl, Is.EqualTo("https://responses/meaning-of-life.json"));
            Assert.That(result.Result.ResponseContinuationId, Is.EqualTo("resp-meaning-of-life"));
            Assert.That(result.Result.Kind, Is.EqualTo("answer"));
            Assert.That(result.Result.Warnings, Is.Not.Null);
            Assert.That(result.Result.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task ExecuteAsync_AskMode_UsesRequestConversationContextWhenProvided()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var agentContext = CreateAgentContextWithTwoConversations();
            var requestConversationHeader = new EntityHeader
            {
                Id = "conv-2",
                Text = "Context 2"
            };

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = agentContext.Id, Text = agentContext.Name },
                ConversationContext = requestConversationHeader,
                Mode = "ask",
                Instruction = "use second context",
                RagScopeFilter = new RagScopeFilter()
            };

            _agentContextManager
                .Setup(m => m.GetAgentContextWithSecretsAsync(agentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            _ragContextBuilder
                .Setup(b => b.BuildContextSectionAsync(agentContext, request.Instruction, request.RagScopeFilter))
                .ReturnsAsync(InvokeResult<string>.Create("CTX-BLOCK"));

            ConversationContext capturedConversationContext = null;

            _reasoner
                .Setup(r => r.ExecuteAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<ConversationContext>(),
                    It.IsAny<AgentExecuteRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<CancellationToken>()))
                .Callback<AgentContext, ConversationContext, AgentExecuteRequest, string, string, EntityHeader, EntityHeader, CancellationToken>(
                    (ctx, convCtx, req, rag, sessionId, o, u, ct) =>
                    {
                        capturedConversationContext = convCtx;
                    })
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(new AgentExecuteResponse { Text = "ok" }));

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.True);
            Assert.That(capturedConversationContext, Is.Not.Null);
            Assert.That(capturedConversationContext.Id, Is.EqualTo("conv-2"));
        }

        #endregion

        #region Ask Mode RAG Failure

        [Test]
        public async Task ExecuteAsync_AskMode_RagFailure_PropagatesErrorAndDoesNotInvokeReasoner()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "ask",
                Instruction = "question",
                RagScopeFilter = new RagScopeFilter()
            };

            var agentContext = CreateAgentContextWithDefaultConversation();

            _agentContextManager
                .Setup(m => m.GetAgentContextWithSecretsAsync(request.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            _ragContextBuilder
                .Setup(b => b.BuildContextSectionAsync(agentContext, request.Instruction, request.RagScopeFilter))
                .ReturnsAsync(InvokeResult<string>.FromError("RAG failed"));

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors.Count, Is.GreaterThan(0));

            _reasoner.Verify(r => r.ExecuteAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<ConversationContext>(),
                    It.IsAny<AgentExecuteRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
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

        private static AgentContext CreateAgentContextWithDefaultConversation()
        {
            var conv = new ConversationContext
            {
                Id = "conv-1",
                Name = "Default Conversation"
            };

            return new AgentContext
            {
                Id = "ctx-1",
                Name = "Context 1",
                DefaultConversationContext = new EntityHeader
                {
                    Id = conv.Id,
                    Text = conv.Name
                },
                ConversationContexts = new List<ConversationContext> { conv }
            };
        }

        private static AgentContext CreateAgentContextWithTwoConversations()
        {
            var conv1 = new ConversationContext
            {
                Id = "conv-1",
                Name = "Context 1"
            };

            var conv2 = new ConversationContext
            {
                Id = "conv-2",
                Name = "Context 2"
            };

            return new AgentContext
            {
                Id = "ctx-1",
                Name = "Context 1",
                DefaultConversationContext = new EntityHeader
                {
                    Id = conv1.Id,
                    Text = conv1.Name
                },
                ConversationContexts = new List<ConversationContext> { conv1, conv2 }
            };
        }

        #endregion
    }
}
