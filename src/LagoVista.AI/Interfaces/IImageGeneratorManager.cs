// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 193f4d984672090fdfe96c178489b7a7b30795a5ff1ddbae28cf24c8ccaef15c
// IndexVersion: 2
// --- END CODE INDEX META ---
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
