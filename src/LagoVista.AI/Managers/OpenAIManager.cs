using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class OpenAIManager : ITextQueryManager, IImageGeneratorManager
    {
        IOpenAISettings _settings;

        const string APIName = "nuvai";

        public OpenAIManager(IOpenAISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<InvokeResult<TextQueryResponse>> HandlePromptAsync(TextQuery query)
        {
            using (var client = new HttpClient())
            {
                var request = new OpenAIRequest()
                {
                    model = "gpt-3.5-turbo",                    
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
                        ConversationId = result.id,
                        Response = result.choices.First().message.content
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

        public async Task<InvokeResult<ImageGenerationResponse[]>> GenerateImageAsync(ImageGenerationRequest imageRequest)
        {
            using (var client = new HttpClient())
            {
                var prompt = "Generate a " + (String.IsNullOrEmpty(imageRequest.ImageType) ? "image" : imageRequest.ImageType);
                prompt += $" {imageRequest.ContentType} {imageRequest.AdditionalDetails}";
              
                var request = new GenerateImageRequest()
                {
                    Prompt = prompt,
                    Amount = imageRequest.NumberGenerated,
                    Size = imageRequest.Size       
                };

                var json = JsonConvert.SerializeObject(request);
                var stringContent = new StringContent(json, System.Text.Encoding.ASCII, "application/json");
                
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.OpenAIApiKey);
                var response = await client.PostAsync($"{_settings.OpenAIUrl}/v1/images/generations", stringContent);
                
                var responseJSON = await response.Content.ReadAsStringAsync();

                Console.WriteLine(json);
                Console.WriteLine(responseJSON);
                var result = JsonConvert.DeserializeObject<OpenAIImageResponse>(responseJSON);

                var generationResponse = new List<ImageGenerationResponse>();

                foreach(var data in result.data)
                {
                    generationResponse.Add(new ImageGenerationResponse()
                    {
                         ImageUrl = data.url,
                         NewResponse = data.revised_prompt
                    });
                }

                return InvokeResult<ImageGenerationResponse[]>.Create(generationResponse.ToArray());
            }
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
            public string Model { get; } = "dall-e-3";

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

        private class OpenAIResponse
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
}

   