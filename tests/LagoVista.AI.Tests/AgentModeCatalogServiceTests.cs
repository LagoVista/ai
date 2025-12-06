using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

using LagoVista.AI.Services;

namespace LagoVista.AI.Tests
{
    [TestFixture]
    public class AgentModeCatalogServiceTests
    {
        [Test]
        public void Constructor_WithValidCatalog_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new AgentModeCatalogService());
        }

        [Test]
        public async Task GetAllModesAsync_ReturnsAtLeastThreeModes()
        {
            var svc = new AgentModeCatalogService();

            var modes = await svc.GetAllModesAsync(CancellationToken.None);

            Assert.That(modes, Is.Not.Null);
            Assert.That(modes.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(modes.Any(m => m.Key == "general"), Is.True);
            Assert.That(modes.Any(m => m.Key == "ddr_authoring"), Is.True);
            Assert.That(modes.Any(m => m.Key == "workflow_authoring"), Is.True);
        }

        [Test]
        public void GetMode_WithKnownKey_ReturnsMode()
        {
            var svc = new AgentModeCatalogService();

            var mode = svc.GetMode("general");

            Assert.That(mode, Is.Not.Null);
            Assert.That(mode.Key, Is.EqualTo("general"));
        }

        [Test]
        public void GetMode_WithUnknownKey_Throws()
        {
            var svc = new AgentModeCatalogService();

            var ex = Assert.Throws<InvalidOperationException>(() => svc.GetMode("unknown_mode"));

            Assert.That(ex!.Message, Does.Contain("unknown mode key 'unknown_mode'"));
        }

        [Test]
        public void GetToolsForMode_WithKnownKey_ReturnsTools()
        {
            var svc = new AgentModeCatalogService();

            var tools = svc.GetToolsForMode("general");

            Assert.That(tools, Is.Not.Null);
            Assert.That(tools.Count, Is.GreaterThan(0));
            Assert.That(tools, Does.Contain("agent_change_mode"));
        }

        [Test]
        public void GetToolsForMode_WithUnknownKey_Throws()
        {
            var svc = new AgentModeCatalogService();

            var ex = Assert.Throws<InvalidOperationException>(() => svc.GetToolsForMode("bad_mode"));

            Assert.That(ex!.Message, Does.Contain("unknown mode key 'bad_mode'"));
        }

        [Test]
        public void BuildSystemPrompt_IncludesCurrentModeAndAllModes()
        {
            var svc = new AgentModeCatalogService();

            var prompt = svc.BuildSystemPrompt("ddr_authoring");

            Assert.That(prompt, Does.Contain("Current Mode: ddr_authoring"));
            Assert.That(prompt, Does.Contain("general:"));
            Assert.That(prompt, Does.Contain("ddr_authoring:"));
            Assert.That(prompt, Does.Contain("workflow_authoring:"));
        }

        [Test]
        public void BuildSystemPrompt_WithUnknownCurrentMode_FallsBackToDefault()
        {
            var svc = new AgentModeCatalogService();

            var prompt = svc.BuildSystemPrompt("non_existent_mode");

            Assert.That(prompt, Does.Contain("Current Mode: general"));
        }
    }
}
