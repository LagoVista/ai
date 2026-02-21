using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace LagoVista.AI.Models
{
    public class SessionCodeFileActivity
    {
        public string Timestamp { get; set; }
        public string FilePath { get; set; }
        public string Reason { get; set; }
    }
}
