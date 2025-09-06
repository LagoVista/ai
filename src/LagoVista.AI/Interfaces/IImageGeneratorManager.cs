using LagoVista.AI.Models;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.MediaServices.Models;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces
{
    public interface IImageGeneratorManager
    {
        Task<InvokeResult<MediaResource[]>> GenerateImageAsync(ImageGenerationRequest imageRequest, EntityHeader org, EntityHeader user);
    }
}
