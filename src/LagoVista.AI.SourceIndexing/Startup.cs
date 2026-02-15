using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.CloudRepos;
using LagoVista.AI.FileServices.Indexes;
using LagoVista.AI.FileServices.Services;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Services;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Quality.Interfaces;
using LagoVista.AI.Quality.Model;
using LagoVista.AI.Quality.Services;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.Services;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.SourceIndexing
{
    public class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            Chunkers.Startup.ConfigureServices(services, adminLogger);

            services.AddSingleton<IFileDiscoveryService, FileDiscoveryService>();
            services.AddSingleton<IFileIngestionPlanner, DefaultIngestionPlanner>();
            services.AddSingleton<IDomainModelCatalogBuilder, DomainModelCatalogBuilder>();
            services.AddSingleton<ILocalIndexStore, JsonLocalIndexStore>();
            services.AddSingleton<IInterfaceSemanticEnricher, InterfaceSemanticEnricher>();
            services.AddSingleton<IIndexingPipeline, DefaultIndexingPipeline>();
            services.AddSingleton<IFacetAccumulator, InMemoryFacetAccumulator>();
            services.AddSingleton<IGitRepoInspector, GitRepoInspector>();
            services.AddSingleton<IMetadataRegistryClient, NuvIoTMetadataRegistryClient>();
            services.AddSingleton<ITitleDescriptionRefinementCatalogStore, JsonTitleDescriptionRefinementCatalogStore>();
            services.AddSingleton<IDomainMetadataSource, RoslynDomainMetadataSource>();
            services.AddSingleton<ITitleDescriptionLlmClient, HttpLlmTitleDescriptionClient>();
            services.AddSingleton<ITitleDescriptionReviewService, TitleDescriptionReviewService>();
            services.AddSingleton<IIndexRunOrchestrator, IndexRunOrchestrator>();
            services.AddSingleton<IChunkerServices, ChunkerServices>();
            services.AddSingleton<IResourceExtractor, ResxResourceExtractor>();
            services.AddSingleton<IResxLabelScanner, ResxLabelScanner>();
            services.AddSingleton<IContentStorage, ContentStorage>();
            services.AddSingleton<IResourceUsageTableWriter, ResourceUsageTableWriter>();


            services.AddSingleton<IModelMetadataSource, RoslynModelMetadataSource>();

            services.AddSingleton<IIndexIdServices, IndexIdServices>();
            services.AddSingleton<IIngestionConfigProvider, JsonIngestionConfigProvider>();
            services.AddSingleton<IIndexFileContextBuilder, IndexFileContextBuilder>();
            services.AddSingleton<IResxUpdateService, ResxUpdateService>();
            services.AddSingleton<IDomainDescriptorUpdateService, DomainDescriptorUpdateService>();
            services.AddSingleton<IDomainCatalogService, DomainCatalogService>();

            services.AddSingleton<ITitleDescriptionRefinementOrchestrator, TitleDescriptionRefinementOrchestrator>();

        }
    }
}