// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: db30b928a52cea7b1c47f72e7e7201edb57aa1e83d1df7de8e391fe21cf48416
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces;
using LagoVista.AI.Managers;
using LagoVista.Core.Models;
using LagoVista.MediaServices.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Tests
{

    [TestClass]
    public class OpenAITests
    {
        OpenAIManager _mgr;

        [TestInitialize]
        public void Setup()
        {
            var settings = new OpenAISettings()
            {
                OpenAIApiKey = Environment.GetEnvironmentVariable("OPEN_AI_KEY"),
                OpenAIUrl = Environment.GetEnvironmentVariable("OPEN_AI_URL")
            };

            _mgr = new OpenAIManager(settings, new Mock<IMediaServicesManager>().Object);
        }

        [TestMethod]
        public async Task TestIt()
        {
            var result = await _mgr.HandlePromptAsync(new Models.TextQuery()            
            {
                Query = "Generate an image of an upside down car"
            });

            Console.WriteLine(JsonConvert.SerializeObject(result));
        }

        [TestMethod]
        public async Task ImageGenerationTest()
        {
            var result = await _mgr.GenerateImageAsync(new Models.ImageGenerationRequest()
            {
                //AdditionalDetails = "Generate a photo realistic image of someone riding a bike on a trail in Iowa"
                ImageType = "Photo Realistic",
                ContentType = "Sensor on a tractor",
                AdditionalDetails = "The tractor is old",
                Size = "1792x1024",
                FullRequest = "Generate an image of gray tabby cat hugging an otter with an orange scarf"

            }, EntityHeader.Create("id","text"), EntityHeader.Create("id", "text")); 

            Console.WriteLine(JsonConvert.SerializeObject(result));
        }

    }

    internal class OpenAISettings : IOpenAISettings
    {
        public string OpenAIUrl { get; set; }
        public string OpenAIApiKey { get; set; }
    }
}
