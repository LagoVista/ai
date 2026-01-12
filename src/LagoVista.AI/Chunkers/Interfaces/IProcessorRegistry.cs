namespace LagoVista.AI.Indexing.Interfaces
{
    public interface IProcessorRegistry<TKey, TProcessor>
    {
        bool TryGet(TKey subKind, out TProcessor processor);

        TProcessor GetOrDefault(TKey subKind);

        TProcessor Default { get; }
    }
}
