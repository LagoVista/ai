using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Services.OpenAI;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;


namespace LagoVista.AI.Services.Pipeline
{
    internal static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddScoped<IAgentContextLoaderPipelineStap, AgentContextLoaderPipelineStap >();
            services.AddScoped<IAgentContextResolverPipelineStep, AgentContextResolverPipelineStep>();
            services.AddScoped<IAgentReasonerPipelineStep, AgentReasonerPipelineStep>();
            services.AddScoped<IAgentRequestHandlerStep, AgentRequestHandlerPipelineStep>();
            services.AddScoped<IAgentSessionCreatorPipelineStep, AgentSessionCreatorPipelineStep>();
            services.AddScoped<IAgentSessionRestorerPipelineStep, AgentSessionRestorerPipelineStep>();
            services.AddScoped<IClientToolCallSessionRestorerPipelineStep, ClientToolCallSessionRestorerPipelineStep>();
            services.AddScoped<IClientToolContinuationResolverPipelineStep, ClientToolContinuationResolverPipelineStep>();
            services.AddScoped<IAgentPipelineContextValidator, AgentPipelineContextValidator>();
            services.AddScoped<IPromptKnowledgeProviderInitializerPipelineStep, PromptKnowledgeProviderInitializerPipelineStep>();
        }
    }
}