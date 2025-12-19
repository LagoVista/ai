using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using LagoVista.AI.Rag.Interfaces;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Default implementation of <see cref="IResxUpdateService"/> that performs
    /// in-place XML updates to .resx files.
    /// </summary>
    public class ResxUpdateService : IResxUpdateService
    {
        private readonly IAdminLogger _logger;

        public ResxUpdateService(IAdminLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task ApplyUpdatesAsync(string resxPath, IReadOnlyDictionary<string, string> updates, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(resxPath))
            {
                throw new ArgumentNullException(nameof(resxPath));
            }

            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            if (updates.Count == 0)
            {
                return Task.CompletedTask;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(resxPath))
            {
                throw new FileNotFoundException($"RESX file not found: {resxPath}", resxPath);
            }

            XDocument document;
            using (var stream = File.Open(resxPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                document = XDocument.Load(stream);
            }

            var root = document.Root;
            if (root == null)
            {
                throw new InvalidOperationException($"RESX document '{resxPath}' has no root element.");
            }

            var dataElements = root.Elements("data").ToList();

            foreach (var kvp in updates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var key = kvp.Key;
                var newValue = kvp.Value ?? string.Empty;

                var dataElement = dataElements.FirstOrDefault(e =>
                    string.Equals((string)e.Attribute("name"), key, StringComparison.Ordinal));

                if (dataElement == null)
                {
                    throw new InvalidOperationException(
                        $"RESX '{resxPath}' does not contain a &lt;data&gt; element with name='{key}'.");
                }

                var valueElement = dataElement.Element("value");
                if (valueElement == null)
                {
                    valueElement = new XElement("value");
                    dataElement.Add(valueElement);
                }

                valueElement.Value = newValue;
            }

            using (var stream = File.Open(resxPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                document.Save(stream);
            }

            _logger.Trace($"[ResxUpdateService] Applied {updates.Count} update(s) to {resxPath}.");

            return Task.CompletedTask;
        }
    }
}
