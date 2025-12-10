//using System.Collections.Generic;
//using System.Threading.Tasks;
//using System.Threading;
//using LagoVista.AI.Models;

//namespace LagoVista.AI.Interfaces
//{
//    public interface IAgentModeCatalogService
//    {
//        /// <summary>
//        /// Returns a summary list of all modes known to the catalog.
//        /// Intended for UI, diagnostics, and LLM introspection tools.
//        /// </summary>
//        Task<IReadOnlyList<AgentModeSummary>> GetAllModesAsync(CancellationToken cancellationToken);

//        /// <summary>
//        /// Builds the Mode Catalog System Prompt block for the current LLM request.
//        /// Includes the current mode key, the one-line “WhenToUse” for each mode,
//        /// and instructions describing how mode switching works.
//        /// </summary>
//        string BuildSystemPrompt(AgentMode mode);


//        /// <summary>
//        /// Returns all tool IDs associated with the given mode key.
//        /// Returns an empty list if the key is unknown.
//        /// </summary>
//        List<string> GetToolsForMode(AgentMode mode);

//        /// <summary>
//        /// When the user transitions into a new mode, we should send them the
//        /// welcome message associated with that mode.
//        /// </summary>
//        /// <returns></returns>
//        string GetWelcomeMessage(AgentMode mode);
//    }
//}
