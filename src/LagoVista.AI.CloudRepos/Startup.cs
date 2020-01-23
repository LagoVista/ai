using LagoVista.Core.Interfaces;
using System;

namespace LagoVista.AI.CloudRepos
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IExperimentResultRepo, ExperimentResultRepo>();
            services.AddTransient<IModelCategoryRepo, ModelCategoryRepo>();
            services.AddTransient<IModelRepo, ModelRepo>();
            services.AddTransient<ISampleRepo, SampleRepo>();
            services.AddTransient<ISampleMediaRepo, SampleMediaRepo>();
            services.AddTransient<ILabelRepo, LabelRepo>();
            services.AddTransient<ITrainingDataSetRepo, TrainingDataSetRepo>();
            services.AddTransient<IMLModelRepo, MLModelRepo>();
        }
    }
}
