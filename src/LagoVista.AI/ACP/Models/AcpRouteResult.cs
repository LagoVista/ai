using System.Collections.Generic;

namespace LagoVista.AI.ACP.Models
{
    public class AcpRouteResult
    {
        public AcpRouteOutcome Outcome { get; set; }

        // Present for SingleMatch
        public AcpRouteMatch Match { get; set; }

        // Present for MultipleMatch
        public List<AcpRouteMatch> Matches { get; set; } = new List<AcpRouteMatch>();
    }
}