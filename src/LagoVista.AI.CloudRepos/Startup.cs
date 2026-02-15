using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Repos;
using LagoVista.Core.Interfaces;

namespace LagoVista.AI.CloudRepos
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IExperimentResultRepo, ExperimentResultRepo>();
            services.AddTransient<IModelCategoryRepo, ModelCategoryRepo>();
            services.AddTransient<IModelRepo, ModelRepo>();
            services.AddTransient<ISampleRepo, SampleRepo>();
            services.AddTransient<ISampleLabelRepo, SampleLabelRepo>();
            services.AddTransient<ILabelSampleRepo, LabelSampleRepo>();
            services.AddTransient<ISampleMediaRepo, SampleMediaRepo>();
            services.AddTransient<ILabelSetRepo, ModelLabelSetRepo>();
            services.AddTransient<ILabelRepo, LabelRepo>();
            services.AddTransient<ITrainingDataSetRepo, TrainingDataSetRepo>();
            services.AddTransient<IMLModelRepo, MLModelRepo>();
            services.AddTransient<ILLMContentRepo, LLMContentRepo>(); //Note this is not thread safe, needs to be a transient.
            services.AddSingleton<IAgentContextRepo, AgentContextRepo>();
            services.AddSingleton<IAgentContextLoaderRepo, AgentContextLoaderRepo>();
            services.AddSingleton<IAgentTurnTranscriptStore, AgentTurnTraanscriptStore>();
            services.AddSingleton<IAiConversationRepo, AiConversationRepo>();
            services.AddSingleton<IAgentSessionRepo, AgentSessionRepo>();
            services.AddSingleton<IDdrRepo, DdrRepo>();
            services.AddSingleton<IWorkflowDefinitionRepo, WorkflowDefinitionRepo>();
            services.AddSingleton<ITlaCatalogRepo, TlaCatalogRepo>();
            services.AddSingleton<IDdrConsumptionFieldProvider, DdrRepo>();
            services.AddSingleton<IAgentToolBoxRepo, AgentToolBoxRepo>();
            services.AddSingleton<IToolCallManifestRepo,  ToolCallManifestRepo>();
            services.AddSingleton<IAgentTurnChatHistoryRepo, AgentTurnTranscriptBlobStore>();
            services.AddSingleton<IMemoryNoteRepo, MemoryNoteRepo>();
            services.AddSingleton<IReferenceEntryRepo, ReferenceEntryRepo>();
            services.AddSingleton<IAgentSessionTurnChapterStore, AgentSessionTurnArchiveStore>();
            services.AddSingleton<IAgentPersonaDefinitionRepo, AgentPersonaDefinitionRepo>();
        }
    }
}
