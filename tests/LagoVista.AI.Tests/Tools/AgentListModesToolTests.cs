using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.AI.Models;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LagoVista.AI.Tests.Tools
{
    [TestFixture]
    public class AgentListModesToolTests
    {
        [Test]
        public void RegisterTool_WithValidContracts_DoesNotThrow()
        {
            var logger = new Mock<IAdminLogger>().Object;
            var registry = new AgentToolRegistry(logger);

            Assert.DoesNotThrow(() => registry.RegisterTool<AgentListModesTool>());
        }

        [Test]
        public async Task ExecuteAsync_WithCatalogData_ReturnsModes()
        {
            var loggerMock = new Mock<IAdminLogger>();
            var tool = new AgentListModesTool(loggerMock.Object);

            var context = new AgentToolExecutionContext
            {
                AgentContext = new AgentContext()
                {
                    AgentModes = new List<AgentMode>()
                    {
                        new AgentMode()
                        {
                             Id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                             Key = "general",
                             Name = "General Assistance"
                        }
                    }
                },
                Request = new AgentExecuteRequest()
            };

            var result = await tool.ExecuteAsync("{}", context, CancellationToken.None);

            Assert.That(result.Successful, Is.True, result.ErrorMessage);
            Assert.That(result.Result, Is.Not.Null.And.Not.Empty);

            var parsed = JObject.Parse(result.Result);
            var modesToken = parsed["modes"];
            Assert.That(modesToken, Is.Not.Null);
            Assert.That(modesToken.Type, Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Array));

            var modesArray = (JArray)modesToken;
            Assert.That(modesArray.Count, Is.GreaterThanOrEqualTo(1));

            var first = modesArray[0];
            Assert.That(first["id"]?.ToString(), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
            Assert.That(first["key"]?.ToString(), Is.EqualTo("general"));
            Assert.That(first["displayName"]?.ToString(), Is.EqualTo("General Assistance"));
        }

        [Test]
        public async Task ExecuteAsync_CatalogThrows_ReturnsError()
        {
            var loggerMock = new Mock<IAdminLogger>();

          
            var tool = new AgentListModesTool(loggerMock.Object);

            var context = new AgentToolExecutionContext
            {
                Request = new AgentExecuteRequest()
            };

            var result = await tool.ExecuteAsync("{}", context, CancellationToken.None);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }
    }
}
