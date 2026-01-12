using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Indexing.Registries;
using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Chunkers.Registries
{
    public class SymbolIdentifierRegistry : ProcessorRegistry<DocumentType, ISubkindTypeIdentifier>, ISubkindTypeIdentifierRegistry
    {
        public SymbolIdentifierRegistry(IDictionary<DocumentType, ISubkindTypeIdentifier> processors, ISubkindTypeIdentifier @default) 
            : base(processors, @default)
        {
        }
    }
}
