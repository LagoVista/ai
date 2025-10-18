namespace RagCli.Types
{
    public class Config
    {
        public QdrantConfig Qdrant { get; set; } = new();
        public IngestionConfig Ingestion { get; set; } = new();
        public EmbeddingsConfig Embeddings { get; set; } = new();
    }

    public class QdrantConfig
    {
        public string Endpoint { get; set; } = "http://localhost:6333";
        public string ApiKey { get; set; } = string.Empty;
        public string Collection { get; set; } = "code_chunks";
        public int VectorSize { get; set; } = 1536;
        public string Distance { get; set; } = "Cosine"; // Cosine | Euclid | Dot
    }

    public class IngestionConfig
    {
        public int MaxTokensPerChunk { get; set; }
        public int OverlapLines { get; set; }
        public string SourceRoot { get; set; } = String.Empty;
        public List<string> Repositories { get; set; } = new();
        public List<string> Include { get; set; } = new();
        public List<string> Exclude { get; set; } = new();
    }

    public class EmbeddingsConfig
    {
        public string Provider { get; set; } = "OpenAI"; // OpenAI | Stub
        public string ApiKey { get; set; } = string.Empty; // Recommend env var injection
        public string Model { get; set; } = "text-embedding-3-large";
        public string? BaseUrl { get; set; } // Use Azure-compatible endpoint if needed
    }
}

