using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using Rystem.OpenAi;
using Rystem.OpenAi.Chat;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LagoVista.AI.Managers
{
    public class OpenAIManager
    {
        IOpenAISettings _settings;

        const string APIName = "nuvai";

        public OpenAIManager(IOpenAISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<InvokeResult<TextQueuryResponse>> HandlePromptAsync(TextQuery query)
        {
            OpenAiService.Instance.AddOpenAi((settings) =>
            {
                settings.ApiKey = _settings.OpenAIApiKey;
            }, APIName);

            var openAiApi = OpenAiService.Factory.Create(APIName);

       //     var results = new List<StreamingChatResult>();
            //await foreach (var x in
                
                
              var results = await openAiApi.Chat
                .Request(new ChatMessage {
                    Role = ChatRole.User, 
                    Content = query.Query })
                .WithModel(ChatModelType.Gpt35Turbo)
                .WithTemperature(1)
                .ExecuteAsync();
            {

        //        results.Add(x);
            }

           // Console.WriteLine("Total Result Count: " + results.Count.ToString());

            foreach (var choice in results.Choices)
            {
                if (choice.Message != null)
                    Console.Write(choice.Message.Content);
            }
 
            return null;
        }
    }
}

   