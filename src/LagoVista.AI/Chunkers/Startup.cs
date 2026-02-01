
using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Chunkers
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            Registries.Startup.ConfigureServices(services, adminLogger);
            PipeLine.Startup.ConfigureServices(services, adminLogger);
            Services.Startup.ConfigureServices(services, adminLogger);
            Registries.Startup.ConfigureServices(services, adminLogger);
            services.AddSingleton<IIndexRunner, IndexRunner>();
            services.AddSingleton<IExtractSymbolsProcessor, CSharpSymbolExtractorProcessor>();
        }
    }
}