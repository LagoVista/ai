using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.AI.Rag.ContractPacks.Content.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Content.Services;
using LagoVista.AI.Rag.ContractPacks.IndexStore.Services;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Services;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Services;
using LagoVista.AI.Rag.ContractPacks.Orchestration.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Orchestration.Services;
using LagoVista.AI.Rag.ContractPacks.Quality.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Registry.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Registry.Services;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Services;
using LagoVista.Core.Interfaces;
using LagoVista.Core.IOC;

namespace LagoVista.AI.Rag
{
    public static class Startup
    {
        public static void Init()
        {
            SLWIOC.RegisterSingleton<IQdrantClient, QdrantClient>();
            SLWIOC.RegisterSingleton<IIndexIdServices, IndexIdServices>();
            SLWIOC.RegisterSingleton<IIngestionConfigProvider, JsonIngestionConfigProvider>();
            SLWIOC.RegisterSingleton<IIndexFileContextBuilder, IndexFileContextBuilder>();
            SLWIOC.RegisterSingleton<IFileDiscoveryService, FileDiscoveryService>();
            SLWIOC.RegisterSingleton<IFileIngestionPlanner, DefaultIngestionPlanner>();
            SLWIOC.RegisterSingleton<IDomainModelCatalogBuilder, DomainModelCatalogBuilder>();
            SLWIOC.RegisterSingleton<ILocalIndexStore, JsonLocalIndexStore>();
            SLWIOC.RegisterSingleton<ISourceFileProcessor, SourceFileProcessor>();
            SLWIOC.RegisterSingleton<IIndexingPipeline, DefaultIndexingPipeline>();
            SLWIOC.RegisterSingleton<IFacetAccumulator, InMemoryFacetAccumulator>();
            SLWIOC.RegisterSingleton<IGitRepoInspector, GitRepoInspector>();
            SLWIOC.RegisterSingleton<IMetadataRegistryClient, NuvIoTMetadataRegistryClient>();
            SLWIOC.RegisterSingleton<IEmbedder, OpenAIEmbedder>();
            SLWIOC.RegisterSingleton<IContentStorage, ContentStorage>();
            SLWIOC.RegisterSingleton<IInterfaceSemanticEnricher, InterfaceSemanticEnricher>();
            SLWIOC.Register<IIndexRunOrchestrator, IndexRunOrchestrator>();
        }
    }
}
