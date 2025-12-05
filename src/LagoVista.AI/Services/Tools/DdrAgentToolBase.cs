using System;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Services.Tools
{
    /// <summary>
    /// Common base class for DDR-related agent tools that implement IAgentTool.
    /// Handles JSON parsing, error handling, and logging, while delegating
    /// core behavior to ExecuteCoreAsync in derived classes.
    /// </summary>
    public abstract class DdrAgentToolBase : IAgentTool
    {
        protected readonly IDdrManager _ddrManager;
        protected readonly IAdminLogger _adminLogger;

        protected DdrAgentToolBase(IDdrManager ddrManager, IAdminLogger adminLogger)
        {
            _ddrManager = ddrManager ?? throw new ArgumentNullException(nameof(ddrManager));
            _adminLogger = adminLogger ?? throw new ArgumentNullException(nameof(adminLogger));
        }

        /// <inheritdoc />
        public bool IsToolFullyExecutedOnServer => true;

        /// <inheritdoc />
        public abstract string Name { get; }

        /// <summary>
        /// Tag used for logging exceptions.
        /// </summary>
        protected abstract string Tag { get; }

        /// <inheritdoc />
        public async Task<InvokeResult<string>> ExecuteAsync(
            string argumentsJson,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                return InvokeResult<string>.FromError("Execution context must not be null.");
            }

            JObject payload;

            try
            {
                if (string.IsNullOrWhiteSpace(argumentsJson))
                {
                    payload = new JObject();
                }
                else
                {
                    payload = JObject.Parse(argumentsJson);
                }
            }
            catch (JsonException jex)
            {
                _adminLogger.AddException(Tag, jex);
                return InvokeResult<string>.FromError("argumentsJson was not valid JSON.");
            }

            try
            {
                return await ExecuteCoreAsync(payload, context, cancellationToken);
            }
            catch (Exception ex)
            {
                _adminLogger.AddException(Tag, ex);
                return InvokeResult<string>.FromException(Tag, ex);
            }
        }

        /// <summary>
        /// Derived tools implement their specific behavior here. The payload
        /// is the parsed JSON argument object the LLM sent for this tool.
        /// </summary>
        protected abstract Task<InvokeResult<string>> ExecuteCoreAsync(
            JObject payload,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken);

        /// <summary>
        /// Helper to wrap a JObject envelope into an InvokeResult&lt;string&gt;.
        /// </summary>
        protected InvokeResult<string> FromEnvelope(JObject envelope)
        {
            var json = envelope.ToString(Formatting.None);
            return InvokeResult<string>.Create(json);
        }

        /// <summary>
        /// Helper to create a simple error InvokeResult&lt;string&gt;.
        /// </summary>
        protected InvokeResult<string> FromError(string message)
        {
            return InvokeResult<string>.FromError(message);
        }
    }
}
