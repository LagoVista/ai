using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Indexing.Interfaces
{
    public interface IBuildDescriptionProcessorRegistry : IProcessorRegistry<SubtypeKind, IBuildDescriptionProcessor>
    {
    }
}
