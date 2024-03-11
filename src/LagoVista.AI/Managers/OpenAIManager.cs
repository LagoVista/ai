using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
//using Rystem.OpenAi;
//using Rystem.OpenAi.Chat;
using System;
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

        public async Task<InvokeResult<TextQueuryResponse>> HandlePromptAsync(TextQuery query)
        {
            /*OpenAiService.Instance.AddOpenAi((settings) =>
            {
                settings.ApiKey = _settings.OpenAIApiKey;
            }, APIName);

            if (query.QueryType == TextQueryType.Reword)
                query.Query = "Please reword the following content: " + query.Query;
            
            var openAiApi = OpenAiService.Factory.Create(APIName);                
                
              var results = await openAiApi.Chat
                .Request(new ChatMessage {
                    Role = ChatRole.User, 
                    Content = query.Query })
                .WithModel(ChatModelType.Gpt35Turbo)
                .WithTemperature(1)
                .ExecuteAsync();
          
            foreach (var choice in results.Choices)
            {
                if (choice.Message != null)
                {

                    return new InvokeResult<TextQueuryResponse>()
                    {
                        Result = new TextQueuryResponse()
                        {
                            Response = choice.Message.Content
                        }
                    };
                }
            }*/

            return InvokeResult<TextQueuryResponse>.FromError("No respones");
        }
    }
}

   