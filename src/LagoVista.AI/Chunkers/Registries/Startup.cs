
using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Registries;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using System.Collections.Generic;

namespace LagoVista.AI.Chunkers.Registries
{
    public static class Startup
    {
        private static readonly IDictionary<DocumentType, ISubtypeKindCategorizer> _categorizers = new Dictionary<DocumentType, ISubtypeKindCategorizer>();
        private static readonly IDictionary<DocumentType, IExtractSymbolsProcessor> _extractors = new Dictionary<DocumentType, IExtractSymbolsProcessor>();

        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddSingleton<ISubtypeKindCategorizerRegistry, SubtypeKindCategorizerRegistry>();
            services.AddSingleton<IExtractSymbolsProcessorRegistry, ExtractSymbolsProcessorRegistry>();
            services.AddSingleton<IDictionary<DocumentType, ISubtypeKindCategorizer>>(_categorizers);
            services.AddSingleton<IDictionary<DocumentType, IExtractSymbolsProcessor>>(_extractors);
        }
    }
}