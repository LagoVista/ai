using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.Core.IOC;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Chunkers
{
    public static class Startup
    {
        public static void Init()
        {
            SLWIOC.RegisterSingleton<IChunkerServices, ChunkerServices>();
            SLWIOC.RegisterSingleton<ICodeDescriptionService, CodeDescriptionService>();
            SLWIOC.RegisterSingleton<IAdminLogger>(new AdminLogger(new ConsoleLogWriter()));
        }
    }
}
