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
