//// File: ./src/Tests/LagoVista.AI.Tests/OpenAIResponsesClientTests.cs
//using System;
//using System.Collections.Generic;
//using System.Net;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using LagoVista.AI.Interfaces;
//using LagoVista.AI.Models;
//using LagoVista.AI.Services;
//using LagoVista.Core;
//using LagoVista.Core.AI.Models;
//using LagoVista.Core.Interfaces;
//using LagoVista.Core.Models;
//using LagoVista.Core.Validation;
//using LagoVista.IoT.Logging.Loggers;
//using Moq;
//using NUnit.Framework;

//namespace LagoVista.AI.Tests
//{
//    [TestFixture]
//    public class OpenAIResponsesClientTests
//    {
//        /// <summary>
//        /// Simulates a successful SSE stream with two delta chunks and a response.completed event.
//        /// </summary>
//        private class FakeSseHandler : HttpMessageHandler
//        {
//            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
//            {
//                var sseBuilder = new StringBuilder();

//                sseBuilder.AppendLine("event: response.output_text.delta");
//                sseBuilder.AppendLine("data: {\"type\":\"response.output_text.delta\",\"delta\":{\"text\":\"Hello \"}}");
//                sseBuilder.AppendLine();

//                sseBuilder.AppendLine("event: response.output_text.delta");
//                sseBuilder.AppendLine("data: {\"type\":\"response.output_text.delta\",\"delta\":{\"text\":\"world!\"}}");
//                sseBuilder.AppendLine();

//                sseBuilder.AppendLine("event: response.completed");
//                sseBuilder.AppendLine(
//                    "data: {" +
//                    "\"type\":\"response.completed\"," +
//                    "\"response\":{" +
//                    "\"id\":\"resp_123\"," +
//                    "\"model\":\"gpt-5.1\"," +
//                    "\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5,\"total_tokens\":15}," +
//                    "\"output\":[{" +
//                    "\"type\":\"output_text\"," +
//                    "\"text\":\"Hello world!\"," +
//                    "\"finish_reason\":\"stop\"" +
//                    "}]" +
//                    "}}");
//                sseBuilder.AppendLine();

//                sseBuilder.AppendLine("data: [DONE]");
//                sseBuilder.AppendLine();

//                var response = new HttpResponseMessage(HttpStatusCode.OK)
//                {
//                    Output = new StringContent(sseBuilder.ToString(), Encoding.UTF8, "text/event-stream")
//                };

//                return Task.FromResult(response);
//            }
//        }

//        /// <summary>
//        /// SSE stream that never sends response.completed, only deltas and [DONE].
//        /// Exercises fallback path in ReadStreamingResponseAsync.
//        /// </summary>
//        private class FakeSseNoCompletedHandler : HttpMessageHandler
//        {
//            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
//            {
//                var sseBuilder = new StringBuilder();

//                sseBuilder.AppendLine("event: response.output_text.delta");
//                sseBuilder.AppendLine("data: {\"type\":\"response.output_text.delta\",\"delta\":{\"text\":\"Hello \"}}");
//                sseBuilder.AppendLine();

//                sseBuilder.AppendLine("event: response.output_text.delta");
//                sseBuilder.AppendLine("data: {\"type\":\"response.output_text.delta\",\"delta\":{\"text\":\"world!\"}}");
//                sseBuilder.AppendLine();

//                sseBuilder.AppendLine("data: [DONE]");
//                sseBuilder.AppendLine();

//                var response = new HttpResponseMessage(HttpStatusCode.OK)
//                {
//                    Output = new StringContent(sseBuilder.ToString(), Encoding.UTF8, "text/event-stream")
//                };

//                return Task.FromResult(response);
//            }
//        }

//        private sealed class FakeMetaDataProvider : IServerToolUsageMetadataProvider
//        {
//            public string GetToolUsageMetadata(string[] mode)
//            {
//                return "THIS IS HOW YOU USE THE TOOL!";
//            }
//        }

