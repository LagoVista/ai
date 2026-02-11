using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Tool: List Categories
    /// Lists categories using ICategoryManager.
    /// </summary>
    public sealed class CategoryListTool : IAgentTool
    {
        /* -------------------------------------------------------------- */
        public const string ToolName = "categories_list";
        public const string ToolUsageMetadata = @"Lists categories. Categories are used to organized entities when adding and saving.
Categories are organized by category type.
Category types map to EntityType which is a top level property of all entities in the system.";
        public const string ToolSummary = "Lists categories.";

        private readonly IAdminLogger _logger;
        private readonly ICategoryManager _categoryManager;

        public CategoryListTool(
            ICategoryManager categoryManager,
            IAdminLogger logger)
        {
            _categoryManager = categoryManager ?? throw new ArgumentNullException(nameof(categoryManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => ToolName;
        public bool IsToolFullyExecutedOnServer => true;

        private sealed class Args
        {
            public string EntityType { get; set; }
            public int PageSize { get; set; } = 100;
            public int PageIndex { get; set; } = 0;
        }

        private sealed class Result
        {
            public Category[] Categories { get; set; }
            public int Count { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            try
            {
                var args = string.IsNullOrWhiteSpace(argumentsJson)
                    ? new Args()
                    : JsonConvert.DeserializeObject<Args>(argumentsJson);

                if (string.IsNullOrWhiteSpace(args.EntityType))
                    return InvokeResult<string>.FromError("entityType is required.");
              
                var listRequest = ListRequest.Create(args.PageSize, args.PageIndex);

                var response = await _categoryManager.GetCategoriesAsync(args.EntityType, listRequest,context.Envelope.Org, context.Envelope.User);

                var result = new Result
                {
                    Categories = response.Model.ToArray(),
                    Count = response.Model.Count()
                };

                return InvokeResult<string>.Create(JsonConvert.SerializeObject(result));
            }
            catch (Exception ex)
            {
                _logger.AddException("[ListCategoriesTool.ExecuteAsync]", ex);
                return InvokeResult<string>.FromError("Failed to list categories.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName,
                "Lists categories of a given EntityType.",
                p =>
                {
                    p.String("entityType","The category type to list.", required: true);
                });
        }
    }
}
