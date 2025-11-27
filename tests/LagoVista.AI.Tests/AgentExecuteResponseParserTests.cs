using System;
using System.Linq;
using NUnit.Framework;
using LagoVista.AI.Helpers;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;

namespace LagoVista.AI.Tests
{
    [TestFixture]
    public class AgentExecuteResponseParserTests
    {
        private AgentExecuteRequest CreateDefaultRequest()
        {
            return new AgentExecuteRequest
            {
                ConversationId = "conv-1",
                Mode = "TEST_MODE",
                AgentContext = new EntityHeader { Id = "agent-ctx", Text = "Agent Ctx" },
                ConversationContext = new EntityHeader { Id = "conv-ctx", Text = "Conv Ctx" }
            };
        }

        [Test]
        public void Parse_EmptyJson_ReturnsError()
        {
            var request = CreateDefaultRequest();

            var response = AgentExecuteResponseParser.Parse(string.Empty, request);

            Assert.That(response.Kind, Is.EqualTo("error"));
            Assert.That(response.ErrorCode, Is.EqualTo("empty-json"));
            Assert.That(response.ErrorMessage, Does.Contain("Empty or null"));
            Assert.That(response.ConversationId, Is.EqualTo("conv-1"));
            Assert.That(response.Mode, Is.EqualTo("TEST_MODE"));
        }

        [Test]
        public void Parse_InvalidJson_ReturnsError()
        {
            var request = CreateDefaultRequest();
            const string rawJson = "{ not valid json";

            var response = AgentExecuteResponseParser.Parse(rawJson, request);

            Assert.That(response.Kind, Is.EqualTo("error"));
            Assert.That(response.ErrorCode, Is.EqualTo("json-parse-failure"));
            Assert.That(response.ErrorMessage, Does.Contain("Failed to parse JSON"));
        }

        [Test]
        public void Parse_MissingOutputArray_ReturnsError()
        {
            var request = CreateDefaultRequest();
            const string rawJson = @"{
  ""id"": ""resp_no_output"",
  ""model"": ""gpt-5.1"",
  ""usage"": {
    ""prompt_tokens"": 10,
    ""completion_tokens"": 5,
    ""total_tokens"": 15
  }
}";

            var response = AgentExecuteResponseParser.Parse(rawJson, request);

            Assert.That(response.Kind, Is.EqualTo("error"));
            Assert.That(response.ErrorCode, Is.EqualTo("missing-output"));
            Assert.That(response.ErrorMessage, Does.Contain("did not contain an 'output' array"));
        }

        [Test]
        public void Parse_TextOnlyResponse_MapsTextUsageAndKindOk()
        {
            var request = CreateDefaultRequest();
            const string rawJson = @"{
  ""id"": ""resp_text_only"",
  ""model"": ""gpt-5.1"",
  ""usage"": {
    ""prompt_tokens"": 21,
    ""completion_tokens"": 34,
    ""total_tokens"": 55
  },
  ""output"": [
    {
      ""type"": ""output_text"",
      ""text"": ""Hello from the model."",
      ""finish_reason"": ""stop""
    }
  ]
}";

            var response = AgentExecuteResponseParser.Parse(rawJson, request);

            Assert.That(response.Kind, Is.EqualTo("ok"));
            Assert.That(response.ResponseContinuationId, Is.EqualTo("resp_text_only"));
            Assert.That(response.TurnId, Is.EqualTo("resp_text_only"));
            Assert.That(response.ModelId, Is.EqualTo("gpt-5.1"));
            Assert.That(response.Text, Is.EqualTo("Hello from the model."));
            Assert.That(response.Usage.PromptTokens, Is.EqualTo(21));
            Assert.That(response.Usage.CompletionTokens, Is.EqualTo(34));
            Assert.That(response.Usage.TotalTokens, Is.EqualTo(55));
            Assert.That(response.FinishReason, Is.EqualTo("stop"));
            Assert.That(response.ToolCalls, Is.Empty);
        }

        [Test]
        public void Parse_ToolOnlyResponse_ClassifiesToolOnlyAndExtractsToolCall()
        {
            var request = CreateDefaultRequest();
            const string rawJson = @"{
  ""id"": ""resp_tool_only"",
  ""model"": ""gpt-5.1"",
  ""usage"": {
    ""prompt_tokens"": 30,
    ""completion_tokens"": 40,
    ""total_tokens"": 70
  },
  ""output"": [
    {
      ""type"": ""tool_call"",
      ""tool_call"": {
        ""id"": ""call_1"",
        ""name"": ""ddr_document"",
        ""arguments"": {
          ""id"": ""IDX-0001"",
          ""title"": ""Sample DDR""
        },
        ""finish_reason"": ""tool_use""
      }
    }
  ]
}";

            var response = AgentExecuteResponseParser.Parse(rawJson, request);

            Assert.That(response.Kind, Is.EqualTo("tool-only"));
            Assert.That(response.Text, Is.Null.Or.Empty);
            Assert.That(response.ToolCalls.Count, Is.EqualTo(1));

            var call = response.ToolCalls.Single();
            Assert.That(call.CallId, Is.EqualTo("call_1"));
            Assert.That(call.Name, Is.EqualTo("ddr_document"));
            Assert.That(call.ArgumentsJson, Does.Contain("""IDX-0001""").And.Contain("Sample DDR"));
            Assert.That(response.FinishReason, Is.EqualTo("tool_use"));
        }

        [Test]
        public void Parse_TextAndToolResponse_CollectsBothAndKindOk()
        {
            var request = CreateDefaultRequest();
            const string rawJson = @"{
  ""id"": ""resp_mixed"",
  ""model"": ""gpt-5.1"",
  ""usage"": {
    ""prompt_tokens"": 50,
    ""completion_tokens"": 80,
    ""total_tokens"": 130
  },
  ""output"": [
    {
      ""type"": ""output_text"",
      ""text"": ""I will create a DDR for you."",
      ""finish_reason"": ""stop""
    },
    {
      ""type"": ""tool_call"",
      ""tool_call"": {
        ""id"": ""call_2"",
        ""name"": ""ddr_document"",
        ""arguments"": {
          ""id"": ""IDX-0002"",
          ""title"": ""Another DDR""
        },
        ""finish_reason"": ""tool_use""
      }
    }
  ]
}";

            var response = AgentExecuteResponseParser.Parse(rawJson, request);

            Assert.That(response.Kind, Is.EqualTo("ok"));
            Assert.That(response.Text, Is.EqualTo("I will create a DDR for you."));
            Assert.That(response.ToolCalls.Count, Is.EqualTo(1));
            Assert.That(response.FinishReason, Is.EqualTo("tool_use"));
        }
    }
}
