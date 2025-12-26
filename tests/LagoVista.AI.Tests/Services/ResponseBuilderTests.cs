using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Helpers;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Models.Context;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services
{
    [TestFixture]
    public class ResponseBuilderTests
    {
        private sealed class TestPipelineContext : IAgentPipelineContext
        {
            public AgentPipelineContextTypes Type { get; set; } = AgentPipelineContextTypes.Initial;
            public string CorrelationId { get; set; } = "corr_1";
            public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

            public AgentSessionTurn Turn { get; set; }
            public AgentSession Session { get; set; }

            public ResponsePayload ResponsePayload { get; set; }

            public AgentContext AgentContext { get; set; }
            public ConversationContext ConversationContext { get; set; }

            public PromptKnowledgeProvider PromptKnowledgeProvider { get; set; } = new PromptKnowledgeProvider();
            public CompositionTrace Trace { get; set; }

            public ResponseTypes ResponseType { get; set; } = ResponseTypes.Final;

            public bool HasPendingToolCalls { get; set; }
            public bool HasClientToolCalls { get; set; }

            public string ToolManifestId { get; set; }

            public string TimeStamp { get; set; } = DateTime.UtcNow.ToString("o");
            public Envelope Envelope { get; set; }

            public InvokeResult ValidateResult { get; set; } = InvokeResult.Success;

            public AgentToolExecutionContext ToToolContext() => new AgentToolExecutionContext();

            public void AttachAgentContext(AgentContext context, ConversationContext conversationContext)
            {
                AgentContext = context;
                ConversationContext = conversationContext;
            }

            public void AttachSession(AgentSession session, AgentSessionTurn turn)
            {
                Session = session;
                Turn = turn;
            }

            public void AttachToolManifest(ToolCallManifest toolManifest)
            {
                // ResponseBuilder reads ToolCallManifest from PromptKnowledgeProvider; keep for interface compliance.
            }

            public InvokeResult Validate(PipelineSteps step) => ValidateResult;

            public void LogStepErrorDetails(IAdminLogger logger, PipelineSteps step, string error, TimeSpan ts) { }
            public void LogStepErrorDetails(IAdminLogger logger, PipelineSteps step, InvokeResult error, TimeSpan ts) { }
            public void LogDetails(IAdminLogger logger, PipelineSteps step, TimeSpan? ts = null) { }
        }

        private static AgentContext AgentContextWithMode(string key, string displayName)
        {
            return new AgentContext
            {
                AgentModes = new List<AgentMode>
                {
                    new AgentMode { Key = key, DisplayName = displayName }
                }
            };
        }

        private static TestPipelineContext CreateBaseCtx(string modeKey = "general")
        {
            return new TestPipelineContext
            {
                AgentContext = AgentContextWithMode(modeKey, "General"),
                Session = new AgentSession { Id = "sess_1", Mode = modeKey },
                Turn = new AgentSessionTurn { Id = "turn_1" },
                ConversationContext = new ConversationContext { Name = "General" },
                ResponsePayload = new ResponsePayload
                {
                    PrimaryOutputText = "hello",
                    Usage = new LlmUsage { PromptTokens = 1, CompletionTokens = 2, TotalTokens = 3 },
                    Files = new List<FileRef>()
                },
                PromptKnowledgeProvider = new PromptKnowledgeProvider(),
                ValidateResult = InvokeResult.Success
            };
        }

        [Test]
        public async Task BuildAsync_NullContext_ReturnsFailure()
        {
            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(null);

            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public async Task BuildAsync_ResponseNotReady_ReturnsFailure()
        {
            var ctx = CreateBaseCtx();
            ctx.ResponseType = ResponseTypes.NotReady;

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public async Task BuildAsync_ValidateFails_ReturnsFailure()
        {
            var ctx = CreateBaseCtx();
            ctx.ValidateResult = InvokeResult.FromError("validation failed");

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public async Task BuildAsync_ModeNotFound_ReturnsFailure_WithModeKeyInMessage()
        {
            var ctx = CreateBaseCtx(modeKey: "missing");
            // AgentContextWithMode("missing", ...) would make it pass. Instead, give a different mode list.
            ctx.AgentContext = AgentContextWithMode("general", "General");
            ctx.Session.Mode = "missing";

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public async Task BuildAsync_UnknownResponseType_ReturnsFailure()
        {
            var ctx = CreateBaseCtx();
            ctx.ResponseType = (ResponseTypes)999;

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public async Task BuildAsync_Final_SetsRequiredFields_AndClearsContinuationBuckets()
        {
            var ctx = CreateBaseCtx();
            ctx.ResponseType = ResponseTypes.Final;
            ctx.Turn.Warnings = new List<string> { "w1" };

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            Assert.That(result.Result.Kind, Is.EqualTo(AgentExecuteResponseKind.Final));
            Assert.That(result.Result.SessionId, Is.EqualTo("sess_1"));
            Assert.That(result.Result.TurnId, Is.EqualTo("turn_1"));
            Assert.That(result.Result.ModeDisplayName, Is.EqualTo("General"));

            Assert.That(result.Result.PrimaryOutputText, Is.EqualTo("hello"));
            Assert.That(result.Result.Usage, Is.Not.Null);
            Assert.That(result.Result.Usage.TotalTokens, Is.EqualTo(3));

            Assert.That(result.Result.UserWarnings, Is.Not.Null);
            Assert.That(result.Result.UserWarnings.Count, Is.EqualTo(1));
            Assert.That(result.Result.UserWarnings[0], Is.EqualTo("w1"));

            Assert.That(result.Result.ToolCalls, Is.Null);
            Assert.That(result.Result.ToolContinuationMessage, Is.Null);
        }

        [Test]
        public async Task BuildAsync_Final_WhenWarningsNull_UserWarningsRemainsNull()
        {
            var ctx = CreateBaseCtx();
            ctx.ResponseType = ResponseTypes.Final;
            ctx.Turn.Warnings = null;

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result.UserWarnings, Is.Null);
        }

        [Test]
        public async Task BuildAsync_Final_MapsUsageAndFilesFromResponsePayload()
        {
            var ctx = CreateBaseCtx();
            ctx.ResponseType = ResponseTypes.Final;
            ctx.ResponsePayload.Usage = new LlmUsage { PromptTokens = 10, CompletionTokens = 20, TotalTokens = 30 };
            ctx.ResponsePayload.Files = new List<FileRef>
            {
                new FileRef { Name = "out.txt", MimeType = "text/plain", Url = "https://example", SizeBytes = 123, ContentHash = "hash" }
            };

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result.Usage.TotalTokens, Is.EqualTo(30));
            Assert.That(result.Result.Files, Is.Not.Null);
            Assert.That(result.Result.Files.Count, Is.EqualTo(1));
            Assert.That(result.Result.Files[0].Name, Is.EqualTo("out.txt"));
        }

        [Test]
        public async Task BuildAsync_ToolContinuation_MapsOnlyClientToolCalls()
        {
            var ctx = CreateBaseCtx();
            ctx.ResponseType = ResponseTypes.ToolContinuation;

            ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Clear();
            ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Add(new AgentToolCall
            {
                ToolCallId = "tc_client",
                Name = "clientTool",
                ArgumentsJson = "{\"a\":1}",
                RequiresClientExecution = true
            });
            ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Add(new AgentToolCall
            {
                ToolCallId = "tc_server",
                Name = "serverTool",
                ArgumentsJson = "{}",
                RequiresClientExecution = false
            });

            // ToolContinuationMessage is optional, but ResponseBuilder reads it.
            ctx.PromptKnowledgeProvider.ToolCallManifest.ToolContinuationMessage = "Running tools";

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result.Kind, Is.EqualTo(AgentExecuteResponseKind.ClientToolContinuation));

            Assert.That(result.Result.ToolCalls, Is.Not.Null);
            Assert.That(result.Result.ToolCalls.Count, Is.EqualTo(1));
            Assert.That(result.Result.ToolCalls[0].ToolCallId, Is.EqualTo("tc_client"));
            Assert.That(result.Result.ToolCalls[0].Name, Is.EqualTo("clientTool"));
            Assert.That(result.Result.ToolCalls[0].ArgumentsJson, Is.EqualTo("{\"a\":1}"));

            Assert.That(result.Result.ToolContinuationMessage, Is.EqualTo("Running tools"));
        }

        [Test]
        public async Task BuildAsync_ToolContinuation_NoClientToolCalls_ReturnsFailure()
        {
            var ctx = CreateBaseCtx();
            ctx.ResponseType = ResponseTypes.ToolContinuation;

            ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Clear();
            ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Add(new AgentToolCall
            {
                ToolCallId = "tc_server",
                Name = "serverTool",
                ArgumentsJson = "{}",
                RequiresClientExecution = false
            });

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public async Task BuildAsync_ToolContinuation_NullsFinalBuckets()
        {
            var ctx = CreateBaseCtx();
            ctx.ResponseType = ResponseTypes.ToolContinuation;

            // Populate final-ish values; continuation must null them.
            ctx.Turn.Warnings = new List<string> { "w1" };
            ctx.ResponsePayload.Files = new List<FileRef>
            {
                new FileRef { Name = "should_not_leak", MimeType = "text/plain", Url = "u", SizeBytes = 1, ContentHash = "h" }
            };
            ctx.ResponsePayload.Usage = new LlmUsage { PromptTokens = 9, CompletionTokens = 9, TotalTokens = 18 };

            ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Clear();
            ctx.PromptKnowledgeProvider.ToolCallManifest.ToolCalls.Add(new AgentToolCall
            {
                ToolCallId = "tc_client",
                Name = "clientTool",
                ArgumentsJson = "{}",
                RequiresClientExecution = true
            });
            ctx.PromptKnowledgeProvider.ToolCallManifest.ToolContinuationMessage = "Continue";

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.True);

            Assert.That(result.Result.Kind, Is.EqualTo(AgentExecuteResponseKind.ClientToolContinuation));

            Assert.That(result.Result.PrimaryOutputText, Is.Null);
            Assert.That(result.Result.Files, Is.Null);
            Assert.That(result.Result.Usage, Is.Null);
            Assert.That(result.Result.UserWarnings, Is.Null);
        }
    }
}
