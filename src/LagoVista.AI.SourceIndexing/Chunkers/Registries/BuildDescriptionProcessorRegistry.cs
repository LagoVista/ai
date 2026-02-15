using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Registries
{
    public sealed class BuildDescriptionProcessorRegistry : ProcessorRegistry<SubtypeKind, IBuildDescriptionProcessor>, IBuildDescriptionProcessorRegistry
    {
        public BuildDescriptionProcessorRegistry(IDictionary<SubtypeKind, IBuildDescriptionProcessor> processors, IBuildDescriptionProcessor @default)
            : base(processors, @default)
        {
        }
    }
}
