using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using PdfSharpCore.Pdf;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Tool: Create Category
    /// Creates a new category using ICategoryManager.
    /// </summary>
    public sealed class CategoryCreateTool : IAgentTool
    {
        /* -------------------------------------------------------------- */
        public const string ToolName = "category_create_tool";
        public const string ToolUsageMetadata = @"Creates a new category. 
- When creating a new entity, you can optionally organize them by categories.  
- Categories are organized by using categoryType that maps to the type of EntityType you want to categorize (e.g. AgentContext, Customer, etc.).
- The EntityType is available as a top level property on the entity that you will be working with.
- Before creating a category you MUST see if one already exists by using the ategories_list tool.
- When creating a category, the Key field must be unique for a given entity type.";

        public const string ToolSummary = "Creates a category used to organized entities";

        private readonly IAdminLogger _logger;
        private readonly ICategoryManager _categoryManager;

        public CategoryCreateTool(
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
            public String Name { get; set; }
            public String CategoryType { get; set; }
            public String Key { get; set; }
            public String Description { get; set; }
        }

        private sealed class Result
        {
            public bool Success { get; set; }
            public string CategoryId { get; set; }
            public string Message { get; set; }
        }

        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return InvokeResult<string>.FromError("CreateCategoryTool requires arguments.");

            try
            {
                var args = JsonConvert.DeserializeObject<Args>(argumentsJson);

                if(String.IsNullOrEmpty(args.Name))
                    return InvokeResult<string>.FromError("name is required.");

                if (String.IsNullOrEmpty(args.Key))
                    return InvokeResult<string>.FromError("key is required.");
             
                if (String.IsNullOrEmpty(args.CategoryType))
                    return InvokeResult<string>.FromError("categoryType is required.");

                var exitingCategories = await _categoryManager.GetCategoriesAsync(args.CategoryType, ListRequest.CreateForAll(), context.Envelope.Org, context.Envelope.User);
                var exisitng = exitingCategories.Model.FirstOrDefault(c => c.Key.Equals(args.Key, StringComparison.OrdinalIgnoreCase));
                if(exisitng != null)
                    return InvokeResult<string>.FromError($"A category with the key '{args.Key}' already exists for type '{args.CategoryType}'. Please choose a different key.");

                var category = new Category()
                {
                    CreationDate = context.TimeStamp,
                    LastUpdatedDate = context.TimeStamp,
                    OwnerOrganization = context.Envelope.Org,
                    CreatedBy = context.Envelope.User,
                    LastUpdatedBy = context.Envelope.User,
                    Name = args.Name,
                    Key = args.Key,
                    Description = args.Description,
                    CategoryType = args.CategoryType
                };

                var result = await _categoryManager.AddCategoryAsync(category, context.Envelope.Org, context.Envelope.User);

                if (!result.Successful)
                    return result.ToInvokeResult<string>();

                var response = new Result
                {
                    Success = true,
                    CategoryId = category.Id,
                    Message = $"Category '{category.Name}' created successfully."
                };

                return InvokeResult<string>.Create(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                _logger.AddException("[CreateCategoryTool.ExecuteAsync]", ex);
                return InvokeResult<string>.FromError("Failed to create category.");
            }
        }

        public static OpenAiToolDefinition GetSchema()
        {
            return ToolSchema.Function(ToolName,
                "Creates a new category used to organize entities.",
                p =>
                {
                    p.String(nameof(Args.Name).CamelCase(), "Display name of the category.", required: true);
                    p.String(nameof(Args.CategoryType).CamelCase(), "Type of the entity for this categor.", required: true);
                    p.String(nameof(Args.Key).CamelCase(), "Key for category, must only be lower case letters and be between 3 and 20 characters.", required: true);
                    p.String(nameof(Args.Description).CamelCase(), "Optional description.");
                });
        }
    }
}
