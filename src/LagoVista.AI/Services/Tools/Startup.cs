using LagoVista.AI.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;


namespace LagoVista.AI.Services.Tools
{
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

            adminLogger.Trace("[AgentToolRegistry_RegisterTool] - Start Register Server Tools");

            toolRegistry.RegisterTool<ActivateToolsTool>();

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

            ///* workflow authoring + registry tools */
            toolRegistry.RegisterTool<ListWorkflowsTool>();
            toolRegistry.RegisterTool<GetWorkflowManifestTool>();
            toolRegistry.RegisterTool<MatchWorkflowTool>();

            /* CRUD authoring tools */
            toolRegistry.RegisterTool<CreateWorkflowTool>();
            toolRegistry.RegisterTool<UpdateWorkflowTool>();
            toolRegistry.RegisterTool<DeleteWorkflowTool>();

            // --- DDR / TLA ActiveTools ---
            toolRegistry.RegisterTool<GetTlaCatalogAgentTool>();
            toolRegistry.RegisterTool<AddTlaAgentTool>();
            toolRegistry.RegisterTool<CreateDdrAgentTool>();
            toolRegistry.RegisterTool<UpdateDdrMetadataAgentTool>();
            toolRegistry.RegisterTool<MoveDdrTlaAgentTool>();

            // --- Goal ActiveTools ---
            toolRegistry.RegisterTool<SetGoalAgentTool>();
            toolRegistry.RegisterTool<ApproveGoalAgentTool>();

            // -- Checkpoint ActiveTools --
            toolRegistry.RegisterTool<SessionCheckpointListTool>();
            toolRegistry.RegisterTool<SessionCheckpointRestoreTool>();
            toolRegistry.RegisterTool<SessionCheckpointSetTool>();

            // -- Session Memory ActiveTools
            toolRegistry.RegisterTool<SessionMemoryListTool>();
            toolRegistry.RegisterTool<SessionMemoryRecallTool>();
            toolRegistry.RegisterTool<SessionMemoryStoreTool>();

            // -- KFR (Known Fact Registry ActiveTools)
            toolRegistry.RegisterTool<SessionKfrClearTool>();
            toolRegistry.RegisterTool<SessionKfrEvictTool>();
            toolRegistry.RegisterTool<SessionKfrListTool>();
            toolRegistry.RegisterTool<SessionKfrUpsertTool>();

            // --- Chapter ActiveTools ---
            toolRegistry.RegisterTool<AddChapterAgentTool>();
            toolRegistry.RegisterTool<AddChaptersAgentTool>();
            toolRegistry.RegisterTool<UpdateChapterSummaryAgentTool>();
            toolRegistry.RegisterTool<UpdateChapterDetailsAgentTool>();
            toolRegistry.RegisterTool<ApproveChapterAgentTool>();
            toolRegistry.RegisterTool<ListChaptersAgentTool>();
            toolRegistry.RegisterTool<ReorderChaptersAgentTool>();
            toolRegistry.RegisterTool<DeleteChapterAgentTool>();

            // --- DDR ModeStatus & Approval ---
            toolRegistry.RegisterTool<SetDdrStatusAgentTool>();
            toolRegistry.RegisterTool<ApproveDdrAgentTool>();

            toolRegistry.RegisterTool<IndexDdrTool>();
            toolRegistry.RegisterTool<ImportDdrTool>();

            // --- DDR Retrieval ---
            toolRegistry.RegisterTool<GetDdrAgentTool>();
            toolRegistry.RegisterTool<ListDdrsAgentTool>();
            /*--*/

            services.AddSingleton<IAgentToolRegistry>(toolRegistry);
            services.AddScoped<IWorkspaceWritePatchOrchestrator, WorkspaceWritePatchOrchestrator>();
            services.AddScoped<IWorkspacePatchStore, InMemoryWorkspacePatchStore>();
            services.AddScoped<IWorkspaceWritePatchValidator, WorkspaceWritePatchValidator>();
            services.AddScoped<IWorkspacePatchBatchFactory, WorkspacePatchBatchFactory>();
        }
    }
}