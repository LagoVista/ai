using LagoVista.AI.Interfaces;
using LagoVista.AI.Managers;
using LagoVista.AI.Services;
using LagoVista.Core.Interfaces;

namespace LagoVista.AI
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
            services.AddTransient<ILabelManager, LabelManager>();
            services.AddTransient<IExperimentResultManager, ExperimentResultManager>();
            services.AddTransient<ITextQueryManager, OpenAIManager>();
            services.AddTransient<IImageGeneratorManager, OpenAIManager>();
            services.AddTransient<ICodeRagAnswerService, CodeRagAnswerService>();
            services.AddTransient<IQdrantClient, QdrantClient>();
            services.AddSingleton<IEmbedder, OpenAIEmbedder>();
            services.AddSingleton<IVectorDatabaseManager, VectorDatabaseManager>();
        }
    }
}