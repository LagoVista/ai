using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Chunkers.Services
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddSingleton<ICSharpSymbolSplitterService, CSharpSymbolSplitterService>();
        }
    }
}