//        private sealed class FakeResponsesRequestBuilder : IResponsesRequestBuilder
//        {
//            public ResponsesApiRequest Build(AgentContextRole conversationContext, AgentExecuteRequest executeRequest, string ragContextBlock, string toolUsageBlock)
//            {
//                // Minimal shape; the tests' fake handlers don't care about request payload.
//                return new ResponsesApiRequest
//                {
//                    Model = conversationContext != null ? conversationContext.ModelName : "gpt-5.1",
//                    Input =  new List<ResponsesMessage>() { new ResponsesMessage() { Output = new List<ResponsesMessageContent>() { new ResponsesMessageContent() { Text = (executeRequest != null ? executeRequest.Instruction : "") } } } } ,
//                    Stream = true
//                };
//            }

//        }

//        private class TestOpenAIResponsesClient : OpenAIResponsesClient
//        {
//            private readonly HttpClient _httpClient;

//            public TestOpenAIResponsesClient(
//                IOpenAISettings settings,
//                IAdminLogger logger,
//                IServerToolUsageMetadataProvider usageProvider,
//                INotificationPublisher publisher,
//                IServerToolSchemaProvider schemaProvider,
//                IResponsesRequestBuilder requestBuilder,
//                IAgentStreamingContext streamingContext,
//                HttpClient httpClient)
//                : base(settings, logger, usageProvider, publisher, schemaProvider, requestBuilder, streamingContext)
//            {
//                _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
//            }

//            protected override HttpClient CreateHttpClient(string baseUrl, string apiKey)
//            {
//                _httpClient.BaseAddress = new Uri(baseUrl);
//                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
//                return _httpClient;
//            }
//        }

//        private static AgentContext CreateAgentContext()
//        {
//            return new AgentContext
//            {
//                Id = "agent-1",
//                Kind = "Test Agent",
//                LlmApiKey = "test-api-key",
//                AgentModes = new List<AgentMode>
//                {
//                    new AgentMode
//                    {
//                        Key = "TEST_MODE",
//                        // keep tools empty for tests
//                        AssociatedToolIds = new List<string>().ToArray()
//                    }
//                }
//            };
//        }

//        private static AgentContextRole CreateConversationContext()
//        {
//            return new AgentContextRole
//            {
//                Id = "conv-ctx-1",
//                Kind = "Test Conversation Context",
//                ModelName = "gpt-5.1",
//                SystemPrompts = new List<string> { "You are the Aptix Reasoner." },
//                Temperature = 0.7f
//            };
//        }

//        private static AgentExecuteRequest CreateExecuteRequest()
//        {
//            return new AgentExecuteRequest
//            {
//                SessionId = "conv-1",
//                ModeKey = "TEST_MODE",
//                Instruction = "Say hello to the world.",
//                AgentContext = new EntityHeader { Id = "agent-1", Text = "Test Agent" },
//                AgentContextRole = new EntityHeader { Id = "conv-ctx-1", Text = "Test Conversation Context" }
//            };
//        }

//        private static AgentPipelineContext BuildCtx(string sessionId)
//        {
//            return new AgentPipelineContext
//            {
//                CorrelationId = "corr-1",
//                Org = new EntityHeader { Id = "org-1", Text = "Org 1" },
//                User = new EntityHeader { Id = "user-1", Text = "User 1" },
//                RagContextBlock = string.Empty,
//                AgentContext = CreateAgentContext(),
//                AgentContextRole = CreateConversationContext(),
//                Request = CreateExecuteRequest(),
//                ThisTurn = new AgentSessionTurn { Id = "turn-1" }
//            };
//        }

//        [Test]
//        public async Task ExecuteAsync_SuccessfulStreamingResponse_SetsCtxResponse_AndPublishesEvents()
//        {
//            var settings = new Mock<IOpenAISettings>();
//            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

//            var logger = new Mock<IAdminLogger>();
//            var publisher = new Mock<INotificationPublisher>();
//            var schemaProvider = new Mock<IServerToolSchemaProvider>(MockBehavior.Loose);
//            var streaming = new Mock<IAgentStreamingContext>(MockBehavior.Loose);

//            var handler = new FakeSseHandler();
//            var httpClient = new HttpClient(handler);

//            var client = new TestOpenAIResponsesClient(
//                settings.Object,
//                logger.Object,
//                new FakeMetaDataProvider(),
//                publisher.Object,
//                schemaProvider.Object,
//                new FakeResponsesRequestBuilder(),
//                streaming.Object,
//                httpClient);

