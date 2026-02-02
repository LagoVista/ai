using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Chunkers.Providers
{
    public interface IDescriptionProvider
    {
        string BuildSummaryForModel();
        string BuildSummaryForEmbedding();
        string BuildSummaryForHuman();
    }
}
