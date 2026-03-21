using LagoVista.AI.Interfaces;
using LagoVista.Core.PlatformSupport;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.Extensions.DependencyInjection;
using LagoVista.DependencyInjection;

namespace LagoVista.AI.Services.Tools
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            adminLogger.Trace("[AgentToolRegistry_RegisterTool] - Start Register Server Tools");

            services.AddAgentTool<ActivateToolsTool>();
            services.AddAgentTool<HelloWorldTool>();

            ///* define our agent tools here */
            services.AddAgentTool<HelloWorldTool>();
            services.AddAgentTool<HelloWorldClientTool>();
            services.AddAgentTool<PingPongTool>();
            services.AddAgentTool<CalculatorTool>();
            services.AddAgentTool<DelayTool>();
            services.AddAgentTool<FailureInjectionTool>();

            ///* utility tools
            services.AddAgentTool<GetAuditFieldsForUpdate>();
            services.AddAgentTool<GetAuditFieldsForCreate>();
            services.AddAgentTool<GetCurrentTimeStamp>();
            services.AddAgentTool<GetCurrentUser>();

            services.AddAgentTool<FetchWebPageTool>();

            services.AddAgentTool<AgentListModesTool>();
            services.AddAgentTool<ModeChangeTool>();
            services.AddAgentTool<RequestUserApprovalAgentTool>();

            services.AddAgentTool<WorkspaceReadServerFileTool>();
            services.AddAgentTool<WorkspaceReadClientFileTool>();
            services.AddAgentTool<WorkspaceWritePatchTool>();
            services.AddAgentTool<CodeHashNormalizedTool>();
            services.AddAgentTool<WorkspaceCreateFileTool>();
            services.AddAgentTool<WorkspaceTocGetTool>();
            services.AddAgentTool<GetSessionCodeFileActivitiesTool>();

            /* session tools */
            services.AddAgentTool<ChapterResetPrepareTool>();
            services.AddAgentTool<ChapterResetCommitTool>();
            services.AddAgentTool<AskAgentFirstTool>();
            services.AddAgentTool<WriteAuthoritativeAnswerTool>();


            ///* workflow authoring + registry tools */
            services.AddAgentTool<ListWorkflowsTool>();
            services.AddAgentTool<GetWorkflowManifestTool>();
            services.AddAgentTool<MatchWorkflowTool>();

            /* CRUD authoring tools */
            services.AddAgentTool<CreateWorkflowTool>();
            services.AddAgentTool<UpdateWorkflowTool>();
            services.AddAgentTool<DeleteWorkflowTool>();

            // --- DDR / TLA ActiveTools ---
            services.AddAgentTool<GetTlaCatalogAgentTool>();
            services.AddAgentTool<AddTlaAgentTool>();
            services.AddAgentTool<CreateDdrAgentTool>();
            services.AddAgentTool<UpdateDdrMetadataAgentTool>();
            services.AddAgentTool<MoveDdrTlaAgentTool>();
            services.AddAgentTool<GetCondensedDdrAgentTool>();
            services.AddAgentTool<GetdDdrInstructionsAgentTool>();

            // --- Goal ActiveTools ---
            services.AddAgentTool<SetGoalAgentTool>();
            services.AddAgentTool<ApproveGoalAgentTool>();

            // -- Checkpoint ActiveTools --
            services.AddAgentTool<SessionCheckpointListTool>();
            services.AddAgentTool<SessionCheckpointRestoreTool>();
            services.AddAgentTool<SessionCheckpointSetTool>();

            // -- Session Memory ActiveTools
            services.AddAgentTool<SessionMemoryListTool>();
            services.AddAgentTool<SessionMemoryRecallTool>();
            services.AddAgentTool<SessionMemoryStoreTool>();

            // -- KFR (Known Fact Registry ActiveTools)
            services.AddAgentTool<SessionKfrClearTool>();
            services.AddAgentTool<SessionKfrEvictTool>();
            services.AddAgentTool<SessionKfrListTool>();
            services.AddAgentTool<SessionKfrUpsertTool>();
            
            services.AddAgentTool<KfrListCategoriesTool>();
            services.AddAgentTool<KfrListTagsTool>();
            services.AddAgentTool<KfrQueryByCategoryTool>();
            services.AddAgentTool<KfrQueryByTagsTool>();
            services.AddAgentTool<KfrSetCategoryTool>();
            services.AddAgentTool<KfrSetTagsTool>();

            // --- Chapter ActiveTools ---
            services.AddAgentTool<AddChapterAgentTool>();
            services.AddAgentTool<AddChaptersAgentTool>();
            services.AddAgentTool<UpdateChapterSummaryAgentTool>();
            services.AddAgentTool<UpdateChapterDetailsAgentTool>();
            services.AddAgentTool<ApproveChapterAgentTool>();
            services.AddAgentTool<ListChaptersAgentTool>();
            services.AddAgentTool<ReorderChaptersAgentTool>();
            services.AddAgentTool<DeleteChapterAgentTool>();

            // --- DDR ModeStatus & Approval ---
            services.AddAgentTool<SetDdrStatusAgentTool>();
            services.AddAgentTool<ApproveDdrAgentTool>();

            services.AddAgentTool<IndexDdrTool>();
            services.AddAgentTool<ImportDdrTool>();

            // --- DDR Retrieval ---
            services.AddAgentTool<GetDdrAgentTool>();
            services.AddAgentTool<ListDdrsAgentTool>();

            // -- Categoires used to organize entities --
            services.AddAgentTool<CategoryCreateTool>();
            services.AddAgentTool<CategoryListTool>();

            // -- chapter tools --
            services.AddAgentTool<ListChaptersTool>();
            services.AddAgentTool<SwitchChapterTool>();
            services.AddAgentTool<RenameCurrentChapterTool>();

            // -- Session Lists ActiveTools --
            services.AddAgentTool<SessionListCreateTool>();
            services.AddAgentTool<SessionListListTool>();
            services.AddAgentTool<SessionListGetTool>();
            services.AddAgentTool<SessionListUpdateTool>();
            services.AddAgentTool<SessionListDeleteTool>();
            services.AddAgentTool<SessionListItemAddTool>();
            services.AddAgentTool<SessionListItemUpdateTool>();
            services.AddAgentTool<SessionListItemRemoveTool>();
            services.AddAgentTool<SessionListItemMoveTool>();
            services.AddAgentTool<SessionListSummaryItemListTool>();

            /*--*/

            services.AddScoped<IWorkspaceWritePatchOrchestrator, WorkspaceWritePatchOrchestrator>();
            services.AddScoped<IWorkspacePatchStore, InMemoryWorkspacePatchStore>();
            services.AddScoped<IWorkspaceWritePatchValidator, WorkspaceWritePatchValidator>();
            services.AddScoped<IWorkspacePatchBatchFactory, WorkspacePatchBatchFactory>();
        }
    }
}