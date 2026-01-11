using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Chunkers.Interfaces
{
    public interface IResxLabelScanner
    {
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ScanResxTree(string rootDirectory);
        IReadOnlyDictionary<string, string> GetSingleResourceDictionary(string rootDirectory);
    }
}
