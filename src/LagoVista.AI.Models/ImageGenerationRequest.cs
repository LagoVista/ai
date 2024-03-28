using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    public class ImageGenerationRequest
    {
        public int NumberGenerated { get; set; } = 1;
        public string ImageType { get; set; }
        public string ContentType { get; set; }
        public string AdditionalDetails { get; set; }
  
        public string Size { get; set; } = "1024x1024";
    }

    public class ImageGenerationResponse
    {
        public string ImageUrl { get; set; }
        public string NewResponse { get; set; }
    }
}
