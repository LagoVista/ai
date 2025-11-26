// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 7c4c600b50992e1d71ce17788dcc94ca66d8eb18ea71f51272f93140a75742b5
// IndexVersion: 2
// --- END CODE INDEX META ---
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IQdrantSettings
    {
        public string QdrantEndpoint { get; }
        public string QdrantApiKey { get; }
        public int VectorSize { get; }
        public string Distance { get; }
    }
}
