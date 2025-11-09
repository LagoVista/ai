// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 391645f6d04e209d66bbcebeb470d282a37f5c16521331291417913caebf350e
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.CloudRepos;
using LagoVista.Core.Interfaces;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Types
{
    public class IngestionConfig
    {
        public string OrgId { get; set; }
      
        public bool Reindex { get; set; }

        public int IndexVersion { get; set; }

        public ContentRepo ContentRepo { get; set; } = new ContentRepo();
        public QdrantConfig Qdrant { get; set; } = new QdrantConfig();
        public FileIngestionConfig Ingestion { get; set; } = new FileIngestionConfig();
        public EmbeddingsConfig Embeddings { get; set; } = new EmbeddingsConfig();
    }

    public class ContentRepo
    {
        public string AccountId { get; set; }
        public string AccessKey { get; set; }
    }

    public class QdrantConfig
    {
        public string Endpoint { get; set; } = "http://localhost:6333";
        public string ApiKey { get; set; } = string.Empty;
        public string Collection { get; set; } = "code_chunks";
        public int VectorSize { get; set; } = 1536;
        public string Distance { get; set; } = "Cosine"; // Cosine | Euclid | Dot
    }

    public class FileIngestionConfig
    {
        private static readonly List<string> list = new List<string>();

        public int MaxTokensPerChunk { get; set; }
        public int OverlapLines { get; set; }
        public string SourceRoot { get; set; } = String.Empty;
        public List<string> Repositories { get; set; } = new List<string>();
        public List<string> Include { get; set; } = new List<string>();
        public List<string> Exclude { get; set; } = new List<string>();
    }

    public class EmbeddingsConfig
    {
        public string Provider { get; set; } = "OpenAI"; // OpenAI | Stub
        public string ApiKey { get; set; } = string.Empty; // Recommend env var injection
        public string Model { get; set; } = "text-embedding-3-large";
        public string BaseUrl { get; set; } // Use Azure-compatible endpoint if needed
    }

    public class AiSettings : IMLRepoSettings
    {
        public IConnectionSettings MLDocDbStorage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IConnectionSettings MLBlobStorage { get; set; }
        public IConnectionSettings MLTableStorage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool ShouldConsolidateCollections => throw new NotImplementedException();
    }
}

