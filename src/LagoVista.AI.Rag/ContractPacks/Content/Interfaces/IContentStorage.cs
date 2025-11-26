using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.ContractPacks.Content.Interfaces
{
    public interface IContentStorage
    {
        Task<InvokeResult> AddContntAsync(string blobName, string content);
        Task<InvokeResult> AddContentAsync(string blobName, byte[] content);
    }
}
