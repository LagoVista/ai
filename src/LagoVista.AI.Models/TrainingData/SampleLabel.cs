// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 37fe34e93144d4af8ecaa83d324a69dcbe392d55fcb89534129794f478a93d6c
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.Core.Models;

namespace LagoVista.AI.Models.TrainingData
{
    /// <summary>
    /// Table storage entity
    /// </summary>
    public class SampleLabel : TableStorageEntity
    {
        /// <summary>
        ///  Row Key - generated
        ///  Parition Key - Label id
        /// </summary>

        public string SampleId { get; set; }
        public string Name { get; set; }
        public string ContentType { get; set; }
        public long ContentSize { get; set; }
        public string LabelId { get; set; }
        public string Label { get; set; }
        public string FileName { get; set; }
        public string CreationDate { get; set; }
        public string CreatedById { get; set; }
        public string OwnerOrganizationId { get; set; }
        public string OwnerOrganizationName { get; set; }
    }
}
