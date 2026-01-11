namespace LagoVista.AI.Indexing.Interfaces
{
    public interface IProcessorRegistry<TProcessor>
    {
        bool TryGet(string subKind, out TProcessor processor);

        TProcessor GetOrDefault(string subKind);

        TProcessor Default { get; }
    }
}
