using LagoVista.AI.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Qdrant
{
    internal static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddSingleton<IQdrantClient, QdrantClient>();
            services.AddSingleton<IRagContextBuilder, QdrantRagContextBuilder>();
        }
    }
}