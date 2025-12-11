using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentOrchestratorTests
    {
        private Mock<IAgentSessionManager> _sessionManager;
        private Mock<IAgentSessionFactory> _sessionFactory;
        private Mock<IAgentTurnExecutor> _turnExecutor;
        private Mock<INotificationPublisher> _notificationPublisher;
        private Mock<IAdminLogger> _adminLogger;
        private Mock<IAgentContextManager> _contextManager;
        private Mock<IAgentTurnTranscriptStore> _transcriptStore;
        private Mock<IAgentStreamingContext> _streamingContext;

        private AgentOrchestrator _sut;

        [SetUp]
        public void SetUp()
        {
            _sessionManager = new Mock<IAgentSessionManager>();
            _sessionFactory = new Mock<IAgentSessionFactory>();
            _turnExecutor = new Mock<IAgentTurnExecutor>();
            _notificationPublisher = new Mock<INotificationPublisher>();
            _adminLogger = new Mock<IAdminLogger>();
            _contextManager = new Mock<IAgentContextManager>();
            _transcriptStore = new Mock<IAgentTurnTranscriptStore>();
            _streamingContext = new Mock<IAgentStreamingContext>();

            _notificationPublisher
                .Setup(p => p.PublishAsync(
                    It.IsAny<Targets>(),
                    It.IsAny<Channels>(),
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<NotificationVerbosity>()))
                .Returns(Task.CompletedTask);

            _sut = new AgentOrchestrator(
                _sessionManager.Object,
                _contextManager.Object,
                _transcriptStore.Object,
                _sessionFactory.Object,
                _turnExecutor.Object,
                _notificationPublisher.Object,
                _adminLogger.Object,
                _streamingContext.Object);
        }

        #region Ctor Guards

        [Test]
        public void Ctor_NullSessionManager_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AgentOrchestrator(
                    null,
                    _contextManager.Object,
                    _transcriptStore.Object,
                    _sessionFactory.Object,
                    _turnExecutor.Object,
                    _notificationPublisher.Object,
                    _adminLogger.Object,
                    _streamingContext.Object));
        }

        [Test]
        public void Ctor_NullContextManager_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AgentOrchestrator(
                    _sessionManager.Object,
                    null,
                    _transcriptStore.Object,
                    _sessionFactory.Object,
                    _turnExecutor.Object,
                    _notificationPublisher.Object,
                    _adminLogger.Object,
                    _streamingContext.Object));
        }

        [Test]
        public void Ctor_NullTranscriptStore_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AgentOrchestrator(
                    _sessionManager.Object,
                    _contextManager.Object,
                    null,
                    _sessionFactory.Object,
                    _turnExecutor.Object,
                    _notificationPublisher.Object,
                    _adminLogger.Object,
                    _streamingContext.Object));
        }

        [Test]
        public void Ctor_NullSessionFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AgentOrchestrator(
                    _sessionManager.Object,
                    _contextManager.Object,
                    _transcriptStore.Object,
                    null,
                    _turnExecutor.Object,
                    _notificationPublisher.Object,
                    _adminLogger.Object,
                    _streamingContext.Object));
        }

        [Test]
        public void Ctor_NullTurnExecutor_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AgentOrchestrator(
                    _sessionManager.Object,
                    _contextManager.Object,
                    _transcriptStore.Object,
                    _sessionFactory.Object,
                    null,
                    _notificationPublisher.Object,
                    _adminLogger.Object,
                    _streamingContext.Object));
        }

        [Test]
        public void Ctor_NullNotificationPublisher_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AgentOrchestrator(
                    _sessionManager.Object,
                    _contextManager.Object,
                    _transcriptStore.Object,
                    _sessionFactory.Object,
                    _turnExecutor.Object,
                    null,
                    _adminLogger.Object,
                    _streamingContext.Object));
        }

        [Test]
        public void Ctor_NullAdminLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AgentOrchestrator(
                    _sessionManager.Object,
                    _contextManager.Object,
                    _transcriptStore.Object,
                    _sessionFactory.Object,
                    _turnExecutor.Object,
                    _notificationPublisher.Object,
                    null,
                    _streamingContext.Object));

        }

        #endregion

        #region BeginNewSessionAsync – request envelope

        [Test]
        public async Task BeginNewSessionAsync_Success_StoresRequestEnvelope_SetsRequestEnvelopeUrl_AndCompletesTurn()
        {
            // Arrange
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-ctx-1", Text = "Agent Ctx" },
                Instruction = "do something",
                Mode = "ask",
                WorkspaceId = "ws-1",
                Repo = "repo-1",
                Language = "csharp",
                ActiveFiles = new List<ActiveFile>(),
                RagScopeFilter = new RagScopeFilter()
            };

            var agentContext = new AgentContext { Id = "ctx-1", Name = "Context 1" };

            var session = new AgentSession
            {
                Id = "session-1",
                AgentContext = EntityHeader.Create("id","text"),
                ConversationContext = EntityHeader.Create("id", "text"),
                Repo = request.Repo,
                DefaultLanguage = request.Language,
                WorkspaceId = request.WorkspaceId
            };

            var turn = new AgentSessionTurn
            {
                Id = "turn-1",
                SequenceNumber = 1,
                ConversationId = "conv-1"
            };

            var agentResponse = new AgentExecuteResponse
            {
                Text = "final answer",
                FullResponseUrl = "https://responses/resp-1.json",
                ResponseContinuationId = "resp-cont-1",
                Warnings = new List<string> { "warn-1" }
            };

            _contextManager
                .Setup(c => c.GetAgentContextAsync(request.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            _sessionFactory
                .Setup(f => f.CreateSession(request, agentContext, OperationKinds.Code, org, user))
                .ReturnsAsync(session);

            _sessionFactory
                .Setup(f => f.CreateTurnForNewSession(session, request, org, user))
                .Returns(turn);

            _sessionManager
                .Setup(m => m.AddAgentSessionAsync(session, org, user))
                .Returns(Task.CompletedTask);

            _sessionManager
                .Setup(m => m.AddAgentSessionTurnAsync(session.Id, turn, org, user))
                .Returns(Task.CompletedTask);

            string capturedRequestJson = null;

            _transcriptStore
                .Setup(t => t.SaveTurnRequestAsync(org.Id, session.Id, turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, string, CancellationToken>((o, sId, tId, json, ct) => capturedRequestJson = json)
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("blob://req-1")));

            _sessionManager
                .Setup(m => m.SetRequestBlobUriAsync(session.Id, turn.Id, "blob://req-1", org, user))
                .Returns(Task.CompletedTask);

            _turnExecutor
                .Setup(t => t.ExecuteNewSessionTurnAsync(agentContext, session, turn, request, org, user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(agentResponse));

            string completedSessionId = null;
            string completedTurnId = null;
            string completedText = null;
            string completedUrl = null;
            string completedContinuationId = null;
            List<string> completedWarnings = null;
            double completedElapsed = 0;

            _sessionManager
                .Setup(m => m.CompleteAgentSessionTurnAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<double>(),
                    It.IsAny<List<string>>(),
                    org,
                    user))
                .Callback<string, string, string, string, string, double, List<string>, EntityHeader, EntityHeader>(
                    (sessId, turnId, text, url, contId, elapsed, warnings, o, u) =>
                    {
                        completedSessionId = sessId;
                        completedTurnId = turnId;
                        completedText = text;
                        completedUrl = url;
                        completedContinuationId = contId;
                        completedWarnings = warnings;
                        completedElapsed = elapsed;
                    })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.BeginNewSessionAsync(request, org, user);

            // Assert – orchestrator result
            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.SameAs(agentResponse));

            // Assert – request envelope JSON persisted correctly
            Assert.That(capturedRequestJson, Is.Not.Null.And.Not.Empty);
            dynamic envelope = JsonConvert.DeserializeObject(capturedRequestJson);
            Assert.That((string)envelope.OrgId, Is.EqualTo(org.Id));
            Assert.That((string)envelope.SessionId, Is.EqualTo(session.Id));
            Assert.That((string)envelope.TurnId, Is.EqualTo(turn.Id));

            // Assert – blob URL was stored on session/turn and on response
            _sessionManager.Verify(m => m.SetRequestBlobUriAsync(session.Id, turn.Id, "blob://req-1/", org, user), Times.Once);
            Assert.That(agentResponse.RequestEnvelopeUrl, Is.EqualTo("blob://req-1/"));

            // Assert – completion call used required fields from AgentExecuteResponse
            Assert.That(completedSessionId, Is.EqualTo(session.Id));
            Assert.That(completedTurnId, Is.EqualTo(turn.Id));
            Assert.That(completedText, Is.EqualTo(agentResponse.Text));
            Assert.That(completedUrl, Is.EqualTo(agentResponse.FullResponseUrl));
            Assert.That(completedContinuationId, Is.EqualTo(agentResponse.ResponseContinuationId));
            Assert.That(completedWarnings, Is.SameAs(agentResponse.Warnings));
            Assert.That(completedElapsed, Is.GreaterThan(0));
        }

        [Test]
        public async Task BeginNewSessionAsync_SaveRequestFails_ReturnsErrorAndDoesNotExecuteTurn()
        {
            // Arrange
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                AgentContext = new EntityHeader { Id = "agent-ctx-1", Text = "Agent Ctx" },
                Instruction = "do something",
                Mode = "ask"
            };

            var agentContext = new AgentContext { Id = "ctx-1", Name = "Context 1" };
            var session = new AgentSession { Id = "session-1", AgentContext = EntityHeader.Create("id","text"), ConversationContext = EntityHeader.Create("id", "text") };
            var turn = new AgentSessionTurn { Id = "turn-1", ConversationId = "conv-1" };

            _contextManager
                .Setup(c => c.GetAgentContextAsync(request.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            _sessionFactory
                .Setup(f => f.CreateSession(request, agentContext, OperationKinds.Code, org, user))
                .ReturnsAsync(session);

            _sessionFactory
                .Setup(f => f.CreateTurnForNewSession(session, request, org, user))
                .Returns(turn);

            _sessionManager
                .Setup(m => m.AddAgentSessionAsync(session, org, user))
                .Returns(Task.CompletedTask);

            _sessionManager
                .Setup(m => m.AddAgentSessionTurnAsync(session.Id, turn, org, user))
                .Returns(Task.CompletedTask);

            _transcriptStore
                .Setup(t => t.SaveTurnRequestAsync(org.Id, session.Id, turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.FromError("failed to save"));

            // Act
            var result = await _sut.BeginNewSessionAsync(request, org, user);

            // Assert
            Assert.That(result.Successful, Is.False);

            _turnExecutor.Verify(t => t.ExecuteNewSessionTurnAsync(
                It.IsAny<AgentContext>(),
                It.IsAny<AgentSession>(),
                It.IsAny<AgentSessionTurn>(),
                It.IsAny<AgentExecuteRequest>(),
                org,
                user,
                It.IsAny<CancellationToken>()), Times.Never);

            _sessionManager.Verify(m => m.SetRequestBlobUriAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), org, user), Times.Never);
            _sessionManager.Verify(m => m.CompleteAgentSessionTurnAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<List<string>>(),
                org,
                user), Times.Never);
        }

        #endregion

        #region ExecuteTurnAsync – request envelope

        [Test]
        public async Task ExecuteTurnAsync_Success_StoresRequestEnvelope_SetsRequestEnvelopeUrl_AndCompletesTurn()
        {
            // Arrange
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                ConversationId = "conv-1",
                Instruction = "follow up",
                PreviousTurnId = null,
                Mode = "ask",
                WorkspaceId = "ws-1",
                Repo = "repo-1",
                Language = "csharp",
                ActiveFiles = new List<ActiveFile>(),
                RagScopeFilter = new RagScopeFilter()
            };

            var agentContext = new AgentContext { Id = "ctx-1", Name = "Context 1" };

            var session = new AgentSession
            {
                Id = "session-1",
                AgentContext = EntityHeader.Create("id", "text"),
                ConversationContext = EntityHeader.Create("id", "text"),
                Repo = request.Repo,
                DefaultLanguage = request.Language,
                WorkspaceId = request.WorkspaceId
            };

            var previousTurn = new AgentSessionTurn
            {
                Id = "prev-turn",
                SequenceNumber = 1,
                ConversationId = "conv-1",
                OpenAIResponseId = "open-ai-prev"
            };

            var newTurn = new AgentSessionTurn
            {
                Id = "turn-2",
                SequenceNumber = 0,
                ConversationId = null
            };

            var agentResponse = new AgentExecuteResponse
            {
                Text = "follow up answer",
                FullResponseUrl = "https://responses/resp-2.json",
                ResponseContinuationId = "resp-cont-2",
                Warnings = new List<string> { "warn-2" }
            };

            _sessionManager
                .Setup(m => m.GetAgentSessionAsync(request.ConversationId, org, user))
                .ReturnsAsync(session);

            _sessionManager
                .Setup(m => m.GetLastAgentSessionTurnAsync(request.ConversationId, org, user))
                .ReturnsAsync(previousTurn);

            _contextManager
                .Setup(c => c.GetAgentContextAsync(session.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            _sessionFactory
                .Setup(f => f.CreateTurnForExistingSession(session, request, org, user))
                .Returns(newTurn);

            _sessionManager
                .Setup(m => m.AddAgentSessionTurnAsync(session.Id, newTurn, org, user))
                .Returns(Task.CompletedTask);

            string capturedRequestJson = null;

            _transcriptStore
                .Setup(t => t.SaveTurnRequestAsync(org.Id, session.Id, newTurn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, string, CancellationToken>((o, sId, tId, json, ct) => capturedRequestJson = json)
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("blob://req-1")));

            _turnExecutor
                .Setup(t => t.ExecuteFollowupTurnAsync(agentContext, session, newTurn, request, org, user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(agentResponse));

            string completedSessionId = null;
            string completedTurnId = null;
            string completedText = null;
            string completedUrl = null;
            string completedContinuationId = null;
            List<string> completedWarnings = null;
            double completedElapsed = 0;

            _sessionManager
                .Setup(m => m.CompleteAgentSessionTurnAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<double>(),
                    It.IsAny<List<string>>(),
                    org,
                    user))
                .Callback<string, string, string, string, string, double, List<string>, EntityHeader, EntityHeader>(
                    (sessId, turnId, text, url, contId, elapsed, warnings, o, u) =>
                    {
                        completedSessionId = sessId;
                        completedTurnId = turnId;
                        completedText = text;
                        completedUrl = url;
                        completedContinuationId = contId;
                        completedWarnings = warnings;
                        completedElapsed = elapsed;
                    })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.ExecuteTurnAsync(request, org, user);

            // Assert – orchestrator result
            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.SameAs(agentResponse));

            // Assert – request envelope JSON persisted correctly with PreviousOpenAIResponseId
            Assert.That(capturedRequestJson, Is.Not.Null.And.Not.Empty);
            dynamic envelope = JsonConvert.DeserializeObject(capturedRequestJson);
            Assert.That((string)envelope.OrgId, Is.EqualTo(org.Id));
            Assert.That((string)envelope.SessionId, Is.EqualTo(session.Id));
            Assert.That((string)envelope.TurnId, Is.EqualTo(newTurn.Id));
            Assert.That((string)envelope.PreviousOpenAIResponseId, Is.EqualTo(previousTurn.OpenAIResponseId));

            // Assert – response completion uses required fields
            Assert.That(completedSessionId, Is.EqualTo(session.Id));
            Assert.That(completedTurnId, Is.EqualTo(newTurn.Id));
            Assert.That(completedText, Is.EqualTo(agentResponse.Text));
            Assert.That(completedUrl, Is.EqualTo(agentResponse.FullResponseUrl));
            Assert.That(completedContinuationId, Is.EqualTo(agentResponse.ResponseContinuationId));
            Assert.That(completedWarnings, Is.SameAs(agentResponse.Warnings));
            Assert.That(completedElapsed, Is.GreaterThan(0));

            // RequestEnvelopeUrl should be set from the blob result
            Assert.That(agentResponse.RequestEnvelopeUrl, Is.EqualTo("blob://req-1/"));
        }

        [Test]
        public async Task ExecuteTurnAsync_SaveRequestFails_ReturnsErrorAndDoesNotExecuteTurn()
        {
            // Arrange
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                ConversationId = "conv-1",
                Instruction = "follow up",
                Mode = "ask"
            };

            var agentContext = new AgentContext { Id = "ctx-1", Name = "Context 1" };
            var session = new AgentSession { Id = "session-1", AgentContext = EntityHeader.Create("id", "text"), ConversationContext = EntityHeader.Create("id", "text") };
            var previousTurn = new AgentSessionTurn { Id = "prev", SequenceNumber = 1, ConversationId = "conv-1", OpenAIResponseId = "prev-open" };
            var newTurn = new AgentSessionTurn { Id = "turn-2" };

            _sessionManager
                .Setup(m => m.GetAgentSessionAsync(request.ConversationId, org, user))
                .ReturnsAsync(session);

            _sessionManager
                .Setup(m => m.GetLastAgentSessionTurnAsync(request.ConversationId, org, user))
                .ReturnsAsync(previousTurn);

            _contextManager
                .Setup(c => c.GetAgentContextAsync(session.AgentContext.Id, org, user))
                .ReturnsAsync(agentContext);

            _sessionFactory
                .Setup(f => f.CreateTurnForExistingSession(session, request, org, user))
                .Returns(newTurn);

            _sessionManager
                .Setup(m => m.AddAgentSessionTurnAsync(session.Id, newTurn, org, user))
                .Returns(Task.CompletedTask);

            _transcriptStore
                .Setup(t => t.SaveTurnRequestAsync(org.Id, session.Id, newTurn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.FromError("failed"));

            // Act
            var result = await _sut.ExecuteTurnAsync(request, org, user);

            // Assert
            Assert.That(result.Successful, Is.False);

            _turnExecutor.Verify(t => t.ExecuteFollowupTurnAsync(
                It.IsAny<AgentContext>(),
                It.IsAny<AgentSession>(),
                It.IsAny<AgentSessionTurn>(),
                It.IsAny<AgentExecuteRequest>(),
                org,
                user,
                It.IsAny<CancellationToken>()), Times.Never);

            _sessionManager.Verify(m => m.CompleteAgentSessionTurnAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<List<string>>(),
                org,
                user), Times.Never);
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
