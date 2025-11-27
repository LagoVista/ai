using LagoVista.AI.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Linq;

namespace LagoVista.AI.Tests
{
    [TestFixture]
    public class OpenAiStreamingEventHelperTests
    {
        [Test]
        public void AnalyzeEventPayload_ResponseCompleted_ExtractsResponseId()
        {
            // Arrange: typical response.completed payload
            var json = @"{
              ""type"": ""response.completed"",
              ""response"": {
                ""id"": ""resp_123"",
                ""output"": [
                  {
                    ""role"": ""assistant"",
                    ""content"": [
                      {
                        ""type"": ""output_text"",
                        ""text"": ""Hello world""
                      }
                    ]
                  }
                ]
              }
            }";

            // Act
            var result = OpenAiStreamingEventHelper.AnalyzeEventPayload(
                "response.completed",
                json
            );

            // Assert
            Assert.That(result.EventType, Is.EqualTo("response.completed"));
            Assert.That(result.ResponseId, Is.EqualTo("resp_123"));
            Assert.That(result.DeltaText, Is.Null);
        }

        [Test]
        public void ExtractCompletedResponseJson_ProducesInnerResponseWithOutput()
        {
            // Arrange: SSE response.completed payload (envelope)
            var sseJson = @"{
      ""type"": ""response.completed"",
      ""response"": {
        ""id"": ""resp_123"",
        ""object"": ""response"",
        ""model"": ""gpt-4.1-mini"",
        ""output"": [
          {
            ""role"": ""assistant"",
            ""content"": [
              {
                ""type"": ""output_text"",
                ""text"": ""Hello world""
              }
            ]
          }
        ]
      }
    }";

            // Act
            var innerJson = OpenAiStreamingEventHelper.ExtractCompletedResponseJson(sseJson);
            Assert.That(innerJson.Successful, Is.EqualTo(true), innerJson.ErrorMessage);

            var root = JObject.Parse(innerJson.Result);
            
