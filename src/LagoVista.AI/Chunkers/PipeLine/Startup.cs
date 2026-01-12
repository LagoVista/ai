using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.PipeLine;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Chunkers.PipeLine
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddSingleton<IUploadContentStep, UploadContentStep>();
            services.AddSingleton<IBuildDescriptionStep, BuildDescriptionStep>();
            services.AddSingleton<IEmbedStep, EmbedStep>();
            services.AddSingleton<IExtractSymbolsStep, ExtractSymbolsStep>();
            services.AddSingleton<IPersistSourceFileStep, PersistSourceFileStep>();
            services.AddSingleton<ISegmentContentStep, SegmentContentStep>();
            services.AddSingleton<IStoreUpsertPointStep, StoreUpsertPointStep>();
            services.AddSingleton<IUploadContentStep, UploadContentStep>();
        }
    }
}