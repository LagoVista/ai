using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using LagoVista.IoT.Logging.Loggers;


namespace LagoVista.AI.Services.Pipeline
{
    internal static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddTransient<IAgentContextLoaderPipelineStap, AgentContextLoaderPipelineStap >();
            services.AddTransient<IAgentContextResolverPipelineStep, AgentContextResolverPipelineStep>();
            services.AddTransient<IAgentReasonerPipelineStep, AgentReasonerPipelineStep>();
            services.AddTransient<IAgentRequestHandlerStep, AgentRequestHandlerPipelineStep>();
            services.AddTransient<IAgentSessionCreatorPipelineStep, AgentSessionCreatorPipelineStep>();
            services.AddTransient<IAgentSessionRestorerPipelineStep, AgentSessionRestorerPipelineStep>();
            services.AddTransient<IClientToolCallSessionRestorerPipelineStep, ClientToolCallSessionRestorerPipelineStep>();
            services.AddTransient<IClientToolContinuationResolverPipelineStep, ClientToolContinuationResolverPipelineStep>();
            services.AddTransient<IAgentPipelineContextValidator, AgentPipelineContextValidator>();
            services.AddTransient<IPromptKnowledgeProviderInitializerPipelineStep, PromptKnowledgeProviderInitializerPipelineStep>();
            services.AddTransient<IAcpCommandPipelineStep, AcpCommandPipelineStep>();
        }
    }
}