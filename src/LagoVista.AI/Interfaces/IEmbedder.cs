using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IEmbedder
    {
        Task<float[]> EmbedAsync(string text);
    }
}
