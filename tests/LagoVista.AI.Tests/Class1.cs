// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: f0caa9c0a35c69fbc60ae63f7e59c99764579f94fe2836d3afbb9474ce9ce1cd
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.AI.Services;
using LagoVista.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Tests
{
    [TestClass]
    public class JupyterNotebookAPITests
    {
        // At some point we may want to revisit this, integration of Jupyter notebooks into NuvIoT

        //EntityHeader _aiOrg;
        //EntityHeader _ai2Org;
        //EntityHeader _user;

        //[TestInitialize]
        //public void Init()
        //{
        //    _aiOrg = new EntityHeader()
        //    {
        //        Id = "ai"
        //    };

        //    _ai2Org = new EntityHeader()
        //    {
        //        Id = "ai2"
        //    };

        //    _user = new EntityHeader()
        //    {

        //    };
        //}

        //[TestMethod]
        //public async Task GetFiles()
        //{
        //    var hubManager = new Mock<IHubManager>();
        //    var hub = new Hub()
        //    {
        //        AccessToken = Environment.GetEnvironmentVariable("JPTY_ADMIN_TOKEN"),
        //        Url = "ai-dev.iothost.net",
        //        IsSecure = true

        //    };

        //    hubManager.Setup(call => call.GetHubForOrgAsync(It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>())).ReturnsAsync(Core.Validation.InvokeResult<Hub>.Create(hub));

        //    var connector = new NotebookConnector(hubManager.Object);
        //    var files = await connector.GetFilesAsync(new Core.Models.EntityHeader()
        //    {
        //        Id = "ai2"
        //    }, new Core.Models.EntityHeader()
        //    {

        //    });

        //    foreach (var file in files.Model)
        //    {
        //        Console.WriteLine(file.Name);
        //    }
        //}

        //[TestMethod]
        //public async Task GetUserToken()
        //{
        //    var hub = new Hub()
        //    {
        //        AccessToken = Environment.GetEnvironmentVariable("JPTY_ADMIN_TOKEN"),
        //        Url = "ai-dev.iothost.net",
        //        IsSecure = true
               
        //    };

        //    var hubManager = new Mock<IHubManager>();
        //    hubManager.Setup(call => call.GetHubForOrgAsync(It.IsAny<EntityHeader>(), It.IsAny<EntityHeader>())).ReturnsAsync(Core.Validation.InvokeResult<Hub>.Create(hub));

        //    var hubConnector = new HubConnector(hubManager.Object);
        //    var result = await hubConnector.GetOrgAccessToken(_aiOrg, _user);
        //    Assert.IsTrue(result.Successful);
        //    Console.WriteLine(result.Result.Token);

        //    var notebookManager = new NotebookConnector(hubManager.Object);

        //    var files = await notebookManager.GetFilesAsync(_aiOrg, _user);
        //    foreach(var file in files.Model)
        //    {
        //        Console.WriteLine(file);
        //    }

        //}
    }
}
