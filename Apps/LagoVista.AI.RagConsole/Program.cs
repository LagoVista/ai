using LagoVista.AI.Models;
using LagoVista.AI.Rag;
using LagoVista.AI.Rag.Types;
using Newtonsoft.Json;


var jsonConfig = File.ReadAllText("appsettings.json");

var cfg = JsonConvert.DeserializeObject<IngestionConfig>(jsonConfig);

if (String.IsNullOrEmpty(cfg.Qdrant.ApiKey)) cfg.Qdrant.ApiKey = Environment.GetEnvironmentVariable("QDRANT_API_KEY") ?? throw new ArgumentNullException("QDRANT_API_KEY");
if (String.IsNullOrEmpty(cfg.Embeddings.ApiKey)) cfg.Embeddings.ApiKey = Environment.GetEnvironmentVariable("EMBEDDING_API_KEY") ?? throw new ArgumentNullException("EMBEDDING_API_KEY");
if (String.IsNullOrEmpty(cfg.ContentRepo.AccessKey)) cfg.ContentRepo.AccessKey = Environment.GetEnvironmentVariable("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY") ?? throw new ArgumentNullException("PROD_TS_STORAGE_ACCOUNT_ACCESS_KEY");

var vectoDb = new VectorDatabase()
{
    CollectionName = cfg.Qdrant.Collection,
    VectorDatabaseApiKey = cfg.Qdrant.ApiKey,
    VectorDatabaseUri = cfg.Qdrant.Endpoint,
    OpenAIApiKey = cfg.Embeddings.ApiKey,
    AzureAccountId = cfg.ContentRepo.AccountId,
    AzureApiToken = cfg.ContentRepo.AccessKey
};

var ingestor = new Ingestor(cfg, vectoDb);

await ingestor.IngestAsync("backend");


//await WebHost.Start(args);