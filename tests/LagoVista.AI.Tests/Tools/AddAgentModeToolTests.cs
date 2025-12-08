using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Tools
{
    [TestFixture]
    public class AddAgentModeToolTests
    {
        private Mock<IAgentContextManager> _agentContextManager;
        private AddAgentModeTool _tool;

        [SetUp]
        public void SetUp()
        {
            _agentContextManager = new Mock<IAgentContextManager>();
            _tool = new AddAgentModeTool(_agentContextManager.Object);
        }

        private static AgentToolExecutionContext CreateExecutionContext()
        {
            return new AgentToolExecutionContext
            {
                AgentContext = new AgentContext { Id = "agent-1", Name = "Agent 1" },
                Org = new EntityHeader { Id = "org-1", Text = "Org 1" },
                User = new EntityHeader { Id = "user-1", Text = "User 1" }
            };
        }

        [Test]
        public async Task ExecuteAsync_PopulatesMode_AndPersists_AndReturnsSummary()
        {
            // Arrange
            var argsObject = new
            {
                key = "ddr_authoring",
                display_name = "DDR Authoring",
                description = "Helps author and refine DDR specs.",
                when_to_use = "Use when working on DDRs.",
                welcome_message = "You are now in DDR Authoring mode.",
                mode_instructions = new[] { "Prefer structured DDR sections.", "Highlight missing fields." },
                behavior_hints = new[] { "preferStructuredOutput" },
                human_role_hints = new[] { "The human is the DDR owner." },
                associated_tool_ids = new[] { "tul-001.code_search", "tul-002.ddr_lookup" },
                tool_group_hints = new[] { "authoring" },
                rag_scope_hints = new[] { "boost:DDR_DDRs" },
                strong_signals = new[] { "DDR", "Design Decision Record" },
                weak_signals = new[] { "requirements doc" },
                example_utterances = new[] { "Help me draft a DDR." },
                status = "active",
                version = "v1.0",
                is_default = false
            };

            var argumentsJson = JObject.FromObject(argsObject).ToString();
            var context = CreateExecutionContext();

            AgentMode capturedMode = null;

            _agentContextManager
                .Setup(m => m.AddAgentModeAsync(
                    It.IsAny<string>(),
                    It.IsAny<AgentMode>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>()))
                .Callback<string, AgentMode, EntityHeader, EntityHeader>((_, mode, __, ___) =>
                {
                    capturedMode = mode;
                })
                .ReturnsAsync(() => InvokeResult.Success);

            // Act
            var result = await _tool.ExecuteAsync(argumentsJson, context, CancellationToken.None);

            // Assert
            _agentContextManager.Verify(m => m.AddAgentModeAsync(
                context.AgentContext.Id,
                It.IsAny<AgentMode>(),
                context.Org,
                context.User), Times.Once);

            Assert.That(capturedMode, Is.Not.Null);

            Assert.That(capturedMode.Key, Is.EqualTo("ddr_authoring"));
            Assert.That(capturedMode.DisplayName, Is.EqualTo("DDR Authoring"));
            Assert.That(capturedMode.Description, Is.EqualTo("Helps author and refine DDR specs."));
            Assert.That(capturedMode.WhenToUse, Is.EqualTo("Use when working on DDRs."));
            Assert.That(capturedMode.WelcomeMessage, Is.EqualTo("You are now in DDR Authoring mode."));
            Assert.That(capturedMode.Status, Is.EqualTo("active"));
            Assert.That(capturedMode.Version, Is.EqualTo("v1.0"));
            Assert.That(capturedMode.IsDefault, Is.False);

            Assert.That(capturedMode.ModeInstructions, Is.EqualTo(new[]
            {
                "Prefer structured DDR sections.",
                "Highlight missing fields."
            }));

            Assert.That(capturedMode.BehaviorHints, Is.EqualTo(new[] { "preferStructuredOutput" }));
            Assert.That(capturedMode.HumanRoleHints, Is.EqualTo(new[] { "The human is the DDR owner." }));
            Assert.That(capturedMode.AssociatedToolIds, Is.EqualTo(new[] { "tul-001.code_search", "tul-002.ddr_lookup" }));
            Assert.That(capturedMode.ToolGroupHints, Is.EqualTo(new[] { "authoring" }));
            Assert.That(capturedMode.RagScopeHints, Is.EqualTo(new[] { "boost:DDR_DDRs" }));
            Assert.That(capturedMode.StrongSignals, Is.EqualTo(new[] { "DDR", "Design Decision Record" }));
            Assert.That(capturedMode.WeakSignals, Is.EqualTo(new[] { "requirements doc" }));
            Assert.That(capturedMode.ExampleUtterances, Is.EqualTo(new[] { "Help me draft a DDR." }));

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            var payload = JObject.Parse(result.Result);

            Assert.That(payload["status"]!.ToString(), Is.EqualTo("ok"));

            var summary = payload["summary"];
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary!["Key"]!.ToString(), Is.EqualTo("ddr_authoring"));
            Assert.That(summary["DisplayName"]!.ToString(), Is.EqualTo("DDR Authoring"));
        }

        [Test]
        public async Task ExecuteAsync_MissingOptionalArrays_DefaultsToEmptyArrays()
        {
            // Arrange
            var argsObject = new
            {
                key = "general",
                display_name = "General",
                description = "General purpose assistant.",
                when_to_use = "Use for everyday questions.",
                status = "active",
                version = "v1",
                is_default = true
            };

            var argumentsJson = JObject.FromObject(argsObject).ToString();
            var context = CreateExecutionContext();

            AgentMode capturedMode = null;

            _agentContextManager
                .Setup(m => m.AddAgentModeAsync(
                    It.IsAny<string>(),
                    It.IsAny<AgentMode>(),
                    It.IsAny<EntityHeader>(),
                    It.IsAny<EntityHeader>()))
                .Callback<string, AgentMode, EntityHeader, EntityHeader>((_, mode, __, ___) =>
                {
                    capturedMode = mode;
                })
                .ReturnsAsync(() => InvokeResult.Success);

            // Act
            var result = await _tool.ExecuteAsync(argumentsJson, context, CancellationToken.None);

            // Assert
            Assert.That(capturedMode, Is.Not.Null);

            Assert.That(capturedMode.Key, Is.EqualTo("general"));
            Assert.That(capturedMode.DisplayName, Is.EqualTo("General"));
            Assert.That(capturedMode.Description, Is.EqualTo("General purpose assistant."));
            Assert.That(capturedMode.WhenToUse, Is.EqualTo("Use for everyday questions."));
            Assert.That(capturedMode.Status, Is.EqualTo("active"));
            Assert.That(capturedMode.Version, Is.EqualTo("v1"));
            Assert.That(capturedMode.IsDefault, Is.True);

            // Arrays → empty
            Assert.That(capturedMode.ModeInstructions, Is.Empty);
            Assert.That(capturedMode.BehaviorHints, Is.Empty);
            Assert.That(capturedMode.HumanRoleHints, Is.Empty);
            Assert.That(capturedMode.AssociatedToolIds, Is.Empty);
            Assert.That(capturedMode.ToolGroupHints, Is.Empty);
            Assert.That(capturedMode.RagScopeHints, Is.Empty);
            Assert.That(capturedMode.StrongSignals, Is.Empty);
            Assert.That(capturedMode.WeakSignals, Is.Empty);
            Assert.That(capturedMode.ExampleUtterances, Is.Empty);

            Assert.That(result.Successful, Is.True);
        }
    }
}
