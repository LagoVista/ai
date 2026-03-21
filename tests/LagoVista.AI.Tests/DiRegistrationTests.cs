using LagoVista.AI.CloudRepos;
using LagoVista.AI.Interfaces;
using LagoVista.CloudStorage.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Interfaces.AutoMapper;
using LagoVista.Core.PlatformSupport;
using LagoVista.Core.TestBase;
using LagoVista.IoT.Logging;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.MediaServices.Interfaces;
using LagoVista.UserAdmin.Interfaces.Managers;
using LagoVista.UserAdmin.Interfaces.Repos.Orgs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Tests
{
    public class DiRegistrationTests
    {
        [Test]
        public void ConfigureServices_Should_Build_ServiceProvider()
        {
            var services = new ServiceCollection();

            services.AddSingleton(Mock.Of<IMLRepoSettings>());
            services.AddSingleton(Mock.Of<IOpenAISettings>());
            services.AddSingleton(Mock.Of<IQdrantSettings>());

            services.AddSingleton(Mock.Of<ICoreAppServices>());
            services.AddSingleton(Mock.Of<IAdminLogger>());
            services.AddSingleton(Mock.Of<IDocumentCloudCachedServices>());
            services.AddSingleton(Mock.Of<ISecureStorage>());
            services.AddSingleton(Mock.Of<ILogger>());
            services.AddSingleton(Mock.Of<ITrainingDataSettings>());
            services.AddSingleton(Mock.Of<ICacheProvider>());
            services.AddSingleton(Mock.Of<IOrganizationManager>());
            services.AddSingleton(Mock.Of<IMediaServicesManager>());
            services.AddSingleton(Mock.Of<IBackgroundServiceTaskQueue>());
            services.AddSingleton(Mock.Of<INotificationPublisher>());
            services.AddSingleton(Mock.Of<IOrganizationLoaderRepo>());

            LagoVista.AI.CloudRepos.Startup.ConfigureServices(services);
            LagoVista.AI.Startup.ConfigureServices(services, new ConsoleLogger());

            try
            {
                using var provider = services.BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateScopes = true,
                    ValidateOnBuild = true
                });
            }
            catch (Exception ex)
            {
                Assert.Fail(DependencyErrorFormatter.Format(ex));
            }
        }
    }
}