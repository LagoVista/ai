// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 22b169a72d03951d415769c7ec58d779494dad3d3355ac78ac9a290228d3a8fb
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.Core.Models;

namespace LagoVista.AI.Models.TrainingData
{
    /// <summary>
    /// Table storage, pointer to blob storage.
    /// </summary>
    public class Sample : TableStorageEntity
    {
        public string Name { get; set; }
        public string Key { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long ContentSize { get; set; }
        public string CreationDate { get; set; }
        public string LastUpdatedDate { get; set; }
        public string CreatedById { get; set; }
        public string CreatedByName { get; set; }
        public string LastUpdatedById { get; set; }
        public string LastUpdatedByName { get; set; }
        public string OwnerOrganizationId { get; set; }
        public string OwnerOrganizationName { get; set; }
    }
}
