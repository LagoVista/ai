using LagoVista.AI.Helpers;
using LagoVista.AI.Indexing.Services;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Pipeline;
using LagoVista.AI.Interfaces.Services;

using LagoVista.AI.Services;
using LagoVista.AI.Services.Hashing;
using LagoVista.AI.Services.OpenAI;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using System.Net.Http;

namespace LagoVista.AI
{
    public class LagoVistaClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }

    public static class Startup
    {

        public static void RegisterTool<T>() where T : IAgentTool
        {
           Services.Tools.Startup.RegisterTool<T>();
        } 

        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {

            Services.Tools.Startup.ConfigureServices(services, adminLogger);
            Services.Startup.ConfigureServices(services, adminLogger);
            Services.OpenAI.Startup.ConfigureServices(services, adminLogger);
            Managers.Startup.ConfigureServices(services, adminLogger);

            services.AddSingleton<IHttpClientFactory>(new LagoVistaClientFactory());
            services.AddTransient<IAgentToolFactory, AgentToolFactory>();


            services.AddSingleton<IResponsesRequestBuilder, ResponsesRequestBuilder>();
            services.AddSingleton<IResponsesRequestBuilder, ResponsesRequestBuilder>();
            services.AddScoped<IAgentSessionNamingService, OpenAISessionNamingService>();
            services.AddScoped<IStructuredTextLlmService, HttpStructuredTextLlmService>();
            services.AddScoped<ITextLlmService, HttpTextLlmService>();
            services.AddScoped<IAgentStreamingContext, AgentStreamingContext>();
            services.AddScoped<IAgentToolLoopGuard>(_ => new ToolLoopGuard(warnThreshold: 2, suppressThreshold: 3));

            services.AddSingleton<IContentHashService, DefaultContentHashService>();

            services.AddSingleton<IStructuredTextLlmService, HttpStructuredTextLlmService>();
            services.AddSingleton<IEmbedder, OpenAIEmbedder>();
            services.AddSingleton<IRagIndexingServices, RagIndexingService>();

            services.AddSingleton<IAgentExecuteResponseParser, AgentExecuteResponseParser>();

         }
    }
}   