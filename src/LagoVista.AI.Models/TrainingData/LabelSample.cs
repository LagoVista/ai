// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 91d06fcae1e2f43838603781e14432e3c3f319a971fe2821071d8bd477bbce80
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.Core.Models;
using System;

namespace LagoVista.AI.Models.TrainingData
{
    public class LabelSample: TableStorageEntity
    {
        public string LabelId { get; set; }
        public string Label { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long ContentSize { get; set; }
    }
}
