using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IQdrantSettings
    {
        public string QdrantEndpoint { get; }
        public string QdrantApiKey { get; }
    }
}
