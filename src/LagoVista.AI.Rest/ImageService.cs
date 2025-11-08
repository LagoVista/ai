// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: a3fde57715cda4d61975c71522322dcb75a2c81797a34136c8eee6cd9581d4e6
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.MediaServices.Models;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{
    [ConfirmedUser]
    public class ImageServices : LagoVistaBaseController
    {
        private readonly IImageGeneratorManager _queryManager;

        public ImageServices(IImageGeneratorManager queryManager, UserManager<AppUser> userManager, IAdminLogger logger)
                : base(userManager, logger)
        {
            _queryManager = queryManager ?? throw new ArgumentNullException(nameof(queryManager));
        }

        [HttpPost("/api/ai/image/generate")]
        public Task<InvokeResult<MediaResource[]>> QueryAsync([FromBody] ImageGenerationRequest request)
        {
            return _queryManager.GenerateImageAsync(request, OrgEntityHeader, UserEntityHeader);
        }
    }
}
