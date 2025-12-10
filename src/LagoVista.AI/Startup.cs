// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8af30e234e22ac58836993d70b71054e82111113afd916ba48c95d9522589431
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces;
using LagoVista.AI.Managers;
using LagoVista.AI.Services;
using LagoVista.AI.Services.Hashing;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using Logzio.DotNet.Core.WebClient;
using System;
using System.Net.Http;

namespace LagoVista.AI
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            var toolRegistry = new AgentToolRegistry(adminLogger);

            adminLogger.Trace("[AgentToolRegistry_RegisterTool] - Start Register Server Tools - vvvv");

            ///* define our agent tools here */
            toolRegistry.RegisterTool<HelloWorldTool>();
            toolRegistry.RegisterTool<HelloWorldClientTool>();
            toolRegistry.RegisterTool<PingPongTool>();
            toolRegistry.RegisterTool<CalculatorTool>();
            toolRegistry.RegisterTool<DelayTool>();
            toolRegistry.RegisterTool<FailureInjectionTool>();

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

            // --- DDR Retrieval ---
            toolRegistry.RegisterTool<GetDdrAgentTool>();
            toolRegistry.RegisterTool<ListDdrsAgentTool>();
            /*--*/

            adminLogger.Trace("[AgentToolRegistry_RegisterTool] - All server tools registered - ^^^");


            services.AddTransient<IDdrManager, DdrManager>();
            services.AddTransient<IWorkflowDefinitionManager, WorkflowDefinitionManager>();

            services.AddSingleton<IAgentToolRegistry>(toolRegistry);
            services.AddTransient<IModelCategoryManager, ModelCategoryManager>();
            services.AddTransient<IModelManager, ModelManager>();
            services.AddTransient<IHubManager, HubManager>();
            services.AddTransient<IAgentToolFactory, AgentToolFactory>();
            services.AddTransient<ITrainingDataSetManager, TrainingDataSetManager>();
            services.AddTransient<ISampleManager, SampleManager>();
            services.AddTransient<ILabelManager, LabelManager>();
            services.AddTransient<IExperimentResultManager, ExperimentResultManager>();
            services.AddTransient<ITextQueryManager, OpenAIManager>();
            services.AddTransient<IImageGeneratorManager, OpenAIManager>();
            services.AddTransient<IQdrantClient, QdrantClient>();
            services.AddSingleton<IEmbedder, OpenAIEmbedder>();
            services.AddSingleton<IAgentContextManager, AgentContextManager>();
            services.AddSingleton<IAgentSessionManager, AgentSessionManager>();
            services.AddSingleton<IAgentExecutionService, AgentExecutionService>();
            services.AddSingleton<ILLMClient, OpenAIResponsesClient>();
            services.AddTransient<IAiConversationManager, AiConversationManager>();
            services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();
            services.AddSingleton<IAgentSessionFactory, AgentSessionFactory>();
            services.AddSingleton<IRagContextBuilder, QdrantRagContextBuilder>();
            services.AddSingleton<IAgentTurnExecutor, AgentTurnExecutor>();
            services.AddSingleton<IAgentRequestHandler, AgentRequestHandler>();
            services.AddSingleton<IAgentReasoner, AgentReasoner>();
            services.AddSingleton<IAgentToolExecutor, AgentToolExecutor>();
            services.AddSingleton<IServerToolSchemaProvider, DefaultServerToolSchemaProvider>();
            services.AddSingleton<IAgentSessionNamingService, OpenAISessionNamingService>();
            services.AddSingleton<IServerToolUsageMetadataProvider, DefaultServerToolUsageMetadataProvider>();
            services.AddSingleton<IContentHashService, DefaultContentHashService>();
            services.AddSingleton<IWorkspaceWritePatchOrchestrator, WorkspaceWritePatchOrchestrator>();
            services.AddSingleton<IWorkspacePatchStore, InMemoryWorkspacePatchStore>();
            services.AddSingleton<IWorkspaceWritePatchValidator, WorkspaceWritePatchValidator>();
            services.AddSingleton<IWorkspacePatchBatchFactory, WorkspacePatchBatchFactory>();

        }
    }
}