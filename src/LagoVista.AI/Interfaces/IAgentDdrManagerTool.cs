using System.Threading.Tasks;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json.Linq;

namespace LagoVista.AI.Interfaces
{
    /// <summary>
    /// Contract for the Agent DDR Manager Tool defined in TUL-005.
    /// This interface exposes a JSON-based operation surface suitable
    /// for use as an LLM tool.
    /// </summary>
    public interface IAgentDdrManagerTool
    {
        /// <summary>
        /// Handles a TUL-005 operation given an operation name and JSON payload.
        /// The result is a JSON object following the envelope:
        /// { "ok": true, "result": { ... } } or
        /// { "ok": false, "error": { "code": "...", "message": "..." } }.
        /// </summary>
        /// <param name="operation">Operation name (for example, create_ddr).</param>
        /// <param name="payload">Operation payload as JObject.</param>
        /// <param name="org">Organization header.</param>
        /// <param name="user">User header.</param>
        /// <returns>InvokeResult containing the JSON response object.</returns>
        Task<InvokeResult<JObject>> HandleAsync(
            string operation,
            JObject payload,
            EntityHeader org,
            EntityHeader user);
    }
}
