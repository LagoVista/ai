// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8af30e234e22ac58836993d70b71054e82111113afd916ba48c95d9522589431
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces;
using LagoVista.AI.Managers;
using LagoVista.AI.Services;
using LagoVista.Core.Interfaces;

namespace LagoVista.AI
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IModelCategoryManager, ModelCategoryManager>();
            services.AddTransient<IModelManager, ModelManager>();
            services.AddTransient<IHubManager, HubManager>();
            services.AddTransient<ITrainingDataSetManager, TrainingDataSetManager>();
            services.AddTransient<ISampleManager, SampleManager>();
            services.AddTransient<ILabelManager, LabelManager>();
            services.AddTransient<IExperimentResultManager, ExperimentResultManager>();
            services.AddTransient<ITextQueryManager, OpenAIManager>();
            services.AddTransient<IImageGeneratorManager, OpenAIManager>();
            services.AddTransient<IRagAnswerService, RagAnswerService>();
            services.AddTransient<IQdrantClient, QdrantClient>();
            services.AddSingleton<IEmbedder, OpenAIEmbedder>();
            services.AddSingleton<IAgentContextManager, AgentContextManager>();
            services.AddSingleton<IAgentSessionManager, AgentSessionManager>();
            services.AddTransient<IAiConversationManager, AiConversationManager>();
            services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();
            services.AddSingleton<IAgentSessionFactory, AgentSessionFactory>();
            services.AddSingleton<IAgentTurnExecutor, AgentTurnExecutor>();

        }
    }
}