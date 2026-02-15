using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Indexing.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;

namespace LagoVista.AI.Indexing.Services
{
    /// <summary>
    /// Implementation of <see cref="IIngestionConfigProvider"/> that accepts raw JSON,
    /// deserializes it into <see cref="IngestionConfig"/>, applies validation and
    /// environment-based overrides for secrets (Qdrant, Embeddings, ContentRepo).
    /// </summary>
    public class JsonIngestionConfigProvider : IIngestionConfigProvider
    {
        public Task<InvokeResult<IngestionConfig>> LoadAsync(string json, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<InvokeResult<IngestionConfig>>(cancellationToken);
            }

            var validationResult = InvokeResult.Success;

            if (string.IsNullOrWhiteSpace(json))
            {
                validationResult.Errors.Add(new ErrorMessage(
                    "INGESTCFG_EMPTY_JSON",
                    "Ingestion configuration JSON is empty or whitespace."));

                return Task.FromResult(InvokeResult<IngestionConfig>.FromInvokeResult(validationResult));
            }

            IngestionConfig config;
            try
            {
                config = JsonConvert.DeserializeObject<IngestionConfig>(json);
            }
            catch (Exception ex)
            {
                // Wrap the exception in a standard InvokeResult<T> shape.
                return Task.FromResult(InvokeResult<IngestionConfig>.FromException(
                    "INGESTCFG_DESERIALIZE",
                    ex));
            }

            if (config == null)
            {
                validationResult.Errors.Add(new ErrorMessage(
                    "INGESTCFG_NULL_CONFIG",
                    "Deserialized ingestion configuration was null."));

                return Task.FromResult(InvokeResult<IngestionConfig>.FromInvokeResult(validationResult));
            }

            // Structural & semantic validation.
            var structuralResult = Validate(config);
            if (!structuralResult.Successful)
            {
                return Task.FromResult(InvokeResult<IngestionConfig>.FromInvokeResult(structuralResult));
            }

            // Apply environment-based overrides for secrets.
            var secretsResult = ApplySecretOverrides(config);
            if (!secretsResult.Successful)
            {
                return Task.FromResult(InvokeResult<IngestionConfig>.FromInvokeResult(secretsResult));
            }

            return Task.FromResult(InvokeResult<IngestionConfig>.Create(config));
        }

        /// <summary>
        /// Validate core configuration fields that are required for a successful run.
        /// </summary>
        private static InvokeResult Validate(IngestionConfig config)
        {
            var result = InvokeResult.Success;

            if (string.IsNullOrWhiteSpace(config.OrgId))
            {
                result.Errors.Add(new ErrorMessage(
                    "INGESTCFG_ORGID_REQUIRED",
                    "OrgId is required for ingestion configuration."));
            }

            if (config.Ingestion == null)
            {
                result.Errors.Add(new ErrorMessage(
                    "INGESTCFG_INGESTION_REQUIRED",
                    "Ingestion settings are required."));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(config.Ingestion.SourceRoot))
                {
                    result.Errors.Add(new ErrorMessage(
                        "INGESTCFG_SOURCEROOT_REQUIRED",
                        "SourceRoot is required in ingestion settings."));
                }

                if (config.Ingestion.MaxTokensPerChunk <= 0)
                {
                    result.Errors.Add(new ErrorMessage(
                        "INGESTCFG_MAXTOKENS_INVALID",
                        "MaxTokensPerChunk must be greater than zero."));
                }

                if (config.Ingestion.OverlapLines < 0)
                {
                    result.Errors.Add(new ErrorMessage(
                        "INGESTCFG_OVERLAP_INVALID",
                        "OverlapLines must be zero or greater."));
                }
            }

            if (config.Qdrant == null)
            {
                result.Errors.Add(new ErrorMessage(
                    "INGESTCFG_QDRANT_REQUIRED",
                    "Qdrant configuration is required."));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(config.Qdrant.Endpoint))
                {
                    result.Errors.Add(new ErrorMessage(
                        "INGESTCFG_QDRANT_ENDPOINT_REQUIRED",
                        "Qdrant endpoint is required."));
                }

                if (config.Qdrant.VectorSize <= 0)
                {
                    result.Errors.Add(new ErrorMessage(
                        "INGESTCFG_QDRANT_VECTORSIZE_INVALID",
                        "Qdrant vector size must be greater than zero."));
                }

                if (string.IsNullOrWhiteSpace(config.Qdrant.Collection))
                {
                    result.Errors.Add(new ErrorMessage(
                        "INGESTCFG_QDRANT_COLLECTION_REQUIRED",
                        "Qdrant collection name is required."));
                }
            }

            if (config.Embeddings == null)
            {
                result.Errors.Add(new ErrorMessage(
                    "INGESTCFG_EMBEDDINGS_REQUIRED",
                    "Embeddings configuration is required."));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(config.Embeddings.Model))
                {
                    result.Errors.Add(new ErrorMessage(
                        "INGESTCFG_EMBEDDINGS_MODEL_REQUIRED",
                        "Embeddings model is required."));
                }

                if (string.IsNullOrWhiteSpace(config.Embeddings.Provider))
                {
                    result.Errors.Add(new ErrorMessage(
                        "INGESTCFG_EMBEDDINGS_PROVIDER_REQUIRED",
                        "Embeddings provider is required."));
                }
            }

            // File ingestion include/exclude lists may legitimately be empty,
            // so we do not enforce constraints there for v1.

            return result;
        }

        /// <summary>
        /// Apply environment variable overrides for secrets if they are not present in config.
        /// </summary>
        private static InvokeResult ApplySecretOverrides(IngestionConfig cfg)
        {
            var result = InvokeResult.Success;

            if (cfg.Qdrant != null && string.IsNullOrEmpty(cfg.Qdrant.ApiKey))
            {
                var apiKey = Environment.GetEnvironmentVariable("QDRANT_API_KEY");
                if (string.IsNullOrEmpty(apiKey))
                {
                    result.Errors.Add(new ErrorMessage(
                        "INGESTCFG_QDRANT_APIKEY_REQUIRED",
                        "QDRANT_API_KEY environment variable is required when Qdrant.ApiKey is not set."));
                }
                else
                {
                    cfg.Qdrant.ApiKey = apiKey;
                }
            }

            if (cfg.Embeddings != null && string.IsNullOrEmpty(cfg.Embeddings.ApiKey))
            {
                var apiKey = Environment.GetEnvironmentVariable("EMBEDDING_API_KEY");
                if (string.IsNullOrEmpty(apiKey))
                {
                    result.Errors.Add(new ErrorMessage(
                        "INGESTCFG_EMBEDDINGS_APIKEY_REQUIRED",
                        "EMBEDDING_API_KEY environment variable is required when Embeddings.ApiKey is not set."));
                }
                else
                {
                    cfg.Embeddings.ApiKey = apiKey;
                }
            }

            if (cfg.ContentRepo != null && string.IsNullOrEmpty(cfg.ContentRepo.AccessKey))
            {
                var key = Environment.GetEnvironmentVariable("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY");
                if (string.IsNullOrEmpty(key))
                {
                    result.Errors.Add(new ErrorMessage(
                        "INGESTCFG_CONTENTREPO_ACCESSKEY_REQUIRED",
                        "PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY environment variable is required when ContentRepo.AccessKey is not set."));
                }
                else
                {
                    cfg.ContentRepo.AccessKey = key;
                }
            }

            return result;
        }
    }
}
