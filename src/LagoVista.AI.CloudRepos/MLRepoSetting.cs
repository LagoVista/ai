using LagoVista.AI.Interfaces;
using LagoVista.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.CloudRepos
{
    public class MLRepoSettings : IMLRepoSettings
    {
        public IConnectionSettings MLDocDbStorage { get; }
        public IConnectionSettings MLBlobStorage { get; }
        public IConnectionSettings MLTableStorage { get; }

        public MLRepoSettings(IConfiguration configuration)
        {
            MLDocDbStorage = configuration.CreateDefaultDBStorageSettings();
            MLBlobStorage = configuration.CreateDefaultTableStorageSettings();
            MLTableStorage = configuration.CreateDefaultTableStorageSettings();
        }
    }

    public class QdrantSettings : IQdrantSettings
    {
        public string QdrantEndpoint { get; }
        public string QdrantApiKey { get; }

        public QdrantSettings(IConfiguration configuration)
        {
            var qdrant =configuration.GetRequiredSection("Qdrant");
            QdrantEndpoint = qdrant.Require("Uri");
            QdrantApiKey = qdrant.Require("ApiKey");
        }
    }

    public class OpenAISettings : IOpenAISettings
    {
        public string OpenAIUrl { get; }
        public string OpenAIApiKey { get; }

        public OpenAISettings(IConfiguration configuration)
        {
            var openAiSection = configuration.GetRequiredSection("OpenAI");
            OpenAIUrl = openAiSection.Require("URL");
            OpenAIApiKey = openAiSection.Require("APIKey");
        }
    }

    public class TrainingDataSettings : ITrainingDataSettings
    {
        public IConnectionSettings SampleConnectionSettings { get; }

        public IConnectionSettings SampleMediaConnectionsSettings { get; }

        public IConnectionSettings TrainingDataSetsConnectionSettings { get; }

        public IConnectionSettings LabelsConnectionSettings { get; }

        public TrainingDataSettings(IConfiguration configuration)
        {
            SampleConnectionSettings = configuration.CreateDefaultTableStorageSettings();
            SampleMediaConnectionsSettings = configuration.CreateDefaultTableStorageSettings();
            TrainingDataSetsConnectionSettings = configuration.CreateDefaultDBStorageSettings();
            LabelsConnectionSettings = configuration.CreateDefaultDBStorageSettings();
        }
    }
}
