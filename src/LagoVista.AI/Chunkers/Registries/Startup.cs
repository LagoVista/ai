
using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Chunkers.Providers.DomainDescription;
using LagoVista.AI.Chunkers.Providers.Interfaces;
using LagoVista.AI.Chunkers.Providers.Managers;
using LagoVista.AI.Chunkers.Providers.ModelStructure;
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
        private static readonly IDictionary<SubtypeKind, ISegmentContentProcessor> _segmenters = new Dictionary<SubtypeKind, ISegmentContentProcessor>();
        private static readonly IDictionary<SubtypeKind, IBuildDescriptionProcessor> _descriptionBuilders = new  Dictionary<SubtypeKind, IBuildDescriptionProcessor>();

        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            _descriptionBuilders.Add(SubtypeKind.Interface, new InterfaceDescriptionBuilder());
            _descriptionBuilders.Add(SubtypeKind.Model, new ModelStructureDescriptionBuilder());
            _descriptionBuilders.Add(SubtypeKind.DomainDescription, new DomainDescriptionBuilder(adminLogger));
            _descriptionBuilders.Add(SubtypeKind.Manager, new ManagerDescriptionBuilder());

            services.AddSingleton<ISubtypeKindCategorizerRegistry, SubtypeKindCategorizerRegistry>();
            services.AddSingleton<IExtractSymbolsProcessorRegistry, ExtractSymbolsProcessorRegistry>();
            services.AddSingleton<ISegmentContentProcessorRegistry, SegmentContentProcessorRegistry>();
            services.AddSingleton<IBuildDescriptionProcessorRegistry, BuildDescriptionProcessorRegistry>();

            services.AddSingleton<IDictionary<DocumentType, ISubtypeKindCategorizer>>(_categorizers);
            services.AddSingleton<IDictionary<DocumentType, IExtractSymbolsProcessor>>(_extractors);
            services.AddSingleton<IDictionary<SubtypeKind, ISegmentContentProcessor>>(_segmenters);
            services.AddSingleton<IDictionary<SubtypeKind, IBuildDescriptionProcessor>>(_descriptionBuilders);
        }
    }
}