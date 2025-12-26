using LagoVista.AI.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Services.OpenAI
{
    internal static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddScoped<IEmbedder, OpenAIEmbedder>();
            services.AddScoped<IOpenAIErrorFormatter, OpenAIErrorFormatter>();
            services.AddScoped<IOpenAINonStreamingResponseReader, OpenAINonStreamingResponseReader>();
            services.AddScoped<IOpenAIResponsesExecutor, OpenAIResponsesExecutor>();
            services.AddScoped<IEmbedder, OpenAIEmbedder>();
            services.AddScoped<ILLMClient, OpenAIResponsesClientPipelineStap>();
            services.AddScoped<IAgentSessionNamingService, OpenAISessionNamingService>();
            services.AddScoped<IOpenAIStreamingResponseReader, OpenAIStreamingResponseReader>();
        }
    }
}