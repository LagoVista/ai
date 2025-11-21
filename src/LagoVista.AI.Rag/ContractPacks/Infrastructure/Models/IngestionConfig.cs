using LagoVista.AI.CloudRepos;
using LagoVista.Core.Interfaces;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Models
{
    public class IngestionConfig
    {
        public string OrgId { get; set; }

        /// <summary>
        /// Global reindex mode for this run.
        /// Stored as a string so it can be manually edited in appsettings.
        /// Suggested values (case-insensitive):
        ///   null/empty  - default behavior (only changed/flagged files)
        ///   "none"     - same as default, no global override
        ///   "chunk"    - force re-chunk/re-embed where applicable
        ///   "full"     - force a full reindex pass
        /// </summary>
        public string Reindex { get; set; }

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
        public int MaxTokensPerChunk { get; set; }
        public int OverlapLines { get; set; }
        public string SourceRoot { get; set; } = string.Empty;
        public List<string> Repositories { get; set; } = new List<string>();
        public List<string> Include { get; set; } = new List<string>();
        public List<string> Exclude { get; set; } = new List<string>();
    }

    public class EmbeddingsConfig
    {
        public string Provider { get; set; } = "OpenAI";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "text-embedding-3-large";
        public string BaseUrl { get; set; }
    }

    public class AiSettings : IMLRepoSettings
    {
        public IConnectionSettings MLDocDbStorage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IConnectionSettings MLBlobStorage { get; set; }
        public IConnectionSettings MLTableStorage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool ShouldConsolidateCollections => throw new NotImplementedException();
    }
}
