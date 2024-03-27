using LagoVista.AI.Interfaces;
using LagoVista.AI.Managers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

            _mgr = new OpenAIManager(settings);

       
        }

        [TestMethod]
        public async Task TestIt()
        {
            var result = await _mgr.HandlePromptAsync(new Models.TextQuery()            
            {
                Query = "How can sensors be used in the pet care industry?"
            });

            Console.WriteLine(JsonConvert.SerializeObject(result));
        }

        [TestMethod]
        public async Task ImageGenerationTest()
        {
            var result = await _mgr.GenerateImageAsync(new Models.ImageGenerationRequests()
            {
                //AdditionalDetails = "Generate a photo realistic image of someone riding a bike on a trail in Iowa"
                ImageType = "Photo Realistic",
                ContentType = "Sensor on a tractor",
                AdditionalDetails = "The tractor is old"
            }); ; ;

            Console.WriteLine(JsonConvert.SerializeObject(result));
        }

    }

    internal class OpenAISettings : IOpenAISettings
    {
        public string OpenAIUrl { get; set; }
        public string OpenAIApiKey { get; set; }
    }
}
