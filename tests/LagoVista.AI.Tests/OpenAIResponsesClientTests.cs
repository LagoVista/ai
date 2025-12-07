using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Tests
{
    [TestFixture]
    public class OpenAIResponsesClientTests
    {
        /// <summary>
        /// Simulates a successful SSE stream with two delta chunks and a response.completed event.
        /// </summary>
        private class FakeSseHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var sseBuilder = new StringBuilder();

                sseBuilder.AppendLine("event: response.output_text.delta");
                sseBuilder.AppendLine("data: {\"type\":\"response.output_text.delta\",\"delta\":{\"text\":\"Hello \"}}\n");
                sseBuilder.AppendLine();

                sseBuilder.AppendLine("event: response.output_text.delta");
                sseBuilder.AppendLine("data: {\"type\":\"response.output_text.delta\",\"delta\":{\"text\":\"world!\"}}\n");
                sseBuilder.AppendLine();

                sseBuilder.AppendLine("event: response.completed");
                sseBuilder.AppendLine(
                    "data: {" +
                    "\"type\":\"response.completed\"," +
                    "\"response\":{" +
                    "\"id\":\"resp_123\"," +
                    "\"model\":\"gpt-5.1\"," +
                    "\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5,\"total_tokens\":15}," +
                    "\"output\":[{" +
                    "\"type\":\"output_text\"," +
                    "\"text\":\"Hello world!\"," +
                    "\"finish_reason\":\"stop\"" +
                    "}]" +
                    "}}\n");
                sseBuilder.AppendLine();

                sseBuilder.AppendLine("data: [DONE]");
                sseBuilder.AppendLine();

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sseBuilder.ToString(), Encoding.UTF8, "text/event-stream")
                };

                return Task.FromResult(response);
            }
        }

        /// <summary>
        /// SSE stream that never sends response.completed, only deltas and [DONE].
        /// This exercises the fallback path in ReadStreamingResponseAsync.
        /// </summary>
        private class FakeSseNoCompletedHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var sseBuilder = new StringBuilder();

                sseBuilder.AppendLine("event: response.output_text.delta");
                sseBuilder.AppendLine("data: {\"type\":\"response.output_text.delta\",\"delta\":{\"text\":\"Hello \"}}\n");
                sseBuilder.AppendLine();

                sseBuilder.AppendLine("event: response.output_text.delta");
                sseBuilder.AppendLine("data: {\"type\":\"response.output_text.delta\",\"delta\":{\"text\":\"world!\"}}\n");
                sseBuilder.AppendLine();

                sseBuilder.AppendLine("data: [DONE]");
                sseBuilder.AppendLine();

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sseBuilder.ToString(), Encoding.UTF8, "text/event-stream")
                };

                return Task.FromResult(response);
            }
        }

        private class TestOpenAIResponsesClient : OpenAIResponsesClient
        {
            private readonly HttpClient _httpClient;

            public TestOpenAIResponsesClient(IOpenAISettings settings, IAdminLogger logger, INotificationPublisher publisher, IAgentModeCatalogService catalogService, HttpClient httpClient)
                : base(settings, logger, new FakeMetaDataProvider(), publisher, catalogService)
            {
                _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            }

            protected override HttpClient CreateHttpClient(string baseUrl, string apiKey)
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                return _httpClient;
            }
        }

        private AgentContext CreateAgentContext()
        {
            return new AgentContext
            {
                Id = "agent-1",
                Name = "Test Agent",
                LlmApiKey = "test-api-key"
            };
        }

        private ConversationContext CreateConversationContext()
        {
            return new ConversationContext
            {
                Id = "conv-ctx-1",
                Name = "Test Conversation Context",
                ModelName = "gpt-5.1",
                SystemPrompts = new List<string>() { "You are the Aptix Reasoner." },
                Temperature = 0.7f
            };
        }

        private AgentExecuteRequest CreateExecuteRequest()
        {
            return new AgentExecuteRequest
            {
                ConversationId = "conv-1",
                Mode = "TEST_MODE",
                Instruction = "Say hello to the world.",
                AgentContext = new Core.Models.EntityHeader { Id = "agent-1", Text = "Test Agent" },
                ConversationContext = new Core.Models.EntityHeader { Id = "conv-ctx-1", Text = "Test Conversation Context" }
            };
        }

        private class FakeMetaDataProvider : IServerToolUsageMetadataProvider
        {
            public string GetToolUsageMetadata(string mode)
            {
                return "THIS IS HOW YOU USE THE TOOL!";
            }
        }

        [Test]
        public async Task GetAnswerAsync_SuccessfulStreamingResponse_ReturnsOkResultWithText()
        {
            var settings = new Mock<IOpenAISettings>();
            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

            var logger = new Mock<IAdminLogger>();
            var publisher = new Mock<INotificationPublisher>();
            var catalogService = new Mock<IAgentModeCatalogService>();
            var handler = new FakeSseHandler();
            var httpClient = new HttpClient(handler);

            catalogService.Setup(cs => cs.BuildSystemPrompt(It.IsAny<string>())).Returns("YOU ARE IN A GREAT MODE!");


            var client = new TestOpenAIResponsesClient(settings.Object, logger.Object, publisher.Object, catalogService.Object, httpClient);

            var agentContext = CreateAgentContext();
            var conversationContext = CreateConversationContext();
            var executeRequest = CreateExecuteRequest();

            var result = await client.GetAnswerAsync(agentContext, conversationContext, executeRequest, string.Empty, "session-1", CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            var response = result.Result;
            Assert.That(response.Text, Is.EqualTo("Hello world!"));
            Assert.That(response.ModelId, Is.EqualTo("gpt-5.1"));
            Assert.That(response.ResponseContinuationId, Is.EqualTo("resp_123"));
            Assert.That(response.TurnId, Is.EqualTo("resp_123"));
            Assert.That(response.Usage.PromptTokens, Is.EqualTo(10));
            Assert.That(response.Usage.CompletionTokens, Is.EqualTo(5));
            Assert.That(response.Usage.TotalTokens, Is.EqualTo(15));
            Assert.That(response.Kind, Is.EqualTo("ok"));

            publisher.Verify(p => p.PublishAsync(
                Targets.WebSocket,
                Channels.Entity,
                "session-1",
                It.Is<AptixOrchestratorEvent>(e => e.Stage == "LLMDelta"),
                NotificationVerbosity.Diagnostics),
                Times.AtLeastOnce);

            publisher.Verify(p => p.PublishAsync(
                Targets.WebSocket,
                Channels.Entity,
                "session-1",
                It.Is<AptixOrchestratorEvent>(e => e.Stage == "LLMCompleted"),
                NotificationVerbosity.Diagnostics),
                Times.Once);
        }

        [Test]
        public async Task GetAnswerAsync_HttpError_ReturnsErrorResultAndLogs()
        {
            var handler = new DelegatingHandlerStub((request, token) =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"error\":\"boom\"}", Encoding.UTF8, "application/json")
                };
                return Task.FromResult(resp);
            });

            var settings = new Mock<IOpenAISettings>();
            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

            var logger = new Mock<IAdminLogger>();
            var publisher = new Mock<INotificationPublisher>();
            var catalogService = new Mock<IAgentModeCatalogService>();
            catalogService.Setup(cs => cs.BuildSystemPrompt(It.IsAny<string>())).Returns("YOU ARE IN A GREAT MODE!");


            var httpClient = new HttpClient(handler);
            var client = new TestOpenAIResponsesClient(settings.Object, logger.Object, publisher.Object, catalogService.Object, httpClient);

            var agentContext = CreateAgentContext();
            var conversationContext = CreateConversationContext();
            var executeRequest = CreateExecuteRequest();

            var result = await client.GetAnswerAsync(agentContext, conversationContext, executeRequest, string.Empty, "session-err", CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("LLM call failed with HTTP"));

            logger.Verify(l => l.AddError("[OpenAIResponsesClient_GetAnswerAsync__HTTP]", It.IsAny<string>()), Times.Once);
            publisher.Verify(p => p.PublishAsync(
                Targets.WebSocket,
                Channels.Entity,
                "session-err",
                It.Is<AptixOrchestratorEvent>(e => e.Stage == "LLMFailed"),
                NotificationVerbosity.Diagnostics),
                Times.Once);
        }

        [Test]
        public async Task GetAnswerAsync_EmptyInstruction_ReturnsErrorWithoutHttpCall()
        {
            var settings = new Mock<IOpenAISettings>();
            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

            var logger = new Mock<IAdminLogger>();
            var publisher = new Mock<INotificationPublisher>();
            var catalogService = new Mock<IAgentModeCatalogService>();
            catalogService.Setup(cs => cs.BuildSystemPrompt(It.IsAny<string>())).Returns("YOU ARE IN A GREAT MODE!");


            var httpCallCount = 0;
            var handler = new DelegatingHandlerStub((request, token) =>
            {
                httpCallCount++;
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Empty)
                };
                return Task.FromResult(resp);
            });

            var httpClient = new HttpClient(handler);
            var client = new TestOpenAIResponsesClient(settings.Object, logger.Object, publisher.Object, catalogService.Object, httpClient);

            var agentContext = CreateAgentContext();
            var conversationContext = CreateConversationContext();
            var executeRequest = CreateExecuteRequest();
            executeRequest.Instruction = null;

            var result = await client.GetAnswerAsync(agentContext, conversationContext, executeRequest, string.Empty, "session-empty", CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Instruction is required"));
            Assert.That(httpCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetAnswerAsync_MissingOpenAIUrl_ReturnsErrorAndNoHttpCall()
        {
            var settings = new Mock<IOpenAISettings>();
            settings.SetupGet(s => s.OpenAIUrl).Returns(string.Empty);

            var logger = new Mock<IAdminLogger>();
            var publisher = new Mock<INotificationPublisher>();
            var catalogService = new Mock<IAgentModeCatalogService>();
            catalogService.Setup(cs => cs.BuildSystemPrompt(It.IsAny<string>())).Returns("YOU ARE IN A GREAT MODE!");


            var httpCallCount = 0;
            var handler = new DelegatingHandlerStub((request, token) =>
            {
                httpCallCount++;
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Empty)
                };
                return Task.FromResult(resp);
            });

            var httpClient = new HttpClient(handler);
            var client = new TestOpenAIResponsesClient(settings.Object, logger.Object, publisher.Object, catalogService.Object, httpClient);

            var agentContext = CreateAgentContext();
            var conversationContext = CreateConversationContext();
            var executeRequest = CreateExecuteRequest();

            var result = await client.GetAnswerAsync(agentContext, conversationContext, executeRequest, string.Empty, "session-url", CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("OpenAIUrl is not configured"));
            Assert.That(httpCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetAnswerAsync_MissingApiKey_ReturnsErrorAndNoHttpCall()
        {
            var settings = new Mock<IOpenAISettings>();
            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

            var logger = new Mock<IAdminLogger>();
            var publisher = new Mock<INotificationPublisher>();
            var catalogService = new Mock<IAgentModeCatalogService>();
            catalogService.Setup(cs => cs.BuildSystemPrompt(It.IsAny<string>())).Returns("YOU ARE IN A GREAT MODE!");


            var httpCallCount = 0;
            var handler = new DelegatingHandlerStub((request, token) =>
            {
                httpCallCount++;
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Empty)
                };
                return Task.FromResult(resp);
            });

            var httpClient = new HttpClient(handler);
            var client = new TestOpenAIResponsesClient(settings.Object, logger.Object, publisher.Object, catalogService.Object, httpClient);

            var agentContext = CreateAgentContext();
            agentContext.LlmApiKey = null;

            var conversationContext = CreateConversationContext();
            var executeRequest = CreateExecuteRequest();

            var result = await client.GetAnswerAsync(agentContext, conversationContext, executeRequest, string.Empty,  "session-key", CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("LlmApiKey is not configured"));
            Assert.That(httpCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetAnswerAsync_SseWithoutCompletedEvent_UsesFallbackResponse()
        {
            var settings = new Mock<IOpenAISettings>();
            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

            var logger = new Mock<IAdminLogger>();
            var publisher = new Mock<INotificationPublisher>();
            var catalogService = new Mock<IAgentModeCatalogService>();
            catalogService.Setup(cs => cs.BuildSystemPrompt(It.IsAny<string>())).Returns("YOU ARE IN A GREAT MODE!");


            var handler = new FakeSseNoCompletedHandler();
            var httpClient = new HttpClient(handler);

            var client = new TestOpenAIResponsesClient(settings.Object, logger.Object, publisher.Object, catalogService.Object, httpClient);

            var agentContext = CreateAgentContext();
            var conversationContext = CreateConversationContext();
            var executeRequest = CreateExecuteRequest();

            var result = await client.GetAnswerAsync(agentContext, conversationContext, executeRequest, string.Empty, "session-fallback", CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            var response = result.Result;
            Assert.That(response.Text, Is.EqualTo("Hello world!"));
            Assert.That(response.Kind, Is.EqualTo("ok"));
            Assert.That(response.ResponseContinuationId, Is.Null);
        }

        [Test]
        public async Task GetAnswerAsync_TaskCanceledExceptionFromHandler_ReportedAsUnexpectedException()
        {
            var settings = new Mock<IOpenAISettings>();
            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

            var logger = new Mock<IAdminLogger>();
            var publisher = new Mock<INotificationPublisher>();
            var catalogService = new Mock<IAgentModeCatalogService>();
            catalogService.Setup(cs => cs.BuildSystemPrompt(It.IsAny<string>())).Returns("YOU ARE IN A GREAT MODE!");  

            var handler = new DelegatingHandlerStub((request, token) =>
            {
                throw new TaskCanceledException("cancelled");
            });

            var httpClient = new HttpClient(handler);
            var client = new TestOpenAIResponsesClient(settings.Object, logger.Object, publisher.Object, catalogService.Object, httpClient);

            var agentContext = CreateAgentContext();
            var conversationContext = CreateConversationContext();
            var executeRequest = CreateExecuteRequest();

            var cts = new CancellationTokenSource();
            var token = cts.Token;

            var result = await client.GetAnswerAsync(agentContext, conversationContext, executeRequest, string.Empty, "session-cancel", token);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Unexpected exception during LLM call."));

            publisher.Verify(p => p.PublishAsync(
                Targets.WebSocket,
                Channels.Entity,
                "session-cancel",
                It.Is<AptixOrchestratorEvent>(e => e.Stage == "LLMFailed"),
                NotificationVerbosity.Diagnostics),
                Times.Once);
        }

        private class DelegatingHandlerStub : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc;

            public DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
            {
                _handlerFunc = handlerFunc ?? throw new ArgumentNullException(nameof(handlerFunc));
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _handlerFunc(request, cancellationToken);
            }
        }
    }
}
