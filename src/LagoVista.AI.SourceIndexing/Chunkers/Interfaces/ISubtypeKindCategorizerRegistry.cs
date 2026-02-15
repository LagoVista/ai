using LagoVista.AI.Chunkers.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Indexing.Interfaces
{
    public interface ISubtypeKindCategorizerRegistry : IProcessorRegistry<DocumentType, ISubtypeKindCategorizer>
    {
    }
}
