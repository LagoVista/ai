using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Represents a knowledge lane (Session or Consumable) within an AKP.
    /// </summary>
    public sealed class KnowledgeLane
    {
        public List<KnowledgeItem> Items { get; set; } = new List<KnowledgeItem>();
    }
}
