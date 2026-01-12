using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Registries
{
    public sealed class SegmentContentProcessorRegistry : ProcessorRegistry<SubtypeKind, ISegmentContentProcessor>, ISegmentContentProcessorRegistry
    {
        public SegmentContentProcessorRegistry(IDictionary<SubtypeKind, ISegmentContentProcessor> processors, ISegmentContentProcessor @default)
            : base(processors, @default)
        {
        }
    }
}
