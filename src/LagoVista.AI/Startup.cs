// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8af30e234e22ac58836993d70b71054e82111113afd916ba48c95d9522589431
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Helpers;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Managers;
using LagoVista.AI.Rag.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.ContractPacks.Content.Interfaces;
using LagoVista.AI.Rag.ContractPacks.IndexStore.Services;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Services;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Services;
using LagoVista.AI.Rag.ContractPacks.Orchestration.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Orchestration.Services;
using LagoVista.AI.Rag.ContractPacks.Quality.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Registry.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Registry.Services;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.AI.Services;
using LagoVista.AI.Services.Hashing;
using LagoVista.AI.Services.OpenAI;
using LagoVista.AI.Services.Qdrant;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.Interfaces;
using LagoVista.Core.IOC;
using LagoVista.IoT.Logging.Loggers;
using Logzio.DotNet.Core.WebClient;
using System;
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
            services.AddScoped<IRagContextBuilder, QdrantRagContextBuilder>();
            services.AddScoped<IAgentSessionNamingService, OpenAISessionNamingService>();
            services.AddScoped<IStructuredTextLlmService, HttpStructuredTextLlmService>();
            services.AddScoped<ITextLlmService, HttpTextLlmService>();
            services.AddScoped<IAgentStreamingContext, AgentStreamingContext>();
            
            services.AddSingleton<IContentHashService, DefaultContentHashService>();
            services.AddSingleton<IChunkerServices, ChunkerServices>();
            services.AddSingleton<ICodeDescriptionService, CodeDescriptionService>();
            services.AddSingleton<IResourceExtractor, ResxResourceExtractor>();
            services.AddSingleton<IResxLabelScanner, ResxLabelScanner>();

            services.AddSingleton<IModelMetadataSource, RoslynModelMetadataSource>();

            services.AddSingleton<IAgentToolBoxManager, AgentToolBoxManager>();
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