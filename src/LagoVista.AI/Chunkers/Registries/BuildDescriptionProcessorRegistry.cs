using LagoVista.AI.Indexing.Interfaces;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Registries
{
    public sealed class BuildDescriptionProcessorRegistry : ProcessorRegistry<IBuildDescriptionProcessor>, IBuildDescriptionProcessorRegistry
    {
        public BuildDescriptionProcessorRegistry(IDictionary<string, IBuildDescriptionProcessor> processors, IBuildDescriptionProcessor @default)
            : base(processors, @default)
        {
        }
    }
}
