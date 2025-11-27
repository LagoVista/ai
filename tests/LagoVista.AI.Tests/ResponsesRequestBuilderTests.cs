using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using LagoVista.AI.Helpers;
using LagoVista.AI.Models;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;

namespace LagoVista.AI.Tests
{
    [TestFixture]
    public class ResponsesRequestBuilderTests
    {
        private ConversationContext CreateConversationContext()
        {
            return new ConversationContext
            {
                Id = "conv-ctx-1",
                Name = "Test Conversation Context",
                ModelName = "gpt-5.1",
                System = "You are the Aptix Reasoner.",
                Temperature = 0.7f
            };
        }

        private AgentExecuteRequest CreateRequest(
            string mode = "TEST_MODE",
            string instruction = "Do something useful.",
            string previousResponseId = null,
            string toolsJson = null,
            string toolChoiceName = null)
        {
            return new AgentExecuteRequest
            {
                ConversationId = "conv-1",
                Mode = mode,
                Instruction = instruction,
                ResponseContinuationId = previousResponseId,
                AgentContext = new EntityHeader { Id = "agent-ctx", Text = "Agent Ctx" },
                ConversationContext = new EntityHeader { Id = "conv-ctx-1", Text = "Conv Ctx" },
                ToolsJson = toolsJson,
                ToolChoiceName = toolChoiceName,
                ActiveFiles = new List<ActiveFile>()
            };
        }

        [Test]
        public void Type_On_Content_ShoudBe_input_text()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest();

            var dto = ResponsesRequestBuilder.Build(convCtx, request, string.Empty);
            Assert.That(dto.Input[0].Content[0].Type, Is.EqualTo("input_text"));
        }

        [Test]
        public void Build_InitialRequest_IncludesSystemMessageAndNoPreviousResponseId()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest();

            var dto = ResponsesRequestBuilder.Build(convCtx, request, string.Empty);

            Assert.That(dto.Model, Is.EqualTo("gpt-5.1"));
            Assert.That(dto.Temperature, Is.EqualTo(0.7f));
            Assert.That(dto.PreviousResponseId, Is.Null);

            Assert.That(dto.Input.Count, Is.GreaterThanOrEqualTo(2));

            var systemMessage = dto.Input[0];
            Assert.That(systemMessage.Role, Is.EqualTo("system"));
            Assert.That(systemMessage.Content.Count, Is.EqualTo(1));
            Assert.That(systemMessage.Content[0].Text, Is.EqualTo("You are the Aptix Reasoner."));
        }

        [Test]
        public void Build_ContinuationRequest_SetsPreviousResponseIdAndOmitsSystemMessage()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest(previousResponseId: "resp_123");

            var dto = ResponsesRequestBuilder.Build(convCtx, request, string.Empty);

            Assert.That(dto.PreviousResponseId, Is.EqualTo("resp_123"));

            Assert.That(dto.Input.Count, Is.EqualTo(1));
            var onlyMessage = dto.Input[0];
            Assert.That(onlyMessage.Role, Is.EqualTo("user"));
        }

        [Test]
        public void Build_UserMessage_ContainsModeAndInstruction()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest(mode: "DDR_CREATION", instruction: "Create a DDR.");

            var dto = ResponsesRequestBuilder.Build(convCtx, request, string.Empty);

            var userMessage = dto.Input[dto.Input.Count - 1];
            Assert.That(userMessage.Role, Is.EqualTo("user"));
            Assert.That(userMessage.Content.Count, Is.EqualTo(1));

            var text = userMessage.Content[0].Text;
            Assert.That(text, Does.Contain("[MODE: DDR_CREATION]"));
            Assert.That(text, Does.Contain("[INSTRUCTION]"));
            Assert.That(text, Does.Contain("Create a DDR."));
        }

        [Test]
        public void Build_IncludesRagContextBlock_WhenProvided()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest();
            var ragContextBlock = "[CONTEXT]\n\n=== CHUNK 1 ===\nId: ctx_1";

            var dto = ResponsesRequestBuilder.Build(convCtx, request, ragContextBlock);

            var userMessage = dto.Input[dto.Input.Count - 1];
            Assert.That(userMessage.Role, Is.EqualTo("user"));
            Assert.That(userMessage.Content.Count, Is.EqualTo(2));

            var ctxItem = userMessage.Content[1];
            Assert.That(ctxItem.Text, Is.EqualTo(ragContextBlock));
        }

        [Test]
        public void Build_InitialRequest_WithToolsJson_ParsesToolsIntoDto()
        {
            var convCtx = CreateConversationContext();

            var toolsJson = "[" +
                            "{\"name\":\"ddr_document\",\"description\":\"Create or update a DDR\",\"input_schema\":{},\"output_schema\":{}}" +
                            "]";

            var request = CreateRequest(toolsJson: toolsJson);

            var dto = ResponsesRequestBuilder.Build(convCtx, request, string.Empty);

            Assert.That(dto.Tools, Is.Not.Null);
            Assert.That(dto.Tools.Count, Is.EqualTo(1));

            var tool = dto.Tools[0];
            Assert.That(tool.Value<string>("name"), Is.EqualTo("ddr_document"));
            Assert.That(tool.Value<string>("description"), Does.Contain("DDR"));
        }

        [Test]
        public void Build_ContinuationRequest_DoesNotSendToolsEvenIfPresent()
        {
            var convCtx = CreateConversationContext();

            var toolsJson = "[" +
                            "{\"name\":\"ddr_document\",\"description\":\"Create or update a DDR\"}" +
                            "]";

            var request = CreateRequest(previousResponseId: "resp_123", toolsJson: toolsJson);

            var dto = ResponsesRequestBuilder.Build(convCtx, request, string.Empty);

            Assert.That(dto.Tools, Is.Null);
        }

        [Test]
        public void Build_WithToolChoice_SetsToolChoiceOnDto()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest(toolChoiceName: "ddr_document");

            var dto = ResponsesRequestBuilder.Build(convCtx, request, string.Empty);

            Assert.That(dto.ToolChoice, Is.Not.Null);
            Assert.That(dto.ToolChoice.Type, Is.EqualTo("tool"));
            Assert.That(dto.ToolChoice.Name, Is.EqualTo("ddr_document"));
        }
    }
}
