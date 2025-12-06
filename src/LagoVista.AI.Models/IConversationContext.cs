// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 5e6c41b82e825d6111b6019ecd32f6afa47b37d64fe6258362dfa279aa8c8571
// IndexVersion: 2
// --- END CODE INDEX META ---
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    public interface IConversationContext
    {
        string Id { get; set; }
        string ModelName { get; set; }
        string Name { get; set; }
        List<string> SystemPrompts { get; set; }
        float Temperature { get; set; }

        List<string> GetFormFields();
    }
}