using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Tools
{
    [TestFixture]
    public class CodeHashNormalizedToolTests
    {
        private Mock<IContentHashService> _contentHashServiceMock;
        private Mock<IAdminLogger> _loggerMock;
        private CodeHashNormalizedTool _tool;
        private AgentToolExecutionContext _context;

        [SetUp]
        public void SetUp()
        {
            _contentHashServiceMock = new Mock<IContentHashService>();
            _loggerMock = new Mock<IAdminLogger>();

            _tool = new CodeHashNormalizedTool(_contentHashServiceMock.Object, _loggerMock.Object);

            _context = new AgentToolExecutionContext
            {
                SessionId = "session-1",
                Request = new AgentExecuteRequest
                {
                    SessionId = "conv-1"
                }
            };
        }

        [Test]
        public async Task ExecuteAsync_ReturnsError_WhenArgumentsJsonIsNull()
        {
            var result = await _tool.ExecuteAsync(null, _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            var payload = JsonConvert.DeserializeObject<CodeHashNormalizedResponse>(result.Result);
            Assert.That(payload.Success, Is.False);
            Assert.That(payload.ErrorCode, Is.EqualTo("MISSING_ARGUMENTS"));
        }

        [Test]
        public async Task ExecuteAsync_ReturnsError_WhenArgumentsJsonIsWhitespace()
        {
            var result = await _tool.ExecuteAsync("   ", _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);
            var payload = JsonConvert.DeserializeObject<CodeHashNormalizedResponse>(result.Result);
            Assert.That(payload.Success, Is.False);
            Assert.That(payload.ErrorCode, Is.EqualTo("MISSING_ARGUMENTS"));
        }

        [Test]
        public async Task ExecuteAsync_ReturnsError_WhenContentIsMissing()
        {
            var args = new { docPath = "repo/file.cs", label = "pre" };
            var argsJson = JsonConvert.SerializeObject(args);

            var result = await _tool.ExecuteAsync(argsJson, _context, CancellationToken.None);

            var payload = JsonConvert.DeserializeObject<CodeHashNormalizedResponse>(result.Result);
            Assert.That(payload.Success, Is.False);
            Assert.That(payload.ErrorCode, Is.EqualTo("MISSING_CONTENT"));
        }

        [Test]
        public async Task ExecuteAsync_ReturnsError_OnDeserializationError()
        {
            // Invalid JSON string
            const string badJson = "{ content: 'missing_quotes }";

            var result = await _tool.ExecuteAsync(badJson, _context, CancellationToken.None);

            var payload = JsonConvert.DeserializeObject<CodeHashNormalizedResponse>(result.Result);
            Assert.That(payload.Success, Is.False);
            Assert.That(payload.ErrorCode, Is.EqualTo("DESERIALIZATION_ERROR"));

            _loggerMock.Verify(l => l.AddException("[code_hash_normalized_Deserialize]", It.IsAny<System.Exception>()), Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_ReturnsCancelled_WhenTokenIsCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await _tool.ExecuteAsync("{}", _context, cts.Token);

            var payload = JsonConvert.DeserializeObject<CodeHashNormalizedResponse>(result.Result);
            Assert.That(payload.Success, Is.False);
            Assert.That(payload.ErrorCode, Is.EqualTo("CANCELLED"));
        }

        [Test]
        public async Task ExecuteAsync_ReturnsHash_OnSuccess()
        {
            const string content = "public class Foo { }";
            const string expectedHash = "abc123hash";
            const string docPath = "repo/Foo.cs";
            const string label = "pre-patch";

            _contentHashServiceMock.Setup(s => s.ComputeTextHash(content)).Returns(expectedHash);

            var args = new
            {
                content,
                docPath,
                label
            };

            var argsJson = JsonConvert.SerializeObject(args);

            var result = await _tool.ExecuteAsync(argsJson, _context, CancellationToken.None);

            Assert.That(result.Successful, Is.True);

            var payload = JsonConvert.DeserializeObject<CodeHashNormalizedResponse>(result.Result);
            Assert.That(payload.Success, Is.True);
            Assert.That(payload.ErrorCode, Is.Null);
            Assert.That(payload.Hash, Is.EqualTo(expectedHash));
            Assert.That(payload.DocPath, Is.EqualTo(docPath));
            Assert.That(payload.Label, Is.EqualTo(label));

            var expectedLength = Encoding.UTF8.GetByteCount(content);
            Assert.That(payload.ContentLength, Is.EqualTo(expectedLength));

            _contentHashServiceMock.Verify(s => s.ComputeTextHash(content), Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_ReturnsUnexpectedError_OnHashException()
        {
            const string content = "class Bad {";

            _contentHashServiceMock
                .Setup(s => s.ComputeTextHash(content))
                .Throws(new System.Exception("boom"));

            var args = new { content };
            var argsJson = JsonConvert.SerializeObject(args);

            var result = await _tool.ExecuteAsync(argsJson, _context, CancellationToken.None);

            var payload = JsonConvert.DeserializeObject<CodeHashNormalizedResponse>(result.Result);
            Assert.That(payload.Success, Is.False);
            Assert.That(payload.ErrorCode, Is.EqualTo("UNEXPECTED_ERROR"));

            _loggerMock.Verify(l => l.AddException("[code_hash_normalized_Execute]", It.IsAny<System.Exception>()), Times.Once);
        }
    }
}
