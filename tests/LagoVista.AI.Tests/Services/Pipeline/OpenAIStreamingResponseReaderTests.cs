using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Services;
using LagoVista.IoT.Logging;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services.Pipeline
{
    [TestFixture]
    public class OpenAIStreamingResponseReaderTests
    {
        private static HttpResponseMessage CreateStreamingResponse(string sse)
        {
            var bytes = Encoding.UTF8.GetBytes(sse);
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

        [Test]
        public async Task ReadAsync_HappyPath_WithCompletedEventAndBlankLine_ReturnsInnerResponseJson()
        {
            // Arrange
            var events = new Mock<ILLMEventPublisher>(MockBehavior.Loose);
            var streamingUi = new Mock<IAgentStreamingContext>(MockBehavior.Loose);

            events
                .Setup(e => e.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            streamingUi
                .Setup(s => s.AddPartialAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sut = CreateSut(events, streamingUi);

            var sse =
                "event: response.output_text.delta\n" +
                "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Hi\"}\n" +
                "\n" +
                "event: response.completed\n" +
                "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"resp_1\",\"output\":[]}}\n" +
                "\n";

            using var http = CreateStreamingResponse(sse);

            // Act
            var result = await sut.ReadAsync(http, "sess_1", CancellationToken.None);

            // Assert
            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(result.Result, Is.Not.Null);
            Assert.That(result.Result, Does.Contain("\"id\":\"resp_1\""));
        }

        [Test]
        public async Task ReadAsync_BrokenPath_CompletedEventAtEndWithoutBlankLine_ShouldStillReturnInnerResponseJson_BUT_CURRENTLY_FAILS()
        {
            // Arrange
            var events = new Mock<ILLMEventPublisher>(MockBehavior.Loose);
            var streamingUi = new Mock<IAgentStreamingContext>(MockBehavior.Loose);

            events
                .Setup(e => e.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            streamingUi
                .Setup(s => s.AddPartialAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sut = CreateSut(events, streamingUi);

            // NOTE: No trailing blank line after the completed event.
            // The current implementation only flushes buffered data on a blank line,
            // so it misses response.completed when the stream ends immediately.
            var sse =
                "event: response.output_text.delta\n" +
                "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Hi\"}\n" +
                "\n" +
                "event: response.completed\n" +
                "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"resp_2\",\"output\":[]}}\n";

            using var http = CreateStreamingResponse(sse);

            // Act
            var result = await sut.ReadAsync(http, "sess_1", CancellationToken.None);

            // Assert (desired behavior; this should FAIL until we fix the reader)
            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(result.Result, Is.Not.Null);
            Assert.That(result.Result, Does.Contain("\"id\":\"resp_2\""));
        }
    }
}
