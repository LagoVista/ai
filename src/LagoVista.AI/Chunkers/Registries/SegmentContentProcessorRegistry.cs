using LagoVista.AI.Indexing.Interfaces;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Registries
{
    public sealed class SegmentContentProcessorRegistry : ProcessorRegistry<ISegmentContentProcessor>, ISegmentContentProcessorRegistry
    {
        public SegmentContentProcessorRegistry(IDictionary<string, ISegmentContentProcessor> processors, ISegmentContentProcessor @default)
            : base(processors, @default)
        {
        }
    }
}
