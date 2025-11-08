// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 6e1b4768c4e3f68bac478d34088f9668694af8861a9e9dedf34561ca631f74c5
// IndexVersion: 2
// --- END CODE INDEX META ---
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
