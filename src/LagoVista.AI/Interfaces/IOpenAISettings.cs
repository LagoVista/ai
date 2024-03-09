using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IOpenAISettings
    {
         public string OpenAIUrl { get;  }
         public string OpenAIApiKey { get; }
    }
}
