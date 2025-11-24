using LagoVista.AI.Rag.ContractPacks.IndexStore.Services;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Infrastructure.Services;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Services;
using LagoVista.AI.Rag.ContractPacks.Orchestration.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Orchestration.Services;
using LagoVista.AI.Rag.ContractPacks.Registry.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Registry.Services;
using LagoVista.AI.Rag.Models;
using LagoVista.Core.IOC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag
{
    public static class Startup
    {
        public static void Init()
        {
            SLWIOC.RegisterSingleton<IIngestionConfigProvider, JsonIngestionConfigProvider>();
            SLWIOC.RegisterSingleton<IIndexFileContextBuilder, IndexFileContextBuilder>();
            SLWIOC.RegisterSingleton<IFileDiscoveryService, FileDiscoveryService>();
            SLWIOC.RegisterSingleton<IFileIngestionPlanner, DefaultIngestionPlanner>();
            SLWIOC.RegisterSingleton<ILocalIndexStore, JsonLocalIndexStore>();
            SLWIOC.RegisterSingleton<IIndexingPipeline, DefaultIndexingPipeline>();
            SLWIOC.RegisterSingleton<IFacetAccumulator, InMemoryFacetAccumulator>();
            SLWIOC.RegisterSingleton<IGitRepoInspector, GitRepoInspector>();
            SLWIOC.RegisterSingleton<IMetadataRegistryClient, NuvIoTMetadataRegistryClient>();
            SLWIOC.Register<IIndexRunOrchestrator, IndexRunOrchestrator>();
        }
    }
}
