using LagoVista.AI.Managers;
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
            services.AddTransient<ILabelManager, ILabelManager>();
            services.AddTransient<IExperimentResultManager, ExperimentResultManager>();
        }
    }
}