using LagoVista.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class ImageGenerationRequest
    {
        public string ResourceName { get; set; }
        public string EntityTypeName { get; set; }
        public string EntityFieldName { get; set; }

        public int NumberGenerated { get; set; } = 1;
        public string ImageType { get; set; }
        public string ContentType { get; set; }
        public string AdditionalDetails { get; set; }
  
        public string FullRequest { get; set; }

        public string Size { get; set; } = "1024x1024";
    
        public string MediaResourceId { get; set; }
        public string PreviousResponseId { get; set; }
        public bool IsPublic { get; set; }
    }

    public class ImageGenerationResponse
    {
        public string ImageUrl { get; set; }
        public string NewResponse { get; set; }
    }
}
