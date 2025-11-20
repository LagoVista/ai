using System;
using System.IO;
using System.Text.Json;

using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Loads IngestionConfig from a JSON file and applies environment-based
    /// secret overrides for API keys and storage access keys.
    /// </summary>
    public static class IngestionConfigLoader
    {
        public static IngestionConfig LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException("Configuration file not found.", path);

            var json = File.ReadAllText(path);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var cfg = JsonSerializer.Deserialize<IngestionConfig>(json, options)
                      ?? throw new InvalidOperationException("Configuration deserialization returned null.");

            ApplyEnvironmentOverrides(cfg);

            return cfg;
        }

        public static void ApplyEnvironmentOverrides(IngestionConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            if (cfg.Qdrant == null)
                cfg.Qdrant = new Models.QdrantConfig();

            if (cfg.Embeddings == null)
                cfg.Embeddings = new EmbeddingsConfig();

            if (cfg.ContentRepo == null)
                cfg.ContentRepo = new ContentRepo();

            if (string.IsNullOrEmpty(cfg.Qdrant.ApiKey))
            {
                cfg.Qdrant.ApiKey =
                    Environment.GetEnvironmentVariable("QDRANT_API_KEY")
                    ?? throw new ArgumentNullException("QDRANT_API_KEY");
            }

            if (string.IsNullOrEmpty(cfg.Embeddings.ApiKey))
            {
                cfg.Embeddings.ApiKey =
                    Environment.GetEnvironmentVariable("EMBEDDING_API_KEY")
                    ?? throw new ArgumentNullException("EMBEDDING_API_KEY");
            }

            if (string.IsNullOrEmpty(cfg.ContentRepo.AccessKey))
            {
                cfg.ContentRepo.AccessKey =
                    Environment.GetEnvironmentVariable("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY")
                    ?? throw new ArgumentNullException("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY");
            }
        }
    }
}
