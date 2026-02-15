
using LagoVista.AI.Chunkers.Providers.Default;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Chunkers.Providers
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddSingleton<IBuildDescriptionProcessor, DefaultDescriptionBuilder>();
            PipeLine.Startup.ConfigureServices(services, adminLogger);
        }
    }
}