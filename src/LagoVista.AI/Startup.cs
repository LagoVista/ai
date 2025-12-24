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
        private static IAgentToolRegistry _agentToolRegistry;

        public static void RegisterTool<T>() where T : IAgentTool
        {
            _agentToolRegistry.RegisterTool<T>();
        } 

        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            var toolRegistry = new AgentToolRegistry(adminLogger);

            _agentToolRegistry = toolRegistry;

            adminLogger.Trace("[AgentToolRegistry_RegisterTool] - Start Register Server Tools - vvvv");

            ///* define our agent tools here */
            toolRegistry.RegisterTool<HelloWorldTool>();
            toolRegistry.RegisterTool<HelloWorldClientTool>();
            toolRegistry.RegisterTool<PingPongTool>();
            toolRegistry.RegisterTool<CalculatorTool>();
            toolRegistry.RegisterTool<DelayTool>();
            toolRegistry.RegisterTool<FailureInjectionTool>();

            toolRegistry.RegisterTool<FetchWebPageTool>();

            toolRegistry.RegisterTool<AgentListModesTool>();
            toolRegistry.RegisterTool<ModeChangeTool>();
            toolRegistry.RegisterTool<RequestUserApprovalAgentTool>();

            toolRegistry.RegisterTool<ReadFileTool>();
            toolRegistry.RegisterTool<WorkspaceWritePatchTool>();
            toolRegistry.RegisterTool<CodeHashNormalizedTool>();
            toolRegistry.RegisterTool<WorkspaceCreateFileTool>();

            /* Mode Authoring */
            toolRegistry.RegisterTool<AddAgentModeTool>();
            toolRegistry.RegisterTool<UpdateAgentModeTool>();

            ///* workflow authoring + registry tools */
            toolRegistry.RegisterTool<ListWorkflowsTool>();
            toolRegistry.RegisterTool<GetWorkflowManifestTool>();
            toolRegistry.RegisterTool<MatchWorkflowTool>();

            /* CRUD authoring tools */
            toolRegistry.RegisterTool<CreateWorkflowTool>();
            toolRegistry.RegisterTool<UpdateWorkflowTool>();
            toolRegistry.RegisterTool<DeleteWorkflowTool>();

            // --- DDR / TLA Tools ---
            toolRegistry.RegisterTool<GetTlaCatalogAgentTool>();
            toolRegistry.RegisterTool<AddTlaAgentTool>();
            toolRegistry.RegisterTool<CreateDdrAgentTool>();
            toolRegistry.RegisterTool<UpdateDdrMetadataAgentTool>();
            toolRegistry.RegisterTool<MoveDdrTlaAgentTool>();

            // --- Goal Tools ---
            toolRegistry.RegisterTool<SetGoalAgentTool>();
            toolRegistry.RegisterTool<ApproveGoalAgentTool>();

            // -- Checkpoint Tools --
            toolRegistry.RegisterTool<SessionCheckpointListTool>();
            toolRegistry.RegisterTool<SessionCheckpointRestoreTool>();
            toolRegistry.RegisterTool<SessionCheckpointSetTool>();

            // -- Session Memory Tools
            toolRegistry.RegisterTool<SessionMemoryListTool>();
            toolRegistry.RegisterTool<SessionMemoryRecallTool>();
            toolRegistry.RegisterTool<SessionMemoryStoreTool>();

            // --- Chapter Tools ---
            toolRegistry.RegisterTool<AddChapterAgentTool>();
            toolRegistry.RegisterTool<AddChaptersAgentTool>();
            toolRegistry.RegisterTool<UpdateChapterSummaryAgentTool>();
            toolRegistry.RegisterTool<UpdateChapterDetailsAgentTool>();
            toolRegistry.RegisterTool<ApproveChapterAgentTool>();
            toolRegistry.RegisterTool<ListChaptersAgentTool>();
            toolRegistry.RegisterTool<ReorderChaptersAgentTool>();
            toolRegistry.RegisterTool<DeleteChapterAgentTool>();

            // --- DDR Status & Approval ---
            toolRegistry.RegisterTool<SetDdrStatusAgentTool>();
            toolRegistry.RegisterTool<ApproveDdrAgentTool>();

            toolRegistry.RegisterTool<IndexDdrTool>();
            toolRegistry.RegisterTool<ImportDdrTool>();

            // --- DDR Retrieval ---
            toolRegistry.RegisterTool<GetDdrAgentTool>();
            toolRegistry.RegisterTool<ListDdrsAgentTool>();
            /*--*/

            adminLogger.Trace("[AgentToolRegistry_RegisterTool] - All server tools registered - ^^^");


            services.AddTransient<IDdrManager, DdrManager>();
            services.AddTransient<IWorkflowDefinitionManager, WorkflowDefinitionManager>();
            services.AddSingleton<IHttpClientFactory>(new LagoVistaClientFactory());

            services.AddSingleton<IAgentToolRegistry>(toolRegistry);
          
            services.AddTransient<IModelCategoryManager, ModelCategoryManager>();
            services.AddTransient<IModelManager, ModelManager>();
            services.AddTransient<IHubManager, HubManager>();
            services.AddTransient<IAgentToolFactory, AgentToolFactory>();
            services.AddTransient<ITrainingDataSetManager, TrainingDataSetManager>();
            services.AddTransient<ISampleManager, SampleManager>();
            services.AddTransient<ILabelManager, LabelManager>();
            services.AddTransient<IExperimentResultManager, ExperimentResultManager>();

            services.AddSingleton<IResponsesRequestBuilder, ResponsesRequestBuilder>();
            services.AddSingleton<IResponsesRequestBuilder, ResponsesRequestBuilder>();
            services.AddScoped<ITextQueryManager, OpenAIManager>();
            services.AddScoped<IImageGeneratorManager, OpenAIManager>();
            services.AddScoped<IQdrantClient, QdrantClient>();
            services.AddScoped<IEmbedder, OpenAIEmbedder>();
            services.AddScoped<IAgentContextManager, AgentContextManager>();
            services.AddScoped<IAgentSessionManager, AgentSessionManager>();
            services.AddScoped<IAgentExecutionService, AgentExecutionService>();
            services.AddScoped<ILLMClient, OpenAIResponsesClient>();
            services.AddScoped<IAiConversationManager, AiConversationManager>();
            services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();
            services.AddScoped<IAgentSessionFactory, AgentSessionFactory>();
            services.AddScoped<IRagContextBuilder, QdrantRagContextBuilder>();
            services.AddScoped<IAgentTurnExecutor, AgentTurnExecutor>();
            services.AddScoped<IAgentRequestHandler, AgentRequestHandler>();
            services.AddScoped<IAgentReasoner, AgentReasoner>();
            services.AddScoped<IAgentToolExecutor, AgentToolExecutor>();
            services.AddScoped<IServerToolSchemaProvider, DefaultServerToolSchemaProvider>();
            services.AddScoped<IAgentSessionNamingService, OpenAISessionNamingService>();
            services.AddScoped<IServerToolUsageMetadataProvider, DefaultServerToolUsageMetadataProvider>();
            services.AddScoped<IContentHashService, DefaultContentHashService>();
            services.AddScoped<IWorkspaceWritePatchOrchestrator, WorkspaceWritePatchOrchestrator>();
            services.AddScoped<IWorkspacePatchStore, InMemoryWorkspacePatchStore>();
            services.AddScoped<IWorkspaceWritePatchValidator, WorkspaceWritePatchValidator>();
            services.AddScoped<IWorkspacePatchBatchFactory, WorkspacePatchBatchFactory>();
            services.AddScoped<IStructuredTextLlmService, HttpStructuredTextLlmService>();
            services.AddScoped<ITextLlmService, HttpTextLlmService>();
            services.AddScoped<IAgentStreamingContext, AgentStreamingContext>();
            services.AddScoped<IModeEntryBootstrapService, ModeEntryBootstrapService>();

            services.AddSingleton<IContentHashService, DefaultContentHashService>();
            services.AddSingleton<IChunkerServices, ChunkerServices>();
            services.AddSingleton<ICodeDescriptionService, CodeDescriptionService>();
            services.AddSingleton<IResourceExtractor, ResxResourceExtractor>();
            services.AddSingleton<IResxLabelScanner, ResxLabelScanner>();

            services.AddSingleton<IModelMetadataSource, RoslynModelMetadataSource>();

            services.AddSingleton<IHttpClientFactory>(new LagoVistaClientFactory());
            services.AddSingleton<IQdrantClient, QdrantClient>();
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

            services.AddSingleton<ITitleDescriptionRefinementCatalogStore, JsonTitleDescriptionRefinementCatalogStore>();
            services.AddSingleton<IDomainMetadataSource, RoslynDomainMetadataSource>();
            services.AddSingleton<ITitleDescriptionLlmClient, HttpLlmTitleDescriptionClient>();
            services.AddSingleton<ITitleDescriptionReviewService, TitleDescriptionReviewService>();

            services.AddSingleton<ITitleDescriptionRefinementOrchestrator, TitleDescriptionRefinementOrchestrator>();
            services.AddSingleton<IDdrInstructionsProvider, DdrInstructionsProvider>();
        }
    }
}