using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IImageGeneratorManager
    {
        Task<InvokeResult<ImageGenerationResponse[]>> GenerateImageAsync(ImageGenerationRequest imageRequest);
    }
}
