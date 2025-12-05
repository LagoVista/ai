using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Interfaces
{
    public interface IResourceDictionary
    {
        string GetResourceByKey(string key, string fallback = "");
    }
}
