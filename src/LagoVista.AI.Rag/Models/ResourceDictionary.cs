using LagoVista.AI.Rag.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Models
{
    public class ResourceDictionary : IResourceDictionary
    {
        IReadOnlyDictionary<string, string> _dictionary;

        public ResourceDictionary(IReadOnlyDictionary<string, string> dictionary)
        {
            _dictionary = dictionary;
        }

        public string GetResourceByKey(string key, string fallback = "")
        {
            if(_dictionary.ContainsKey(key) == false)
            {
                return fallback;
            }

            return _dictionary[key];
        }
    }
}
