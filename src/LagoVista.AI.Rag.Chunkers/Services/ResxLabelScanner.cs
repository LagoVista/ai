using LagoVista.AI.Rag.Chunkers.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    /// <summary>
    /// Scans .resx files under a directory tree and extracts resource labels
    /// into simple C# dictionaries (name → value).
    ///
    /// This is text/XML based only – no reflection or ResourceManager usage.
    /// </summary>
    public class ResxLabelScanner : IResxLabelScanner
    {
        /// <summary>
        /// Scans a directory tree for *.resx files and returns a map:
        ///   relativePath (from root) → (labelName → labelValue).
        /// 
        /// rootDirectory must exist; throws if not.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ScanResxTree(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentNullException(nameof(rootDirectory));

            if (!Directory.Exists(rootDirectory))
                throw new DirectoryNotFoundException($"Root directory not found: {rootDirectory}");

            var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var fullPath in Directory.EnumerateFiles(rootDirectory, "*.resx", SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(rootDirectory, fullPath).Replace('\\', '/');

                try
                {
                    var labels = ReadResxFile(fullPath);
                    result[relPath] = labels;
                }
                catch (Exception ex)
                {
                    // For now, just log to console; you can swap this for your logging abstraction.
                    Console.WriteLine($"[ResxLabelScanner] Failed to read '{fullPath}': {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Scans a directory tree for *.resx files and returns a map:
        ///   relativePath (from root) → (labelName → labelValue).
        /// 
        /// rootDirectory must exist; throws if not.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetSingleResourceDictionary(string rootDirectory)
        {
            var dictionaries = ScanResxTree(rootDirectory);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach(var parentDictionary in dictionaries)
            {
                foreach (var dictionary in parentDictionary.Value)
                {
                    if (!result.ContainsKey(dictionary.Key))
                    {
                        result[dictionary.Key] = dictionary.Value;
                    }
                }
            }

            return result;

        }

        /// <summary>
        /// Parses a single .resx file and returns a dictionary of
        ///   labelName → labelValue
        /// for all &lt;data name="..."&gt;&lt;value&gt;...&lt;/value&gt;&lt;/data&gt; elements.
        /// </summary>
        public static IReadOnlyDictionary<string, string> ReadResxFile(string resxPath)
        {
            if (string.IsNullOrWhiteSpace(resxPath))
                throw new ArgumentNullException(nameof(resxPath));

            var doc = XDocument.Load(resxPath);

            var root = doc.Root;
            if (root == null)
                throw new InvalidDataException($"RESX file '{resxPath}' has no root element.");

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dataElem in root.Elements("data"))
            {
                var nameAttr = dataElem.Attribute("name");
                if (nameAttr == null) continue;

                var name = nameAttr.Value?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var valueElem = dataElem.Element("value");
                if (valueElem == null) continue;

                var value = valueElem.Value ?? string.Empty;

                // Last one wins if duplicates exist – tweak if you prefer first-wins.
                dict[name] = value;
            }

            return dict;
        }

        /// <summary>
        /// Convenience API for cases where you already have the XML text.
        /// </summary>
        public static IReadOnlyDictionary<string, string> ReadResxText(string xmlText)
        {
            if (string.IsNullOrWhiteSpace(xmlText))
                throw new ArgumentNullException(nameof(xmlText));

            var doc = XDocument.Parse(xmlText);

            var root = doc.Root;
            if (root == null)
                throw new InvalidDataException("RESX XML has no root element.");

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dataElem in root.Elements("data"))
            {
                var nameAttr = dataElem.Attribute("name");
                if (nameAttr == null) continue;

                var name = nameAttr.Value?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var valueElem = dataElem.Element("value");
                if (valueElem == null) continue;

                var value = valueElem.Value ?? string.Empty;
                dict[name] = value;
            }

            return dict;
        }
    }
}
