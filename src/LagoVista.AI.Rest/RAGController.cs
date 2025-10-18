using LagoVista.AI.Interfaces;
using LagoVista.AI.Services;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Rest
{
    [Authorize]
    [ConfirmedUser]
    public class RAGController : LagoVistaBaseController
    {
        private readonly ICodeRagAnswerService _answerService;

        public RAGController(ICodeRagAnswerService answerService, UserManager<AppUser> userManager, IAdminLogger logger) : base(userManager, logger)
        {
            _answerService = answerService ?? throw new ArgumentNullException(nameof(answerService));
        }

        [HttpGet("/api/ai/codebase/query")]
        public async Task<InvokeResult<AnswerResult>> GetCodeAnswerAsync([FromQuery] string question)
        {
            var answer = await _answerService.AnswerAsync(question);
            return InvokeResult<AnswerResult>.Create(answer); 
        }
    }
}
