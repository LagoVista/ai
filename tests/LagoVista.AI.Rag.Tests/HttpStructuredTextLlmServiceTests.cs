using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.Services;
using LagoVista.AI.Services;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    [TestFixture]
    public class HttpStructuredTextLlmServiceTests
    {
        private Mock<IHttpClientFactory> _httpClientFactory;
        private Mock<IOpenAISettings> _settings;
        private Mock<IAdminLogger> _logger;

        [SetUp]
        public void SetUp()
        {
            _httpClientFactory = new Mock<IHttpClientFactory>();
            _settings = new Mock<IOpenAISettings>();
            _logger = new Mock<IAdminLogger>();

            _settings.Setup(s => s.OpenAIUrl).Returns("https://api.example.com");
            _settings.Setup(s => s.OpenAIApiKey).Returns("test-key");
        }

        [Test]
        public async Task ExecuteAsync_String_Success_ReturnsResult()
        {
            // Arrange
            var responseJson = new JObject
            {
                ["output"] = new JArray
                {
                    new JObject
                    {
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["json"] = "\"Refined text result\""
                            }
                        }
                    }
                }
            }.ToString();

            var client = CreateHttpClient(responseJson, HttpStatusCode.OK);

            _httpClientFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(client);

            var svc = new HttpStructuredTextLlmService(
                _httpClientFactory.Object,
                _settings.Object,
                _logger.Object);

            // Act
            var result = await svc.ExecuteAsync("system", "input");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Successful, Is.True);
                Assert.That(result.Result, Is.EqualTo("Refined text result"));
            });
        }

        private sealed class TestDto
        {
            public string Title { get; set; }
            public int Count { get; set; }
        }

        [Test]
        public async Task ExecuteAsync_Object_Success_MapsToDto()
        {
            // Arrange
            var payload = new JObject
            {
                ["Title"] = "My Title",
                ["Count"] = 3
            };

            var responseJson = new JObject
            {
                ["output"] = new JArray
                {
                    new JObject
                    {
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["json"] = payload
                            }
                        }
                    }
                }
            }.ToString();

            var client = CreateHttpClient(responseJson, HttpStatusCode.OK);

            _httpClientFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(client);

            var svc = new HttpStructuredTextLlmService(
                _httpClientFactory.Object,
                _settings.Object,
                _logger.Object);

            // Act
            var result = await svc.ExecuteAsync<TestDto>("system", "input");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Successful, Is.True);
                Assert.That(result.Result, Is.Not.Null);
                Assert.That(result.Result.Title, Is.EqualTo("My Title"));
                Assert.That(result.Result.Count, Is.EqualTo(3));
            });
        }

        [Test]
        public async Task ExecuteAsync_InvalidPayload_ReturnsError()
        {
            // Arrange: malformed response structure (no output array)
            var responseJson = new JObject
            {
                ["unexpected"] = "value"
            }.ToString();

            var client = CreateHttpClient(responseJson, HttpStatusCode.OK);

            _httpClientFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(client);

            var svc = new HttpStructuredTextLlmService(
                _httpClientFactory.Object,
                _settings.Object,
                _logger.Object);

            // Act
            var result = await svc.ExecuteAsync<string>("system", "input");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Successful, Is.False);
                Assert.That(result.Result, Is.Null);
                Assert.That(result.Errors, Is.Not.Null);
                Assert.That(result.Errors.Count, Is.GreaterThan(0));
            });
        }

        private static HttpClient CreateHttpClient(string responseContent, HttpStatusCode statusCode)
        {
            var handler = new FakeMessageHandler(responseContent, statusCode);
            return new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.example.com")
            };
        }

        private sealed class FakeMessageHandler : HttpMessageHandler
        {
            private readonly string _content;
            private readonly HttpStatusCode _statusCode;

            public FakeMessageHandler(string content, HttpStatusCode statusCode)
            {
                _content = content;
                _statusCode = statusCode;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_content, Encoding.UTF8, "application/json")
                };

                return Task.FromResult(response);
            }
        }
    }
}
