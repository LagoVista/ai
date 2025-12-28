using LagoVista.AI.Helpers;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Services.Hashing;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services
{
    internal static class Startup
    {

        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddSingleton<IAgentKnowledgePackService, AgentKnowledgePackService>();
            services.AddScoped<IAgentSessionFactory, AgentSessionFactory>();
            services.AddScoped<IAgentToolExecutor, AgentToolExecutor>();
            services.AddScoped<IServerToolSchemaProvider, DefaultServerToolSchemaProvider>();
            services.AddScoped<IServerToolUsageMetadataProvider, DefaultServerToolUsageMetadataProvider>();
            services.AddScoped<IContentHashService, DefaultContentHashService>();
            services.AddScoped<ILLMEventPublisher, LlmEventPublisher>();
            services.AddScoped<IAgentExecuteResponseBuilder, AgentExecuteResponseBuilder>();
            services.AddScoped<ILLMWorkflowNarrator, LlmWorkflowNarrator>();
            Pipeline.Startup.ConfigureServices(services, adminLogger);
            Qdrant.Startup.ConfigureServices(services, adminLogger);
        }
    }
}
