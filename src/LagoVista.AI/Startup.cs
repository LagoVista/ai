// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8af30e234e22ac58836993d70b71054e82111113afd916ba48c95d9522589431
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces;
using LagoVista.AI.Managers;
using LagoVista.AI.Services;
using LagoVista.AI.Services.Tools;
using LagoVista.Core.AI.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using System;

namespace LagoVista.AI
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            var toolRegistry = new AgentToolRegistry(adminLogger);

            /* define our agent tools here */
            toolRegistry.RegisterTool<PingPongTool>();
            toolRegistry.RegisterTool<CalculatorTool>();
            toolRegistry.RegisterTool<DelayTool>();
            toolRegistry.RegisterTool<FailureInjectionTool>();
            toolRegistry.RegisterTool<DelayTool>();
            toolRegistry.RegisterTool<ReadFileTool>();
            /*--*/

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
            services.AddTransient<IRagAnswerService, RagAnswerService>();
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

        }
    }
}