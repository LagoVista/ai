using LagoVista.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.CloudRepos
{
    public interface ITrainingDataSettings
    {
        IConnectionSettings SampleConnectionSettings { get; }
        IConnectionSettings SampleMediaConnectionsSettings { get; }
        IConnectionSettings TrainingDataSetsConnectionSettings { get; }
        IConnectionSettings LabelsConnectionSettings { get; }

        bool ShouldConsolidateCollections { get; }
    }
}
