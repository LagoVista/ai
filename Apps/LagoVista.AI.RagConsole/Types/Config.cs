namespace RagCli.Types
{
    public class Config
    {
        public QdrantConfig Qdrant { get; set; } = new();
        public IngestionConfig Ingestion { get; set; } = new();
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
        public List<string> RootPaths { get; set; } = new();
        public List<string> Include { get; set; } = new();
        public List<string> Exclude { get; set; } = new();
    }
}

