using LagoVista.AI.Helpers;
using LagoVista.AI.Indexing;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Services;
using LagoVista.AI.Services.Hashing;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.Extensions.DependencyInjection;

namespace LagoVista.AI.Services
{
    internal static class Startup
    {

        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddTransient<IAgentKnowledgePackService, AgentKnowledgePackService>();
            services.AddTransient<IAgentSessionFactory, AgentSessionFactory>();
            services.AddTransient<IAgentToolExecutor, AgentToolExecutor>();
            services.AddTransient<IServerToolSchemaProvider, DefaultServerToolSchemaProvider>();
            services.AddTransient<IServerToolUsageMetadataProvider, DefaultServerToolUsageMetadataProvider>();
            services.AddTransient<IContentHashService, DefaultContentHashService>();
            services.AddTransient<ILLMEventPublisher, LlmEventPublisher>();
            services.AddTransient<IAgentExecuteResponseBuilder, AgentExecuteResponseBuilder>();
            services.AddTransient<ILLMWorkflowNarrator, LlmWorkflowNarrator>();
            services.AddTransient<IPromptKnowledgeProvider, PromptKnowledgeProvider>();
            services.AddTransient<IEntityIndexDocumentBuilder, EntityIndexDocumentBuilder>();

            Pipeline.Startup.ConfigureServices(services, adminLogger);
            Qdrant.Startup.ConfigureServices(services, adminLogger);
            ACP.Startup.ConfigureServices(services, adminLogger);
        }
    }
}
