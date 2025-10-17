namespace RagCli.Services
{
    public interface IEmbedder
    {
        Task<float[]> EmbedAsync(string text);
    }

    // TEMP stub for wiring; replace with a real embedding model client.
    public class StubEmbedder : IEmbedder
    {
        private readonly int _dims;
        private readonly Random _rng = new(42);
        public StubEmbedder(int dims) => _dims = dims;
        public Task<float[]> EmbedAsync(string text)
        {
            var v = new float[_dims];
            for (int i = 0; i < _dims; i++) v[i] = (float)_rng.NextDouble();
            return Task.FromResult(v);
        }
    }
}
