using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.Core.Models;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests.Services
{
    [TestFixture]
    public class HttpLlmTitleDescriptionClientTests
    {
        private class TestOpenAISettings : IOpenAISettings
        {
            public string OpenAIUrl { get; set; } = "https://api.openai.com";
            public string OpenAIApiKey { get; set; } = "test-key";
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }

        private static HttpClient CreateHttpClient(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var handler = new StubHttpMessageHandler(_ =>
            {
                var msg = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseJson)
                };

                return msg;
            });

            return new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.openai.com")
            };
        }

        private static TitleDescriptionReviewRequest CreateSampleRequest()
        {
            return new TitleDescriptionReviewRequest
            {
                Kind = SummaryObjectKind.Model,
                SymbolName = "SampleModel",
                Title = "Original Title",
                Description = "Original description.",
                Help = "Original help.",
                Model = HttpLlmTitleDescriptionClient.DefaultModel,
                DomainKey = "AIAdmin",
                DomainName = "AI Admin",
                DomainDescription = "Admin domain for AI metadata.",
                // Fields intentionally left empty for this smoke test.
                Fields = new System.Collections.Generic.List<ModelFieldMetadata>()
            };
        }

        [Test]
        public async Task ReviewAsync_ParsesStructuredOutput_Success()
        {
            // Arrange
            var structuredJson = "{" +
                                  "\"title\":\"Refined Title\"," +
                                  "\"description\":\"Refined Description\"," +
                                  "\"help\":\"Refined Help\"," +
                                  "\"hasChanges\":true," +
                                  "\"requiresAttention\":false," +
                                  "\"warnings\":[\"First warning\",\"Second warning\"]" +
                                  "}";

            var responsesEnvelope = "{" +
                                     "\"id\":\"resp_123\"," +
                                     "\"output\":[{" +
                                     "\"role\":\"assistant\"," +
                                     "\"content\":[{" +
                                     "\"type\":\"output_json\"," +
                                     "\"json\":" + structuredJson +
                                     "}]}]}";

            var httpClient = CreateHttpClient(responsesEnvelope);

            var factory = new Mock<System.Net.Http.IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var logger = new Mock<IAdminLogger>();
            var settings = new TestOpenAISettings();

            var client = new HttpLlmTitleDescriptionClient(factory.Object, settings, logger.Object);
            var request = CreateSampleRequest();

            // Act
            var result = await client.ReviewAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Title, Is.EqualTo("Refined Title"));
            Assert.That(result.Description, Is.EqualTo("Refined Description"));
            Assert.That(result.Help, Is.EqualTo("Refined Help"));
            Assert.That(result.HasChanges, Is.True);
            Assert.That(result.RequiresAttention, Is.False);
            Assert.That(result.Warnings, Has.Count.EqualTo(2));
            Assert.That(result.Warnings[0], Is.EqualTo("First warning"));
        }

        [Test]
        public void ReviewAsync_NonSuccessStatus_Throws()
        {
            // Arrange
            var httpClient = CreateHttpClient("{\"error\":\"boom\"}", HttpStatusCode.BadRequest);

            var factory = new Mock<System.Net.Http.IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var logger = new Mock<IAdminLogger>();
            var settings = new TestOpenAISettings();

            var client = new HttpLlmTitleDescriptionClient(factory.Object, settings, logger.Object);
            var request = CreateSampleRequest();

            // Act + Assert
            Assert.ThrowsAsync<HttpRequestException>(async () => await client.ReviewAsync(request));
        }

        [Test]
        public void ReviewAsync_InvalidJson_Throws()
        {
            // Arrange
            var httpClient = CreateHttpClient("{not valid json}");

            var factory = new Mock<System.Net.Http.IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var logger = new Mock<IAdminLogger>();
            var settings = new TestOpenAISettings();

            var client = new HttpLlmTitleDescriptionClient(factory.Object, settings, logger.Object);
            var request = CreateSampleRequest();

            // Act + Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () => await client.ReviewAsync(request));
        }
    }
}
