// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 8efc0f61bf692fa62065ed970fa97ea755cdced8f3daa7d05d1ea55f4cddf38b
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models;
using LagoVista.Core.Validation;
using System.Diagnostics;
using System;
using System.Threading.Tasks;

namespace LagoVista.AI.Interfaces.Repos
{
    public interface ILLMContentRepo
    {
        Task<InvokeResult> AddImageContentAsync(AgentContext vectorDb, string path, string fileName, byte[] model, string contentType);
        Task<InvokeResult<byte[]>> GetImageContentAsync(AgentContext vectorDb, string path, string fileName);
        Task<InvokeResult> AddTextContentAsync(AgentContext vectorDb, string path, string fileName, string content, string contentType);
        Task<InvokeResult<string>> GetTextContentAsync(AgentContext vectorDb, string path, string fileName);
        Task<InvokeResult<string>> GetTextContentAsync(AgentContext vectorDb, string blobName);

        Task<InvokeResult<Uri>> AddContentAsync(string orgNs, string blobName, string content);

        Task<InvokeResult<Uri>> AddContentAsync(string orgNs, string blobName, byte[] content);
    }
}