//            var ctx = BuildCtx("session-1");

//            var result = await client.ExecuteAsync(ctx);

//            Assert.That(result.Successful, Is.True, result.ErrorMessage);
//            Assert.That(ctx.Response, Is.Not.Null);

//            var response = ctx.Response;

//            Assert.That(response.Text, Is.EqualTo("Hello world!"));
//            Assert.That(response.ModelId, Is.EqualTo("gpt-5.1"));
//            Assert.That(response.ResponseContinuationId, Is.EqualTo("resp_123"));
//            Assert.That(response.PreviousTurnId, Is.EqualTo("resp_123"));
//            Assert.That(response.Usage.PromptTokens, Is.EqualTo(10));
//            Assert.That(response.Usage.CompletionTokens, Is.EqualTo(5));
//            Assert.That(response.Usage.TotalTokens, Is.EqualTo(15));
//            Assert.That(response.Kind, Is.EqualTo("ok"));

//            publisher.Verify(p => p.PublishAsync(
//                    Targets.WebSocket,
//                    Channels.Entity,
//                    "session-1",
//                    It.Is<AptixOrchestratorEvent>(e => e.Stage == "LLMDelta"),
//                    NotificationVerbosity.Diagnostics),
//                Times.AtLeastOnce);

//            publisher.Verify(p => p.PublishAsync(
//                    Targets.WebSocket,
//                    Channels.Entity,
//                    "session-1",
//                    It.Is<AptixOrchestratorEvent>(e => e.Stage == "LLMCompleted"),
//                    NotificationVerbosity.Diagnostics),
//                Times.Once);
//        }

//        [Test]
//        public async Task ExecuteAsync_HttpError_ReturnsError_AndLogs_AndPublishesFailed()
//        {
//            var handler = new DelegatingHandlerStub((request, token) =>
//            {
//                var resp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
//                {
//                    Output = new StringContent("{\"error\":\"boom\"}", Encoding.UTF8, "application/json")
//                };
//                return Task.FromResult(resp);
//            });

//            var settings = new Mock<IOpenAISettings>();
//            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

//            var logger = new Mock<IAdminLogger>();
//            var publisher = new Mock<INotificationPublisher>();
//            var schemaProvider = new Mock<IServerToolSchemaProvider>(MockBehavior.Loose);
//            var streaming = new Mock<IAgentStreamingContext>(MockBehavior.Loose);

//            var httpClient = new HttpClient(handler);

//            var client = new TestOpenAIResponsesClient(
//                settings.Object,
//                logger.Object,
//                new FakeMetaDataProvider(),
//                publisher.Object,
//                schemaProvider.Object,
//                new FakeResponsesRequestBuilder(),
//                streaming.Object,
//                httpClient);

//            var ctx = BuildCtx("session-err");

//            var result = await client.ExecuteAsync(ctx);

//            Assert.That(result.Successful, Is.False);
//            Assert.That(result.ErrorMessage, Does.Contain("LLM call failed with HTTP"));

//            logger.Verify(l => l.AddError("[OpenAIResponsesClient_ExecuteAsync__HTTP]", It.IsAny<string>()), Times.Once);

//            publisher.Verify(p => p.PublishAsync(
//                    Targets.WebSocket,
//                    Channels.Entity,
//                    "session-err",
//                    It.Is<AptixOrchestratorEvent>(e => e.Stage == "LLMFailed"),
//                    NotificationVerbosity.Diagnostics),
//                Times.Once);
//        }

//        [Test]
//        public async Task ExecuteAsync_EmptyInstruction_ReturnsErrorWithoutHttpCall()
//        {
//            var settings = new Mock<IOpenAISettings>();
//            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

//            var logger = new Mock<IAdminLogger>();
//            var publisher = new Mock<INotificationPublisher>();
//            var schemaProvider = new Mock<IServerToolSchemaProvider>(MockBehavior.Loose);
//            var streaming = new Mock<IAgentStreamingContext>(MockBehavior.Loose);

//            var httpCallCount = 0;
//            var handler = new DelegatingHandlerStub((request, token) =>
//            {
//                httpCallCount++;
//                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
//                {
//                    Output = new StringContent(string.Empty)
//                });
//            });

