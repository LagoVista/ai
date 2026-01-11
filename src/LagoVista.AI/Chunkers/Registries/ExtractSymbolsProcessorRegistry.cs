using LagoVista.AI.Indexing.Interfaces;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Registries
{
    public sealed class ExtractSymbolsProcessorRegistry : ProcessorRegistry<IExtractSymbolsProcessor>, IExtractSymbolsProcessorRegistry
    {
        public ExtractSymbolsProcessorRegistry(IDictionary<string, IExtractSymbolsProcessor> processors, IExtractSymbolsProcessor @default)
            : base(processors, @default)
        {
        }
    }
}
