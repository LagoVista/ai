using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
//using Rystem.OpenAi;
//using Rystem.OpenAi.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class OpenAIManager : ITextQueryManager
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

                var queryResponse = new TextQueryResponse()
                {
                    ConversationId = result.id,
                    Response = result.choices.First().message.content
                };
                                
                return InvokeResult<TextQueryResponse>.Create(queryResponse); 

            }

            return InvokeResult<TextQueryResponse>.FromError("No respones");
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

   