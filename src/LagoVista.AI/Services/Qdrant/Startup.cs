using LagoVista.AI.Interfaces;
using Microsoft.Extensions.DependencyInjection;

using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.Qdrant
{
    internal static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddTransient<IQdrantClient, QdrantClient>();
            services.AddTransient<IRagContextBuilder, QdrantRagContextBuilder>();
        }
    }
}