// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: a27eb1c13ec36c494606c61641bc0faf9a49475edbd635b2b55772bd73be8e95
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces;
using LagoVista.AI.Managers;
using LagoVista.AI.Rag.ContractPacks.Content.Interfaces;
using LagoVista.Core.Interfaces;
using System;

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
            services.AddSingleton<IContentStorage, ContentStorage>();
            services.AddSingleton<IResourceUsageTableWriter, ResourceUsageTableWriter>();
            services.AddSingleton<IDdrConsumptionFieldProvider, DdrRepo>();
            services.AddSingleton<IAgentToolBoxRepo, AgentToolBoxRepo>();
            services.AddSingleton<IToolCallManifestRepo,  ToolCallManifestRepo>();
            services.AddSingleton<IAgentTurnChatHistoryRepo, AgentTurnTranscriptBlobStore>();
            services.AddSingleton<IMemoryNoteRepo, MemoryNoteRepo>();
            services.AddSingleton<IAgentPersonaDefinitionRepo, AgentPersonaDefinitionRepo>();
        }
    }
}
