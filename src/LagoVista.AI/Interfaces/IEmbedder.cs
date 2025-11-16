// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: c3b710d10d87a1a0b2a2834a71bb271736d41558b286c69273c0ebe1caf3c4f5
// IndexVersion: 2
// --- END CODE INDEX META ---
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IEmbedder
    {
        Task<float[]> EmbedAsync(string text, int estimatedTokens);
    }
}