            // Assert: now we should see output at the top level
            Assert.That((string)root["id"], Is.EqualTo("resp_123"));
            Assert.That(root["output"], Is.Not.Null, "output node should be present at top-level");
            Assert.That(root["output"].Type, Is.EqualTo(JTokenType.Array));
        }





        [Test]
        public void AnalyzeEventPayload_DeltaEvent_ExtractsDeltaText()
        {
            // Arrange
            var json =
                "{" +
                "\"type\":\"response.output_text.delta\"," +
                "\"delta\":{\"text\":\"Hello\"}" +
                "}";

            // Act
            var result = OpenAiStreamingEventHelper.AnalyzeEventPayload("response.output_text.delta", json);

            // Assert
            Assert.That(result.EventType, Is.EqualTo("response.output_text.delta"));
            Assert.That(result.DeltaText, Is.EqualTo("Hello"));
            Assert.That(result.ResponseId, Is.Null);
        }

        [Test]
        public void AnalyzeEventPayload_DeltaIsString_ExtractsCorrectText()
        {
            // Arrange: exact shape from the real OpenAI payload
            var json = @"{
              ""type"": ""response.output_text.delta"",
              ""sequence_number"": 205,
              ""item_id"": ""msg_05200b76a43d668100692897df4758819f9e48441fbd8af8af"",
              ""output_index"": 1,
              ""content_index"": 0,
              ""delta"": "" execution"",
              ""logprobs"": [],
              ""obfuscation"": ""1VkMGY""
            }";

            // Act
            var result = OpenAiStreamingEventHelper.AnalyzeEventPayload(
                "response.output_text.delta",
                json
            );

            // Assert
            Assert.That(result.EventType, Is.EqualTo("response.output_text.delta"));
            Assert.That(result.DeltaText, Is.EqualTo(" execution"));
            Assert.That(result.ResponseId, Is.Null);
        }

        [Test]
        public void AnalyzeEventPayload_DeltaEvent_AlternateShape_ExtractsDeltaText()
        {
            // Arrange: alternate nested shape we’ve seen in some responses
            var json =
                "{" +
                "\"type\":\"response.output_text.delta\"," +
                "\"output_text\":{\"delta\":{\"text\":\"World\"}}" +
                "}";

            // Act
            var result = OpenAiStreamingEventHelper.AnalyzeEventPayload("response.output_text.delta", json);

            // Assert
            Assert.That(result.DeltaText, Is.EqualTo("World"));
        }


        [Test]
        public void ExtractCompletedResponseJson_RealWorldShape_HasOutputAndContentText()
        {
            // Arrange: approximate real-world SSE response.completed payload
            var sseJson = @"
        {
            ""type"": ""response.completed"",
            ""response"": {
            ""id"": ""resp_123"",
            ""object"": ""response"",
            ""model"": ""gpt-4.1-mini"",
            ""output"": [
                {
                ""id"": ""rs_00153e3b7b97a4f0006928564bc48d18cbfd7f5ee57a209"",
                ""type"": ""reasoning"",
                ""summary"": []
                },
                {
                ""id"": ""msg_00153e3b7b97a4f000692b5d4ba50881ca7b505d29c3147c7"",
                ""type"": ""message"",
                ""status"": ""completed"",
                ""content"": [
                    {
                    ""type"": ""output_text"",
                    ""annotations"": [],
                    ""logprobs"": [],
                    ""text"": ""Short answer\nIn this codebase, \""agent_context\"" is not created ad hoc; it's passed in or resolved.""
                    }
                ]
                }
            ]
            }
        }";

            // Act: extract the inner response object
            var innerJson = OpenAiStreamingEventHelper.ExtractCompletedResponseJson(sseJson);
            Assert.That(innerJson.Successful, Is.EqualTo(true), innerJson.ErrorMessage);

            var root = JObject.Parse(innerJson.Result);

            // Assert: top-level output array exists
            var outputArray = root["output"] as JArray;
            Assert.That(outputArray, Is.Not.Null, "output array should be present at top level");
            Assert.That(outputArray.Count, Is.GreaterThanOrEqualTo(2), "expected at least reasoning + message items");

            // Find the message item
            var messageItem = outputArray.First(o => (string)o["type"] == "message");
            Assert.That(messageItem, Is.Not.Null, "expected a message-type output item");

            var contentArray = messageItem["content"] as JArray;
            Assert.That(contentArray, Is.Not.Null, "content array should be present on message item");
            Assert.That(contentArray.Count, Is.GreaterThanOrEqualTo(1));

            var firstContent = contentArray[0];
            Assert.That((string)firstContent["type"], Is.EqualTo("output_text"));

            var text = (string)firstContent["text"];
            Assert.That(text, Does.StartWith("Short answer"), "text should start with the assistant answer");
        }



        [Test]
        public void AnalyzeEventPayload_MalformedJson_FallsBackToEventName()
        {
            // Arrange
            var badJson = "{ this is not valid json";

            // Act
            var result = OpenAiStreamingEventHelper.AnalyzeEventPayload("response.output_text.delta", badJson);

            // Assert
            Assert.That(result.EventType, Is.EqualTo("response.output_text.delta"));
            Assert.That(result.DeltaText, Is.Null);
            Assert.That(result.ResponseId, Is.Null);
        }

        [Test]
        public void ExtractCompletedResponseJson_WithInnerResponse_ReturnsInner()
        {
            // Arrange
            var completedJson =
                "{" +
                "\"type\":\"response.completed\"," +
                "\"response\":{\"id\":\"resp_999\",\"foo\":\"bar\"}" +
                "}";

            // Act
            var result = OpenAiStreamingEventHelper.ExtractCompletedResponseJson(completedJson);
            Assert.That(result.Successful, Is.EqualTo(true), result.ErrorMessage);

            // Assert
            Assert.That(result.Result, Is.EqualTo("{\"id\":\"resp_999\",\"foo\":\"bar\"}"));
        }

        [Test]
        public void ExtractCompletedResponseJson_WithoutInnerResponse_ReturnsRoot()
        {
            // Arrange
            var completedJson = "{\"type\":\"response.completed\",\"other\":\"value\"}";

            // Act
            var result = OpenAiStreamingEventHelper.ExtractCompletedResponseJson(completedJson);

            Assert.That(result.Successful, Is.EqualTo(true), result.ErrorMessage);

            // Assert
            Assert.That(result.Result, Is.EqualTo("{\"type\":\"response.completed\",\"other\":\"value\"}"));
        }
    }
}
