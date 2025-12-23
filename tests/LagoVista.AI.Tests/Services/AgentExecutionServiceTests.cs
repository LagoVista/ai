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
using log4net.Filter;
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

        private AgentExecutionService _sut;

        [SetUp]
        public void SetUp()
        {
            _agentContextManager = new Mock<IAgentContextManager>(MockBehavior.Strict);
            _adminLogger = new Mock<IAdminLogger>(MockBehavior.Loose);
            _reasoner = new Mock<IAgentReasoner>(MockBehavior.Strict);
            _ragContextBuilder = new Mock<IRagContextBuilder>(MockBehavior.Strict);

            _sut = new AgentExecutionService(
                _agentContextManager.Object,
                _reasoner.Object,
                _ragContextBuilder.Object,
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
                    null));
        }

        #endregion

        #region Pipeline ExecuteAsync Validation

        [Test]
        public async Task ExecuteAsync_NullPipelineContext_ReturnsErrorAndDoesNotCallDeps()
        {
            var result = await _sut.ExecuteAsync((AgentPipelineContext)null);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Errors[0].Message, Is.EqualTo("AgentPipelineContext cannot be null."));

            _agentContextManager.Verify(
                m => m.GetAgentContextWithSecretsAsync(It.IsAny<string>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()),
                Times.Never);

            _reasoner.Verify(
                r => r.ExecuteAsync(It.IsAny<AgentPipelineContext>()),
                Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_NullRequest_ReturnsErrorAndDoesNotCallDeps()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var ctx = new AgentPipelineContext
            {
                CorrelationId = "corr-1",
                Org = org,
                User = user,
                Request = null
            };

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Errors[0].Message, Is.EqualTo("AgentExecuteRequest cannot be null."));

            _agentContextManager.Verify(
                m => m.GetAgentContextWithSecretsAsync(It.IsAny<string>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()),
                Times.Never);

            _reasoner.Verify(
                r => r.ExecuteAsync(It.IsAny<AgentPipelineContext>()),
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

            var ctx = new AgentPipelineContext
            {
                CorrelationId = "corr-1",
                Org = org,
                User = user,
                Request = request
            };

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].Message, Is.EqualTo("AgentContext is required."));

            _adminLogger.Verify(l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__ValidateRequest]",
                    "AgentContext is required."),
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
                Mode = "general",
                Instruction = "  "
            };

            var ctx = new AgentPipelineContext
            {
                CorrelationId = "corr-1",
                Org = org,
                User = user,
                Request = request
            };

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].Message, Is.EqualTo("Instruction is required."));

            _adminLogger.Verify(l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__ValidateRequest]",
                    "Instruction is required."),
                Times.Once);
        }

        #endregion

        #region Mode / ConversationContext Behavior

        [Test]
        public async Task ExecuteAsync_MissingMode_DefaultsToGeneral_AndLogs()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "   ",
                Instruction = "do something",
                RagScopeFilter = new RagScopeFilter()
            };

            var ctx = new AgentPipelineContext
            {
                CorrelationId = "corr-2",
                Org = org,
                User = user,
                Request = request
            };

            var agentContext = CreateAgentContextWithDefaultConversation();

            _agentContextManager
                .Setup(m => m.GetAgentContextWithSecretsAsync(request.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            _reasoner
                .Setup(r => r.ExecuteAsync(It.IsAny<AgentPipelineContext>()))
                .Callback<AgentPipelineContext>((c) =>
                {
                    c.Response = new AgentExecuteResponse { Text = "ok" };
                })
                .ReturnsAsync((AgentPipelineContext c) => InvokeResult<AgentPipelineContext>.Create(c));

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(ctx.Response, Is.Not.Null);
            Assert.That(request.Mode, Is.EqualTo("general"));

            _adminLogger.Verify(
                l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__MissingMode]",
                    "Mode was null or whitespace; defaulting to 'general'."),
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

            var ctx = new AgentPipelineContext
            {
                CorrelationId = "corr-3",
                Org = org,
                User = user,
                Request = request
            };

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

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors[0].Message, Is.EqualTo("Unable to resolve ConversationContext for the request."));

            _adminLogger.Verify(l => l.AddError(
                    "[AgentExecutionService_ExecuteAsync__MissingConversationContext]",
                    It.Is<string>(msg => msg.Contains("Unable to resolve ConversationContext for the request."))),
                Times.Once);

            _reasoner.Verify(
                r => r.ExecuteAsync(It.IsAny<AgentPipelineContext>()),
                Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_AllowsArbitraryMode_NormalizesMode_AndInvokesReasoner()
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                Mode = "DdR_Authoring ",
                Instruction = "design a DDR",
                Repo = "repo-1",
                Language = "csharp",
                RagScopeFilter = new RagScopeFilter()
            };

            var ctx = new AgentPipelineContext()
            {
                CorrelationId = "corr-4",
                Org = org,
                User = user,
                Request = request
            };

            var agentContext = CreateAgentContextWithDefaultConversation();
            var chosenConversationContext = agentContext.ConversationContexts[0];

            _agentContextManager
                .Setup(m => m.GetAgentContextWithSecretsAsync(request.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            AgentPipelineContext capturedCtx = null;

            var reasonerResponse = new AgentExecuteResponse
            {
                Text = "ok-ddr",
                Kind = "answer"
            };

            _reasoner
                .Setup(r => r.ExecuteAsync(It.IsAny<AgentPipelineContext>()))
                .Callback<AgentPipelineContext>((c ) =>
                {
                    capturedCtx = c;
                    c.Response = reasonerResponse;
                })
                .ReturnsAsync((AgentPipelineContext c) => InvokeResult<AgentPipelineContext>.Create(c));

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(ctx.Response, Is.SameAs(reasonerResponse));

            Assert.That(request.Mode, Is.EqualTo("ddr_authoring"));

            Assert.That(capturedCtx, Is.Not.Null);
            Assert.That(capturedCtx.AgentContext, Is.SameAs(agentContext));
            Assert.That(capturedCtx.ConversationContext, Is.SameAs(chosenConversationContext));
            Assert.That(capturedCtx.Request, Is.SameAs(request));
            Assert.That(capturedCtx.Org, Is.SameAs(org));
            Assert.That(capturedCtx.User, Is.SameAs(user));
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

            var ctx = new AgentPipelineContext()
            {
                CorrelationId = "corr-5",
                Org = org,
                User = user,
                Request = request
            };

            _agentContextManager
                .Setup(m => m.GetAgentContextWithSecretsAsync(agentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            ConversationContext capturedConversationContext = null;

            _reasoner
                .Setup(r => r.ExecuteAsync(It.IsAny<AgentPipelineContext>()))
                .Callback<AgentPipelineContext>((c) =>
                {
                    capturedConversationContext = c.ConversationContext;
                    c.Response = new AgentExecuteResponse { Text = "ok" };
                })
                .ReturnsAsync((AgentPipelineContext c) => InvokeResult<AgentPipelineContext>.Create(c));

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(capturedConversationContext, Is.Not.Null);
            Assert.That(capturedConversationContext.Id, Is.EqualTo("conv-2"));
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
                ConversationContexts = new List<ConversationContext> { conv },
                AgentModes = new List<AgentMode>
                {
                    new AgentMode { Key = "general", DisplayName = "General", WhenToUse = "Default general-purpose mode." },
                    new AgentMode { Key = "ddr_authoring", DisplayName = "DDR Authoring", WhenToUse = "Use when authoring or editing DDRs." }
                }
            };
        }

        private static AgentContext CreateAgentContextWithTwoConversations()
        {
            var conv1 = new ConversationContext { Id = "conv-1", Name = "Context 1" };
            var conv2 = new ConversationContext { Id = "conv-2", Name = "Context 2" };

            return new AgentContext
            {
                Id = "ctx-1",
                Name = "Context 1",
                DefaultConversationContext = new EntityHeader
                {
                    Id = conv1.Id,
                    Text = conv1.Name
                },
                ConversationContexts = new List<ConversationContext> { conv1, conv2 },
                AgentModes = new List<AgentMode>
                {
                    new AgentMode { Key = "general", DisplayName = "General", WhenToUse = "Default general-purpose mode." },
                    new AgentMode { Key = "ddr_authoring", DisplayName = "DDR Authoring", WhenToUse = "Use when authoring or editing DDRs." }
                }
            };
        }

        #endregion
    }
}
