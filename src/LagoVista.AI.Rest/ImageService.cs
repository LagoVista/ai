using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
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
        public Task<InvokeResult<ImageGenerationResponse[]>> QueryAsync([FromBody] ImageGenerationRequest request)
        {
            return _queryManager.GenerateImageAsync(request);
        }
    }
}
