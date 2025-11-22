using System;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Services;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests.Services
{
    [TestFixture]
    public class JsonIngestionConfigProviderTests
    {
        private JsonIngestionConfigProvider _provider;

        [SetUp]
        public void Setup()
        {
            _provider = new JsonIngestionConfigProvider();
        }

        #region HappyPath

        [Test]
        public async Task LoadAsync_ValidJson_ReturnsSuccessfulResult()
        {
            var json = @"
            {
                ""OrgId"": ""test-org"",
                ""IndexVersion"": 1,
                ""Ingestion"": {
                    ""MaxTokensPerChunk"": 500,
                    ""OverlapLines"": 5,
                    ""SourceRoot"": ""c:\\src"",
                    ""Repositories"": [ ""Repo1"" ]
                },
                ""Qdrant"": {
                    ""Endpoint"": ""http://localhost:6333"",
                    ""ApiKey"": ""key"",
                    ""Collection"": ""code_chunks"",
                    ""VectorSize"": 1536
                },
                ""Embeddings"": {
                    ""Provider"": ""OpenAI"",
                    ""ApiKey"": ""emb-key"",
                    ""Model"": ""text-embedding-3-large""
                },
                ""ContentRepo"": {
                    ""AccountId"": ""account"",
                    ""AccessKey"": ""access""
                }
            }
            ";

            var result = await _provider.LoadAsync(json);

            Assert.That(result.Successful, Is.True, result.ToString());
            Assert.That(result.Result, Is.Not.Null);

            var cfg = result.Result;
            Assert.That(cfg.OrgId, Is.EqualTo("test-org"));
            Assert.That(cfg.Ingestion.SourceRoot, Is.EqualTo("c:\\src"));
            Assert.That(cfg.Ingestion.MaxTokensPerChunk, Is.EqualTo(500));
            Assert.That(cfg.Qdrant.Collection, Is.EqualTo("code_chunks"));
            Assert.That(cfg.Embeddings.Model, Is.EqualTo("text-embedding-3-large"));
        }

        #endregion

        #region StructuralValidation

        [Test]
        public async Task LoadAsync_MissingOrgId_ReturnsError()
        {
            var json = @"
            {
                ""IndexVersion"": 1,
                ""Ingestion"": {
                    ""MaxTokensPerChunk"": 500,
                    ""OverlapLines"": 5,
                    ""SourceRoot"": ""c:\\src""
                },
                ""Qdrant"": { ""Endpoint"": ""http://localhost:6333"", ""Collection"": ""code_chunks"", ""VectorSize"": 1536 },
                ""Embeddings"": { ""Provider"": ""OpenAI"", ""Model"": ""text-embedding-3-large"" }
            }
            ";

            var result = await _provider.LoadAsync(json);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors.Any(e => e.ErrorCode == "INGESTCFG_ORGID_REQUIRED"), Is.True);
        }

        [Test]
        public async Task LoadAsync_MaxTokensZero_ReturnsError()
        {
            var json = @"
            {
                ""OrgId"": ""test"",
                ""Ingestion"": {
                    ""MaxTokensPerChunk"": 0,
                    ""OverlapLines"": 5,
                    ""SourceRoot"": ""c:\\src""
                },
                ""Qdrant"": { ""Endpoint"": ""http://localhost:6333"", ""Collection"": ""code_chunks"", ""VectorSize"": 1536 },
                ""Embeddings"": { ""Provider"": ""OpenAI"", ""Model"": ""text-embedding-3-large"" }
            }
            ";

            var result = await _provider.LoadAsync(json);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors.Any(e => e.ErrorCode == "INGESTCFG_MAXTOKENS_INVALID"), Is.True);
        }

        #endregion

        #region SecretResolution

        [Test]
        public async Task LoadAsync_MissingKeys_UsesEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable("QDRANT_API_KEY", "qdrant-test");
            Environment.SetEnvironmentVariable("EMBEDDING_API_KEY", "embed-test");
            Environment.SetEnvironmentVariable("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY", "repo-test");

            try
            {
                var json = @"
                {
                    ""OrgId"": ""test-org"",
                    ""IndexVersion"": 1,
                    ""Ingestion"": {
                        ""MaxTokensPerChunk"": 500,
                        ""OverlapLines"": 5,
                        ""SourceRoot"": ""c:\\src""
                    },
                    ""Qdrant"": {
                        ""Endpoint"": ""http://localhost:6333"",
                        ""Collection"": ""code_chunks"",
                        ""VectorSize"": 1536
                    },
                    ""Embeddings"": {
                        ""Provider"": ""OpenAI"",
                        ""Model"": ""text-embedding-3-large""
                    },
                    ""ContentRepo"": {
                        ""AccountId"": ""account""
                    }
                }
                ";

                var result = await _provider.LoadAsync(json);

                Assert.That(result.Successful, Is.True);
                Assert.That(result.Result.Qdrant.ApiKey, Is.EqualTo("qdrant-test"));
                Assert.That(result.Result.Embeddings.ApiKey, Is.EqualTo("embed-test"));
                Assert.That(result.Result.ContentRepo.AccessKey, Is.EqualTo("repo-test"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("QDRANT_API_KEY", null);
                Environment.SetEnvironmentVariable("EMBEDDING_API_KEY", null);
                Environment.SetEnvironmentVariable("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY", null);
            }
        }

        [Test]
        public async Task LoadAsync_MissingSecretsAndEnvVars_ReturnsErrors()
        {
            Environment.SetEnvironmentVariable("QDRANT_API_KEY", null);
            Environment.SetEnvironmentVariable("EMBEDDING_API_KEY", null);
            Environment.SetEnvironmentVariable("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY", null);

            var json = @"
            {
                ""OrgId"": ""test-org"",
                ""IndexVersion"": 1,
                ""Ingestion"": {
                    ""MaxTokensPerChunk"": 500,
                    ""OverlapLines"": 5,
                    ""SourceRoot"": ""c:\\src""
                },
                ""Qdrant"": {
                    ""Endpoint"": ""http://localhost:6333"",
                    ""Collection"": ""code_chunks"",
                    ""VectorSize"": 1536
                },
                ""Embeddings"": {
                    ""Provider"": ""OpenAI"",
                    ""Model"": ""text-embedding-3-large""
                },
                ""ContentRepo"": {
                    ""AccountId"": ""account""
                }
            }
            ";

            var result = await _provider.LoadAsync(json);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors.Any(e => e.ErrorCode == "INGESTCFG_QDRANT_APIKEY_REQUIRED"), Is.True);
            Assert.That(result.Errors.Any(e => e.ErrorCode == "INGESTCFG_EMBEDDINGS_APIKEY_REQUIRED"), Is.True);
            Assert.That(result.Errors.Any(e => e.ErrorCode == "INGESTCFG_CONTENTREPO_ACCESSKEY_REQUIRED"), Is.True);
        }

        #endregion

        #region InvalidJson

        [Test]
        public async Task LoadAsync_EmptyJson_ReturnsError()
        {
            var result = await _provider.LoadAsync(string.Empty);

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors.Any(e => e.ErrorCode == "INGESTCFG_EMPTY_JSON"), Is.True);
        }

        [Test]
        public async Task LoadAsync_GarbageJson_ReturnsExceptionInvokeResult()
        {
            var result = await _provider.LoadAsync("this is not json");

            Assert.That(result.Successful, Is.False);
            Assert.That(result.Errors.Any(e => e.ErrorCode == "EXC9999" || e.ErrorCode == "EXC9998"), Is.True);
        }

        #endregion
    }
}
