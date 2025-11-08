// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 2c6c84416c7ed5afe5b5b88bee805bf7d8499c6bf3fb04895a2b439bfd0c1c39
// IndexVersion: 2
// --- END CODE INDEX META ---
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IEmbedder
    {
        Task<float[]> EmbedAsync(string text);
    }
}
