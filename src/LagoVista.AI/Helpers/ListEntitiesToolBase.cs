// Abstract base class for "List {Entity}" tools.
// - Standardizes Org/User validation, paging defaults, result payload shape, schema.
// - Avoids dynamic; uses strongly typed args parsing.
// - Keeps derived tools tiny: wire manager call + tool name.
//
// Suggested location: LagoVista.AI.Tools (or similar shared namespace).

using LagoVista.AI.Helper;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Helpers
{
    public abstract class ListEntitiesToolBase<TEntitySummary> : IAgentTool where TEntitySummary : SummaryData
    {
        public abstract string Name { get; }
        public virtual bool IsToolFullyExecutedOnServer => true;

        protected virtual int DefaultPageSize => 50;
        protected virtual int DefaultPageIndex => 1;

        protected virtual string FailureMessage => $"{Name} failed to list items.";

        protected sealed class Args
        {
            public int? PageSize { get; set; }
            public int? PageIndex { get; set; }
            public bool? ShowDrafts { get; set; } = true;
            public bool? IncludeDeleted { get; set; }
        }

        protected sealed class Result
        {
            public object Items { get; set; }
        }

        private readonly IAdminLogger _adminlogger;

        public ListEntitiesToolBase(IAdminLogger adminlogger)
        {
            _adminlogger = adminlogger ?? throw new ArgumentNullException(nameof(adminlogger));
        }


        public Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public async Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context)
        {
            try
            {
                var args = ParseArgs(argumentsJson);

                var listRequest = new ListRequest
                {
                    PageSize = args.PageSize ?? DefaultPageSize,
                    PageIndex = args.PageIndex ?? DefaultPageIndex,
                    ShowDrafts = args.ShowDrafts ?? false,
                    ShowDeleted = args.IncludeDeleted ?? false
                    // List tools typically want to show drafts, but this can be made more flexible if needed
                };



                var items = await ListAsync(listRequest, context);

                var payload = new Result
                {
                    Items = items
                };


                return InvokeResult<string>.Create(JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                await OnExceptionAsync(ex, context);
                return InvokeResult<string>.FromError(FailureMessage);
            }
        }

        protected virtual Args ParseArgs(string argumentsJson)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return new Args();

            try
            {
                return JsonConvert.DeserializeObject<Args>(argumentsJson) ?? new Args();
            }
            catch
            {
                // Treat bad args as defaults (keeps tool robust)
                return new Args();
            }
        }

        /// <summary>
        /// Implement: call into your manager to return a paged list.
        /// Return type can be whatever your list endpoint returns (PagedResult, ListResponse, IEnumerable, etc.).
        /// </summary>
        protected abstract Task<ListResponse<TEntitySummary>> ListAsync(ListRequest request, IAgentPipelineContext context);

        protected virtual Task OnExceptionAsync(Exception ex, IAgentPipelineContext context)
        {
            _adminlogger.AddException($"[{nameof(ListEntitiesToolBase<TEntitySummary>)}__{typeof(TEntitySummary).Name}__GetAsync]", ex);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Canonical schema for list tools.
        /// Derived tools can expose a static GetSchema() wrapper if your registry requires static.
        /// </summary>
        public static OpenAiToolDefinition BuildSchema(string toolName)
        {
            return ToolSchema.Function(toolName, $"List {typeof(TEntitySummary).Name} (summary list) for the current organization.", p =>
            {
                p.Integer("pageSize", $"Max results to return (default 50).");
                p.Integer("pageIndex", $"0-based page index (default 1).");
                p.Boolean("showDrafts", $"Whether to include draft items (default true).");
                p.Boolean("includeDeleted", $"Whether to include deleted items (default false).");
            });
        }
    }
}
