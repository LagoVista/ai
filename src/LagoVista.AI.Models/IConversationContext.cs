using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    public interface IConversationContext
    {
        string Id { get; set; }
        string ModelName { get; set; }
        string Name { get; set; }
        string System { get; set; }
        float Temperature { get; set; }

        List<string> GetFormFields();
    }
}