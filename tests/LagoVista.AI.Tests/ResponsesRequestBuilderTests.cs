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
                SystemPrompts = new List<string>() { "You are the Aptix Reasoner." },
                Temperature = 0.7f
            };
        }

        private AgentExecuteRequest CreateRequest(
            string mode = "TEST_MODE",
            string instruction = "Do something useful.",
            string previousResponseId = null,
            string toolsJson = null,
            string toolChoiceName = null,
            string toolResultsJson = null)
        {
            return new AgentExecuteRequest
            {
                SessionId = "conv-1",
                Mode = mode,
                Instruction = instruction,
                ResponseContinuationId = previousResponseId,
                AgentContext = new EntityHeader { Id = "agent-ctx", Text = "Agent Ctx" },
                ConversationContext = new EntityHeader { Id = "conv-ctx-1", Text = "Conv Ctx" },
                ToolsJson = toolsJson,
                ToolChoiceName = toolChoiceName,
                ToolResultsJson = toolResultsJson,
                ActiveFiles = new List<ActiveFile>()
            };
        }

        private ResponsesRequestBuilder _builder = new ResponsesRequestBuilder();

        [Test]
        public void Type_On_Content_ShoudBe_input_text()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest();

            var dto = _builder.Build(convCtx, request, string.Empty, string.Empty);
            Assert.That(dto.Input[0].Content[0].Type, Is.EqualTo("input_text"));
        }

        [Test]
        public void Build_InitialRequest_IncludesSystemMessageAndNoPreviousResponseId()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest();

            var dto = _builder.Build(convCtx, request, string.Empty, string.Empty);

            Assert.That(dto.Model, Is.EqualTo("gpt-5.1"));
            Assert.That(dto.Temperature, Is.EqualTo(0.7f));
            Assert.That(dto.PreviousResponseId, Is.Null);

            Assert.That(dto.Input.Count, Is.GreaterThanOrEqualTo(2));

            var systemMessage = dto.Input[0];
            Assert.That(systemMessage.Role, Is.EqualTo("system"));
            Assert.That(systemMessage.Content.Count, Is.EqualTo(2));
            Assert.That(systemMessage.Content[0].Text, Is.EqualTo("You are the Aptix Reasoner."));
        }

        [Test]
        public void Build_InitialRequest_IncludesSystemPrompt_WhenProvided()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest();
            request.SystemPrompt = "You are working only with billing-related data.";

            var dto = _builder.Build(convCtx, request, string.Empty, string.Empty);

            Assert.That(dto.Input.Count, Is.GreaterThanOrEqualTo(2));

            var systemMessage = dto.Input[0];
            Assert.That(systemMessage.Role, Is.EqualTo("system"));
            Assert.That(systemMessage.Content.Count, Is.EqualTo(3));

            Assert.That(systemMessage.Content[0].Text, Is.EqualTo("You are the Aptix Reasoner."));
            Assert.That(systemMessage.Content[1].Text, Is.EqualTo("You are working only with billing-related data."));
        }

        [Test]
        public void Build_InitialRequest_IncludesSystemPrompt_AndToolUsageMetadata_WhenBothProvided()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest();
            request.SystemPrompt = "You are working only with billing-related data.";

            var toolUsageMetadataBlock =
                "<<<APTIX_SERVER_TOOL_USAGE_METADATA_BEGIN>>>\n...tool usage here...\n<<<APTIX_SERVER_TOOL_USAGE_METADATA_END>>>";

            var dto = _builder.Build(convCtx, request, string.Empty, toolUsageMetadataBlock);

            Assert.That(dto.Input.Count, Is.GreaterThanOrEqualTo(2));

            var systemMessage = dto.Input[0];
            Assert.That(systemMessage.Role, Is.EqualTo("system"));
            Assert.That(systemMessage.Content.Count, Is.EqualTo(4));

            Assert.That(systemMessage.Content[0].Text, Is.EqualTo("You are the Aptix Reasoner."));
            Assert.That(systemMessage.Content[1].Text, Is.EqualTo("You are working only with billing-related data."));
            Assert.That(systemMessage.Content[3].Text, Is.EqualTo(toolUsageMetadataBlock));
        }

        [Test]
        public void Build_InitialRequest_IncludesSystemPrompt_AndToolUsageMetadata_WhenNoConversationSystemPrompts()
        {
            var convCtx = CreateConversationContext();
            convCtx.SystemPrompts.Clear();

            var request = CreateRequest();
            request.SystemPrompt = "You are working only with billing-related data.";

            var toolUsageMetadataBlock =
                "<<<APTIX_SERVER_TOOL_USAGE_METADATA_BEGIN>>>\n...tool usage here...\n<<<APTIX_SERVER_TOOL_USAGE_METADATA_END>>>";

            var dto = _builder.Build(convCtx, request, string.Empty, toolUsageMetadataBlock);

            Assert.That(dto.Input.Count, Is.GreaterThanOrEqualTo(2));

            var systemMessage = dto.Input[0];
            Assert.That(systemMessage.Role, Is.EqualTo("system"));

            // No conversation-level system prompts; we should see just SystemPrompt + tool usage metadata
            Assert.That(systemMessage.Content.Count, Is.EqualTo(3));
            Assert.That(systemMessage.Content[0].Text, Is.EqualTo("You are working only with billing-related data."));
            Assert.That(systemMessage.Content[2].Text, Is.EqualTo(toolUsageMetadataBlock));
        }

        [Test]
        public void Build_ContinuationRequest_IncludesSystemMessage_AndSetsPreviousResponseId()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest(previousResponseId: "resp_123");
            request.SystemPrompt = "Continuation-specific override.";

            var dto = _builder.Build(convCtx, request, string.Empty, string.Empty);

            Assert.That(dto.PreviousResponseId, Is.EqualTo("resp_123"));

            // We now expect a system message + a user message on continuation as well
            Assert.That(dto.Input.Count, Is.EqualTo(2));

            var systemMessage = dto.Input[0];
            Assert.That(systemMessage.Role, Is.EqualTo("system"));
            Assert.That(systemMessage.Content.Count, Is.EqualTo(3));
            Assert.That(systemMessage.Content[0].Text, Is.EqualTo("You are the Aptix Reasoner."));
            Assert.That(systemMessage.Content[1].Text, Is.EqualTo("Continuation-specific override."));

            var userMessage = dto.Input[1];
            Assert.That(userMessage.Role, Is.EqualTo("user"));
        }

        [Test]
        public void Build_UserMessage_ContainsModeAndInstruction()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest(mode: "DDR_CREATION", instruction: "Create a DDR.");

            var dto = _builder.Build(convCtx, request, string.Empty, string.Empty);

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

            var dto = _builder.Build(convCtx, request, ragContextBlock, string.Empty);

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

            var dto = _builder.Build(convCtx, request, string.Empty, string.Empty);

            Assert.That(dto.Tools, Is.Not.Null);
            Assert.That(dto.Tools.Count, Is.EqualTo(1));

            var tool = dto.Tools[0];
            Assert.That(tool.Value<string>("name"), Is.EqualTo("ddr_document"));
            Assert.That(tool.Value<string>("description"), Does.Contain("DDR"));
        }

        [Test]
        public void Build_ContinuationRequest_StillSendsTools_WhenPresent()
        {
            var convCtx = CreateConversationContext();

            var toolsJson = "[" +
                            "{\"name\":\"ddr_document\",\"description\":\"Create or update a DDR\"}" +
                            "]";

            var request = CreateRequest(previousResponseId: "resp_123", toolsJson: toolsJson);

            var dto = _builder.Build(convCtx, request, string.Empty, string.Empty);

            Assert.That(dto.PreviousResponseId, Is.EqualTo("resp_123"));
            Assert.That(dto.Tools, Is.Not.Null);
            Assert.That(dto.Tools.Count, Is.EqualTo(1));

            var tool = dto.Tools[0];
            Assert.That(tool.Value<string>("name"), Is.EqualTo("ddr_document"));
        }

        [Test]
        public void Build_WithToolChoice_SetsToolChoiceOnDto()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest(toolChoiceName: "ddr_document");

            var dto = _builder.Build(convCtx, request, string.Empty, string.Empty);

            Assert.That(dto.ToolChoice, Is.Not.Null);
            Assert.That(dto.ToolChoice.Type, Is.EqualTo("tool"));
            Assert.That(dto.ToolChoice.Name, Is.EqualTo("ddr_document"));
        }

        [Test]
        public void Build_ContinuationRequest_WithToolResults_AppendsToolResultsBlock()
        {
            var convCtx = CreateConversationContext();

            // Simulate a continuation with tool results from the server-side tool executor.
            var request = CreateRequest(
                previousResponseId: "resp_123",
                toolsJson: null
            );

            var toolCalls = new[]
            {
                new
                {
                    CallId = "call_123",
                    Name = "testing_ping_pong",
                    ArgumentsJson = "{\"message\":\"hello\",\"count\":0}",
                    IsServerTool = true,
                    WasExecuted = true,
                    ResultJson = "{\"Reply\":\"pong: hello\",\"Count\":1}",
                    ErrorMessage = (string)null
                }
            };

            request.ToolResultsJson = Newtonsoft.Json.JsonConvert.SerializeObject(toolCalls);


            var dto = _builder.Build(convCtx, request, string.Empty, string.Empty);

            // Still a continuation.
            // When we return a tool result that should get routed without a response id, the call_id is used to identify the tool and populate it (I assume)
            //Assert.That(dto.PreviousResponseId, Is.EqualTo("resp_123"));
            Assert.That(dto.PreviousResponseId, Is.Null);   

            // Expect system + user message now
            Assert.That(dto.Input.Count, Is.EqualTo(2));

            var userMessage = dto.Input[1];
            Assert.That(userMessage.Role, Is.EqualTo("user"));
            Assert.That(userMessage.Content.Count, Is.EqualTo(2));

            var instructionContent = userMessage.Content[0];
            var toolResultsContent = userMessage.Content[1];

            Assert.That(toolResultsContent.Text, Does.Contain("[TOOL_RESULTS]"));
            Assert.That(toolResultsContent.Text, Does.Contain("testing_ping_pong"));
        }

        [Test]
        public void Build_InitialRequest_IncludesToolUsageMetadata_WhenProvided()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest();
            var toolUsageMetadataBlock =
                "<<<APTIX_SERVER_TOOL_USAGE_METADATA_BEGIN>>>\n...tool usage here...\n<<<APTIX_SERVER_TOOL_USAGE_METADATA_END>>>";

            var dto = _builder.Build(convCtx, request, string.Empty, toolUsageMetadataBlock);

            Assert.That(dto.Input.Count, Is.GreaterThanOrEqualTo(2));

            var systemMessage = dto.Input[0];
            Assert.That(systemMessage.Role, Is.EqualTo("system"));
            Assert.That(systemMessage.Content.Count, Is.EqualTo(3));

            Assert.That(systemMessage.Content[0].Text, Is.EqualTo("You are the Aptix Reasoner."));
            Assert.That(systemMessage.Content[2].Text, Is.EqualTo(toolUsageMetadataBlock));
        }

        [Test]
        public void Build_ContinuationRequest_IncludesToolUsageMetadataBlock()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest(previousResponseId: "resp_456");
            var toolUsageMetadataBlock =
                "<<<APTIX_SERVER_TOOL_USAGE_METADATA_BEGIN>>>\n...tool usage here...\n<<<APTIX_SERVER_TOOL_USAGE_METADATA_END>>>";

            var dto = _builder.Build(convCtx, request, string.Empty, toolUsageMetadataBlock);

            Assert.That(dto.PreviousResponseId, Is.EqualTo("resp_456"));

            // Continuation: we still have system + user message,
            // and the toolUsageMetadataBlock should be present on the system message.
            Assert.That(dto.Input.Count, Is.EqualTo(2));

            var systemMessage = dto.Input[0];
            Assert.That(systemMessage.Role, Is.EqualTo("system"));
            Assert.That(systemMessage.Content.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(systemMessage.Content[systemMessage.Content.Count - 1].Text, Is.EqualTo(toolUsageMetadataBlock));

            var userMessage = dto.Input[1];
            Assert.That(userMessage.Role, Is.EqualTo("user"));
        }

        [Test]
        public void Build_WithStreamTrue_SetsStreamTrueOnDto()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest();
            request.Streaming = true;

            var dto = _builder.Build(convCtx, request, string.Empty, string.Empty);

            Assert.That(dto.Stream, Is.True);
        }

        [Test]
        public void Build_WithStreamFalse_SetsStreamFalseOnDto()
        {
            var convCtx = CreateConversationContext();
            var request = CreateRequest();
            request.Streaming = false;

            var dto = _builder.Build(convCtx, request, string.Empty, string.Empty);

            Assert.That(dto.Stream, Is.False);
        }
    }
}
