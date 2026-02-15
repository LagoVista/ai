using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Registries
{
    public sealed class SubtypeKindCategorizerRegistry : ProcessorRegistry<DocumentType, ISubtypeKindCategorizer>, ISubtypeKindCategorizerRegistry
    {
        public SubtypeKindCategorizerRegistry(IDictionary<DocumentType, ISubtypeKindCategorizer> processors, ISubtypeKindCategorizer @default)
            : base(processors, @default)
        {
        }
    }
}
