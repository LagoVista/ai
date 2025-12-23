// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: fe468e5390deacd056b26a961eab5e5048bd7a1a820f5586530aace5c3498a6c
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.MediaServices.Interfaces;
using LagoVista.MediaServices.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class OpenAIManager : ITextQueryManager, IImageGeneratorManager
    {
        private readonly IOpenAISettings _settings;
        private readonly IMediaServicesManager _mediaSerivcesManager;

        const string APIName = "nuvai";

        public OpenAIManager(IOpenAISettings settings, IMediaServicesManager mediaSerivcesManager)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _mediaSerivcesManager = mediaSerivcesManager ?? throw new ArgumentNullException(nameof(mediaSerivcesManager));
        }

        public async Task<InvokeResult<TextQueryResponse>> HandlePromptAsync(TextQuery query)
        {
            using (var client = new HttpClient())
            {
                var request = new OpenAIRequest()
                {
                    model = "gpt-4",                    
                };

                request.messages.Add(new OpenAIMessage()
                {
                    content = query.Query,
                    role = query.Role ?? "user"
                });

                var stringContent = new StringContent(JsonConvert.SerializeObject(request), System.Text.Encoding.ASCII, "application/json");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.OpenAIApiKey);
                var response = await client.PostAsync($"{_settings.OpenAIUrl}/v1/chat/completions", stringContent);

                var responeJSON = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<OpenAIResponse>(responeJSON);

                if (result.choices?.Count > 0)
                {
                    var queryResponse = new TextQueryResponse()
                    {
                        SessionId = result.id,
                        Response = result.choices.First().message.content,
                        
                    };

                    return InvokeResult<TextQueryResponse>.Create(queryResponse);
                }
                else
                {
                    return InvokeResult<TextQueryResponse>.Create(new TextQueryResponse()
                    {
                        Response = "Please try again."
                    });
                }
            }
        }

        public async Task<InvokeResult<MediaResource[]>> GenerateImageAsync(ImageGenerationRequest imageRequest, EntityHeader org, EntityHeader user)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(600);
                var prompt = imageRequest.FullRequest;
                if (String.IsNullOrEmpty(prompt))
                {
                    prompt = "Generate a " + (String.IsNullOrEmpty(imageRequest.ImageType) ? "image" : imageRequest.ImageType);
                    prompt += $" {imageRequest.ContentType} {imageRequest.AdditionalDetails}";
                }

                prompt = imageRequest.AdditionalDetails;

                if(String.IsNullOrEmpty(prompt))
                {
                    return InvokeResult<MediaResource[]>.FromError("Missing image request");
                }

                var request = new ResponseImageApi()
                {
                    Input = prompt,
                    PreviousResponse = String.IsNullOrEmpty(imageRequest.PreviousResponseId) ? null : imageRequest.PreviousResponseId,
                };

                var json = JsonConvert.SerializeObject(request);
                Console.WriteLine("Request\r\n===================");
                Console.WriteLine(json);
                Console.WriteLine("\r\n");

                var stringContent = new StringContent(json, System.Text.Encoding.ASCII, "application/json");
                
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.OpenAIApiKey);
                var response = await client.PostAsync($"{_settings.OpenAIUrl}/v1/responses", stringContent);

                var responseJSON = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response\r\n===================");
                Console.WriteLine(responseJSON);
                Console.WriteLine("\r\n");

                var result = JsonConvert.DeserializeObject<Root>(responseJSON);

                if (result.error != null)
                {
                    return InvokeResult<MediaResource[]>.FromError(result.error.message);
                }

                Console.WriteLine(result);

                var generationResponse = new List<ImageGenerationResponse>();

                var imageResults = result.output.Where(res => res.type == "image_generation_call");
               
                var resourceRecords = new List<MediaResource>();

                foreach (var imageResult in imageResults)
                {
                    var b64 = imageResult.result;
                    var buffer = Convert.FromBase64String(b64);
                    using (var ms = new MemoryStream(buffer))
                    {
                        var fileName = $"generated{DateTime.UtcNow.Ticks}.{imageResult.output_format}";
                        var resourceResult = String.IsNullOrEmpty(imageRequest.MediaResourceId) ?
                            await _mediaSerivcesManager.AddResourceMediaAsync(Guid.NewGuid().ToId(), ms, fileName, imageResult.output_format, org, user, true, imageRequest.IsPublic,
                                responseId: result.id, originalPrompt: prompt, revisedPrompt: imageResult.revised_prompt, entityTypeName: imageRequest.EntityTypeName, entityTypeFieldName: imageRequest.EntityFieldName, size: imageResult.size, resourceName: imageRequest.ResourceName)
                            :
                            await _mediaSerivcesManager.AddResourceMediaRevisionAsync(imageRequest.MediaResourceId, ms, fileName, imageResult.output_format, org, user, true, imageRequest.IsPublic,
                                responseId: result.id, originalPrompt: prompt, revisedPrompt: imageResult.revised_prompt, size: imageResult.size);
                        if (resourceResult.Successful)
                        {
                            var resource = resourceResult.Result;
                            resourceRecords.Add(resource);
                        }
                        else
                        {
                            InvokeResult<ImageGenerationResponse[]>.FromInvokeResult(resourceResult.ToInvokeResult());
                        }
                    }
                }

                return InvokeResult<MediaResource[]>.Create(resourceRecords.ToArray());
            }
        }

        internal class ResponseImageApi
        {
            [JsonProperty("input")]
            public string Input { get; set; }

            [JsonProperty("model")]
            public string Model { get; set; } = "gpt-5";


            [JsonProperty("tools")]
            public List<ToolType> Tools { get; set; } = new List<ToolType>() { new ToolType() { Type = "image_generation" } };


            [JsonProperty("previous_response_id")]
            public string PreviousResponse { get; set; }
        }

        internal class ToolType
        {
            [JsonProperty("type")]
            public string Type { get; set; }
        }

        internal class GenerateImageRequest
        {
            /// <summary>
            ///     The prompt for the image generation.
            /// </summary>
            [JsonProperty("prompt")]
            public string Prompt { get; set; }

            /// <summary>
            ///     The name of the model to use for 
            ///     image generation.
            /// </summary>
            [JsonProperty("model")]
            public string Model { get; set; } = "dall-e-3";
            
            /// <summary>
            ///     The number of images to generate.
            /// </summary>
            [JsonProperty("n")]
            public int Amount { get; set; } = 1;

            /// <summary>
            ///     The quality of the generated image.
            /// </summary>
            [JsonProperty("quality")]
            public string Quality { get; set; } = "standard";

            /// <summary>
            ///     The format of the response.
            /// </summary>
            [JsonProperty("response_format")]
            public string ResponseFormat { get; } = "url";

            /// <summary>
            ///     The size of the generated image.
            /// </summary>
            [JsonProperty("size")]
            public string Size { get; set; } = "1024x1024";

            /// <summary>
            ///     The style of the generated image.
            /// </summary>
            [JsonProperty("style")]
            public string Style { get; set; } = "vivid";

            /// <summary>
            ///     The user requesting the image generation.
            /// </summary>
            [JsonProperty("user")]
            public string User { get; set; } = string.Empty;
        }

        public class OpenAIImageResponse
        {
            public long created { get; set; }
            public List<OpenAIImageResponseData> data { get; set; }
        }

        public class OpenAIImageResponseData
        {
            public string revised_prompt { get; set; }
            public string url { get; set; }
        }

        private class OpenAIRequest
        {
            public string model { get; set; }
            public List<OpenAIMessage> messages { get; set; } = new List<OpenAIMessage>();
            public float temperature { get; set; }
        }

        private class OpenAIMessage
        {
            public string role { get; set; }
            public string content { get; set; }
        }

        public class OpenAIResponse
        {
            public string id { get; set; }
            public string @object { get; set; }        
            public long created { get; set; } 
            public OpenAIResponseUsage usage { get; set; }

            public List<OpenAIResponseChoice> choices { get; set; }

        }

        public class OpenAIResponseUsage
        {
            public int prompt_tokens { get; set; }
            public int completion_tokens { get; set; }
            public int total_tokens { get; set; }

            
        }

        public class OpenAIResponseChoice
        {
            public OpenAIResponseChoiceMessage message { get; set; }
            public string logprobs { get; set; }

            public string finish_reason { get; set; }
            public int index { get; set; }
        }

        public class OpenAIResponseChoiceMessage
        {
            public string role { get; set; }
            public string content { get; set; }
        }
    }

    public class Content
    {
        public string type { get; set; }
        public List<object> annotations { get; set; }
        public List<object> logprobs { get; set; }
        public string text { get; set; }
    }

    public class Format
    {
        public string type { get; set; }
    }

    public class InputTokensDetails
    {
        public int cached_tokens { get; set; }
    }

    public class Metadata
    {
    }

    public class Output
    {
        public string id { get; set; }
        public string type { get; set; }
        public List<object> summary { get; set; }
        public string status { get; set; }
        public string background { get; set; }
        public string output_format { get; set; }
        public string quality { get; set; }
        public string result { get; set; }
        public string revised_prompt { get; set; }
        public string size { get; set; }
        public List<Content> content { get; set; }
        public string role { get; set; }

        public int Width { get => Convert.ToInt32(size.Split('x')[0]);}
        public int Height { get => Convert.ToInt32(size.Split('x')[1]); }
    }

    public class OutputTokensDetails
    {
        public int reasoning_tokens { get; set; }
    }

    public class Reasoning
    {
        public string effort { get; set; }
        public object summary { get; set; }
    }

    public class OpenAIError
    {
        public string message { get; set; }
        public string type { get; set; }
        public string param { get; set; }
        public string code { get; set; }
    }

    public class Root
    {
        public string id { get; set; }
        public string @object { get; set; }
        public int created_at { get; set; }
        public string status { get; set; }
        public bool background { get; set; }
        public OpenAIError error { get; set; }
        public object incomplete_details { get; set; }
        public object instructions { get; set; }
        public object max_output_tokens { get; set; }
        public object max_tool_calls { get; set; }
        public string model { get; set; }
        public List<Output> output { get; set; }
        public bool parallel_tool_calls { get; set; }
        public object previous_response_id { get; set; }
        public object prompt_cache_key { get; set; }
        public Reasoning reasoning { get; set; }
        public object safety_identifier { get; set; }
        public string service_tier { get; set; }
        public bool store { get; set; }
        public double temperature { get; set; }
        public Text text { get; set; }
        public string tool_choice { get; set; }
        public List<Tool> tools { get; set; }
        public int top_logprobs { get; set; }
        public double top_p { get; set; }
        public string truncation { get; set; }
        public Usage usage { get; set; }
        public object user { get; set; }
        public Metadata metadata { get; set; }
    }

    public class Text
    {
        public Format format { get; set; }
        public string verbosity { get; set; }
    }

    public class Tool
    {
        public string type { get; set; }
        public string background { get; set; }
        public string moderation { get; set; }
        public int n { get; set; }
        public int output_compression { get; set; }
        public string output_format { get; set; }
        public string quality { get; set; }
        public string size { get; set; }
    }

    public class Usage
    {
        public int input_tokens { get; set; }
        public InputTokensDetails input_tokens_details { get; set; }
        public int output_tokens { get; set; }
        public OutputTokensDetails output_tokens_details { get; set; }
        public int total_tokens { get; set; }
    }
}

   