using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Managers
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            services.AddTransient<IModelCategoryManager, ModelCategoryManager>();
            services.AddTransient<IModelManager, ModelManager>();
            services.AddTransient<IHubManager, HubManager>();
            services.AddTransient<ITrainingDataSetManager, TrainingDataSetManager>();
            services.AddTransient<ISampleManager, SampleManager>();
            services.AddTransient<Interfaces.Managers.ILabelManager, LabelManager>();
            services.AddTransient<IExperimentResultManager, ExperimentResultManager>();
            services.AddTransient<IDdrManager, DdrManager>();
            services.AddTransient<IWorkflowDefinitionManager, WorkflowDefinitionManager>();
            services.AddScoped<IAgentContextManager, AgentContextManager>();
            services.AddScoped<IAgentSessionManager, AgentSessionManager>();
            services.AddScoped<IImageGeneratorManager, OpenAIManager>();
            services.AddScoped<IAiConversationManager, AiConversationManager>();
            services.AddScoped<ITextQueryManager, OpenAIManager>();
            services.AddSingleton<IAgentPersonaDefinitionManager, AgentPersonaDefinitionManager>();
            services.AddSingleton<IAgentToolBoxManager, AgentToolBoxManager>();
            services.AddSingleton<IAuthoritativeAnswerManager, AuthoritativeAnswerManager>();
        }
    }
}