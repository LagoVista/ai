using System;
using System.Linq;
using NUnit.Framework;
using LagoVista.AI.Helpers;
using LagoVista.Core.AI.Models;
using LagoVista.Core.Models;
using Newtonsoft.Json.Linq;

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

            var result = AgentExecuteResponseParser.Parse(string.Empty, request);
            Assert.That(result.Successful, Is.False, result.ErrorMessage);
        }

        [Test]
        public void Parse_InvalidJson_ReturnsError()
        {
            var request = CreateDefaultRequest();
            const string rawJson = "{ not valid json";

            var result = AgentExecuteResponseParser.Parse(rawJson, request);
            Assert.That(result.Successful, Is.False, result.ErrorMessage);
        }

        [Test]
        public void Parse_MessageOutputWithContentArray_MapsTextUsageAndKindOk()
        {
            var request = CreateDefaultRequest();

            const string rawJson = @"{
  ""id"": ""resp_real_world"",
  ""object"": ""response"",
  ""model"": ""gpt-4.1-mini"",
  ""usage"": {
    ""prompt_tokens"": 21,
    ""completion_tokens"": 34,
    ""total_tokens"": 55
  },
  ""output"": [
    {
      ""id"": ""rs_00153e3b7b97a4f0006928564bc48d18cbfd7f5ee57a209"",
      ""type"": ""reasoning"",
      ""summary"": []
    },
    {
      ""id"": ""msg_00153e3b7b97a4f000692b5d4ba50881ca7b505d29c3147c7"",
      ""type"": ""message"",
      ""role"": ""assistant"",
      ""status"": ""completed"",
      ""content"": [
        {
          ""type"": ""output_text"",
          ""annotations"": [],
          ""logprobs"": [],
          ""text"": ""Short answer for this codebase.""
        }
      ]
    }
  ]
}";

            var result = AgentExecuteResponseParser.Parse(rawJson, request);
            var response = result.Result;

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(response.Kind, Is.EqualTo("ok"));

            // Ids
            Assert.That(response.ResponseContinuationId, Is.EqualTo("resp_real_world"));
            Assert.That(response.TurnId, Is.EqualTo("resp_real_world"));

            // Model
            Assert.That(response.ModelId, Is.EqualTo("gpt-4.1-mini"));

            // Text extracted from the message.content[0].text
            Assert.That(response.Text, Is.EqualTo("Short answer for this codebase."));

            // Usage mapped correctly
            Assert.That(response.Usage.PromptTokens, Is.EqualTo(21));
            Assert.That(response.Usage.CompletionTokens, Is.EqualTo(34));
            Assert.That(response.Usage.TotalTokens, Is.EqualTo(55));

            // No tool calls expected in this payload
            Assert.That(response.ToolCalls, Is.Empty);
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

            var result = AgentExecuteResponseParser.Parse(rawJson, request);
            Assert.That(result.Successful, Is.False, result.ErrorMessage);
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

            var result = AgentExecuteResponseParser.Parse(rawJson, request);
            var response = result.Result;
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

            var result = AgentExecuteResponseParser.Parse(rawJson, request);
            var response = result.Result;
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

            var result = AgentExecuteResponseParser.Parse(rawJson, request);
            var response = result.Result;
            Assert.That(response.Kind, Is.EqualTo("ok"));
            Assert.That(response.Text, Is.EqualTo("I will create a DDR for you."));
            Assert.That(response.ToolCalls.Count, Is.EqualTo(1));
            Assert.That(response.FinishReason, Is.EqualTo("tool_use"));
        }


        [Test]
        public void Parse_ToolOnly_MessageShape_ExtractsToolCallAndClassifiesToolOnly()
        {
            var request = CreateDefaultRequest();

            const string rawJson = @"{
  ""id"": ""resp_tool_only_message"",
  ""object"": ""response"",
  ""model"": ""gpt-5.1"",
  ""usage"": {
    ""prompt_tokens"": 12,
    ""completion_tokens"": 8,
    ""total_tokens"": 20
  },
  ""output"": [
    {
      ""id"": ""msg_tool_only"",
      ""type"": ""message"",
      ""role"": ""assistant"",
      ""status"": ""completed"",
      ""content"": [
        {
          ""type"": ""tool_call"",
          ""tool_call"": {
            ""id"": ""call_msg_1"",
            ""name"": ""ddr_document"",
            ""arguments"": {
              ""id"": ""IDX-0100"",
              ""title"": ""Tool Only From Message""
            },
            ""finish_reason"": ""tool_use""
          }
        }
      ]
    }
  ]
}";

            var result = AgentExecuteResponseParser.Parse(rawJson, request);
            Assert.That(result.Successful, Is.True, result.ErrorMessage);

            var response = result.Result;
            Assert.That(response.Kind, Is.EqualTo("tool-only"));
            Assert.That(response.Text, Is.Null.Or.Empty);
            Assert.That(response.ToolCalls.Count, Is.EqualTo(1));

            var call = response.ToolCalls.Single();
            Assert.That(call.CallId, Is.EqualTo("call_msg_1"));
            Assert.That(call.Name, Is.EqualTo("ddr_document"));
            Assert.That(call.ArgumentsJson, Does.Contain("IDX-0100").And.Contain("Tool Only From Message"));
            Assert.That(response.FinishReason, Is.EqualTo("tool_use"));
        }

        [Test]
        public void Parse_TextAndTool_MessageShape_CollectsBothAndKindOk()
        {
            var request = CreateDefaultRequest();

            const string rawJson = @"{
  ""id"": ""resp_mixed_message"",
  ""object"": ""response"",
  ""model"": ""gpt-5.1"",
  ""usage"": {
    ""prompt_tokens"": 42,
    ""completion_tokens"": 58,
    ""total_tokens"": 100
  },
  ""output"": [
    {
      ""id"": ""msg_mixed"",
      ""type"": ""message"",
      ""role"": ""assistant"",
      ""status"": ""completed"",
      ""content"": [
        {
          ""type"": ""output_text"",
          ""annotations"": [],
          ""logprobs"": [],
          ""text"": ""Here is your DDR and I will call a tool.""
        },
        {
          ""type"": ""tool_call"",
          ""tool_call"": {
            ""id"": ""call_msg_2"",
            ""name"": ""ddr_document"",
            ""arguments"": {
              ""id"": ""IDX-0200"",
              ""title"": ""Mixed Message DDR""
            },
            ""finish_reason"": ""tool_use""
          }
        }
      ]
    }
  ]
}";

            var result = AgentExecuteResponseParser.Parse(rawJson, request);
            Assert.That(result.Successful, Is.True, result.ErrorMessage);

            var response = result.Result;
            Assert.That(response.Kind, Is.EqualTo("ok"));
            Assert.That(response.Text, Is.EqualTo("Here is your DDR and I will call a tool."));
            Assert.That(response.ToolCalls.Count, Is.EqualTo(1));

            var call = response.ToolCalls.Single();
            Assert.That(call.CallId, Is.EqualTo("call_msg_2"));
            Assert.That(call.Name, Is.EqualTo("ddr_document"));
            Assert.That(call.ArgumentsJson, Does.Contain("IDX-0200").And.Contain("Mixed Message DDR"));
            Assert.That(response.FinishReason, Is.EqualTo("tool_use"));
        }

        [Test]
        public void Parse_ReasoningOnlyOutput_ReturnsNoToolOrTextError()
        {
            var request = CreateDefaultRequest();

            const string rawJson = @"{
  ""id"": ""resp_reasoning_only"",
  ""object"": ""response"",
  ""model"": ""gpt-5.1"",
  ""usage"": {
    ""prompt_tokens"": 10,
    ""completion_tokens"": 5,
    ""total_tokens"": 15
  },
  ""output"": [
    {
      ""id"": ""rs_123"",
      ""type"": ""reasoning"",
      ""summary"": []
    }
  ]
}";

            var result = AgentExecuteResponseParser.Parse(rawJson, request);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage,
                Does.Contain("No tool or text response"),
                "Parser should fail when there is output but it contains neither text nor tool calls.");
        }



        [Test]
        public void Parse_FunctionCallOnlyResponse_TreatedAsToolOnly()
        {
            var request = CreateDefaultRequest();

            const string rawJson = @"{
  ""id"": ""resp_func_call_only"",
  ""object"": ""response"",
  ""model"": ""gpt-5-2025-08-07"",
  ""usage"": {
    ""input_tokens"": 784,
    ""input_tokens_details"": { ""cached_tokens"": 0 },
    ""output_tokens"": 157,
    ""output_tokens_details"": { ""reasoning_tokens"": 128 },
    ""total_tokens"": 941
  },
  ""output"": [
    {
      ""id"": ""rs_foo"",
      ""type"": ""reasoning"",
      ""summary"": []
    },
    {
      ""id"": ""fc_bar"",
      ""type"": ""function_call"",
      ""status"": ""completed"",
      ""arguments"": ""{\""message\"":\""hello from VS Code\"",\""count\"":0}"",
      ""call_id"": ""call_Stn77FFUlDVMln2NuzFYaeDN"",
      ""name"": ""testing_ping_pong""
    }
  ]
}";

            var result = AgentExecuteResponseParser.Parse(rawJson, request);
            Assert.That(result.Successful, Is.True, result.ErrorMessage);

            var response = result.Result;
            Assert.That(response.Kind, Is.EqualTo("tool-only"));
            Assert.That(response.Text, Is.Null.Or.Empty);

            Assert.That(response.ToolCalls, Is.Not.Null);
            Assert.That(response.ToolCalls.Count, Is.EqualTo(1));

            var call = response.ToolCalls.Single();
            Assert.That(call.CallId, Is.EqualTo("call_Stn77FFUlDVMln2NuzFYaeDN"));
            Assert.That(call.Name, Is.EqualTo("testing_ping_pong"));
            Assert.That(call.ArgumentsJson, Does.Contain("hello from VS Code").And.Contain("\"count\":0"));

            // New usage mapping should pick up input/output_tokens.
            Assert.That(response.Usage.PromptTokens, Is.EqualTo(784));
            Assert.That(response.Usage.CompletionTokens, Is.EqualTo(157));
            Assert.That(response.Usage.TotalTokens, Is.EqualTo(941));
        }
    }
}