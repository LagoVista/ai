using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Indexing.Interfaces
{
    public interface IExtractSymbolsProcessorRegistry : IProcessorRegistry<DocumentType, IExtractSymbolsProcessor>
    {
    }
}
