// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: a3652352a1255299185f2493953a29896f9404fcb215d603964723548022a739
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Interfaces;
using LagoVista.AI.Rag.ContractPacks.Ingestion.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Rag.ContractPacks.Ingestion.Services
{

    public class GitRepoInspector : IGitRepoInspector
    {
        private readonly IAdminLogger _adminLogger;
        public GitRepoInspector(IAdminLogger adminLogger)
        {
            _adminLogger = adminLogger;
        }

        /// <summary>
        /// Try to read repo info from a working directory (or any subdir under it).
        /// Returns true on success; false and error message on failure.
        /// </summary>
        public InvokeResult<GitRepoInfo> GetRepoInfo(string workingDirectory)
        {

            if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            {

                return InvokeResult<GitRepoInfo>.FromError("Working directory not found.");
            }

            try
            {
                // 1) Find the .git directory (supports file indirection for worktrees/submodules)
                var repoRoot = FindRepoRoot(workingDirectory, out var gitDirPath);
                if (repoRoot == null || gitDirPath == null)
                {
                    return InvokeResult<GitRepoInfo>.FromError($"Not inside a Git repository - {workingDirectory}.");
                }

                // 2) Attempt git.exe fast-path first (if present) for maximum compatibility
                if (TryGitCli(repoRoot, out var urlCli, out var shaCli, out var branchCli))
                {
                    return InvokeResult<GitRepoInfo>.Create(new GitRepoInfo
                    {
                        RepositoryRoot = repoRoot,
                        RemoteUrl = urlCli,
                        CommitSha = shaCli,
                        BranchRef = branchCli
                    });
                }

                // 3) Fallback: read from files in .git
                var headRefOrSha = ReadFirstLine(Path.Combine(gitDirPath, "HEAD"))?.Trim();
                if (string.IsNullOrWhiteSpace(headRefOrSha))
                {
                    return InvokeResult<GitRepoInfo>.FromError("Could not read HEAD.");
                }

                string branchRef = null;
                string commitSha;

                if (headRefOrSha.StartsWith("ref: ", StringComparison.OrdinalIgnoreCase))
                {
                    // On a branch
                    branchRef = headRefOrSha.Substring(5).Trim(); // e.g., refs/heads/main
                    commitSha = ResolveRefToSha(gitDirPath, branchRef);

                    return InvokeResult<GitRepoInfo>.FromError($"Could not resolve ref '{branchRef}' to a commit.");
                }
                else
                {
                    // Detached HEAD -> HEAD contains the commit SHA directly
                    commitSha = headRefOrSha;
                }

                // Remote URL from config: prefer remote "origin", else first remote url
                string remoteUrl = ReadRemoteUrlFromConfig(Path.Combine(gitDirPath, "config"));

                return InvokeResult<GitRepoInfo>.Create(new GitRepoInfo
                {
                    RepositoryRoot = repoRoot,
                    RemoteUrl = remoteUrl,
                    CommitSha = commitSha,
                    BranchRef = branchRef
                });
            }
            catch (Exception ex)
            {
                return InvokeResult<GitRepoInfo>.FromException("[GitRepoInspector_GetRepoInfo]", ex);
            }
        }

        // -------- helpers --------

        private static string FindRepoRoot(string startDir, out string gitDirPath)
        {
            gitDirPath = null;
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                var gitPath = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(gitPath))
                {
                    gitDirPath = gitPath;
                    return dir.FullName;
                }
                if (File.Exists(gitPath))
                {
                    // .git is a file that points to the actual gitdir
                    var text = File.ReadAllText(gitPath).Trim();
                    const string prefix = "gitdir:";
                    if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var rel = text.Substring(prefix.Length).Trim();
                        var resolved = ResolveGitdirPointer(dir.FullName, rel);
                        if (Directory.Exists(resolved))
                        {
                            gitDirPath = resolved;
                            return dir.FullName;
                        }
                    }
                }
                dir = dir.Parent;
            }
            return null;
        }

        private static string ResolveGitdirPointer(string repoRoot, string pointer)
        {
            // pointer can be relative to repoRoot or absolute
            if (Path.IsPathRooted(pointer)) return pointer;
            return Path.GetFullPath(Path.Combine(repoRoot, pointer));
        }

        private static string ReadFirstLine(string path)
        {
            if (!File.Exists(path)) return null;
            using (var sr = new StreamReader(path, Encoding.UTF8, true))
                return sr.ReadLine();
        }

        private static string ResolveRefToSha(string gitDir, string refName)
        {
            // 1) Loose ref file
            var refPath = Path.Combine(gitDir, refName.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(refPath))
            {
                var sha = ReadFirstLine(refPath)?.Trim();
                if (LooksLikeSha(sha)) return sha;
            }

            // 2) packed-refs
            var packed = Path.Combine(gitDir, "packed-refs");
            if (File.Exists(packed))
            {
                foreach (var line in File.ReadLines(packed))
                {
                    var ln = line.Trim();
                    if (ln.Length == 0 || ln[0] == '#') continue;
                    if (ln[0] == '^') continue; // peeled entries
                    var parts = ln.Split(new[] { ' ' }, 2);
                    if (parts.Length == 2 && parts[1] == refName && LooksLikeSha(parts[0]))
                        return parts[0];
                }
            }
            return null;
        }

        private static string ReadRemoteUrlFromConfig(string configPath)
        {
            if (!File.Exists(configPath)) return null;

            string currentSection = null;
            string firstRemoteUrl = null;
            foreach (var raw in File.ReadLines(configPath))
            {
                var line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim(); // e.g., remote "origin"
                    continue;
                }

                if (string.IsNullOrEmpty(currentSection)) continue;

                // Only remote sections
                if (currentSection.StartsWith("remote ", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract name between quotes if present
                    var isOrigin = currentSection.IndexOf("\"origin\"", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (line.StartsWith("url", StringComparison.OrdinalIgnoreCase))
                    {
                        var idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            var url = line.Substring(idx + 1).Trim();
                            if (isOrigin) return url;
                            if (firstRemoteUrl == null) firstRemoteUrl = url;
                        }
                    }
                }
            }
            return firstRemoteUrl;
        }

        private static bool LooksLikeSha(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || s.Length < 7 || s.Length > 64) return false;
            // Git SHAs are 40 hex chars; allow short/long in practice
            foreach (var ch in s)
            {
                bool hex = (ch >= '0' && ch <= '9') ||
                           (ch >= 'a' && ch <= 'f') ||
                           (ch >= 'A' && ch <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        /// <summary>
        /// If git.exe is available, use it to fetch remote URL and HEAD sha reliably.
        /// Falls back to file parsing if git is missing or fails.
        /// </summary>
        private static bool TryGitCli(string repoRoot, out string remoteUrl, out string headSha, out string branchRef)
        {
            remoteUrl = null;
            headSha = null;
            branchRef = null;

            try
            {
                string Run(string args)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = args,
                        WorkingDirectory = repoRoot,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        var output = p.StandardOutput.ReadToEnd().Trim();
                        p.WaitForExit(4000);
                        if (p.ExitCode != 0) return null;
                        return output;
                    }
                }

                // remote URL (origin), then fallback to any remote
                remoteUrl = Run("config --get remote.origin.url");
                if (string.IsNullOrWhiteSpace(remoteUrl))
                    remoteUrl = Run("remote get-url --all origin") ?? Run("remote -v")?.Split('\n').FirstOrDefault()?.Split('\t').ElementAtOrDefault(1)?.Split(' ')?.FirstOrDefault();

                headSha = Run("rev-parse HEAD");
                var branchName = Run("symbolic-ref --quiet --short HEAD"); // e.g., "main"
                if (!string.IsNullOrWhiteSpace(branchName))
                    branchRef = "refs/heads/" + branchName;

                return !string.IsNullOrWhiteSpace(headSha);
            }
            catch
            {
                return false;
            }
        }
    }
}
