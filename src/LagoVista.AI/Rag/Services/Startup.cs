using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Rag.Services
{
    internal static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddSingleton<IRagIndexingServices, RagIndexingService>();
        }
    }
}
