using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.AI.Services.Tools;
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
    public class AgentReasonerTests
    {
        private Mock<ILLMClient> _llmClient;
        private Mock<IAgentToolExecutor> _toolExecutor;
        private Mock<IAdminLogger> _logger;
        private Mock<IAgentStreamingContext> _streaming;
        private Mock<IModeEntryBootstrapService> _bsExecutor;

        private AgentReasoner _sut;

        [SetUp]
        public void SetUp()
        {
            _llmClient = new Mock<ILLMClient>(MockBehavior.Strict);
            _toolExecutor = new Mock<IAgentToolExecutor>(MockBehavior.Strict);
            _logger = new Mock<IAdminLogger>(MockBehavior.Loose);
            _streaming = new Mock<IAgentStreamingContext>(MockBehavior.Loose);
            _bsExecutor = new Mock<IModeEntryBootstrapService>(MockBehavior.Strict);

            _sut = new AgentReasoner(
                _llmClient.Object,
                _toolExecutor.Object,
                _logger.Object,
                _streaming.Object,
                _bsExecutor.Object);
        }

        #region Helpers

        private static AgentContext CreateAgentContext()
        {
            return new AgentContext
            {
                Id = "agent-1",
                Name = "Test Agent",
                AgentModes = new List<AgentMode>
                {
                    new AgentMode
                    {
                        Key = "ddr_authoring",
                        DisplayName = "DDR Authoring",
                        WelcomeMessage = "Welcome to DDR Authoring mode!"
                    },
                    new AgentMode
                    {
                        Key = "workflow_authoring",
                        DisplayName = "Workflow Authoring",
                        WelcomeMessage = "Welcome to Workflow Authoring mode!"
                    },
                    new AgentMode
                    {
                        Key = "general",
                        DisplayName = "General",
                        WelcomeMessage = null
                    }
                }
            };
        }

        private static ConversationContext CreateConversationContext()
        {
            return new ConversationContext
            {
                Id = "conv-1",
                Name = "Test Conversation"
            };
        }

        private static EntityHeader CreateOrg()
        {
            return new EntityHeader { Id = "org-1", Text = "Org 1" };
        }

        private static EntityHeader CreateUser()
        {
            return new EntityHeader { Id = "user-1", Text = "User 1" };
        }

        private static AgentPipelineContext BuildCtx(string mode = "general")
        {
            var org = CreateOrg();
            var user = CreateUser();

            var request = new AgentExecuteRequest
            {
                SessionId = "conv-1",
                Mode = mode,
                Instruction = "do something",
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                RagScopeFilter = new RagScopeFilter(),
                ActiveFiles = new List<ActiveFile>()
            };

            // If your AgentExecuteRequest supports it, set it. If not, Reasoner falls back to Turn.Id.
            try { request.CurrentTurnId = "turn-1"; } catch { /* ignore */ }

            return new AgentPipelineContext
            {
                CorrelationId = "corr-1",
                Org = org,
                User = user,
                Request = request,
                AgentContext = CreateAgentContext(),
                ConversationContext = CreateConversationContext(),
                RagContextBlock = string.Empty,
                Turn = new AgentSessionTurn { Id = "turn-1" }
            };
        }

        private void SetupBootstrapSuccess()
        {
            _bsExecutor
                .Setup(b => b.ExecuteAsync(It.IsAny<ModeEntryBootstrapRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<ModeEntryBootstrapDetails>.Create(new ModeEntryBootstrapDetails()));
        }

        /// <summary>
        /// Configure the LLM pipeline step to return a different AgentExecuteResponse each time it is called,
        /// by mutating ctx.Response and returning InvokeResult(ctx).
        /// </summary>
        private void SetupLlmResponses(params AgentExecuteResponse[] responses)
        {
            if (responses == null) throw new ArgumentNullException(nameof(responses));
            if (responses.Length == 0) throw new ArgumentException("At least one response is required.", nameof(responses));

            var idx = 0;

            _llmClient
                .Setup(s => s.ExecuteAsync(It.IsAny<AgentPipelineContext>()))
                .Returns((AgentPipelineContext c) =>
                {
                    var pick = idx < responses.Length ? responses[idx] : responses[responses.Length - 1];
                    idx++;

                    c.Response = pick;
                    return Task.FromResult(InvokeResult<AgentPipelineContext>.Create(c));
                });
        }

        #endregion

        #region Tests

        [Test]
        public async Task ExecuteAsync_NoToolCalls_SetsCtxResponse_AndStampsMode_WhenResponseModeMissing()
        {
            var ctx = BuildCtx(mode: "general");

            var llmResponse = new AgentExecuteResponse
            {
                Mode = null,
                ToolCalls = new List<AgentToolCall>(),
                Text = "final answer"
            };

            SetupLlmResponses(llmResponse);

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(result.Result, Is.SameAs(ctx));

            Assert.That(ctx.Response, Is.Not.Null);
            Assert.That(ctx.Response.Mode, Is.EqualTo("general"));
            Assert.That(ctx.Response.Text, Is.EqualTo("final answer"));

            _toolExecutor.Verify(
                t => t.ExecuteServerToolAsync(It.IsAny<AgentToolCall>(), It.IsAny<AgentPipelineContext>()),
                Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_ModeChangeTool_UpdatesRequestAndResponseMode_AndPrependsWelcomeMessage()
        {
            var ctx = BuildCtx(mode: "general");

            var firstResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall { CallId = "call-1", Name = ModeChangeTool.ToolName }
                },
                Text = "calling mode change tool"
            };

            var secondResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>(),
                Mode = null,
                Text = "final answer in new mode"
            };

            SetupLlmResponses(firstResponse, secondResponse);

            var updatedCall = new AgentToolCall
            {
                CallId = "call-1",
                Name = ModeChangeTool.ToolName,
                WasExecuted = true,
                RequiresClientExecution = false,
                ResultJson = JsonConvert.SerializeObject(new
                {
                    success = true,
                    mode = "ddr_authoring",
                    branch = false,
                    reason = "Switch to DDR authoring for this request."
                })
            };

            _toolExecutor
                .Setup(t => t.ExecuteServerToolAsync(
                    It.IsAny<AgentToolCall>(),
                    It.IsAny<AgentPipelineContext>()))
                .ReturnsAsync(InvokeResult<AgentToolCall>.Create(updatedCall));

            SetupBootstrapSuccess();

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(ctx.Response, Is.Not.Null);

            Assert.That(ctx.Request.Mode, Is.EqualTo("ddr_authoring"));
            Assert.That(ctx.Response.Mode, Is.EqualTo("ddr_authoring"));

            Assert.That(ctx.Response.Text, Does.Contain("Welcome to DDR Authoring mode!"));
            Assert.That(ctx.Response.Text, Does.Contain("final answer in new mode"));

            _bsExecutor.Verify(
                b => b.ExecuteAsync(It.Is<ModeEntryBootstrapRequest>(r => r.ModeKey == "ddr_authoring"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_MultipleModeChangeCalls_UsesLastModeAndLogsWarning()
        {
            var ctx = BuildCtx(mode: "general");

            var firstResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall { CallId = "call-1", Name = ModeChangeTool.ToolName },
                    new AgentToolCall { CallId = "call-2", Name = ModeChangeTool.ToolName }
                },
                Text = "multiple mode changes"
            };

            var secondResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>(),
                Mode = null,
                Text = "final answer after multiple mode changes"
            };

            SetupLlmResponses(firstResponse, secondResponse);

            var firstUpdatedCall = new AgentToolCall
            {
                CallId = "call-1",
                Name = ModeChangeTool.ToolName,
                WasExecuted = true,
                RequiresClientExecution = false,
                ResultJson = JsonConvert.SerializeObject(new { success = true, mode = "ddr_authoring" })
            };

            var secondUpdatedCall = new AgentToolCall
            {
                CallId = "call-2",
                Name = ModeChangeTool.ToolName,
                WasExecuted = true,
                RequiresClientExecution = false,
                ResultJson = JsonConvert.SerializeObject(new { success = true, mode = "workflow_authoring" })
            };

            _toolExecutor
                .SetupSequence(t => t.ExecuteServerToolAsync(
                    It.IsAny<AgentToolCall>(),
                    It.IsAny<AgentPipelineContext>()))
                .ReturnsAsync(InvokeResult<AgentToolCall>.Create(firstUpdatedCall))
                .ReturnsAsync(InvokeResult<AgentToolCall>.Create(secondUpdatedCall));

            SetupBootstrapSuccess();

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(ctx.Response, Is.Not.Null);

            Assert.That(ctx.Request.Mode, Is.EqualTo("workflow_authoring"));
            Assert.That(ctx.Response.Mode, Is.EqualTo("workflow_authoring"));

            Assert.That(ctx.Response.Text, Does.Contain("Welcome to Workflow Authoring mode!"));
            Assert.That(ctx.Response.Text, Does.Contain("final answer after multiple mode changes"));

            _logger.Verify(
                l => l.AddError(
                    "[AgentReasoner_ExecuteAsync__MultipleModeChanges]",
                    It.Is<string>(msg => msg.Contains("Detected 2 successful mode-change tool calls"))),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_AllServerFinalTools_StaysOnServerAndEndsWithFinalLLMAnswer()
        {
            var ctx = BuildCtx(mode: "general");

            var firstResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall { CallId = "call-1", Name = "testing_ping_pong" }
                },
                Text = "calling ping tool"
            };

            var secondResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>(),
                Mode = null,
                Text = "final answer after server tool"
            };

            SetupLlmResponses(firstResponse, secondResponse);

            var updatedCall = new AgentToolCall
            {
                CallId = "call-1",
                Name = "testing_ping_pong",
                WasExecuted = true,
                RequiresClientExecution = false,
                ResultJson = "{\"reply\":\"pong\"}"
            };

            _toolExecutor
                .Setup(t => t.ExecuteServerToolAsync(
                    It.IsAny<AgentToolCall>(),
                    It.IsAny<AgentPipelineContext>()))
                .ReturnsAsync(InvokeResult<AgentToolCall>.Create(updatedCall));

            // No mode change in this test; bootstrap won't be called.
            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(ctx.Response, Is.Not.Null);
            Assert.That(ctx.Response.Text, Is.EqualTo("final answer after server tool"));
            Assert.That(ctx.Response.ToolCalls, Is.Null.Or.Empty);
        }

        [Test]
        public async Task ExecuteAsync_ClientFinalTool_StopsAndReturnsPendingToolCallToClient()
        {
            var ctx = BuildCtx(mode: "general");

            var firstResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall { CallId = "call-1", Name = "apply_file_bundle" }
                },
                Text = "calling apply_file_bundle"
            };

            SetupLlmResponses(firstResponse);

            var updatedCall = new AgentToolCall
            {
                CallId = "call-1",
                Name = "apply_file_bundle",
                WasExecuted = true,
                RequiresClientExecution = true,
                ResultJson = "{\"bundleId\":\"B-123\",\"files\":[\"foo.cs\"]}"
            };

            _toolExecutor
                .Setup(t => t.ExecuteServerToolAsync(
                    It.IsAny<AgentToolCall>(),
                    It.IsAny<AgentPipelineContext>()))
                .ReturnsAsync(InvokeResult<AgentToolCall>.Create(updatedCall));

            var result = await _sut.ExecuteAsync(ctx);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(ctx.Response, Is.Not.Null);

            Assert.That(ctx.Response.ToolCalls, Is.Not.Null);
            Assert.That(ctx.Response.ToolCalls.Count, Is.EqualTo(1));

            var returnedCall = ctx.Response.ToolCalls[0];
            Assert.That(returnedCall.CallId, Is.EqualTo("call-1"));
            Assert.That(returnedCall.RequiresClientExecution, Is.True);
            Assert.That(returnedCall.ResultJson, Does.Contain("bundleId"));
        }

        #endregion
    }
}
