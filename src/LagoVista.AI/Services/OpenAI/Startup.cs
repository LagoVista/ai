using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Services;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.Extensions.DependencyInjection;

namespace LagoVista.AI.Services.OpenAI
{
    internal static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddScoped<IOpenAIErrorFormatter, OpenAIErrorFormatter>();
            services.AddScoped<IOpenAINonStreamingResponseReader, OpenAINonStreamingResponseReader>();
            services.AddScoped<IOpenAIResponsesExecutor, OpenAIResponsesExecutor>();
            services.AddScoped<LagoVista.Core.Interfaces.IEmbedder, OpenAIEmbedder>();
            services.AddScoped<ILLMClient, OpenAIResponsesClientPipelineStap>();
            services.AddScoped<IAgentSessionNamingService, OpenAISessionNamingService>();
            services.AddScoped<IOpenAIStreamingResponseReader, OpenAIStreamingResponseReader>();
        }
    }
}