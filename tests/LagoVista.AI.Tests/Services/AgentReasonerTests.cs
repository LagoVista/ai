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
using Newtonsoft.Json;
using NUnit.Framework;
using LagoVista.AI.Services.Tools;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentReasonerTests
    {
        private Mock<ILLMClient> _llmClient;
        private Mock<IAgentToolExecutor> _toolExecutor;
        private Mock<IAdminLogger> _logger;
        private AgentReasoner _sut;
        private Mock<IModeEntryBootstrapService> _bsExecutor;

        [SetUp]
        public void SetUp()
        {
            _llmClient = new Mock<ILLMClient>();
            _toolExecutor = new Mock<IAgentToolExecutor>();
            _logger = new Mock<IAdminLogger>();
            _bsExecutor = new Mock<IModeEntryBootstrapService>();
            _sut = new AgentReasoner(
                _llmClient.Object,
                _toolExecutor.Object,
                _logger.Object, 
                new Mock<IAgentStreamingContext>().Object,
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

        private static ConversationContext CreateConversationContext() =>
            new ConversationContext
            {
                Id = "conv-1",
                Name = "Test Conversation"
            };

        private static EntityHeader CreateOrg() =>
            new EntityHeader { Id = "org-1", Text = "Org 1" };

        private static EntityHeader CreateUser() =>
            new EntityHeader { Id = "user-1", Text = "User 1" };

        private static AgentExecuteRequest CreateRequest(string mode = "general")
        {
            return new AgentExecuteRequest
            {
                ConversationId = "conv-1",
                Mode = mode,
                Instruction = "do something",
                AgentContext = new EntityHeader { Id = "agent-1", Text = "Agent" },
                RagScopeFilter = new RagScopeFilter(),
                ActiveFiles = new List<ActiveFile>()
            };
        }

        #endregion

        #region Tests

        [Test]
        public async Task ExecuteAsync_NoToolCalls_SetsResponseModeToRequestMode_WhenResponseModeMissing()
        {
            var agentContext = CreateAgentContext();
            var conversationContext = CreateConversationContext();
            var org = CreateOrg();
            var user = CreateUser();
            var request = CreateRequest(mode: "general");

            var llmResponse = new AgentExecuteResponse
            {
                // Note: Mode is intentionally left null to exercise the override.
                Mode = null,
                ToolCalls = new List<AgentToolCall>(),
                Text = "final answer"
            };

            _llmClient
                .Setup(c => c.GetAnswerAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<ConversationContext>(),
                    It.IsAny<AgentExecuteRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(llmResponse));

            var result = await _sut.ExecuteAsync(
                agentContext,
                conversationContext,
                request,
                ragContextBlock: null,
                sessionId: "session-1",
                org: org,
                user: user,
                cancellationToken: CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);
            Assert.That(result.Result.Mode, Is.EqualTo("general"),
                "Response.Mode should default to request.Mode when missing.");
        }

        [Test]
        public async Task ExecuteAsync_ModeChangeTool_UpdatesRequestAndResponseMode_AndPrependsWelcomeMessage()
        {
            var agentContext = CreateAgentContext();
            var conversationContext = CreateConversationContext();
            var org = CreateOrg();
            var user = CreateUser();
            var request = CreateRequest(mode: "general");

            // First LLM call: asks for mode change via tool.
            var firstResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall
                    {
                        CallId = "call-1",
                        Name = ModeChangeTool.ToolName,
                        IsServerTool = true,
                        WasExecuted = false
                    }
                },
                Text = "calling mode change tool"
            };

            // Second LLM call: no tools, final answer.
            var secondResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>(),
                Mode = null, // Let Reasoner stamp this based on request.Mode
                Text = "final answer in new mode"
            };

            _llmClient
                .SetupSequence(c => c.GetAnswerAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<ConversationContext>(),
                    It.IsAny<AgentExecuteRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(firstResponse))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(secondResponse));

            // Tool executor: execute the mode-change tool, returning a successful result.
            var updatedCall = new AgentToolCall
            {
                CallId = "call-1",
                Name = ModeChangeTool.ToolName,
                IsServerTool = true,
                WasExecuted = true,
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
                    It.IsAny<AgentToolExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentToolCall>.Create(updatedCall));

            var result = await _sut.ExecuteAsync(
                agentContext,
                conversationContext,
                request,
                ragContextBlock: null,
                sessionId: "session-1",
                org: org,
                user: user,
                cancellationToken: CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            // Request mode should be updated in-flight.
            Assert.That(request.Mode, Is.EqualTo("ddr_authoring"));

            // Final response mode should reflect the new mode.
            Assert.That(result.Result.Mode, Is.EqualTo("ddr_authoring"));

            // Welcome message should be prepended to the final answer text.
            var text = result.Result.Text;
            Assert.That(text, Does.Contain("Welcome to DDR Authoring mode!"));
            Assert.That(text, Does.Contain("final answer in new mode"));
        }

        [Test]
        public async Task ExecuteAsync_MultipleModeChangeCalls_UsesLastModeAndLogsWarning()
        {
            var agentContext = CreateAgentContext();
            var conversationContext = CreateConversationContext();
            var org = CreateOrg();
            var user = CreateUser();
            var request = CreateRequest(mode: "general");

            // First LLM call: two mode-change tool calls.
            var firstResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall
                    {
                        CallId = "call-1",
                        Name = ModeChangeTool.ToolName,
                        IsServerTool = true,
                        WasExecuted = false
                    },
                    new AgentToolCall
                    {
                        CallId = "call-2",
                        Name = ModeChangeTool.ToolName,
                        IsServerTool = true,
                        WasExecuted = false
                    }
                },
                Text = "multiple mode changes"
            };

            // Second LLM call: no tools, final answer.
            var secondResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>(),
                Mode = null,
                Text = "final answer after multiple mode changes"
            };

            _llmClient
                .SetupSequence(c => c.GetAnswerAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<ConversationContext>(),
                    It.IsAny<AgentExecuteRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(firstResponse))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(secondResponse));

            // First mode-change result: switch to ddr_authoring.
            var firstUpdatedCall = new AgentToolCall
            {
                CallId = "call-1",
                Name = ModeChangeTool.ToolName,
                IsServerTool = true,
                WasExecuted = true,
                ResultJson = JsonConvert.SerializeObject(new
                {
                    success = true,
                    mode = "ddr_authoring",
                    branch = false,
                    reason = "First mode change."
                })
            };

            // Second mode-change result: switch to workflow_authoring.
            var secondUpdatedCall = new AgentToolCall
            {
                CallId = "call-2",
                Name = ModeChangeTool.ToolName,
                IsServerTool = true,
                WasExecuted = true,
                ResultJson = JsonConvert.SerializeObject(new
                {
                    success = true,
                    mode = "workflow_authoring",
                    branch = false,
                    reason = "Second mode change."
                })
            };

            _toolExecutor
                .SetupSequence(t => t.ExecuteServerToolAsync(
                    It.IsAny<AgentToolCall>(),
                    It.IsAny<AgentToolExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentToolCall>.Create(firstUpdatedCall))
                .ReturnsAsync(InvokeResult<AgentToolCall>.Create(secondUpdatedCall));

            var result = await _sut.ExecuteAsync(
                agentContext,
                conversationContext,
                request,
                ragContextBlock: null,
                sessionId: "session-1",
                org: org,
                user: user,
                cancellationToken: CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            // The last mode change should win.
            Assert.That(request.Mode, Is.EqualTo("workflow_authoring"));
            Assert.That(result.Result.Mode, Is.EqualTo("workflow_authoring"));

            var text = result.Result.Text;
            Assert.That(text, Does.Contain("Welcome to Workflow Authoring mode!"));
            Assert.That(text, Does.Contain("final answer after multiple mode changes"));

            // We should log a warning that multiple mode changes occurred.
            _logger.Verify(
                l => l.AddError(
                    "[AgentReasoner_ExecuteAsync__MultipleModeChanges]",
                    It.Is<string>(msg => msg.Contains("Detected 2 successful mode-change tool calls"))),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_AllServerFinalTools_StaysOnServerAndFeedsToolResultsBackToLLM()
        {
            var agentContext = CreateAgentContext();
            var conversationContext = CreateConversationContext();
            var org = CreateOrg();
            var user = CreateUser();
            var request = CreateRequest(mode: "general");

            // First LLM call: one server-final tool
            var firstResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall
                    {
                        CallId = "call-1",
                        Name = "testing_ping_pong"
                    }
                },
                Text = "calling ping tool"
            };

            // Second LLM call: no tools, final answer
            var secondResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>(),
                Mode = null,
                Text = "final answer after server tool"
            };

            _llmClient
                .SetupSequence(c => c.GetAnswerAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<ConversationContext>(),
                    It.IsAny<AgentExecuteRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(firstResponse))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(secondResponse));

            // Executor returns a server-final tool call (RequiresClientExecution=false)
            var updatedCall = new AgentToolCall
            {
                CallId = "call-1",
                Name = "testing_ping_pong",
                IsServerTool = true,
                WasExecuted = true,
                RequiresClientExecution = false,
                ResultJson = "{\"reply\":\"pong\"}"
            };

            _toolExecutor
                .Setup(t => t.ExecuteServerToolAsync(
                    It.IsAny<AgentToolCall>(),
                    It.IsAny<AgentToolExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentToolCall>.Create(updatedCall));

            var result = await _sut.ExecuteAsync(
                agentContext,
                conversationContext,
                request,
                ragContextBlock: null,
                sessionId: "session-1",
                org: org,
                user: user,
                cancellationToken: CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result.Text, Is.EqualTo("final answer after server tool"));

            // No client handoff: the final response should have no pending ToolCalls.
            Assert.That(result.Result.ToolCalls, Is.Null.Or.Empty);
        }

        [Test]
        public async Task ExecuteAsync_ClientFinalTool_StopsAndReturnsPendingToolCallToClient()
        {
            var agentContext = CreateAgentContext();
            var conversationContext = CreateConversationContext();
            var org = CreateOrg();
            var user = CreateUser();
            var request = CreateRequest(mode: "general");

            // LLM: asks to apply a file bundle (client-final tool)
            var firstResponse = new AgentExecuteResponse
            {
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall
                    {
                        CallId = "call-1",
                        Name = "apply_file_bundle"
                    }
                },
                Text = "calling apply_file_bundle"
            };

            _llmClient
                .Setup(c => c.GetAnswerAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<ConversationContext>(),
                    It.IsAny<AgentExecuteRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentExecuteResponse>.Create(firstResponse));

            // Executor: preflight succeeded, but final behavior must be done on client.
            var updatedCall = new AgentToolCall
            {
                CallId = "call-1",
                Name = "apply_file_bundle",
                IsServerTool = true,
                WasExecuted = true,
                RequiresClientExecution = true,
                ResultJson = "{\"bundleId\":\"B-123\",\"files\":[\"foo.cs\"]}"
            };

            _toolExecutor
                .Setup(t => t.ExecuteServerToolAsync(
                    It.IsAny<AgentToolCall>(),
                    It.IsAny<AgentToolExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(InvokeResult<AgentToolCall>.Create(updatedCall));

            var result = await _sut.ExecuteAsync(
                agentContext,
                conversationContext,
                request,
                ragContextBlock: null,
                sessionId: "session-1",
                org: org,
                user: user,
                cancellationToken: CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            // Reasoner should STOP and return the tool call to the client, not call LLM again.
            Assert.That(result.Result.ToolCalls, Is.Not.Null);
            Assert.That(result.Result.ToolCalls.Count, Is.EqualTo(1));

            var returnedCall = result.Result.ToolCalls[0];
            Assert.That(returnedCall.CallId, Is.EqualTo("call-1"));
            Assert.That(returnedCall.RequiresClientExecution, Is.True);
            Assert.That(returnedCall.IsServerTool, Is.True);       // produced by server
            Assert.That(returnedCall.ResultJson, Does.Contain("bundleId"));
        }

        #endregion
    }
}
