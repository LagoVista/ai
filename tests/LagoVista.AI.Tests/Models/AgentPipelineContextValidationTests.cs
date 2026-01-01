using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Pipeline;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Models
{
    [TestFixture]
    public sealed class AgentPipelineContextValidationTests
    {
        IAgentPipelineContextValidator _validator = new AgentPipelineContextValidator();

        private static AgentPipelineContext CreateValidContext()
        {
            var request = new AgentExecuteRequest
            {
                Instruction = "hello",
                SessionId = null,
                TurnId = null,
                ToolResults = new List<ToolResultSubmission>(),
                ClipboardImages = new List<ClipboardImage>(),
                InputArtifacts = new List<InputArtifact>(),
                RagScope = null,
                AgentContextId = null,
                RoleId = null,
                Streaming = false
            };

            var org = EntityHeader.Create("org1", "Org 1");
            var user = EntityHeader.Create("user1", "User 1");

            return new AgentPipelineContext(request, org, user, CancellationToken.None);
        }

        private static void SetBackingField(object target, string propertyName, object value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            // Works for auto-properties: <PropertyName>k__BackingField
            var fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Assert.Fail($"Could not locate backing field '{fieldName}' on type '{target.GetType().FullName}'.");
            }

            field.SetValue(target, value);
        }

        private static void SetEnvelopeOrgToNull(AgentPipelineContext ctx)
        {
            var env = ctx.Envelope;
            Assert.That(env, Is.Not.Null);

            var orgField = env.GetType().GetField("<Org>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(orgField, Is.Not.Null, "Could not locate backing field for Envelope.Org");
            orgField.SetValue(env, null);
        }

        private static void SetEnvelopeUserToNull(AgentPipelineContext ctx)
        {
            var env = ctx.Envelope;
            Assert.That(env, Is.Not.Null);

            var userField = env.GetType().GetField("<User>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(userField, Is.Not.Null, "Could not locate backing field for Envelope.User");
            userField.SetValue(env, null);
        }

        [Test]
        public void Validate_Core_WhenAllCoreFieldsPresent_ReturnsSuccess()
        {
            var ctx = CreateValidContext();

            var result = _validator.ValidatePreStep(ctx, PipelineSteps.RequestHandler);

            // TST-001 §3.5: include ErrorMessage in Successful assertions.
            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void Validate_Core_TypeIsUndefined_ReturnsFailure()
        {
            // Intentionally failing until Validate enforces defined enum values.
            var ctx = CreateValidContext();
            SetBackingField(ctx, nameof(AgentPipelineContext.Type), (AgentPipelineContextTypes)999);

            var result = _validator.ValidatePreStep(ctx, PipelineSteps.RequestHandler);

            Assert.That(result.Successful, Is.False, result.ErrorMessage);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_Core_TimeStampMissing_ReturnsFailure()
        {
            // Intentionally failing until Validate enforces required timestamp.
            var ctx = CreateValidContext();
            SetBackingField(ctx, nameof(AgentPipelineContext.TimeStamp), "");

            var result = _validator.ValidatePreStep(ctx, PipelineSteps.RequestHandler);

            Assert.That(result.Successful, Is.False, result.ErrorMessage);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_Core_CorrelationIdMissing_ReturnsFailure()
        {
            // Intentionally failing until Validate enforces required correlation id.
            var ctx = CreateValidContext();
            SetBackingField(ctx, nameof(AgentPipelineContext.CorrelationId), "");

            var result = _validator.ValidatePreStep(ctx, PipelineSteps.RequestHandler);

            Assert.That(result.Successful, Is.False, result.ErrorMessage);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_Core_EnvelopeOrgMissing_ReturnsFailure()
        {
            // Intentionally failing until Validate enforces required Envelope.Org.
            var ctx = CreateValidContext();
            SetEnvelopeOrgToNull(ctx);

            var result = _validator.ValidatePreStep(ctx, PipelineSteps.RequestHandler);

            Assert.That(result.Successful, Is.False, result.ErrorMessage);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_Core_EnvelopeUserMissing_ReturnsFailure()
        {
            // Intentionally failing until Validate enforces required Envelope.User.
            var ctx = CreateValidContext();
            SetEnvelopeUserToNull(ctx);

            var result = _validator.ValidatePreStep(ctx, PipelineSteps.RequestHandler);

            Assert.That(result.Successful, Is.False, result.ErrorMessage);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Errors, Is.Not.Empty);
        }
    }
}
