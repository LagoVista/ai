using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Services.OpenAI;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services.Pipeline
{
    [TestFixture]
    public class OpenAIStreamingResponseReaderTests
    {
        private static HttpResponseMessage CreateStreamingResponse(string sse)
        {
            var bytes = Encoding.UTF8.GetBytes(sse ?? string.Empty);
            var stream = new MemoryStream(bytes);

            var msg = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            };

            return msg;
        }

        private static OpenAIStreamingResponseReader CreateSut(
            Mock<ILLMEventPublisher> events,
            Mock<IAgentStreamingContext> streamingUi)
        {
            var logger = new AdminLogger(new ConsoleLogWriter());
            return new OpenAIStreamingResponseReader(logger, events.Object, streamingUi.Object);
        }

        private static (OpenAIStreamingResponseReader Sut, Mock<ILLMEventPublisher> Events, Mock<IAgentStreamingContext> Ui) CreateSutWithMocks()
        {
            var events = new Mock<ILLMEventPublisher>(MockBehavior.Loose);
            var ui = new Mock<IAgentStreamingContext>(MockBehavior.Loose);

            events
                .Setup(e => e.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            ui
                .Setup(s => s.AddPartialAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sut = CreateSut(events, ui);
            return (sut, events, ui);
        }

        private static void AssertSuccess(InvokeResult<string> result)
        {
            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(result.Result, Is.Not.Null);
        }

        private static void AssertFailure(InvokeResult<string> result)
        {
            Assert.That(result.Successful, Is.False, result.ErrorMessage);
        }

        private static void AssertContains(string haystack, string needle)
        {
            Assert.That(haystack, Is.Not.Null);
            Assert.That(haystack.Contains(needle), Is.True);
        }

        private static string CompletedPayload(string id)
        {
            return JsonConvert.SerializeObject(new
            {
                type = "response.completed",
                response = new { id = id, output = Array.Empty<object>() }
            });
        }

        private static string DeltaPayload_DeltaString(string text)
        {
            return JsonConvert.SerializeObject(new
            {
                type = "response.output_text.delta",
                delta = text
            });
        }

        private static string DeltaPayload_DeltaObject(string text)
        {
            return JsonConvert.SerializeObject(new
            {
                type = "response.output_text.delta",
                delta = new { text = text }
            });
        }

        private static string DeltaPayload_NestedOutputText(string text)
        {
            return JsonConvert.SerializeObject(new
            {
                type = "response.output_text.delta",
                output_text = new { delta = text }
            });
        }

        private static string DeltaPayload_NestedOutputTextObject(string text)
        {
            return JsonConvert.SerializeObject(new
            {
                type = "response.output_text.delta",
                output_text = new { delta = new { text = text } }
            });
        }

        [Test]
        public async Task ReadAsync_HappyPath_WithCompletedEventAndBlankLine_ReturnsInnerResponseJson()
        {
            // Arrange
            var ctx = CreateSutWithMocks();

            var sse = SseScript
                .Event("response.output_text.delta").DataJson(DeltaPayload_DeltaString("Hi")).Blank()
                .Event("response.completed").DataJson(CompletedPayload("resp_1")).Blank()
                .End();

            using var http = CreateStreamingResponse(sse);

            // Act
            var result = await ctx.Sut.ReadAsync(http, "sess_1", CancellationToken.None);

            // Assert
            AssertSuccess(result);
            AssertContains(result.Result, "\"id\":\"resp_1\"");
        }

        [Test]
        public async Task ReadAsync_CompletedEventAtEndWithoutBlankLine_ReturnsInnerResponseJson()
        {
            // Arrange
            var ctx = CreateSutWithMocks();

            var sse = SseScript
                .Event("response.output_text.delta").DataJson(DeltaPayload_DeltaString("Hi")).Blank()
                .Event("response.completed").DataJson(CompletedPayload("resp_2"))
                .End();

            using var http = CreateStreamingResponse(sse);

            // Act
            var result = await ctx.Sut.ReadAsync(http, "sess_1", CancellationToken.None);

            // Assert
            AssertSuccess(result);
            AssertContains(result.Result, "\"id\":\"resp_2\"");
        }

        [Test]
        public async Task ReadAsync_CompletedEventFollowedByDoneWithoutBlankLine_ReturnsInnerResponseJson()
        {
            // Arrange
            var ctx = CreateSutWithMocks();

            var sse = SseScript
                .Event("response.completed").DataJson(CompletedPayload("resp_3"))
                .DataRaw("[DONE]")
                .End();

            using var http = CreateStreamingResponse(sse);

            // Act
            var result = await ctx.Sut.ReadAsync(http, "sess_1", CancellationToken.None);

            // Assert
            AssertSuccess(result);
            AssertContains(result.Result, "\"id\":\"resp_3\"");
        }

        [Test]
        public async Task ReadAsync_StreamEndsImmediatelyAfterCompletedDataWithoutTrailingNewline_ReturnsInnerResponseJson()
        {
            // Arrange
            var ctx = CreateSutWithMocks();

            var sse = SseScript
                .Event("response.completed")
                .DataJson(CompletedPayload("resp_4"))
                .EndWithoutTrailingNewline();

            using var http = CreateStreamingResponse(sse);

            // Act
            var result = await ctx.Sut.ReadAsync(http, "sess_1", CancellationToken.None);

            // Assert
            AssertSuccess(result);
            AssertContains(result.Result, "\"id\":\"resp_4\"");
        }

        [Test]
        public async Task ReadAsync_DeltaString_PublishesDeltaAndStreamsPartial()
        {
            // Arrange
            var ctx = CreateSutWithMocks();

            var sse = SseScript
                .Event("response.output_text.delta").DataJson(DeltaPayload_DeltaString("Hello"))
                .Blank()
                .Event("response.completed").DataJson(CompletedPayload("resp_5"))
                .Blank()
                .End();

            using var http = CreateStreamingResponse(sse);

            // Act
            var result = await ctx.Sut.ReadAsync(http, "sess_1", CancellationToken.None);

            // Assert
            AssertSuccess(result);
            ctx.Ui.Verify(u => u.AddPartialAsync("Hello", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            ctx.Events.Verify(e => e.PublishAsync("sess_1", "LLMDelta", "in-progress", "Hello", null, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task ReadAsync_DeltaObjectText_PublishesDeltaAndStreamsPartial()
        {
            // Arrange
            var ctx = CreateSutWithMocks();

            var sse = SseScript
                .Event("response.output_text.delta").DataJson(DeltaPayload_DeltaObject("World"))
                .Blank()
                .Event("response.completed").DataJson(CompletedPayload("resp_6"))
                .Blank()
                .End();

            using var http = CreateStreamingResponse(sse);

            // Act
            var result = await ctx.Sut.ReadAsync(http, "sess_1", CancellationToken.None);

            // Assert
            AssertSuccess(result);
            ctx.Ui.Verify(u => u.AddPartialAsync("World", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            ctx.Events.Verify(e => e.PublishAsync("sess_1", "LLMDelta", "in-progress", "World", null, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task ReadAsync_NestedOutputTextDelta_PublishesDeltaAndStreamsPartial()
        {
            // Arrange
            var ctx = CreateSutWithMocks();

            var sse = SseScript
                .Event("response.output_text.delta").DataJson(DeltaPayload_NestedOutputText("Nested"))
                .Blank()
                .Event("response.completed").DataJson(CompletedPayload("resp_7"))
                .Blank()
                .End();

            using var http = CreateStreamingResponse(sse);

            // Act
            var result = await ctx.Sut.ReadAsync(http, "sess_1", CancellationToken.None);

            // Assert
            AssertSuccess(result);
            ctx.Ui.Verify(u => u.AddPartialAsync("Nested", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            ctx.Events.Verify(e => e.PublishAsync("sess_1", "LLMDelta", "in-progress", "Nested", null, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task ReadAsync_NestedOutputTextDeltaObject_PublishesDeltaAndStreamsPartial()
        {
            // Arrange
            var ctx = CreateSutWithMocks();

            var sse = SseScript
                .Event("response.output_text.delta").DataJson(DeltaPayload_NestedOutputTextObject("ObjNested"))
                .Blank()
                .Event("response.completed").DataJson(CompletedPayload("resp_8"))
                .Blank()
                .End();

            using var http = CreateStreamingResponse(sse);

            // Act
            var result = await ctx.Sut.ReadAsync(http, "sess_1", CancellationToken.None);

            // Assert
            AssertSuccess(result);
            ctx.Ui.Verify(u => u.AddPartialAsync("ObjNested", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            ctx.Events.Verify(e => e.PublishAsync("sess_1", "LLMDelta", "in-progress", "ObjNested", null, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task ReadAsync_MalformedDeltaJson_DoesNotThrow_AndStillReturnsCompleted()
        {
            // Arrange
            var ctx = CreateSutWithMocks();

            var sse = SseScript
                .Event("response.output_text.delta")
                .DataRaw("{ this is not json }")
                .Blank()
                .Event("response.completed")
                .DataJson(CompletedPayload("resp_9"))
                .Blank()
                .End();

            using var http = CreateStreamingResponse(sse);

            // Act
            var result = await ctx.Sut.ReadAsync(http, "sess_1", CancellationToken.None);

            // Assert
            AssertSuccess(result);
            AssertContains(result.Result, "\"id\":\"resp_9\"");
        }

        [Test]
        public async Task ReadAsync_CancelledToken_ReturnsAbortOrFailureWithoutThrowing()
        {
            // Arrange
            var ctx = CreateSutWithMocks();

            var sse = SseScript
                .Event("response.output_text.delta").DataJson(DeltaPayload_DeltaString("Hi"))
                .Blank()
                .Event("response.completed").DataJson(CompletedPayload("resp_10"))
                .Blank()
                .End();

            using var http = CreateStreamingResponse(sse);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await ctx.Sut.ReadAsync(http, "sess_1", cts.Token);

            // Assert
            AssertFailure(result);
        }

        /// <summary>
        /// Small DSL to build SSE payloads without noisy string concatenation.
        /// Keeps tests intention-revealing.
        /// </summary>
        private sealed class SseScriptBuilder
        {
            private readonly StringBuilder _sb = new StringBuilder();

            public SseScriptBuilder Event(string name)
            {
                _sb.Append("event: ").Append(name ?? string.Empty).Append("\n");
                return this;
            }

            public SseScriptBuilder DataJson(string json)
            {
                _sb.Append("data: ").Append(json ?? string.Empty).Append("\n");
                return this;
            }

            public SseScriptBuilder DataRaw(string data)
            {
                _sb.Append("data: ").Append(data ?? string.Empty).Append("\n");
                return this;
            }

            public SseScriptBuilder Blank()
            {
                _sb.Append("\n");
                return this;
            }

            public string End()
            {
                return _sb.ToString();
            }

            public string EndWithoutTrailingNewline()
            {
                var s = _sb.ToString();
                if (s.EndsWith("\n", StringComparison.Ordinal))
                {
                    // Remove exactly one trailing newline.
                    return s.Substring(0, s.Length - 1);
                }

                return s;
            }
        }

        private static class SseScript
        {
            public static SseScriptBuilder Event(string name) => new SseScriptBuilder().Event(name);
        }
    }
}
