using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
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
            services.AddSingleton<ISegmentContentProcessor, SegmentContentProcessor>();
            services.AddSingleton<ICSharpSymbolSplitterService, CSharpSymbolSplitterService>();
            services.AddSingleton<ISubtypeKindCategorizer, SubtypeKindCategorizer>();            
        }
    }
}
