using System;
using System.IO;
using System.Text.Json;
using LagoVista.AI.Rag.Types;

namespace LagoVista.AI.Rag.Services
{
    /// <summary>
    /// Writes the local index atomically (IDX-0036 Atomicity).
    /// - Write to temp file
    /// - Replace original atomically
    /// - Ensures directory exists
    /// </summary>
    public static class LocalIndexWriter
    {
        public static void Save(string indexPath, LocalIndexStore store)
        {
            if (string.IsNullOrWhiteSpace(indexPath))
                throw new ArgumentNullException(nameof(indexPath));
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            var dir = Path.GetDirectoryName(indexPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

            var tmp = indexPath + ".tmp";

            var json = JsonSerializer.Serialize(store, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(tmp, json);

            // Atomic replace
            File.Move(tmp, indexPath, overwrite: true);
        }
    }
}
