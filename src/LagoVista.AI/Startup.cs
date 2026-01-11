// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8af30e234e22ac58836993d70b71054e82111113afd916ba48c95d9522589431
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.FileServices.Indexes;
using LagoVista.AI.FileServices.Services;
using LagoVista.AI.Helpers;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Services;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Quality.Interfaces;
using LagoVista.AI.Quality.Model;
using LagoVista.AI.Quality.Services;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.Services;
using LagoVista.AI.Services;
using LagoVista.AI.Services.Hashing;
using LagoVista.AI.Services.OpenAI;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using System.Net.Http;

namespace LagoVista.AI
{
    public class LagoVistaClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }

    public static class Startup
    {

        public static void RegisterTool<T>() where T : IAgentTool
        {
           Services.Tools.Startup.RegisterTool<T>();
        } 

        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {

            Services.Tools.Startup.ConfigureServices(services, adminLogger);
            Services.Startup.ConfigureServices(services, adminLogger);
            Services.OpenAI.Startup.ConfigureServices(services, adminLogger);
            Managers.Startup.ConfigureServices(services, adminLogger);

            services.AddSingleton<IHttpClientFactory>(new LagoVistaClientFactory());
            services.AddTransient<IAgentToolFactory, AgentToolFactory>();


            services.AddSingleton<IResponsesRequestBuilder, ResponsesRequestBuilder>();
            services.AddSingleton<IResponsesRequestBuilder, ResponsesRequestBuilder>();
            services.AddScoped<IAgentSessionNamingService, OpenAISessionNamingService>();
            services.AddScoped<IStructuredTextLlmService, HttpStructuredTextLlmService>();
            services.AddScoped<ITextLlmService, HttpTextLlmService>();
            services.AddScoped<IAgentStreamingContext, AgentStreamingContext>();
            
            services.AddSingleton<IContentHashService, DefaultContentHashService>();
            services.AddSingleton<IChunkerServices, ChunkerServices>();
            services.AddSingleton<IResourceExtractor, ResxResourceExtractor>();
            services.AddSingleton<IResxLabelScanner, ResxLabelScanner>();

            services.AddSingleton<IModelMetadataSource, RoslynModelMetadataSource>();

            services.AddSingleton<IIndexIdServices, IndexIdServices>();
            services.AddSingleton<IIngestionConfigProvider, JsonIngestionConfigProvider>();
            services.AddSingleton<IIndexFileContextBuilder, IndexFileContextBuilder>();
            services.AddSingleton<IStructuredTextLlmService, HttpStructuredTextLlmService>();
            services.AddSingleton<IFileDiscoveryService, FileDiscoveryService>();
            services.AddSingleton<IFileIngestionPlanner, DefaultIngestionPlanner>();
            services.AddSingleton<IDomainModelCatalogBuilder, DomainModelCatalogBuilder>();
            services.AddSingleton<ILocalIndexStore, JsonLocalIndexStore>();
            services.AddSingleton<IInterfaceSemanticEnricher, InterfaceSemanticEnricher>();
            services.AddSingleton<ISourceFileProcessor, SourceFileProcessor>();
            services.AddSingleton<IIndexingPipeline, DefaultIndexingPipeline>();
            services.AddSingleton<IFacetAccumulator, InMemoryFacetAccumulator>();
            services.AddSingleton<IGitRepoInspector, GitRepoInspector>();
            services.AddSingleton<IMetadataRegistryClient, NuvIoTMetadataRegistryClient>();
            services.AddSingleton<IEmbedder, OpenAIEmbedder>();
            services.AddSingleton<IIndexRunOrchestrator, IndexRunOrchestrator>();

            services.AddSingleton<IResxUpdateService, ResxUpdateService>();
            services.AddSingleton<IDomainDescriptorUpdateService, DomainDescriptorUpdateService>();
            services.AddSingleton<IDomainCatalogService, DomainCatalogService>();

            services.AddSingleton<IAgentExecuteResponseParser, AgentExecuteResponseParser>();

            services.AddSingleton<ITitleDescriptionRefinementCatalogStore, JsonTitleDescriptionRefinementCatalogStore>();
            services.AddSingleton<IDomainMetadataSource, RoslynDomainMetadataSource>();
            services.AddSingleton<ITitleDescriptionLlmClient, HttpLlmTitleDescriptionClient>();
            services.AddSingleton<ITitleDescriptionReviewService, TitleDescriptionReviewService>();

            services.AddSingleton<ITitleDescriptionRefinementOrchestrator, TitleDescriptionRefinementOrchestrator>();
         }
    }
}