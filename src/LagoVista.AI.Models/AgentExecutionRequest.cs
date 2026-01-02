using System.Collections.Generic;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Models
{
    public class ActiveFileDescriptor
    {
        public string Path { get; set; }
        public string Content { get; set; }
        public string ContentType { get; set; }
        public long SizeBytes { get; set; }
    }
}
