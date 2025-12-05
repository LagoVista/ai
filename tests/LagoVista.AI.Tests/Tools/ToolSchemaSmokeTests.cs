using System;
using System.Reflection;
using LagoVista.AI.Services.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LagoVista.AI.AgentTools.Tests
{
    /// <summary>
    /// Smoke tests to ensure each tool's GetSchema() can be serialized to
    /// valid JSON and that the name matches the ToolName constant.
    /// </summary>
    [TestFixture]
    public class ToolSchemaSmokeTests
    {
        [TestCase(typeof(PingPongTool))]
        [TestCase(typeof(GetTlaCatalogAgentTool))]
        [TestCase(typeof(AddTlaAgentTool))]
        [TestCase(typeof(CreateDdrAgentTool))]
        [TestCase(typeof(UpdateDdrMetadataAgentTool))]
        [TestCase(typeof(MoveDdrTlaAgentTool))]
        [TestCase(typeof(SetGoalAgentTool))]
        [TestCase(typeof(ApproveGoalAgentTool))]
        [TestCase(typeof(AddChapterAgentTool))]
        [TestCase(typeof(AddChaptersAgentTool))]
        [TestCase(typeof(UpdateChapterSummaryAgentTool))]
        [TestCase(typeof(UpdateChapterDetailsAgentTool))]
        [TestCase(typeof(ApproveChapterAgentTool))]
        [TestCase(typeof(ListChaptersAgentTool))]
        [TestCase(typeof(ReorderChaptersAgentTool))]
        [TestCase(typeof(DeleteChapterAgentTool))]
        [TestCase(typeof(SetDdrStatusAgentTool))]
        [TestCase(typeof(ApproveDdrAgentTool))]
        [TestCase(typeof(GetDdrAgentTool))]
        [TestCase(typeof(ListDdrsAgentTool))]
        [TestCase(typeof(RequestUserApprovalAgentTool))]
        public void ToolSchema_CanBeSerialized_AndHasMatchingName(Type toolType)
        {
            var toolNameField = toolType.GetField(
                "ToolName",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            Assert.That(toolNameField, Is.Not.Null, $"{toolType.Name} must define public const string ToolName.");

            var toolName = toolNameField.GetValue(null) as string;
            Assert.That(toolName, Is.Not.Null.And.Not.Empty, $"{toolType.Name}.ToolName must be a non-empty string.");

            var getSchemaMethod = toolType.GetMethod(
                "GetSchema",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
                null,
                Type.EmptyTypes,
                null);

            Assert.That(getSchemaMethod, Is.Not.Null, $"{toolType.Name} must define public static object GetSchema().");

            var schema = getSchemaMethod.Invoke(null, Array.Empty<object>());
            Assert.That(schema, Is.Not.Null, $"{toolType.Name}.GetSchema() returned null.");

            var json = JsonConvert.SerializeObject(schema);
            Assert.That(json, Is.Not.Null.And.Not.Empty, $"{toolType.Name} schema JSON must not be empty.");

            var jObj = JObject.Parse(json);

            Assert.That(jObj.Value<string>("type"), Is.EqualTo("function"),
                $"{toolType.Name} schema type must be 'function'.");

            Assert.That(jObj.Value<string>("name"), Is.EqualTo(toolName),
                $"{toolType.Name} schema name must match ToolName.");

            var parameters = jObj["parameters"] as JObject;
            Assert.That(parameters, Is.Not.Null, $"{toolType.Name} schema must define 'parameters'.");
            Assert.That(parameters.Value<string>("type"), Is.EqualTo("object"),
                $"{toolType.Name} parameters.type must be 'object'.");
        }
    }
}
