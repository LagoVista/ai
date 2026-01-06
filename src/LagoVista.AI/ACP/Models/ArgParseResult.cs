using System;

namespace LagoVista.AI.ACP.Models
{
    public sealed class ArgParseResult
    {
        public bool Success { get; set; }
        public string[] Args { get; set; } = Array.Empty<string>();
    }
}