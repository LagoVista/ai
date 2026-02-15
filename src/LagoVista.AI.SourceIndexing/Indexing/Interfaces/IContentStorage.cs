using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Indexing.Interfaces
{
    public interface IContentStorage
    {
        Task<InvokeResult<Uri>> AddContentAsync(string blobName, string content);
        Task<InvokeResult<Uri>> AddContentAsync(string blobName, byte[] content);
    }
}
