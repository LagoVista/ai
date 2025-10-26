using LagoVista.AI.Models;
using LagoVista.AI.Rag;
using LagoVista.AI.Rag.Types;
using LagoVista.Core.Models;
using Newtonsoft.Json;


var jsonConfig = System.IO.File.ReadAllText("appsettings.json");

var cfg = JsonConvert.DeserializeObject<IngestionConfig>(jsonConfig);

if (String.IsNullOrEmpty(cfg.Qdrant.ApiKey)) cfg.Qdrant.ApiKey = Environment.GetEnvironmentVariable("QDRANT_API_KEY") ?? throw new ArgumentNullException("QDRANT_API_KEY");
if (String.IsNullOrEmpty(cfg.Embeddings.ApiKey)) cfg.Embeddings.ApiKey = Environment.GetEnvironmentVariable("EMBEDDING_API_KEY") ?? throw new ArgumentNullException("EMBEDDING_API_KEY");
if (String.IsNullOrEmpty(cfg.ContentRepo.AccessKey)) cfg.ContentRepo.AccessKey = Environment.GetEnvironmentVariable("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY") ?? throw new ArgumentNullException("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY");

var vectoDb = new AgentContext()
{
    VectorDatabaseCollectionName = cfg.Qdrant.Collection,
    VectorDatabaseApiKey = cfg.Qdrant.ApiKey,
    VectorDatabaseUri = cfg.Qdrant.Endpoint,
    LlmApiKey = cfg.Embeddings.ApiKey,
    AzureAccountId = cfg.ContentRepo.AccountId,
    AzureApiToken = cfg.ContentRepo.AccessKey,
    OwnerOrganization = new EntityHeader()
    {
        Id = cfg.OrgId,
    }

};

var ingestor = new Ingestor(cfg, vectoDb);

await ingestor.IngestAsync("backend");


//await WebHost.Start(args);