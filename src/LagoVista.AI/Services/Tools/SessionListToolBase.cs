using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Interfaces.Managers;
using LagoVista.AI.Models;
using LagoVista.Core;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Shared base for session_list_* tools.
    /// Stores lists on context.Session.Lists (session persistence handled by host).
    /// </summary>
    public abstract class SessionListToolBase : IAgentTool
    {
        protected readonly IAdminLogger Logger;
        protected readonly IAgentSessionManager Sessions;

        protected SessionListToolBase(IAdminLogger logger, IAgentSessionManager sessions)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        public abstract string Name { get; }
        public virtual bool IsToolFullyExecutedOnServer => true;

        public virtual Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken cancellationToken = default)
            => throw new NotImplementedException("Use ExecuteAsync(string, IAgentPipelineContext).");

        public abstract Task<InvokeResult<string>> ExecuteAsync(string argumentsJson, IAgentPipelineContext context);

        protected static Task<InvokeResult<string>> Fail(string message)
        {
            var result = new
            {
                success = false,
                status = "error",
                error = message
            };

            return Task.FromResult(InvokeResult<string>.Create(JsonConvert.SerializeObject(result)));
        }

        protected static string UtcStamp() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        protected static void EnsureLists(IAgentPipelineContext context)
        {
            if (context?.Session == null)
                return;

            context.Session.Lists ??= new List<AgentSessionListDefinition>();
        }

        protected static AgentSessionListDefinition FindList(IAgentPipelineContext context, string listSlug)
        {
            EnsureLists(context);
            if (string.IsNullOrWhiteSpace(listSlug))
                return null;

            return context.Session.Lists.FirstOrDefault(l => string.Equals(l.Slug, listSlug, StringComparison.OrdinalIgnoreCase));
        }

        protected static AgentSessionListItem FindItem(AgentSessionListDefinition list, string itemSlug)
        {
            if (list == null || string.IsNullOrWhiteSpace(itemSlug))
                return null;

            return list.Items.FirstOrDefault(i => string.Equals(i.Slug, itemSlug, StringComparison.OrdinalIgnoreCase));
        }

        protected static string Slugify(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var lower = input.Trim().ToLowerInvariant();

            // Replace whitespace with '-'
            lower = Regex.Replace(lower, "\\s+", "-");

            // Strip non [a-z0-9-]
            lower = Regex.Replace(lower, "[^a-z0-9-]", string.Empty);

            // Collapse repeated '-'
            lower = Regex.Replace(lower, "-+", "-");

            // Trim '-'
            lower = lower.Trim('-');

            return lower;
        }

        protected static string EnsureUniqueListSlug(IAgentPipelineContext context, string desiredSlug)
        {
            EnsureLists(context);

            var baseSlug = desiredSlug;
            if (string.IsNullOrWhiteSpace(baseSlug))
                baseSlug = "list";

            var slug = baseSlug;
            var n = 2;

            while (context.Session.Lists.Any(l => string.Equals(l.Slug, slug, StringComparison.OrdinalIgnoreCase)))
            {
                slug = $"{baseSlug}-{n}";
                n++;
            }

            return slug;
        }

        protected static string EnsureUniqueItemSlug(AgentSessionListDefinition list, string desiredSlug)
        {
            if (list == null)
                return desiredSlug;

            var baseSlug = desiredSlug;
            if (string.IsNullOrWhiteSpace(baseSlug))
                baseSlug = "item";

            var slug = baseSlug;
            var n = 2;

            while (list.Items.Any(i => string.Equals(i.Slug, slug, StringComparison.OrdinalIgnoreCase)))
            {
                slug = $"{baseSlug}-{n}";
                n++;
            }

            return slug;
        }

        protected static InvokeResult<string> Ok(string op, object payload)
        {
            var result = new Result
            {
                Operation = op,
                CanRetry = false,
                Success = true,
                Status = "success",
                Payload = payload
            };

            return InvokeResult<string>.Create(JsonConvert.SerializeObject(result));
        }

        protected sealed class Result
        {
            public bool Success { get; set; }
            public string Status { get; set; }
            public bool CanRetry { get; set; }
            public string Operation { get; set; }
            public object Payload { get; set; }
        }

        protected static InvokeResult<string> OkList(string op, AgentSessionListDefinition list)
        {
            var payload = new
            {
                list = list
            };

            return Ok(op, payload);
        }

        protected static InvokeResult<string> OkLists(string op, IEnumerable<AgentSessionListDefinition> lists)
        {
            var payload = new
            {
                lists = lists?.ToList() ?? new List<AgentSessionListDefinition>()
            };

            return Ok(op, payload);
        }

        protected static InvokeResult<string> OkItem(string op, AgentSessionListDefinition list, AgentSessionListItem item)
        {
            var payload = new
            {
                listSlug = list?.Slug,
                item = item
            };

            return Ok(op, payload);
        }

        protected static InvokeResult<string> OkItems(string op, AgentSessionListDefinition list)
        {
            var payload = new
            {
                listSlug = list?.Slug,
                items = list?.Items?.OrderBy(i => i.Order).ToList() ?? new List<AgentSessionListItem>()
            };

            return Ok(op, payload);
        }
        protected static InvokeResult<string> OkMove(string op, AgentSessionListDefinition list)
        {
            var payload = new
            {
                listSlug = list?.Slug,
                items = (list?.Items?
                            .OrderBy(i => i.Order)
                            .Select(i => (object)new { i.Slug, i.Order })
                            .ToList())
                        ?? new List<object>()
            };

            return Ok(op, payload);
        }

        protected static InvokeResult<string> OkDeleted(string op, string listSlug, string itemSlug = null)
        {
            var payload = new
            {
                listSlug,
                itemSlug
            };

            return Ok(op, payload);
        }

        protected static InvokeResult<string> OkUpdated(string op, AgentSessionListDefinition list)
        {
            var payload = new
            {
                listSlug = list?.Slug,
                lastUpdatedDate = list?.LastUpdatedDate
            };

            return Ok(op, payload);
        }

        protected static InvokeResult<string> OkSessionTouched(string op, IAgentPipelineContext context)
        {
            var payload = new
            {
                sessionId = context?.Session?.Id
            };

            return Ok(op, payload);
        }

        protected static InvokeResult<string> OkListCreated(string op, AgentSessionListDefinition list)
        {
            var payload = new
            {
                listSlug = list?.Slug,
                listId = list?.Id,
                createdAt = list?.CreationDate
            };

            return Ok(op, payload);
        }

        protected static InvokeResult<string> OkItemCreated(string op, AgentSessionListDefinition list, AgentSessionListItem item)
        {
            var payload = new
            {
                listSlug = list?.Slug,
                itemSlug = item?.Slug,
                itemId = item?.Id,
                createdAt = item?.CreationDate
            };

            return Ok(op, payload);
        }

        protected static InvokeResult<string> OkItemUpdated(string op, AgentSessionListDefinition list, AgentSessionListItem item)
        {
            var payload = new
            {
                listSlug = list?.Slug,
                itemSlug = item?.Slug,
                lastUpdatedDate = item?.LastUpdatedDate
            };

            return Ok(op, payload);
        }

        protected static int NextOrder(AgentSessionListDefinition list)
        {
            if (list?.Items == null || list.Items.Count == 0)
                return 10;

            return list.Items.Max(i => i.Order) + 10;
        }

        protected static void RenumberOrders(AgentSessionListDefinition list)
        {
            if (list?.Items == null)
                return;

            var ordered = list.Items.OrderBy(i => i.Order).ToList();
            var order = 10;
            foreach (var item in ordered)
            {
                item.Order = order;
                order += 10;
            }
        }

        protected static InvokeResult<string> ValidateAgainstSchema(AgentSessionListDefinition list, Dictionary<string, string> data, out string error)
        {
            error = null;

            if (list?.Fields == null || list.Fields.Count == 0)
                return null;

            data ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var field in list.Fields.OrderBy(f => f.SortOrder))
            {
                if (field.Required)
                {
                    if (!data.TryGetValue(field.Key, out var val) || string.IsNullOrWhiteSpace(val))
                    {
                        error = $"Missing required field '{field.Key}'.";
                        return InvokeResult<string>.FromError(error);
                    }
                }

                if (!data.TryGetValue(field.Key, out var raw) || string.IsNullOrWhiteSpace(raw))
                    continue;

                switch (field.Type)
                {
                    case AgentSessionListFieldDataType.Number:
                        if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                        {
                            error = $"Field '{field.Key}' must be a number.";
                            return InvokeResult<string>.FromError(error);
                        }
                        break;

                    case AgentSessionListFieldDataType.Bool:
                        if (!TryParseBool(raw, out _))
                        {
                            error = $"Field '{field.Key}' must be a boolean (true/false/1/0).";
                            return InvokeResult<string>.FromError(error);
                        }
                        break;

                    case AgentSessionListFieldDataType.Date:
                    case AgentSessionListFieldDataType.DateTime:
                        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)
                            && !DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
                        {
                            error = $"Field '{field.Key}' must be a date.";
                            return InvokeResult<string>.FromError(error);
                        }
                        break;

                    case AgentSessionListFieldDataType.Enum:
                        if (field.EnumValues != null && field.EnumValues.Count > 0)
                        {
                            if (!field.EnumValues.Any(ev => string.Equals(ev, raw, StringComparison.OrdinalIgnoreCase)))
                            {
                                error = $"Field '{field.Key}' must be one of: {string.Join(", ", field.EnumValues)}.";
                                return InvokeResult<string>.FromError(error);
                            }
                        }
                        break;

                    case AgentSessionListFieldDataType.Text:
                    default:
                        break;
                }
            }

            return null;
        }

        private static bool TryParseBool(string raw, out bool value)
        {
            value = false;
            if (raw == null) return false;

            var s = raw.Trim();
            if (string.Equals(s, "1", StringComparison.OrdinalIgnoreCase)) { value = true; return true; }
            if (string.Equals(s, "0", StringComparison.OrdinalIgnoreCase)) { value = false; return true; }

            return bool.TryParse(s, out value);
        }
    }
}
