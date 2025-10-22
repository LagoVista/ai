﻿using LagoVista.AI.Interfaces;
using LagoVista.AI.Services;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Web.Common.Attributes;
using LagoVista.IoT.Web.Common.Controllers;
using LagoVista.UserAdmin.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RingCentral;
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

        [HttpGet("/api/ai/llm/{vectordb}/query")]
        public Task<InvokeResult<AnswerResult>> GetCodeAnswerAsync(string vectordbid, [FromQuery] string question)
        {
            return _answerService.AnswerAsync(vectordbid, question, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/llm/{vectordbid}/content")]
        public Task<InvokeResult<string>> GetContent(string vectordbid, [FromQuery] string path, [FromQuery] string fileName, [FromQuery]int start, [FromQuery]int end)
        {
            return _answerService.GetContentAsync(vectordbid, path, fileName, start, end, OrgEntityHeader, UserEntityHeader);
        }


        [HttpGet("/api/ai/llm/query")]
        public Task<InvokeResult<AnswerResult>> GetCodeAnswerAsync([FromQuery] string question)
        {
            return _answerService.AnswerAsync(question, OrgEntityHeader, UserEntityHeader);
        }

        [HttpGet("/api/ai/llm/content")]
        public Task<InvokeResult<string>> GetContent([FromQuery] string path, [FromQuery] string fileName, [FromQuery] int start, [FromQuery] int end)
        {
            return _answerService.GetContentAsync(path, fileName, start, end, OrgEntityHeader, UserEntityHeader);
        }
    }
}
