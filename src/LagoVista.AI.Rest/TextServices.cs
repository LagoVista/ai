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
    public class TextServices : LagoVistaBaseController
    {
        private readonly ITextQueryManager _queryManager;

        public TextServices(ITextQueryManager queryManager, UserManager<AppUser> userManager, IAdminLogger logger)
                : base(userManager, logger)
        {
            _queryManager = queryManager ?? throw new ArgumentNullException(nameof(queryManager));
        }

        [HttpPost("/api/ai/textquery")]
        public  Task<InvokeResult<TextQueryResponse>> QueryAsync([FromBody] TextQuery query)
        {
            return _queryManager.HandlePromptAsync(query);
        }

        [HttpPost("/api/ai/textquery/factory/{type}")]
        public TextQuery CreateQueryAsync(TextQueryType query)
        {
            return new TextQuery()
            {
                QueryType = query
            };
        }
    }
}
