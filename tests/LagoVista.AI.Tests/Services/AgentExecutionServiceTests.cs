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
            _catalogService = new Mock<IAgentModeCatalogService>(MockBehavior.Strict);

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
                () => new AgentExecutionService(
                    null,
                    _reasoner.Object,
                    _ragContextBuilder.Object,
                    _catalogService.Object,
                    _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullReasoner_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentExecutionService(
                    _agentContextManager.Object,
                    null,
                    _ragContextBuilder.Object,
                    _catalogService.Object,
                    _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullRagContextBuilder_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentExecutionService(
                    _agentContextManager.Object,
                    _reasoner.Object,
                    null,
                    _catalogService.Object,
                    _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullModeCatalogService_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentExecutionService(
                    _agentContextManager.Object,
                    _reasoner.Object,
                    _ragContextBuilder.Object,
                    null,
                    _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullAdminLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentExecutionService(
                    _agentContextManager.Object,
                    _reasoner.Object,
                    _ragContextBuilder.Object,
                    _catalogService.Object,
                    null));
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

            _agentContextManager.Verify(
                m => m.GetAgentContextWithSecretsAsync(
                    It.IsAny<string>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>()),
                Times.Never);

            _ragContextBuilder.Verify(
                b => b.BuildContextSectionAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<string>(),
                    It.IsAny<RagScopeFilter>()),
                Times.Never);

            _reasoner.Verify(
                r => r.ExecuteAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<ConversationContext>(),
                    It.IsAny<AgentExecuteRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            _catalogService.Verify(
                c => c.BuildSystemPrompt(It.IsAny<string>()),
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
                Mode = "general",
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
        public async Task ExecuteAsync_MissingMode_DefaultsToGeneral_AndLogs()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "   ", // intentionally blank
                Instruction = "do something",
                RagScopeFilter = new RagScopeFilter()
            };

            var agentContext = CreateAgentContextWithDefaultConversation();

            _agentContextManager
                .Setup(m => m.GetAgentContextWithSecretsAsync(request.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            _catalogService
                .Setup(c => c.BuildSystemPrompt("general"))
                .Returns("MODE-PROMPT");

            _ragContextBuilder
                .Setup(b => b.BuildContextSectionAsync(agentContext, request.Instruction, request.RagScopeFilter))
                .ReturnsAsync(InvokeResult<string>.Create("RAG-BLOCK"));

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
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(new AgentExecuteResponse { Text = "ok" }));

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);
            Assert.That(request.Mode, Is.EqualTo("general"));

            _adminLogger.Verify(
                l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__MissingMode]",
                    "Mode was null or whitespace; defaulting to 'general'."),
                Times.Once);

            _catalogService.Verify(
                c => c.BuildSystemPrompt("general"),
                Times.Once);

            Assert.That(agentContext.ConversationContexts[0].SystemPrompts,
                Does.Contain("MODE-PROMPT"));
        }

        [Test]
        public async Task ExecuteAsync_MissingInstruction_ReturnsError()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "general",
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
        public async Task ExecuteAsync_MissingConversationContext_ReturnsError_AndDoesNotInvokeReasoner()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "general",
                Instruction = "do something",
                RagScopeFilter = new RagScopeFilter(),
                ConversationContext = null,
                ConversationId = null
            };

            // AgentContext with NO DefaultConversationContext set.
            var agentContext = new AgentContext
            {
                Id = "ctx-1",
                Name = "Context 1",
                DefaultConversationContext = null,
                ConversationContexts = new List<ConversationContext>()
            };

            _agentContextManager
                .Setup(m => m.GetAgentContextWithSecretsAsync(request.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].Message, Is.EqualTo("Unable to resolve ConversationContext for the request."));

            _adminLogger.Verify(l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__MissingConversationContext]",
                    It.Is<string>(msg => msg.Contains("Unable to resolve ConversationContext for the request."))),
                Times.Once);

            _ragContextBuilder.Verify(
                b => b.BuildContextSectionAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<string>(),
                    It.IsAny<RagScopeFilter>()),
                Times.Never);

            _reasoner.Verify(
                r => r.ExecuteAsync(
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

        #region Mode / Prompt Behavior

        [Test]
        public async Task ExecuteAsync_AllowsArbitraryMode_UsesModePrompt_AndInvokesReasoner()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "DdR_Authoring ", // intentionally mixed case & trailing space
                Instruction = "design a DDR",
                Repo = "repo-1",
                Language = "csharp",
                RagScopeFilter = new RagScopeFilter()
            };

            var agentContext = CreateAgentContextWithDefaultConversation();
            var chosenConversationContext = agentContext.ConversationContexts[0];

            _agentContextManager
                .Setup(m => m.GetAgentContextWithSecretsAsync(request.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            // Mode key should be normalized to "ddr_authoring".
            _catalogService
                .Setup(c => c.BuildSystemPrompt("ddr_authoring"))
                .Returns("DDR-MODE-PROMPT");

            _ragContextBuilder
                .Setup(b => b.BuildContextSectionAsync(agentContext, request.Instruction, request.RagScopeFilter))
                .ReturnsAsync(InvokeResult<string>.Create("RAG-BLOCK"));

            AgentContext capturedAgentContext = null;
            ConversationContext capturedConversationContext = null;
            AgentExecuteRequest capturedRequest = null;
            string capturedRagBlock = null;
            string capturedSessionId = null;
            EntityHeader capturedOrg = null;
            EntityHeader capturedUser = null;

            var reasonerResponse = new AgentExecuteResponse
            {
                Text = "ok-ddr",
                Kind = "answer"
            };

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

            // Mode should be normalized on the request.
            Assert.That(request.Mode, Is.EqualTo("ddr_authoring"));

            // Reasoner wiring.
            Assert.That(capturedAgentContext, Is.SameAs(agentContext));
            Assert.That(capturedConversationContext, Is.SameAs(chosenConversationContext));
            Assert.That(capturedRequest, Is.SameAs(request));
            Assert.That(capturedRagBlock, Is.EqualTo("RAG-BLOCK"));
            Assert.That(capturedSessionId, Is.Not.Null.And.Not.Empty);
            Assert.That(capturedOrg, Is.SameAs(org));
            Assert.That(capturedUser, Is.SameAs(user));

            // Mode prompt should have been added to the conversation context.
            Assert.That(chosenConversationContext.SystemPrompts, Does.Contain("DDR-MODE-PROMPT"));

            _catalogService.Verify(
                c => c.BuildSystemPrompt("ddr_authoring"),
                Times.Once);
        }

        #endregion

        #region RAG & ConversationContext Behavior

        [Test]
        public async Task ExecuteAsync_UsesDefaultConversationContextWhenRequestDoesNotSpecifyOne()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "general",
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

            _catalogService
                .Setup(c => c.BuildSystemPrompt("general"))
                .Returns("MODE-PROMPT");

            _ragContextBuilder
                .Setup(b => b.BuildContextSectionAsync(agentContext, request.Instruction, request.RagScopeFilter))
                .ReturnsAsync(InvokeResult<string>.Create("RAG-BLOCK"));

            AgentContext capturedAgentContext = null;
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
                        capturedAgentContext = ctx;
                        capturedConversationContext = convCtx;
                    })
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(new AgentExecuteResponse { Text = "42" }));

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.True);
            Assert.That(capturedAgentContext, Is.SameAs(agentContext));
            Assert.That(capturedConversationContext, Is.SameAs(chosenConversationContext));
            Assert.That(chosenConversationContext.SystemPrompts, Does.Contain("MODE-PROMPT"));
        }

        [Test]
        public async Task ExecuteAsync_UsesRequestConversationContextWhenProvided()
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
                Mode = "general",
                Instruction = "use second context",
                RagScopeFilter = new RagScopeFilter()
            };

            _agentContextManager
                .Setup(m => m.GetAgentContextWithSecretsAsync(agentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            _catalogService
                .Setup(c => c.BuildSystemPrompt("general"))
                .Returns("MODE-PROMPT");

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

            // Mode prompt should still be applied to the chosen context.
            Assert.That(capturedConversationContext.SystemPrompts, Does.Contain("MODE-PROMPT"));
        }

        [Test]
        public async Task ExecuteAsync_RagFailure_PropagatesErrorAndDoesNotInvokeReasoner()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "general",
                Instruction = "question",
                RagScopeFilter = new RagScopeFilter()
            };

            var agentContext = CreateAgentContextWithDefaultConversation();

            _agentContextManager
                .Setup(m => m.GetAgentContextWithSecretsAsync(request.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            _catalogService
                .Setup(c => c.BuildSystemPrompt("general"))
                .Returns("MODE-PROMPT");

            _ragContextBuilder
                .Setup(b => b.BuildContextSectionAsync(agentContext, request.Instruction, request.RagScopeFilter))
                .ReturnsAsync(InvokeResult<string>.FromError("RAG failed"));

            var result = await _sut.ExecuteAsync(request, org, user);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors.Count, Is.GreaterThan(0));

            _reasoner.Verify(
                r => r.ExecuteAsync(
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
