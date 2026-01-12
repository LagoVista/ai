using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Registries
{
    public sealed class SubtypeKindCategorizerRegistry : ProcessorRegistry<SubtypeKind, ISubtypeKindCategorizer>, ISubtypeKindCategorizerRegistry
    {
        public SubtypeKindCategorizerRegistry(IDictionary<SubtypeKind, ISubtypeKindCategorizer> processors, ISubtypeKindCategorizer @default)
            : base(processors, @default)
        {
        }
    }
}
