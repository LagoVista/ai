using System;
using System.IO;
using System.Threading.Tasks;
using LagoVista.AI.Interfaces;

namespace LagoVista.AI.Services.Hashing
{
    /// <summary>
    /// Default implementation of IContentHashService that wraps the
    /// existing canonical content hashing helper.
    /// </summary>
    public sealed class DefaultContentHashService : IContentHashService
    {
        public async Task<string> ComputeFileHashAsync(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ArgumentNullException(nameof(fullPath));

            if (!File.Exists(fullPath))
                return string.Empty;

            var text = await File.ReadAllTextAsync(fullPath).ConfigureAwait(false);
            return ComputeTextHash(text);
        }

        public string ComputeTextHash(string content)
        {
            // For now we delegate directly to the existing ContentHashHelper.
            // The helper is expected to implement the DDR-defined semantics
            // (line ending normalization, truncation, etc.).
            return ContentHashHelper.GetHashForText(content);
        }
    }
}
