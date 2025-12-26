using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Models;
using LagoVista.AI.Models.Context;
using LagoVista.AI.Services.Pipeline;
using LagoVista.Core;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using LagoVista.UserAdmin.Interfaces.Managers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Services.Pipeline
{
    [TestFixture]
    public sealed class AgentRequestHandlerPipelineStepTests
    {
        private static IAdminLogger CreateLogger()
        {
            // TST-001 §3.4: default logger (do NOT mock by default)
            return new AdminLogger(new ConsoleLogWriter());
        }

        private static EntityHeader CreateOrg() => EntityHeader.Create("org1", "Org 1");
        private static EntityHeader CreateUser() => EntityHeader.Create("user1", "User 1");

        private static AgentExecuteRequest CreateRequest()
        {
            // Keep the request minimal. This suite is intentionally focused on routing + guardrails.
            return new AgentExecuteRequest();
        }

        /// <summary>
        /// Attempts to set an enum-valued property on AgentExecuteRequest using reflection.
        /// This lets us create an "unknown" ctx.Type without taking dependencies on any single
        /// request field name.
        ///
        /// If no compatible property exists, we leave the request as-is (the test will still
        /// fail today if the production code throws; once a stable setter exists, it will become deterministic).
        /// </summary>
        private static void TrySetRequestPipelineType(AgentExecuteRequest request, int rawEnumValue)
        {
            if (request == null) return;

            var reqType = request.GetType();

            // Common candidate property names (we don't assume which one exists).
            var candidates = new[]
            {
                "Type",
                "PipelineContextType",
                "ContextType",
                "AgentPipelineContextType",
                "PipelineType"
            };

            foreach (var name in candidates)
            {
                var prop = reqType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop == null || !prop.CanWrite) continue;

                try
                {
                    // If the property is exactly AgentPipelineContextTypes (or an enum), assign the raw value.
                    if (prop.PropertyType.IsEnum)
                    {
                        var boxed = Enum.ToObject(prop.PropertyType, rawEnumValue);
                        prop.SetValue(request, boxed);
                        return;
                    }

                    // If it is an int, also allow setting directly.
                    if (prop.PropertyType == typeof(int))
                    {
                        prop.SetValue(request, rawEnumValue);
                        return;
                    }
                }
                catch
                {
                    // Best effort only.
                }
            }
        }

        private static InvokeResult<T> CreateFailure<T>(string errorMessage)
        {
            // We avoid depending on any single InvokeResult factory method.
            // Populate via reflection as a last-resort fallback.
            var result = (InvokeResult<T>)Activator.CreateInstance(typeof(InvokeResult<T>));
            result.Errors.Add(new ErrorMessage() { Message = errorMessage });
            return result;
        }

        private static InvokeResult<T> CreateSuccess<T>(T value)
        {
            var result = (InvokeResult<T>)Activator.CreateInstance(typeof(InvokeResult<T>));
            SetPropertyIfPresent(result, "Successful", true);
            SetPropertyIfPresent(result, "Result", value);
            SetPropertyIfPresent(result, "ErrorMessage", null);
            return result;
        }

        private static void SetPropertyIfPresent(object target, string propertyName, object value)
        {
            if (target == null) return;
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanWrite) return;

            try
            {
                prop.SetValue(target, value);
            }
            catch
            {
                // best effort
            }
        }

        private static IAgentPipelineContext CreatePipelineContextWithSession()
        {
            var mock = new Mock<IAgentPipelineContext>(MockBehavior.Loose);

            // Create a concrete AgentSession if available; otherwise use null (tests that require it will fail and drive fixes).
            try
            {
                var session = new AgentSession();
                mock.SetupGet(x => x.Session).Returns(session);
            }
            catch
            {
                mock.SetupGet(x => x.Session).Returns((AgentSession)null);
            }

            return mock.Object;
        }

        [Test]
        public async Task HandleAsync_Initial_WhenSessionCreatorSucceeds_BuildsResponseAndUpdatesSession()
        {
            var logger = CreateLogger();

            var contextProvider = new Mock<IAgentContextResolverPipelineStep>(MockBehavior.Loose);
            var sessionRestorer = new Mock<IAgentSessionRestorerPipelineStep>(MockBehavior.Loose);
            var toolSessionRestorer = new Mock<IClientToolCallSessionRestorerPipelineStep>(MockBehavior.Loose);
            var responseBuilder = new Mock<IAgentExecuteResponseBuilder>(MockBehavior.Loose);
            var sessionManager = new Mock<IAgentSessionManager>(MockBehavior.Loose);
            var streamingContext = new Mock<IAgentStreamingContext>(MockBehavior.Loose);
            var validator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);
            validator.Setup(val => val.ValidateCore(It.IsAny<IAgentPipelineContext>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidatePostStep(It.IsAny<IAgentPipelineContext>(), It.IsAny<PipelineSteps>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidatePreStep(It.IsAny<IAgentPipelineContext>(), It.IsAny<PipelineSteps>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidateToolCallManifest(It.IsAny<ToolCallManifest>())).Returns(InvokeResult.Success);

            var pipelineCtx = CreatePipelineContextWithSession();
            contextProvider
                .Setup(x => x.ExecuteAsync(It.IsAny<AgentPipelineContext>()))
                .ReturnsAsync(CreateSuccess(pipelineCtx));

            var built = CreateSuccess(new AgentExecuteResponse());
            responseBuilder
                .Setup(x => x.BuildAsync(It.IsAny<IAgentPipelineContext>()))
                .ReturnsAsync(built);

            // Use loose behavior; we only verify the few calls that are the contract for this test.
            sessionManager
                .Setup(x => x.UpdateSessionAsync(It.IsAny<AgentSession>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()))
                .ReturnsAsync(InvokeResult.Success);


            var sut = new AgentRequestHandlerPipelineStep(
                contextProvider.Object,
                sessionRestorer.Object,
                toolSessionRestorer.Object,
                logger,
                validator.Object,
                responseBuilder.Object,
                sessionManager.Object,
                streamingContext.Object);

            var request = CreateRequest();
            request.Instruction = "Hello Wold";
            var org = CreateOrg();
            var user = CreateUser();

            var result = await sut.HandleAsync(request, org, user, CancellationToken.None);

            // TST-001 §3.5: include ErrorMessage in Successful assertions.
            Assert.That(result.Successful, Is.True, result.ErrorMessage);

            // Outcome first, then interactions.
            responseBuilder.Verify(x => x.BuildAsync(It.IsAny<IAgentPipelineContext>()), Times.Once);
            sessionManager.Verify(x => x.UpdateSessionAsync(It.IsAny<AgentSession>(), org, user), Times.Once);

            // Routing contract: only creator should be invoked.
            contextProvider.Verify(x => x.ExecuteAsync(It.IsAny<AgentPipelineContext>()), Times.Once);
            sessionRestorer.Verify(x => x.ExecuteAsync(It.IsAny<AgentPipelineContext>()), Times.Never);
            toolSessionRestorer.Verify(x => x.ExecuteAsync(It.IsAny<AgentPipelineContext>()), Times.Never);
        }

        [Test]
        public async Task HandleAsync_WhenDownstreamStepFails_ShouldNotPersistSession_AndReturnsFailure()
        {
            // This test is intentionally expected to FAIL until production code is fixed.
            // Current implementation always calls UpdateSessionAsync(result.Result.Session, ...)
            // which is unsafe on failure and/or null Result.

            var logger = CreateLogger();

            var contextProvider = new Mock<IAgentContextResolverPipelineStep>(MockBehavior.Loose);
            var sessionRestorer = new Mock<IAgentSessionRestorerPipelineStep>(MockBehavior.Loose);
            var toolSessionRestorer = new Mock<IClientToolCallSessionRestorerPipelineStep>(MockBehavior.Loose);
            var responseBuilder = new Mock<IAgentExecuteResponseBuilder>(MockBehavior.Loose);
            var sessionManager = new Mock<IAgentSessionManager>(MockBehavior.Loose);
            var streamingContext = new Mock<IAgentStreamingContext>(MockBehavior.Loose);
            var validator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            validator.Setup(val => val.ValidateCore(It.IsAny<IAgentPipelineContext>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidatePostStep(It.IsAny<IAgentPipelineContext>(), It.IsAny<PipelineSteps>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidatePreStep(It.IsAny<IAgentPipelineContext>(), It.IsAny<PipelineSteps>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidateToolCallManifest(It.IsAny<ToolCallManifest>())).Returns(InvokeResult.Success);


            // Return a failure with no Result.
            contextProvider
                .Setup(x => x.ExecuteAsync(It.IsAny<AgentPipelineContext>()))
                .ReturnsAsync(CreateFailure<IAgentPipelineContext>("downstream failed"));

            sessionManager
                .Setup(x => x.UpdateSessionAsync(It.IsAny<AgentSession>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()))
                 .ReturnsAsync(InvokeResult.Success);

            var sut = new AgentRequestHandlerPipelineStep(
                contextProvider.Object,
                sessionRestorer.Object,
                toolSessionRestorer.Object,
                logger,
                validator.Object,
                responseBuilder.Object,
                sessionManager.Object,
                streamingContext.Object);

            var request = CreateRequest();
            var org = CreateOrg();
            var user = CreateUser();

            InvokeResult<AgentExecuteResponse> result = null;

            // We want: no exception, just a clean failure result.
            Assert.That(async () => result = await sut.HandleAsync(request, org, user, CancellationToken.None), Throws.Nothing);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False, result.ErrorMessage);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);

            // Contract requirement: should not persist when downstream failed / no session is available.
            // TST-001 §3.3: explicitly assert non-persistence.
            sessionManager.Verify(
                x => x.UpdateSessionAsync(It.IsAny<AgentSession>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()),
                Times.Never);

            // Should not attempt to build a response on failure.
            responseBuilder.Verify(x => x.BuildAsync(It.IsAny<IAgentPipelineContext>()), Times.Never);
        }

        [Test]
        public async Task HandleAsync_WhenContextTypeIsUnknown_ShouldReturnFailure_NotThrow()
        {
            // This test is intentionally expected to FAIL until production code is fixed.
            // Today, an unknown ctx.Type can leave 'result' null and cause a NullReferenceException.

            var logger = CreateLogger();

            var contextProvider = new Mock<IAgentContextResolverPipelineStep>(MockBehavior.Loose);
            var sessionRestorer = new Mock<IAgentSessionRestorerPipelineStep>(MockBehavior.Loose);
            var toolSessionRestorer = new Mock<IClientToolCallSessionRestorerPipelineStep>(MockBehavior.Loose);
            var responseBuilder = new Mock<IAgentExecuteResponseBuilder>(MockBehavior.Loose);
            var sessionManager = new Mock<IAgentSessionManager>(MockBehavior.Loose);
            var streamingContext = new Mock<IAgentStreamingContext>(MockBehavior.Loose);
            var validator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            validator.Setup(val => val.ValidateCore(It.IsAny<IAgentPipelineContext>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidatePostStep(It.IsAny<IAgentPipelineContext>(), It.IsAny<PipelineSteps>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidatePreStep(It.IsAny<IAgentPipelineContext>(), It.IsAny<PipelineSteps>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidateToolCallManifest(It.IsAny<ToolCallManifest>())).Returns(InvokeResult.Success);


            var sut = new AgentRequestHandlerPipelineStep(
                contextProvider.Object,
                sessionRestorer.Object,
                toolSessionRestorer.Object,
                logger,
                validator.Object,
                responseBuilder.Object,
                sessionManager.Object,
                streamingContext.Object);

            var request = CreateRequest();

            // Force an out-of-range enum value if the request exposes a compatible field.
            TrySetRequestPipelineType(request, 999);

            var org = CreateOrg();
            var user = CreateUser();

            InvokeResult<AgentExecuteResponse> result = null;

            Assert.That(async () => result = await sut.HandleAsync(request, org, user, CancellationToken.None), Throws.Nothing);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False, result.ErrorMessage);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);

            // On unknown type, no session should be persisted and no builder invoked.
            sessionManager.Verify(
                x => x.UpdateSessionAsync(It.IsAny<AgentSession>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()),
                Times.Never);

            responseBuilder.Verify(x => x.BuildAsync(It.IsAny<IAgentPipelineContext>()), Times.Never);
        }

        [Test]
        public async Task HandleAsync_ShouldPassCancellationTokenIntoStreamingCalls()
        {
            // Observable behavior: the token passed to AddWorkflowAsync should be the same one provided to HandleAsync.
            // This is a sanity check and should pass today.

            var logger = CreateLogger();

            var contextProvider = new Mock<IAgentContextResolverPipelineStep>(MockBehavior.Loose);
            var sessionRestorer = new Mock<IAgentSessionRestorerPipelineStep>(MockBehavior.Loose);
            var toolSessionRestorer = new Mock<IClientToolCallSessionRestorerPipelineStep>(MockBehavior.Loose);
            var responseBuilder = new Mock<IAgentExecuteResponseBuilder>(MockBehavior.Loose);
            var sessionManager = new Mock<IAgentSessionManager>(MockBehavior.Loose);
            var streamingContext = new Mock<IAgentStreamingContext>(MockBehavior.Loose);
            var validator = new Mock<IAgentPipelineContextValidator>(MockBehavior.Loose);

            validator.Setup(val => val.ValidateCore(It.IsAny<IAgentPipelineContext>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidatePostStep(It.IsAny<IAgentPipelineContext>(), It.IsAny<PipelineSteps>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidatePreStep(It.IsAny<IAgentPipelineContext>(), It.IsAny<PipelineSteps>())).Returns(InvokeResult.Success);
            validator.Setup(val => val.ValidateToolCallManifest(It.IsAny<ToolCallManifest>())).Returns(InvokeResult.Success);


            var pipelineCtx = CreatePipelineContextWithSession();
            contextProvider
                .Setup(x => x.ExecuteAsync(It.IsAny<AgentPipelineContext>()))
                .ReturnsAsync(CreateSuccess(pipelineCtx));

            responseBuilder
                .Setup(x => x.BuildAsync(It.IsAny<IAgentPipelineContext>()))
                .ReturnsAsync(CreateSuccess(new AgentExecuteResponse()));

            sessionManager
                .Setup(x => x.UpdateSessionAsync(It.IsAny<AgentSession>(), It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>()))
                .ReturnsAsync(InvokeResult.Success);

            var sut = new AgentRequestHandlerPipelineStep(
                contextProvider.Object,
                sessionRestorer.Object,
                toolSessionRestorer.Object,
                logger,
                validator.Object,
                responseBuilder.Object,
                sessionManager.Object,
                streamingContext.Object);

            var request = CreateRequest();
            request.Instruction = "Hello World";
            var org = CreateOrg();
            var user = CreateUser();

            using var cts = new CancellationTokenSource();

            var result = await sut.HandleAsync(request, org, user, cts.Token);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);

            streamingContext.Verify(
                x => x.AddWorkflowAsync(It.IsAny<string>(), cts.Token),
                Times.Once);
        }
    }
}
