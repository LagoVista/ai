using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.MediaServices.Models;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IImageGeneratorManager
    {
        Task<InvokeResult<ImageGenerationResponse[]>> GenerateImageAsync(ImageGenerationRequests imageRequest);
    }
}
