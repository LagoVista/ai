using System.Collections.Generic;
using System.Linq;
using LagoVista.Core.Validation;
using Newtonsoft.Json;

namespace LagoVista.AI.Services.Tools
{
    public interface IWorkspaceWritePatchValidator
    {
        InvokeResult Validate(WorkspaceWritePatchArgs args);
    }

    /// <summary>
    /// Pure validation logic for TUL-002 arguments.
    /// Fully unit-testable without any external dependencies.
    /// </summary>
    public sealed class WorkspaceWritePatchValidator : IWorkspaceWritePatchValidator
    {
        public InvokeResult Validate(WorkspaceWritePatchArgs args)
        {
            if (args == null)
            {
                return InvokeResult.FromError("Arguments payload was null.");
            }

            if (args.Files == null || args.Files.Count == 0)
            {
                return InvokeResult.FromError("At least one file patch must be provided in files[].");
            }

            // Ensure docPath uniqueness within batch.
            var duplicateDocPaths = args.Files
                .GroupBy(f => (f.DocPath ?? string.Empty).Trim().ToLowerInvariant())
                .Where(g => g.Count() > 1 && !string.IsNullOrWhiteSpace(g.Key))
                .Select(g => g.Key)
                .ToList();

            if (duplicateDocPaths.Count > 0)
            {
                return InvokeResult.FromError($"Each docPath may appear at most once per batch. Duplicates: {string.Join(", ", duplicateDocPaths)}");
            }

            foreach (var file in args.Files)
            {
                if (string.IsNullOrWhiteSpace(file.DocPath))
                {
                    return InvokeResult.FromError("Each file patch must include a non-empty docPath.");
                }

                if (string.IsNullOrWhiteSpace(file.OriginalSha256))
                {
                    return InvokeResult.FromError($"File '{file.DocPath}' is missing originalSha256.");
                }

                if (file.OriginalSha256.Length != 64)
                {
                    return InvokeResult.FromError($"File '{file.DocPath}' originalSha256 must be a 64-character SHA256 hex string.");
                }

                if (file.Changes == null || file.Changes.Count == 0)
                {
                    return InvokeResult.FromError($"File '{file.DocPath}' must contain at least one change.");
                }

                foreach (var change in file.Changes)
                {
                    if (string.IsNullOrWhiteSpace(change.Operation))
                    {
                        return InvokeResult.FromError($"File '{file.DocPath}' has a change with missing operation.");
                    }

                    var op = change.Operation.Trim().ToLowerInvariant();
                    if (op != "insert" && op != "replace" && op != "delete")
                    {
                        return InvokeResult.FromError($"File '{file.DocPath}' has a change with unsupported operation '{change.Operation}'.");
                    }

                    if (op == "insert")
                    {
                        if (!change.AfterLine.HasValue || change.AfterLine.Value < 0)
                        {
                            return InvokeResult.FromError($"INSERT changes for file '{file.DocPath}' must specify a non-negative afterLine (0 for top-of-file).");
                        }

                        if (change.NewLines == null || change.NewLines.Count == 0)
                        {
                            return InvokeResult.FromError($"INSERT changes for file '{file.DocPath}' must include at least one new line in newLines.");
                        }
                    }
                    else if (op == "replace" || op == "delete")
                    {
                        if (!change.StartLine.HasValue || !change.EndLine.HasValue ||
                            change.StartLine.Value <= 0 || change.EndLine.Value < change.StartLine.Value)
                        {
                            return InvokeResult.FromError($"REPLACE or DELETE changes for file '{file.DocPath}' must specify valid startLine and endLine (1-based, start <= end).");
                        }

                        if (op == "replace")
                        {
                            if (change.NewLines == null || change.NewLines.Count == 0)
                            {
                                return InvokeResult.FromError($"REPLACE changes for file '{file.DocPath}' must include at least one new line in newLines.");
                            }
                        }
                    }
                }

                // Optional: non-overlap / ordering checks could be added here later.
            }

            return InvokeResult.Success;
        }
    }

    #region Argument DTOs

    public sealed class WorkspaceWritePatchArgs
    {
        [JsonProperty("batchLabel")]
        public string BatchLabel { get; set; }

        [JsonProperty("batchKey")]
        public string BatchKey { get; set; }

        [JsonProperty("files")]
        public List<FilePatchArgs> Files { get; set; } = new List<FilePatchArgs>();
    }

    public sealed class FilePatchArgs
    {
        [JsonProperty("docPath")]
        public string DocPath { get; set; }

        [JsonProperty("fileKey")]
        public string FileKey { get; set; }

        [JsonProperty("fileLabel")]
        public string FileLabel { get; set; }

        [JsonProperty("originalSha256")]
        public string OriginalSha256 { get; set; }

        [JsonProperty("changes")]
        public List<ChangeArgs> Changes { get; set; } = new List<ChangeArgs>();
    }

    public sealed class ChangeArgs
    {
        [JsonProperty("changeKey")]
        public string ChangeKey { get; set; }

        [JsonProperty("operation")]
        public string Operation { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("afterLine")]
        public int? AfterLine { get; set; }

        [JsonProperty("startLine")]
        public int? StartLine { get; set; }

        [JsonProperty("endLine")]
        public int? EndLine { get; set; }

        [JsonProperty("expectedOriginalLines")]
        public List<string> ExpectedOriginalLines { get; set; } = new List<string>();

        [JsonProperty("newLines")]
        public List<string> NewLines { get; set; } = new List<string>();
    }

    #endregion
}
