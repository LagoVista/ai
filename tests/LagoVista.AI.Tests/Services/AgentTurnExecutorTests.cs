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
using Newtonsoft.Json;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentTurnExecutorTests
    {
        private Mock<IAgentExecutionService> _agentExecutionService;
        private Mock<IAgentTurnTranscriptStore> _transcriptStore;
        private Mock<IAdminLogger> _adminLogger;
        private AgentTurnExecutor _sut;

        [SetUp]
        public void SetUp()
        {
            _agentExecutionService = new Mock<IAgentExecutionService>();
            _transcriptStore = new Mock<IAgentTurnTranscriptStore>();
            _adminLogger = new Mock<IAdminLogger>();

            _sut = new AgentTurnExecutor(
                _agentExecutionService.Object,
                _transcriptStore.Object,
                _adminLogger.Object);
        }

        #region Ctor Guards

        [Test]
        public void Ctor_NullAgentExecutionService_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentTurnExecutor(null, _transcriptStore.Object, _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullTranscriptStore_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentTurnExecutor(_agentExecutionService.Object, null, _adminLogger.Object));
        }

        [Test]
        public void Ctor_NullAdminLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentTurnExecutor(_agentExecutionService.Object, _transcriptStore.Object, null));
        }

        #endregion

        #region Argument Guards

        [Test]
        public void ExecuteNewSessionTurnAsync_NullAgentContext_Throws()
        {
            var session = CreateSession();
            var turn = CreateTurn();
            var execRequest = CreateExecRequestForNewSession(session, turn);
            var org = CreateOrg();
            var user = CreateUser();

            Assert.ThrowsAsync<ArgumentNullException>(
                () => _sut.ExecuteNewSessionTurnAsync(null, session, turn, execRequest, org, user));
        }

        [Test]
        public void ExecuteFollowupTurnAsync_NullAgentContext_Throws()
        {
            var session = CreateSession();
            var turn = CreateTurn();
            var execRequest = CreateExecRequestForFollowup(session, turn);
            var org = CreateOrg();
            var user = CreateUser();

            Assert.ThrowsAsync<ArgumentNullException>(
                () => _sut.ExecuteFollowupTurnAsync(null, session, turn, execRequest, org, user));
        }

        #endregion

        #region ExecuteNewSessionTurnAsync

        [Test]
        public async Task ExecuteNewSessionTurnAsync_Success_ReturnsExecResult()
        {
            // Arrange
            var agentContext = new AgentContext();
            var session = CreateSession();
            var turn = CreateTurn();
            var execRequest = CreateExecRequestForNewSession(session, turn);
            var org = CreateOrg();
            var user = CreateUser();

            _transcriptStore
                .Setup(t => t.SaveTurnRequestAsync(org.Id, session.Id, turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("https://www.test.ai/reqeust")));

            var execResponse = new AgentExecuteResponse
            {
                Text = "hello world",
                ResponseContinuationId = "resp-123"
            };

            _agentExecutionService
                .Setup(s => s.ExecuteAsync(execRequest, org, user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(execResponse));

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(org.Id, session.Id, turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("https://www.test.ai/response")));

            // Act
            var result = await _sut.ExecuteNewSessionTurnAsync(agentContext, session, turn, execRequest, org, user);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.SameAs(execResponse));

            _agentExecutionService.Verify(s => s.ExecuteAsync(execRequest, org, user, It.IsAny<CancellationToken>()), Times.Once);
            _transcriptStore.Verify(t => t.SaveTurnResponseAsync(org.Id, session.Id, turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ExecuteNewSessionTurnAsync_ExecFails_ReturnsErrorAndDoesNotWriteResponseTranscript()
        {
            // Arrange
            var agentContext = new AgentContext();
            var session = CreateSession();
            var turn = CreateTurn();
            var execRequest = CreateExecRequestForNewSession(session, turn);
            var org = CreateOrg();
            var user = CreateUser();

            _transcriptStore
                .Setup(t => t.SaveTurnRequestAsync(org.Id, session.Id, turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("https://www.test.ai/reqeust")));

            var failingExecResult = InvokeResult<AgentExecuteResponse>.FromError("exec-failed");

            _agentExecutionService
                .Setup(s => s.ExecuteAsync(execRequest, org, user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(failingExecResult);

            // Act
            var result = await _sut.ExecuteNewSessionTurnAsync(agentContext, session, turn, execRequest, org, user);

            // Assert
            Assert.That(result.Successful, Is.False);

            _transcriptStore.Verify(t => t.SaveTurnResponseAsync( It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task ExecuteNewSessionTurnAsync_SaveResponseFails_LogsErrorAndReturnsError()
        {
            // Arrange
            var agentContext = new AgentContext();
            var session = CreateSession();
            var turn = CreateTurn();
            var execRequest = CreateExecRequestForNewSession(session, turn);
            var org = CreateOrg();
            var user = CreateUser();

            _transcriptStore
                .Setup(t => t.SaveTurnRequestAsync(org.Id, session.Id, turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("https://www.test.ai/reqeust")));

            var execResponse = new AgentExecuteResponse { Text = "answer" };

            _agentExecutionService
                .Setup(s => s.ExecuteAsync(execRequest, org, user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(execResponse));

            var failingResponseResult = InvokeResult<Uri>.FromError("failed-to-save-response");

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(org.Id, session.Id, turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(failingResponseResult);

            // Act
            var result = await _sut.ExecuteNewSessionTurnAsync(agentContext, session, turn, execRequest, org, user);

            // Assert
            Assert.That(result.Successful, Is.False);

            _adminLogger.Verify(l => l.AddError(
                    "[AgentTurnExecutor_ExecuteNewSessionTurnAsync__Transcript]",
                    "Failed to store turn response transcript."),
                Times.Once);
        }

        #endregion

        #region ExecuteFollowupTurnAsync

        [Test]
        public async Task ExecuteFollowupTurnAsync_Success_ReturnsExecResult_AndWritesPrevResponseIdAndResponseId()
        {
            // Arrange
            var agentContext = new AgentContext();
            var session = CreateSession();
            var turn = CreateTurn();
            turn.PreviousOpenAIResponseId = "prev-001";

            var execRequest = CreateExecRequestForFollowup(session, turn);
            execRequest.ResponseContinuationId = "resp-cont-999";

            var org = CreateOrg();
            var user = CreateUser();

            string capturedResponseJson = null;

            var execResponse = new AgentExecuteResponse
            {
                Text = "follow-up-answer",
                ResponseContinuationId = "resp-456"
            };

            _agentExecutionService
                .Setup(s => s.ExecuteAsync(execRequest, org, user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(execResponse));

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(org.Id, session.Id, turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, string, CancellationToken>((o, sid, tid, json, ct) => capturedResponseJson = json)
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("https://www.test.ai/response")));

            // Act
            var result = await _sut.ExecuteFollowupTurnAsync(agentContext, session, turn, execRequest, org, user);

            // Assert
            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.SameAs(execResponse));
            Assert.That(capturedResponseJson, Is.Not.Null);

            
            dynamic responseEnvelope = JsonConvert.DeserializeObject(capturedResponseJson);
            Assert.That((string)responseEnvelope.ResponseId, Is.EqualTo("resp-cont-999"));
        }

        [Test]
        public async Task ExecuteFollowupTurnAsync_SaveResponseFails_LogsErrorAndReturnsError()
        {
            // Arrange
            var agentContext = new AgentContext();
            var session = CreateSession();
            var turn = CreateTurn();
            var execRequest = CreateExecRequestForFollowup(session, turn);
            var org = CreateOrg();
            var user = CreateUser();

            _transcriptStore
                .Setup(t => t.SaveTurnRequestAsync(org.Id, session.Id, turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<Uri>.Create(new Uri("https://www.test.ai/reqeust")));

            var execResponse = new AgentExecuteResponse { Text = "answer" };

            _agentExecutionService
                .Setup(s => s.ExecuteAsync(execRequest, org, user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(execResponse));

            var failingResponseResult = InvokeResult<Uri>.FromError("failed-to-save-response");

            _transcriptStore
                .Setup(t => t.SaveTurnResponseAsync(org.Id, session.Id, turn.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(failingResponseResult);

            // Act
            var result = await _sut.ExecuteFollowupTurnAsync(agentContext, session, turn, execRequest, org, user);

            // Assert
            Assert.That(result.Successful, Is.False);

            _adminLogger.Verify(l => l.AddError(
                    "[AgentTurnExecutor_ExecuteFollowupTurnAsync__Transcript]",
                    "Failed to store turn response transcript."),
                Times.Once);
        }

        #endregion

        #region Helpers

        private static AgentSession CreateSession()
        {
            return new AgentSession
            {
                Id = "session-1",
                AgentContext = new EntityHeader(),
                ConversationContext = new EntityHeader(),
                Repo = "repo-name",
                DefaultLanguage = "csharp",
                WorkspaceId = "workspace-1"
            };
        }

        private static AgentSessionTurn CreateTurn()
        {
            return new AgentSessionTurn
            {
                Id = "turn-1",
                ConversationId = "conversation-1",
                PreviousOpenAIResponseId = null
            };
        }

        private static AgentExecuteRequest CreateExecRequestForNewSession(AgentSession session, AgentSessionTurn turn)
        {
            return new AgentExecuteRequest
            {
                AgentContext = session.AgentContext,
                ConversationContext = session.ConversationContext,
                Mode = "ask",
                Instruction = "do something",
                ConversationId = turn.ConversationId,
                Repo = "repo-name",
                Language = "csharp",
                WorkspaceId = session.WorkspaceId,
                ActiveFiles = new List<ActiveFile>
                {
                    new ActiveFile
                    {
                        AbsolutePath = "d:/path/one.cs",
                        RelativePath = "./path/onme.cs",
                        Sha256Hash = "SHAAA---FF",
                        Contents = "// file one",
                        Language = "csharp"
                    },
                    new ActiveFile
                    {
                        AbsolutePath = "d:/path/one.cs",
                        RelativePath = "./path/onme.cs",
                        Sha256Hash = "SHAAA---FF",
                        Contents = "// file one",
                        Language = "csharp"
                    }
                },
                RagScopeFilter = new RagScopeFilter()
            };
        }

        private static AgentExecuteRequest CreateExecRequestForFollowup(AgentSession session, AgentSessionTurn turn)
        {
            return new AgentExecuteRequest
            {
                AgentContext = session.AgentContext,
                ConversationContext = session.ConversationContext,
                Mode = "ask",
                Instruction = "follow up",
                ConversationId = turn.ConversationId,
                Repo = session.Repo,
                Language = session.DefaultLanguage,
                WorkspaceId = session.WorkspaceId,
                ActiveFiles = new List<ActiveFile>
                {
                    new ActiveFile
                    {

                        AbsolutePath = "d:/path/one.cs",
                        RelativePath = "./path/onme.cs",
                        Sha256Hash = "SHAAA---FF",
                        Contents = "// file one",
                        Language = "csharp"
                    }
                },
                RagScopeFilter = new RagScopeFilter()
            };
        }

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
