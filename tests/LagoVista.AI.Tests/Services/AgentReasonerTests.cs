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

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class AgentReasonerTests
    {
        private Mock<ILLMClient> _llmClientMock;
        private Mock<IAgentToolExecutor> _toolExecutorMock;
        private Mock<IAdminLogger> _loggerMock;
        private AgentReasoner _reasoner;
        private AgentContext _agentContext;
        private ConversationContext _conversationContext;
        private AgentExecuteRequest _request;
        private EntityHeader _org;
        private EntityHeader _user;

        [SetUp]
        public void SetUp()
        {
            _llmClientMock = new Mock<ILLMClient>(MockBehavior.Strict);
            _toolExecutorMock = new Mock<IAgentToolExecutor>(MockBehavior.Strict);
            _loggerMock = new Mock<IAdminLogger>(MockBehavior.Loose);

            _reasoner = new AgentReasoner(_llmClientMock.Object, _toolExecutorMock.Object, _loggerMock.Object);

            _agentContext = new AgentContext { Id = "agent-1" };
            _conversationContext = new ConversationContext { Id = "conv-ctx-1" };
            _request = new AgentExecuteRequest
            {
                Mode = "ask",
                Instruction = "do something",
                ConversationId = "conv-1"
            };

            _org = EntityHeader.Create("org-1", "Org 1");
            _user = EntityHeader.Create("user-1", "User 1");
        }

        [Test]
        public async Task ExecuteAsync_NoToolCalls_ReturnsFirstLlmResponse()
        {
            var response = new AgentExecuteResponse
            {
                Kind = "ok",
                Text = "hello world",
                ToolCalls = new List<AgentToolCall>()
            };

            var llmResult = InvokeResult<AgentExecuteResponse>.Create(response);

            _llmClientMock
                .Setup(c => c.GetAnswerAsync(_agentContext, _conversationContext, _request, "RAG", "session-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(llmResult);

            var result = await _reasoner.ExecuteAsync(
                _agentContext,
                _conversationContext,
                _request,
                "RAG",
                "session-1",
                _org,
                _user,
                CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result.Text, Is.EqualTo("hello world"));

            _llmClientMock.Verify(c => c.GetAnswerAsync(_agentContext, _conversationContext, _request, "RAG", "session-1", It.IsAny<CancellationToken>()), Times.Once);
            _toolExecutorMock.Verify(e => e.ExecuteServerToolAsync(It.IsAny<AgentToolCall>(), It.IsAny<AgentToolExecutionContext>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task ExecuteAsync_ServerToolOnly_ExecutesTool_AndCallsLlmTwice()
        {
            var firstResponse = new AgentExecuteResponse
            {
                Kind = "tool",
                Text = null,
                ResponseContinuationId = "resp-1",
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall
                    {
                        CallId = "call-1",
                        Name = "server-tool",
                        ArgumentsJson = "{\"msg\":\"hi\"}"
                    }
                }
            };

            var secondResponse = new AgentExecuteResponse
            {
                Kind = "ok",
                Text = "final answer",
                ToolCalls = new List<AgentToolCall>()
            };

            var firstResult = InvokeResult<AgentExecuteResponse>.Create(firstResponse);
            var secondResult = InvokeResult<AgentExecuteResponse>.Create(secondResponse);

            _llmClientMock
                .SetupSequence(c => c.GetAnswerAsync(_agentContext, _conversationContext, _request, "RAG", "session-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstResult)
                .ReturnsAsync(secondResult);

            _toolExecutorMock
                .Setup(e => e.ExecuteServerToolAsync(
                    It.Is<AgentToolCall>(c => c.Name == "server-tool"),
                    It.IsAny<AgentToolExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AgentToolCall call, 
                               AgentToolExecutionContext ctx, 
                               CancellationToken ct) =>
                {
                    call.IsServerTool = true;
                    call.WasExecuted = true;
                    call.ResultJson = "{\"handledBy\":\"server\"}";
                    return InvokeResult<AgentToolCall>.Create(call);
                });

            var result = await _reasoner.ExecuteAsync(
                _agentContext,
                _conversationContext,
                _request,
                "RAG",
                "session-1",
                _org,
                _user,
                CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result.Text, Is.EqualTo("final answer"));

            _llmClientMock.Verify(c => c.GetAnswerAsync(_agentContext, _conversationContext, _request, "RAG", "session-1", It.IsAny<CancellationToken>()), Times.Exactly(2));
            _toolExecutorMock.Verify(e => e.ExecuteServerToolAsync(It.Is<AgentToolCall>(c => c.Name == "server-tool"), It.IsAny<AgentToolExecutionContext>(), It.IsAny<CancellationToken>()), Times.Once);

            Assert.That(_request.ToolResults, Is.Not.Null);
            Assert.That(_request.ToolResults.Count, Is.EqualTo(1));
            Assert.That(_request.ToolResults[0].Name, Is.EqualTo("server-tool"));
            Assert.That(_request.ToolResultsJson, Is.Not.Null.And.Not.Empty);

            var parsed = JsonConvert.DeserializeObject<List<AgentToolCall>>(_request.ToolResultsJson);
            Assert.That(parsed.Count, Is.EqualTo(1));
            Assert.That(parsed[0].Name, Is.EqualTo("server-tool"));
        }

        [Test]
        public async Task ExecuteAsync_MixedServerAndClientTools_StopsAfterFirstIteration()
        {
            var response = new AgentExecuteResponse
            {
                Kind = "tool",
                Text = null,
                ToolCalls = new List<AgentToolCall>
                {
                    new AgentToolCall
                    {
                        CallId = "call-1",
                        Name = "server-tool",
                        ArgumentsJson = "{}"
                    },
                    new AgentToolCall
                    {
                        CallId = "call-2",
                        Name = "client-tool",
                        ArgumentsJson = "{}"
                    }
                }
            };

            var llmResult = InvokeResult<AgentExecuteResponse>.Create(response);

            _llmClientMock
                .Setup(c => c.GetAnswerAsync(_agentContext, _conversationContext, _request, "RAG", "session-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(llmResult);

            _toolExecutorMock
                .Setup(e => e.ExecuteServerToolAsync(
                    It.Is<AgentToolCall>(c => c.Name == "server-tool"),
                    It.IsAny<AgentToolExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AgentToolCall call, AgentToolExecutionContext ctx, CancellationToken ct) =>
                 {
                    call.IsServerTool = true;
                    call.WasExecuted = true;
                    call.ResultJson = "{\"handledBy\":\"server\"}";
                     return InvokeResult<AgentToolCall>.Create(call);
                 });

            _toolExecutorMock
                .Setup(e => e.ExecuteServerToolAsync(
                    It.Is<AgentToolCall>(c => c.Name == "client-tool"),
                    It.IsAny<AgentToolExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AgentToolCall call, AgentToolExecutionContext ctx, CancellationToken ct) =>
                {
                    return InvokeResult<AgentToolCall>.Create(call);
                });
                

            var result = await _reasoner.ExecuteAsync(
                _agentContext,
                _conversationContext,
                _request,
                "RAG",
                "session-1",
                _org,
                _user,
                CancellationToken.None);

            Assert.That(result.Successful, Is.True);

            _llmClientMock.Verify(c => c.GetAnswerAsync(_agentContext, _conversationContext, _request, "RAG", "session-1", It.IsAny<CancellationToken>()), Times.Once);
            _toolExecutorMock.Verify(e => e.ExecuteServerToolAsync(It.IsAny<AgentToolCall>(), It.IsAny<AgentToolExecutionContext>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            Assert.That(result.Result.ToolCalls, Is.Not.Null);
            Assert.That(result.Result.ToolCalls.Count, Is.EqualTo(2));

            var serverCall = result.Result.ToolCalls.Find(c => c.Name == "server-tool");
            var clientCall = result.Result.ToolCalls.Find(c => c.Name == "client-tool");

            Assert.That(serverCall, Is.Not.Null);
            Assert.That(serverCall.IsServerTool, Is.True);
            Assert.That(serverCall.WasExecuted, Is.True);
            Assert.That(serverCall.ResultJson, Is.EqualTo("{\"handledBy\":\"server\"}"));

            Assert.That(clientCall, Is.Not.Null);
            Assert.That(clientCall.IsServerTool, Is.False);
            Assert.That(clientCall.WasExecuted, Is.False);
            Assert.That(clientCall.ResultJson, Is.Null);
        }

        [Test]
        public async Task ExecuteAsync_LlmError_PropagatesError()
        {
            var llmError = InvokeResult<AgentExecuteResponse>.FromError("LLM error");

            _llmClientMock
                .Setup(c => c.GetAnswerAsync(_agentContext, _conversationContext, _request, "RAG", "session-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(llmError);

            var result = await _reasoner.ExecuteAsync(
                _agentContext,
                _conversationContext,
                _request,
                "RAG",
                "session-1",
                _org,
                _user,
                CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("LLM error"));

            _llmClientMock.Verify(c => c.GetAnswerAsync(_agentContext, _conversationContext, _request, "RAG", "session-1", It.IsAny<CancellationToken>()), Times.Once);
            _toolExecutorMock.Verify(e => e.ExecuteServerToolAsync(It.IsAny<AgentToolCall>(), It.IsAny<AgentToolExecutionContext>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