//            var httpClient = new HttpClient(handler);

//            var client = new TestOpenAIResponsesClient(
//                settings.Object,
//                logger.Object,
//                new FakeMetaDataProvider(),
//                publisher.Object,
//                schemaProvider.Object,
//                new FakeResponsesRequestBuilder(),
//                streaming.Object,
//                httpClient);

//            var ctx = BuildCtx("session-empty");
//            ctx.Request.Instruction = null;

//            var result = await client.ExecuteAsync(ctx);

//            Assert.That(result.Successful, Is.False);
//            Assert.That(result.ErrorMessage, Does.Contain("Instruction is required"));
//            Assert.That(httpCallCount, Is.EqualTo(0));
//        }

//        [Test]
//        public async Task ExecuteAsync_MissingOpenAIUrl_ReturnsErrorAndNoHttpCall()
//        {
//            var settings = new Mock<IOpenAISettings>();
//            settings.SetupGet(s => s.OpenAIUrl).Returns(string.Empty);

//            var logger = new Mock<IAdminLogger>();
//            var publisher = new Mock<INotificationPublisher>();
//            var schemaProvider = new Mock<IServerToolSchemaProvider>(MockBehavior.Loose);
//            var streaming = new Mock<IAgentStreamingContext>(MockBehavior.Loose);

//            var httpCallCount = 0;
//            var handler = new DelegatingHandlerStub((request, token) =>
//            {
//                httpCallCount++;
//                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
//            });

//            var httpClient = new HttpClient(handler);

//            var client = new TestOpenAIResponsesClient(
//                settings.Object,
//                logger.Object,
//                new FakeMetaDataProvider(),
//                publisher.Object,
//                schemaProvider.Object,
//                new FakeResponsesRequestBuilder(),
//                streaming.Object,
//                httpClient);

//            var ctx = BuildCtx("session-url");

//            var result = await client.ExecuteAsync(ctx);

//            Assert.That(result.Successful, Is.False);
//            Assert.That(result.ErrorMessage, Does.Contain("OpenAIUrl is not configured"));
//            Assert.That(httpCallCount, Is.EqualTo(0));
//        }

//        [Test]
//        public async Task ExecuteAsync_MissingApiKey_ReturnsErrorAndNoHttpCall()
//        {
//            var settings = new Mock<IOpenAISettings>();
//            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

//            var logger = new Mock<IAdminLogger>();
//            var publisher = new Mock<INotificationPublisher>();
//            var schemaProvider = new Mock<IServerToolSchemaProvider>(MockBehavior.Loose);
//            var streaming = new Mock<IAgentStreamingContext>(MockBehavior.Loose);

//            var httpCallCount = 0;
//            var handler = new DelegatingHandlerStub((request, token) =>
//            {
//                httpCallCount++;
//                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
//            });

//            var httpClient = new HttpClient(handler);

//            var client = new TestOpenAIResponsesClient(
//                settings.Object,
//                logger.Object,
//                new FakeMetaDataProvider(),
//                publisher.Object,
//                schemaProvider.Object,
//                new FakeResponsesRequestBuilder(),
//                streaming.Object,
//                httpClient);

//            var ctx = BuildCtx("session-key");
//            ctx.AgentContext.LlmApiKey = null;

//            var result = await client.ExecuteAsync(ctx);

//            Assert.That(result.Successful, Is.False);
//            Assert.That(result.ErrorMessage, Does.Contain("LlmApiKey is not configured"));
//            Assert.That(httpCallCount, Is.EqualTo(0));
//        }

//        [Test]
//        public async Task ExecuteAsync_SseWithoutCompletedEvent_UsesFallbackResponse()
//        {
//            var settings = new Mock<IOpenAISettings>();
//            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

//            var logger = new Mock<IAdminLogger>();
//            var publisher = new Mock<INotificationPublisher>();
//            var schemaProvider = new Mock<IServerToolSchemaProvider>(MockBehavior.Loose);
//            var streaming = new Mock<IAgentStreamingContext>(MockBehavior.Loose);

//            var handler = new FakeSseNoCompletedHandler();
//            var httpClient = new HttpClient(handler);

