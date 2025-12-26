namespace LagoVista.AI.Tests.Services.Pipeline
{
    internal class ToolCallRequest
    {
        public string ToolCallId { get; set; }
        public string Name { get; set; }
        public bool RequiresClientExecution { get; set; }
    }
}