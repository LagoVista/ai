using LagoVista.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class ExperimentResult
    {
        public string Id { get; set; }
        public EntityHeader Model { get; set; }
        public EntityHeader Experiment { get; set; }
        public EntityHeader PerformedBy { get; set; }
        public EntityHeader<int> Revision { get; set; }
        public string Datestamp { get; set; }
        public bool Success { get; set; }
        public int SuccessPercent { get; set; }
        public int Accuracy { get; set; }
        public bool Subjective { get; set; }
    }
}
