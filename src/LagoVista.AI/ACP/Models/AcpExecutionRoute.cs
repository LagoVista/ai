using System;

public enum AcpRouteOutcome
{
    NoMatch = 0,
    SingleMatch = 1,
    MultipleMatch = 2
}

public sealed class AcpExecutionRoute
{
    public AcpRouteOutcome Outcome { get; set; }

    // For SingleMatch
    public string CommandId { get; set; }
    public string[] Args { get; set; } = Array.Empty<string>();

    // For MultipleMatch (optional until picker exists)
    public string[] CandidateCommandIds { get; set; } = Array.Empty<string>();

    public bool CanExecute => Outcome == AcpRouteOutcome.SingleMatch && !String.IsNullOrWhiteSpace(CommandId);
}