using System;
using System.Collections;
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

namespace LagoVista.AI.Tests.Helpers
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

            public PromptKnowledgeProvider PromptKnowledgeProvider { get; set; }
            public CompositionTrace Trace { get; set; }

            public ResponseTypes ResponseType { get; set; } = ResponseTypes.Final;

            public bool HasPendingToolCalls { get; set; }
            public bool HasClientToolCalls { get; set; }

            public string ToolManifestId { get; set; }

            public string TimeStamp { get; set; } = DateTime.UtcNow.ToString("o");
            public Envelope Envelope { get; set; }

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
                // Not needed for these tests; ResponseBuilder reads ToolCallManifest from PromptKnowledgeProvider.
                // Keep this method for interface compliance.
            }

            public InvokeResult Validate(PipelineSteps step) => ValidateResult ?? InvokeResult.Success;

            public InvokeResult ValidateResult { get; set; }

            public void LogStepErrorDetails(IAdminLogger logger, PipelineSteps step, string error, TimeSpan ts) { }
            public void LogStepErrorDetails(IAdminLogger logger, PipelineSteps step, InvokeResult error, TimeSpan ts) { }
            public void LogDetails(IAdminLogger logger, PipelineSteps step, TimeSpan? ts = null) { }
        }

        private static AgentContext CreateAgentContextWithMode(string modeKey, string displayName)
        {
            var ctx = new AgentContext();

            var agentModesProp = ctx.GetType().GetProperty("AgentModes");
            if (agentModesProp == null)
            {
                Assert.Inconclusive("AgentContext.AgentModes property not found.");
                return ctx;
            }

            var agentModesObj = agentModesProp.GetValue(ctx);
            if (agentModesObj == null)
            {
                Assert.Inconclusive("AgentContext.AgentModes is null.");
                return ctx;
            }

            if (agentModesObj is not IList list)
            {
                Assert.Inconclusive("AgentContext.AgentModes is not an IList.");
                return ctx;
            }

            var agentModeType = ctx.GetType().Assembly.GetType("LagoVista.AI.Models.AgentMode");
            if (agentModeType == null)
            {
                Assert.Inconclusive("Type LagoVista.AI.Models.AgentMode not found in LagoVista.AI.Models assembly.");
                return ctx;
            }

            var mode = Activator.CreateInstance(agentModeType);
            if (mode == null)
            {
                Assert.Inconclusive("Unable to create AgentMode instance.");
                return ctx;
            }

            var keyProp = agentModeType.GetProperty("Key");
            var displayNameProp = agentModeType.GetProperty("DisplayName");

            if (keyProp == null)
            {
                Assert.Inconclusive("AgentMode.Key property not found.");
                return ctx;
            }

            if (displayNameProp == null)
            {
                Assert.Inconclusive("AgentMode.DisplayName property not found.");
                return ctx;
            }

            keyProp.SetValue(mode, modeKey);
            displayNameProp.SetValue(mode, displayName);

            list.Add(mode);
            return ctx;
        }

        private static PromptKnowledgeProvider CreateContentProviderWithClientToolCalls(params AgentToolCall[] calls)
        {
            var cp = new PromptKnowledgeProvider();
            cp.ToolCallManifest.ToolCalls.AddRange(calls);
            return cp;
        }

        private static void TrySetToolContinuationMessage(PromptKnowledgeProvider cp, string message)
        {
            var manifest = cp.ToolCallManifest;
            var prop = manifest.GetType().GetProperty("ToolContinuationMessage");
            prop?.SetValue(manifest, message);
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
            var ctx = new TestPipelineContext
            {
                ResponseType = ResponseTypes.NotReady,
                ValidateResult = InvokeResult.Success,
                Session = new AgentSession { Id = "sess_1", Mode = "general" },
                Turn = new AgentSessionTurn { Id = "turn_1" },
                AgentContext = CreateAgentContextWithMode("general", "General"),
                PromptKnowledgeProvider = new PromptKnowledgeProvider(),
                ResponsePayload = new ResponsePayload { PrimaryOutputText = "ignored" }
            };

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public async Task BuildAsync_ValidateFails_PropagatesFailure()
        {
            var ctx = new TestPipelineContext
            {
                ResponseType = ResponseTypes.Final,
                ValidateResult = InvokeResult.FromError("validation failed"),
                Session = new AgentSession { Id = "sess_1", Mode = "general" },
                Turn = new AgentSessionTurn { Id = "turn_1" },
                AgentContext = CreateAgentContextWithMode("general", "General"),
                PromptKnowledgeProvider = new PromptKnowledgeProvider(),
                ResponsePayload = new ResponsePayload { PrimaryOutputText = "ignored" }
            };

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public async Task BuildAsync_ModeNotFound_ReturnsFailure()
        {
            var ctx = new TestPipelineContext
            {
                ResponseType = ResponseTypes.Final,
                ValidateResult = InvokeResult.Success,
                Session = new AgentSession { Id = "sess_1", Mode = "missing_mode" },
                Turn = new AgentSessionTurn { Id = "turn_1" },
                AgentContext = CreateAgentContextWithMode("general", "General"),
                PromptKnowledgeProvider = new PromptKnowledgeProvider(),
                ResponsePayload = new ResponsePayload { PrimaryOutputText = "ignored" }
            };

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public async Task BuildAsync_Final_SetsBuckets_AndClearsContinuationBuckets()
        {
            var ctx = new TestPipelineContext
            {
                ResponseType = ResponseTypes.Final,
                ValidateResult = InvokeResult.Success,
                Session = new AgentSession { Id = "sess_1", Mode = "general" },
                Turn = new AgentSessionTurn { Id = "turn_1", Warnings = new List<string> { "w1", "w2" } },
                AgentContext = CreateAgentContextWithMode("general", "General"),
                PromptKnowledgeProvider = new PromptKnowledgeProvider(),
                ResponsePayload = new ResponsePayload
                {
                    PrimaryOutputText = "hello",
                    Usage = new LlmUsage { PromptTokens = 1, CompletionTokens = 2, TotalTokens = 3 },
                    Files = new List<FileRef> { new FileRef { Name = "a.txt", MimeType = "text/plain", Url = "u", SizeBytes = 1, ContentHash = "h" } }
                }
            };

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
            Assert.That(result.Result.Files, Is.Not.Null);
            Assert.That(result.Result.Files.Count, Is.EqualTo(1));

            Assert.That(result.Result.UserWarnings, Is.Not.Null);
            Assert.That(result.Result.UserWarnings.Count, Is.EqualTo(2));

            Assert.That(result.Result.ToolCalls, Is.Null);
            Assert.That(result.Result.ToolContinuationMessage, Is.Null);
        }

        [Test]
        public async Task BuildAsync_ToolContinuation_EmptyClientToolCalls_ReturnsFailure()
        {
            var cp = CreateContentProviderWithClientToolCalls(
                new AgentToolCall { ToolCallId = "tc_server", Name = "server", ArgumentsJson = "{}", RequiresClientExecution = false }
            );
            TrySetToolContinuationMessage(cp, "Running tools");

            var ctx = new TestPipelineContext
            {
                ResponseType = ResponseTypes.ToolContinuation,
                ValidateResult = InvokeResult.Success,
                Session = new AgentSession { Id = "sess_1", Mode = "general" },
                Turn = new AgentSessionTurn { Id = "turn_1" },
                AgentContext = CreateAgentContextWithMode("general", "General"),
                PromptKnowledgeProvider = cp,
                ResponsePayload = new ResponsePayload { PrimaryOutputText = "ignored" }
            };

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.False);
        }

        [Test]
        public async Task BuildAsync_ToolContinuation_MapsToolCalls_AndNullsFinalBuckets()
        {
            var cp = CreateContentProviderWithClientToolCalls(
                new AgentToolCall { ToolCallId = "tc_1", Name = "client", ArgumentsJson = "{}", RequiresClientExecution = true },
                new AgentToolCall { ToolCallId = "tc_server", Name = "server", ArgumentsJson = "{}", RequiresClientExecution = false }
            );
            TrySetToolContinuationMessage(cp, "Running tools");

            var ctx = new TestPipelineContext
            {
                ResponseType = ResponseTypes.ToolContinuation,
                ValidateResult = InvokeResult.Success,
                Session = new AgentSession { Id = "sess_1", Mode = "general" },
                Turn = new AgentSessionTurn { Id = "turn_1", Warnings = new List<string> { "w1" } },
                AgentContext = CreateAgentContextWithMode("general", "General"),
                PromptKnowledgeProvider = cp,
                ResponsePayload = new ResponsePayload
                {
                    PrimaryOutputText = "ignored",
                    Usage = new LlmUsage { PromptTokens = 9, CompletionTokens = 9, TotalTokens = 18 },
                    Files = new List<FileRef> { new FileRef { Name = "x", MimeType = "y", Url = "z", SizeBytes = 1, ContentHash = "h" } }
                }
            };

            var sut = new ResponseBuilder();
            var result = await sut.BuildAsync(ctx);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            Assert.That(result.Result.Kind, Is.EqualTo(AgentExecuteResponseKind.ClientToolContinuation));
            Assert.That(result.Result.ToolCalls, Is.Not.Null);
            Assert.That(result.Result.ToolCalls.Count, Is.EqualTo(1));
            Assert.That(result.Result.ToolCalls[0].ToolCallId, Is.EqualTo("tc_1"));
            Assert.That(result.Result.ToolCalls[0].Name, Is.EqualTo("client"));
            Assert.That(result.Result.ToolCalls[0].ArgumentsJson, Is.EqualTo("{}"));

            // Forbidden buckets for continuation
            Assert.That(result.Result.PrimaryOutputText, Is.Null);
            Assert.That(result.Result.Files, Is.Null);
            Assert.That(result.Result.Usage, Is.Null);
            Assert.That(result.Result.UserWarnings, Is.Null);
        }
    }
}