//            var client = new TestOpenAIResponsesClient(
//                settings.Object,
//                logger.Object,
//                new FakeMetaDataProvider(),
//                publisher.Object,
//                schemaProvider.Object,
//                new FakeResponsesRequestBuilder(),
//                streaming.Object,
//                httpClient);

//            var ctx = BuildCtx("session-fallback");

//            var result = await client.ExecuteAsync(ctx);

//            Assert.That(result.Successful, Is.True, result.ErrorMessage);
//            Assert.That(ctx.Response, Is.Not.Null);

//            Assert.That(ctx.Response.Text, Is.EqualTo("Hello world!"));
//            Assert.That(ctx.Response.Kind, Is.EqualTo("ok"));
//            Assert.That(ctx.Response.ResponseContinuationId, Is.Null);
//        }

//        [Test]
//        public async Task ExecuteAsync_NonStreamingResponse_ReturnsOkAndSetsCtxResponse()
//        {
//            var settings = new Mock<IOpenAISettings>();
//            settings.SetupGet(s => s.OpenAIUrl).Returns("https://api.openai.com");

//            var logger = new Mock<IAdminLogger>();
//            var publisher = new Mock<INotificationPublisher>();
//            var schemaProvider = new Mock<IServerToolSchemaProvider>(MockBehavior.Loose);
//            var streaming = new Mock<IAgentStreamingContext>(MockBehavior.Loose);

//            var handler = new DelegatingHandlerStub((request, token) =>
//            {
//                var json =
//                    "{" +
//                    "\"id\":\"resp_123\"," +
//                    "\"object\":\"response\"," +
//                    "\"model\":\"gpt-5.1\"," +
//                    "\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5,\"total_tokens\":15}," +
//                    "\"output\":[{" +
//                    "\"type\":\"output_text\"," +
//                    "\"text\":\"Hello world!\"," +
//                    "\"finish_reason\":\"stop\"" +
//                    "}]" +
//                    "}";

//                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
//                {
//                    Output = new StringContent(json, Encoding.UTF8, "application/json")
//                });
//            });

//            var httpClient = new HttpClient(handler);

//            var client = new TestOpenAIResponsesClient(
//                settings.Object,
//                logger.Object,
//                new FakeMetaDataProvider(),
//                publisher.Object,
//                schemaProvider.Object,
//                new FakeResponsesRequestBuilder(),
//                streaming.Object,
//                httpClient)
//            {
//                UseStreaming = false
//            };

//            var ctx = BuildCtx("session-nonstream");

//            var result = await client.ExecuteAsync(ctx);

//            Assert.That(result.Successful, Is.True, result.ErrorMessage);
//            Assert.That(ctx.Response, Is.Not.Null);

//            Assert.That(ctx.Response.Text, Is.EqualTo("Hello world!"));
//            Assert.That(ctx.Response.ModelId, Is.EqualTo("gpt-5.1"));
//            Assert.That(ctx.Response.Usage.PromptTokens, Is.EqualTo(10));
//            Assert.That(ctx.Response.Usage.CompletionTokens, Is.EqualTo(5));
//            Assert.That(ctx.Response.Usage.TotalTokens, Is.EqualTo(15));
//            Assert.That(ctx.Response.Kind, Is.EqualTo("ok"));

//            publisher.Verify(p => p.PublishAsync(
//                    Targets.WebSocket,
//                    Channels.Entity,
//                    "session-nonstream",
//                    It.Is<AptixOrchestratorEvent>(e => e.Stage == "LLMDelta"),
//                    NotificationVerbosity.Diagnostics),
//                Times.Never);

//            publisher.Verify(p => p.PublishAsync(
//                    Targets.WebSocket,
//                    Channels.Entity,
//                    "session-nonstream",
//                    It.Is<AptixOrchestratorEvent>(e => e.Stage == "LLMCompleted"),
//                    NotificationVerbosity.Diagnostics),
//                Times.Once);
//        }

//        private class DelegatingHandlerStub : HttpMessageHandler
//        {
//            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc;

//            public DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
//            {
//                _handlerFunc = handlerFunc ?? throw new ArgumentNullException(nameof(handlerFunc));
//            }

//            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
//            {
//                return _handlerFunc(request, cancellationToken);
//            }
//        }
//    }
//}
