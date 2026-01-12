using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Registries
{
    public sealed class ExtractSymbolsProcessorRegistry : ProcessorRegistry<DocumentType, IExtractSymbolsProcessor>, IExtractSymbolsProcessorRegistry
    {
        public ExtractSymbolsProcessorRegistry(IDictionary<DocumentType, IExtractSymbolsProcessor> processors, IExtractSymbolsProcessor @default)
            : base(processors, @default)
        {
        }
    }
}
