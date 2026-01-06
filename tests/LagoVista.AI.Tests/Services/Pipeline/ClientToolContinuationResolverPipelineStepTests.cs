using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.AI.Models.Context;
using LagoVista.AI.Services.Pipeline;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services.Pipeline
{
    [TestFixture]
    public class ClientToolContinuationResolverPipelineStepTests
    {
        private static ClientToolContinuationResolverPipelineStep CreateSut(
            Mock<IAgentContextLoaderPipelineStap> next,
            Mock<IAgentPipelineContextValidator> validator,
            Mock<IToolCallManifestRepo> repo)
        {
            var logger = new AdminLogger(new ConsoleLogWriter());

            validator.Setup(val => val.ValidateCore(It.IsAny<IAgentPipelineContext>(), It.IsAny<PipelineSteps>(), It.IsAny<bool>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidatePostStep(It.IsAny<IAgentPipelineContext>(), It.IsAny<PipelineSteps>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidatePreStep(It.IsAny<IAgentPipelineContext>(), It.IsAny<PipelineSteps>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidateToolCallManifest(It.IsAny<ToolCallManifest>())).Returns(InvokeResult.Success);

            return new ClientToolContinuationResolverPipelineStep(next.Object, validator.Object, repo.Object, logger);
        }

        private static async Task<InvokeResult<IAgentPipelineContext>> InvokeExecuteStepAsync(
            ClientToolContinuationResolverPipelineStep sut,
            IAgentPipelineContext ctx)
        {
            return await sut.ExecuteAsync(ctx);
        }

        private static void AssertSuccess(InvokeResult<IAgentPipelineContext> result)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(result.Result, Is.Not.Null);
        }

        private static void AssertFailure(InvokeResult<IAgentPipelineContext> result)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False, result.ErrorMessage);
        }

        private static string JoinErrors(InvokeResult result)
        {
            if (result?.Errors == null || result.Errors.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("\n", result.Errors.Select(e => e.Message));;
        }

        private static void AssertContainsAll(string combinedErrors, params string[] expectedFragments)
        {
            Assert.That(combinedErrors, Is.Not.Null);

            foreach (var frag in expectedFragments ?? Array.Empty<string>())
            {
                Assert.That(combinedErrors.Contains(frag), Is.True, $"Expected errors to contain: {frag}\nErrors:\n{combinedErrors}");
            }
        }

        private static Envelope CreateEnvelope(EntityHeader org, EntityHeader user, IEnumerable<ToolResultSubmission> toolResults)
        {
            return new Envelope(
                agentContextId: null,
                roleId: null,
                sessionId: "sess_1",
                thisTurnId: "turn_1",
                previousTurnid: "prev_i",
                agentPersonaId: null,
                instructions: "",
                stream: false,
                toolResults: toolResults ?? Array.Empty<ToolResultSubmission>(),
                clipboardImages: Array.Empty<ClipboardImage>(),
                inputArtifacts: Array.Empty<InputArtifact>(),
                ragScope: new RagScope(),
                org: org,
                user: user);
        }

        private static Mock<IAgentPipelineContext> CreateContextMock(
            string toolManifestId,
            Envelope envelope)
        {
            var ctx = new Mock<IAgentPipelineContext>(MockBehavior.Loose);

            ctx.SetupGet(c => c.ToolManifestId).Returns(toolManifestId);
            ctx.SetupGet(c => c.Envelope).Returns(envelope);

            // AttachToolManifest is part of the observable contract of this step.
            ctx.Setup(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()));

            return ctx;
        }

        private static ToolCallManifest CreateManifest(
            IEnumerable<dynamic> toolCalls,
            IEnumerable<dynamic> toolCallResults)
        {
            var manifest = new ToolCallManifest
            {
                ToolCalls = toolCalls?.Select(tc => new AgentToolCall
                {
                    ToolCallId = (string)tc.ToolCallId,
                    Name = (string)tc.Name,
                    RequiresClientExecution = (bool)tc.RequiresClientExecution
                }).ToList() ?? new List<AgentToolCall>(),

                ToolCallResults = toolCallResults?.Select(tr => new AgentToolCallResult
                {
                    ToolCallId = (string)tr.ToolCallId,
                    RequiresClientExecution = (bool)tr.RequiresClientExecution,
                    ErrorMessage = (string)tr.ErrorMessage,
                    ResultJson = (string)tr.ResultJson
                }).ToList() ?? new List<AgentToolCallResult>()
            };

            return manifest;
        }

        [Test]
        public async Task ExecuteStepAsync_ManifestNotFound_ReturnsError()
        {
            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);

            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var envelope = CreateEnvelope(org, user, toolResults: Array.Empty<ToolResultSubmission>());
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);
            var valiator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .ReturnsAsync((ToolCallManifest)null);

            var sut = CreateSut(next, valiator, repo);

            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertFailure(result);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1));
            Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("CLIENT_TOOL_CONTINUATION_RESOLVER_MANIFEST_NOT_FOUND"));

            ctxMock.Verify(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()), Times.Never);
            repo.Verify(r => r.RemoveToolCallManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteStepAsync_DuplicateClientToolCallIds_ReturnsFailure()
        {
            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);
            var valiator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);


            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var toolResults = new[]
            {
                new ToolResultSubmission { ToolCallId = "tc_1", ResultJson = "{\"ok\":true}", ErrorMessage = null },
                new ToolResultSubmission { ToolCallId = "tc_1", ResultJson = "{\"ok\":true}", ErrorMessage = null }
            };

            var envelope = CreateEnvelope(org, user, toolResults);
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);

            var manifest = CreateManifest(
                toolCalls: new[] { new { ToolCallId = "tc_1", Name = "client_tool", RequiresClientExecution = true } },
                toolCallResults: new[] { new { ToolCallId = "tc_1", RequiresClientExecution = false, ErrorMessage = (string)null, ResultJson = (string)null } });

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .ReturnsAsync(manifest);

            var sut = CreateSut(next, valiator, repo);

            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertFailure(result);
            var combined = JoinErrors(result.ToInvokeResult());
            Assert.That(combined.Contains("Duplicate ToolCallId"), Is.True);

            ctxMock.Verify(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()), Times.Never);
            repo.Verify(r => r.RemoveToolCallManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteStepAsync_ClientExecutionRequiredButNoManifestResult_ReturnsFailure()
        {
            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);
            var valiator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var envelope = CreateEnvelope(org, user, toolResults: Array.Empty<ToolResultSubmission>());
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);

            var manifest = CreateManifest(
                toolCalls: new[] { new { ToolCallId = "tc_1", Name = "client_tool", RequiresClientExecution = true } },
                toolCallResults: Array.Empty<object>());

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .ReturnsAsync(manifest);

            var sut = CreateSut(next,valiator, repo);

            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertFailure(result);
            var combined = JoinErrors(result.ToInvokeResult());
            Assert.That(combined.Contains("requires execution but no result was provided"), Is.True);

            ctxMock.Verify(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()), Times.Never);
            repo.Verify(r => r.RemoveToolCallManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteStepAsync_ClientProvidesResultForNonClientTool_ReturnsFailure()
        {
            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);
            var valiator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var toolResults = new[]
            {
                new ToolResultSubmission { ToolCallId = "tc_1", ResultJson = "{\"ok\":true}", ErrorMessage = null }
            };

            var envelope = CreateEnvelope(org, user, toolResults);
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);

            var manifest = CreateManifest(
                toolCalls: new[] { new { ToolCallId = "tc_1", Name = "server_tool", RequiresClientExecution = false } },
                toolCallResults: new[] { new { ToolCallId = "tc_1", RequiresClientExecution = false, ErrorMessage = (string)null, ResultJson = (string)null } });

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .ReturnsAsync(manifest);

            var sut = CreateSut(next,valiator, repo);

            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertFailure(result);
            var combined = JoinErrors(result.ToInvokeResult());
            Assert.That(combined.Contains("was not a client tool type"), Is.True);

            ctxMock.Verify(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()), Times.Never);
            repo.Verify(r => r.RemoveToolCallManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteStepAsync_ExtraClientToolCallIdNotInManifest_ReturnsFailure()
        {
            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);
            var valiator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var toolResults = new[]
            {
                new ToolResultSubmission { ToolCallId = "tc_extra", ResultJson = "{\"ok\":true}", ErrorMessage = null }
            };

            var envelope = CreateEnvelope(org, user, toolResults);
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);

            var manifest = CreateManifest(
                toolCalls: new[] { new { ToolCallId = "tc_1", Name = "client_tool", RequiresClientExecution = true } },
                toolCallResults: new[] { new { ToolCallId = "tc_1", RequiresClientExecution = false, ErrorMessage = (string)null, ResultJson = (string)null } });

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .ReturnsAsync(manifest);

            var sut = CreateSut(next, valiator, repo);

            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertFailure(result);
            var combined = JoinErrors(result.ToInvokeResult());
            Assert.That(combined.Contains("Tool Call with Id tc_extra not found in Manifest sess_1.turn_1."), Is.True, combined);

            ctxMock.Verify(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()), Times.Never);
            repo.Verify(r => r.RemoveToolCallManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteStepAsync_ClientResultToolCallIdNotInManifestToolCalls_ReturnsFailure()
        {
            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);
            var valiator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var toolResults = new[]
            {
                new ToolResultSubmission { ToolCallId = "tc_missing", ResultJson = "{\"ok\":true}", ErrorMessage = null }
            };

            var envelope = CreateEnvelope(org, user, toolResults);
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);

            var manifest = CreateManifest(
                toolCalls: new[] { new { ToolCallId = "tc_1", Name = "client_tool", RequiresClientExecution = true } },
                toolCallResults: new[] { new { ToolCallId = "tc_1", RequiresClientExecution = false, ErrorMessage = (string)null, ResultJson = (string)null } });

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .ReturnsAsync(manifest);

            var sut = CreateSut(next, valiator, repo);

            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertFailure(result);
            var combined = JoinErrors(result.ToInvokeResult());
            Assert.That(combined.Contains("not found in Manifest"), Is.True);

            ctxMock.Verify(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()), Times.Never);
            repo.Verify(r => r.RemoveToolCallManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteStepAsync_ClientToolExistingResultMarkedRequiresClientExecution_ReturnsFailure()
        {
            // This test is aligned to the "flipped" production condition:
            // it fails when the existing result row is NOT marked RequiresClientExecution=true.

            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);            
            var validator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var toolResults = new[]
            {
                new ToolResultSubmission { ToolCallId = "tc_1", ResultJson = "{\"ok\":true}", ErrorMessage = null }
            };

            var envelope = CreateEnvelope(org, user, toolResults);
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);

            // Existing result row is NOT marked RequiresClientExecution=true => should be rejected (under flipped logic).
            var manifest = CreateManifest(
                toolCalls: new[] { new { ToolCallId = "tc_1", Name = "client_tool", RequiresClientExecution = true } },
                toolCallResults: new[] { new { ToolCallId = "tc_1", RequiresClientExecution = false, ErrorMessage = (string)null, ResultJson = (string)null } });

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
            .ReturnsAsync(manifest);

            var sut = CreateSut(next, validator, repo);

            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertFailure(result);
            var combined = JoinErrors(result.ToInvokeResult());
            Assert.That(combined.Contains("not marked as requiring client execution"), Is.True);

            ctxMock.Verify(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()), Times.Never);
            repo.Verify(r => r.RemoveToolCallManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteStepAsync_ExistingResultAlreadyHasErrorMessage_ReturnsFailure()
        {
            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);
            var validator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var toolResults = new[]
            {
                new ToolResultSubmission { ToolCallId = "tc_1", ResultJson = "{\"value\":123}", ErrorMessage = null }
            };

            var envelope = CreateEnvelope(org, user, toolResults);
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);

            var manifest = CreateManifest(
                toolCalls: new[] { new { ToolCallId = "tc_1", Name = "client_tool", RequiresClientExecution = true } },
                toolCallResults: new[] { new { ToolCallId = "tc_1", RequiresClientExecution = true, ErrorMessage = "already set", ResultJson = (string)null } });

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .ReturnsAsync(manifest);

            var sut = CreateSut(next, validator, repo);

            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertFailure(result);
            var combined = JoinErrors(result.ToInvokeResult());
            Assert.That(combined.Contains("error message was already set"), Is.True);

            ctxMock.Verify(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()), Times.Never);
            repo.Verify(r => r.RemoveToolCallManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteStepAsync_ExistingResultAlreadyHasResultJson_ReturnsFailure()
        {
            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);
            var validator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var toolResults = new[]
            {
                new ToolResultSubmission { ToolCallId = "tc_1", ResultJson = "{\"value\":123}", ErrorMessage = null }
            };

            var envelope = CreateEnvelope(org, user, toolResults);
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);

            var manifest = CreateManifest(
                toolCalls: new[] { new { ToolCallId = "tc_1", Name = "client_tool", RequiresClientExecution = true } },
                toolCallResults: new[] { new { ToolCallId = "tc_1", RequiresClientExecution = true, ErrorMessage = (string)null, ResultJson = "{\"already\":true}" } });

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .ReturnsAsync(manifest);

            var sut = CreateSut(next, validator, repo);

            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertFailure(result);
            var combined = JoinErrors(result.ToInvokeResult());
            Assert.That(combined.Contains("results json was already set"), Is.True);

            ctxMock.Verify(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()), Times.Never);
            repo.Verify(r => r.RemoveToolCallManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteStepAsync_MultipleValidationFailures_AggregatesErrors_ReturnsFailure()
        {
            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);
            var validator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var toolResults = new[]
            {
                new ToolResultSubmission { ToolCallId = "tc_dup", ResultJson = "{\"v\":1}", ErrorMessage = null },
                new ToolResultSubmission { ToolCallId = "tc_dup", ResultJson = "{\"v\":2}", ErrorMessage = null },
                new ToolResultSubmission { ToolCallId = "tc_extra", ResultJson = "{\"v\":3}", ErrorMessage = null }
            };

            var envelope = CreateEnvelope(org, user, toolResults);
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);

            var manifest = CreateManifest(
                toolCalls: new[]
                {
                    new { ToolCallId = "tc_dup", Name = "client_tool_dup", RequiresClientExecution = true },
                    new { ToolCallId = "tc_need", Name = "client_tool_need", RequiresClientExecution = true }
                },
                toolCallResults: new[]
                {
                    // placeholder row for tc_dup
                    new { ToolCallId = "tc_dup", RequiresClientExecution = true, ErrorMessage = (string)null, ResultJson = (string)null }
                    // Intentionally no ToolCallResult for tc_need
                });

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .ReturnsAsync(manifest);

            var sut = CreateSut(next, validator, repo);

            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertFailure(result);

            var combined = JoinErrors(result.ToInvokeResult());
            AssertContainsAll(combined,
                "Duplicate ToolCallId tc_dup",
                "requires execution but no result was provided",
                "does not exist in Manifest");

            ctxMock.Verify(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()), Times.Never);
            repo.Verify(r => r.RemoveToolCallManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteStepAsync_MultipleExistingResultIntegrityFailures_AggregatesErrors_ReturnsFailure()
        {
            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);
            var validator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var toolResults = new[]
            {
                new ToolResultSubmission { ToolCallId = "tc_reqflag", ResultJson = "{\"v\":1}", ErrorMessage = null },
                new ToolResultSubmission { ToolCallId = "tc_errset", ResultJson = "{\"v\":2}", ErrorMessage = null },
                new ToolResultSubmission { ToolCallId = "tc_jsonset", ResultJson = "{\"v\":3}", ErrorMessage = null }
            };

            var envelope = CreateEnvelope(org, user, toolResults);
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);

            var manifest = CreateManifest(
                toolCalls: new[]
                {
                    new { ToolCallId = "tc_reqflag", Name = "client_tool_a", RequiresClientExecution = true },
                    new { ToolCallId = "tc_errset", Name = "client_tool_b", RequiresClientExecution = true },
                    new { ToolCallId = "tc_jsonset", Name = "client_tool_c", RequiresClientExecution = true }
                },
                toolCallResults: new[]
                {
                    // violates "flipped" rule: not marked RequiresClientExecution=true
                    new { ToolCallId = "tc_reqflag", RequiresClientExecution = false, ErrorMessage = (string)null, ResultJson = (string)null },
                    // violates error already set
                    new { ToolCallId = "tc_errset", RequiresClientExecution = true, ErrorMessage = "already set", ResultJson = (string)null },
                    // violates result json already set
                    new { ToolCallId = "tc_jsonset", RequiresClientExecution = true, ErrorMessage = (string)null, ResultJson = "{\"already\":true}" }
                });

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .ReturnsAsync(manifest);

            var sut = CreateSut(next, validator, repo);

            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertFailure(result);

            var combined = JoinErrors(result.ToInvokeResult());
            AssertContainsAll(combined,
                "not marked as requiring client execution",
                "error message was already set",
                "results json was already set");

            ctxMock.Verify(c => c.AttachToolManifest(It.IsAny<ToolCallManifest>()), Times.Never);
            repo.Verify(r => r.RemoveToolCallManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ExecuteStepAsync_HappyPath_ReconcilesClientResults_AttachesManifest_RemovesManifest_ReturnsCtx()
        {
            // Arrange
            var next = new Mock<IAgentContextLoaderPipelineStap>(MockBehavior.Loose);
            var repo = new Mock<IToolCallManifestRepo>(MockBehavior.Loose);
            var validator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            var org = EntityHeader.Create("org-1", "Org One");
            var user = EntityHeader.Create("user-1", "User One");

            var toolResults = new[]
            {
                new ToolResultSubmission { ToolCallId = "tc_1", ResultJson = "{\"value\":42}", ErrorMessage = null }
            };

            var envelope = CreateEnvelope(org, user, toolResults);
            var ctxMock = CreateContextMock("sess_1.turn_1", envelope);

            // Under flipped rule, placeholder rows should be RequiresClientExecution=true
            var manifest = CreateManifest(
                toolCalls: new[] { new { ToolCallId = "tc_1", Name = "client_tool", RequiresClientExecution = true } },
                toolCallResults: new[] { new { ToolCallId = "tc_1", RequiresClientExecution = true, ErrorMessage = (string)null, ResultJson = (string)null } });

            repo.Setup(r => r.GetToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .ReturnsAsync(manifest);

            repo.Setup(r => r.RemoveToolCallManifestAsync("sess_1.turn_1", "org-1"))
                .Returns(Task.CompletedTask);

            var sut = CreateSut(next, validator, repo);

            next.Setup(nxt => nxt.ExecuteAsync(It.IsAny<IAgentPipelineContext>())).ReturnsAsync(InvokeResult<IAgentPipelineContext>.Create(ctxMock.Object));


            // Act
            var result = await InvokeExecuteStepAsync(sut, ctxMock.Object);

            // Assert
            AssertSuccess(result);
            Assert.That(result.Result, Is.SameAs(ctxMock.Object));

            var reconciled = manifest.ToolCallResults.Single(r => r.ToolCallId == "tc_1");
            Assert.That(reconciled.ResultJson, Is.EqualTo("{\"value\":42}"));
            Assert.That(reconciled.ErrorMessage, Is.Null);

            ctxMock.Verify(c => c.AttachToolManifest(manifest), Times.Once);
            repo.Verify(r => r.RemoveToolCallManifestAsync("sess_1.turn_1", "org-1"), Times.Once);
        }
    }
}
