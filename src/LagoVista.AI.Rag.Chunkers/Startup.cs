using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Services.Hashing;
using LagoVista.Core.IOC;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;

namespace LagoVista.AI.Rag.Chunkers
{
    public static class Startup
    {
        public static void Init()
        {
            SLWIOC.RegisterSingleton<IContentHashService, DefaultContentHashService>();
            SLWIOC.RegisterSingleton<IChunkerServices, ChunkerServices>();
            SLWIOC.RegisterSingleton<ICodeDescriptionService, CodeDescriptionService>();
            SLWIOC.RegisterSingleton<IResourceExtractor, ResxResourceExtractor>();
            SLWIOC.RegisterSingleton<IResxLabelScanner, ResxLabelScanner>();
        }
    }
}
