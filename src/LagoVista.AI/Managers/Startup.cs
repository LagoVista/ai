using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using Microsoft.Extensions.DependencyInjection;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.Core.PlatformSupport;

namespace LagoVista.AI.Managers
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
            services.AddTransient<Interfaces.Managers.ILabelManager, LabelManager>();
            services.AddTransient<IExperimentResultManager, ExperimentResultManager>();
            services.AddTransient<IDdrManager, DdrManager>();
            services.AddTransient<IWorkflowDefinitionManager, WorkflowDefinitionManager>();
            services.AddTransient<IAgentContextManager, AgentContextManager>();
            services.AddTransient<IAgentSessionManager, AgentSessionManager>();
            services.AddTransient<IImageGeneratorManager, OpenAIManager>();
            services.AddTransient<IAiConversationManager, AiConversationManager>();
            services.AddTransient<ITextQueryManager, OpenAIManager>();
            services.AddTransient<IAgentPersonaDefinitionManager, AgentPersonaDefinitionManager>();
            services.AddTransient<IAgentToolBoxManager, AgentToolBoxManager>();
            services.AddTransient<IReferenceEntryManager, ReferenceEntryManager>();
        }
    }
}