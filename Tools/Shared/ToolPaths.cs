using System;
using System.Diagnostics;
using System.IO;

namespace JxqyTools
{
    /// <summary>
    /// Locates the Jxqy build output and Data directory so the cross-platform
    /// tools work from any current directory. The game must be run with the
    /// Data folder as working directory.
    /// </summary>
    public static class ToolPaths
    {
        /// <summary>Data directory: ./Data under the repo root, or the cwd itself.</summary>
        public static string FindDataDir()
        {
            var dir = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(dir, "Jxqy.ini")))
                return dir;
            var root = FindRepoRoot();
            if (root != null)
            {
                var data = Path.Combine(root, "Data");
                if (Directory.Exists(data)) return data;
            }
            return dir;
        }

        /// <summary>Walks up from cwd looking for the Jxqy project output.</summary>
        public static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Jxqy.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Jxqy.sln")) ||
                    Directory.Exists(Path.Combine(dir.FullName, "Jxqy")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        public static string FindGameDll()
        {
            return FindBuiltDll("Jxqy", "Jxqy.dll");
        }

        public static string FindToolDll(string projectName)
        {
            return FindBuiltDll(projectName, projectName + ".dll");
        }

        private static string FindBuiltDll(string projectDir, string dllName)
        {
            var root = FindRepoRoot();
            if (root == null) return null;
            var dir = Path.Combine(root, projectDir, "bin");
            if (!Directory.Exists(dir)) return null;
            var found = Directory.GetFiles(dir, dllName, SearchOption.AllDirectories);
            Array.Sort(found, StringComparer.Ordinal);
            // Prefer Debug then Release, newest last wins on ordinal sort for
            // identical paths; pick the most recently written file instead.
            string best = null;
            var bestTime = DateTime.MinValue;
            foreach (var f in found)
            {
                var t = File.GetLastWriteTimeUtc(f);
                if (t > bestTime)
                {
                    bestTime = t;
                    best = f;
                }
            }
            return best;
        }

        /// <summary>Runs `dotnet &lt;dll&gt; [args]` with the Data dir as cwd.</summary>
        public static Process Launch(string dll, string args = "")
        {
            var psi = new ProcessStartInfo("dotnet", '"' + dll + '"' + (args.Length > 0 ? " " + args : ""))
            {
                WorkingDirectory = FindDataDir(),
                UseShellExecute = false
            };
            return Process.Start(psi);
        }

        /// <summary>
        /// Runs `dotnet mgcb` (repo-local tool) for one content file.
        /// Returns the combined output; exitCode is set on success.
        /// </summary>
        public static string RunMgcb(string file, string outputDir, string platform,
            bool compress, out int exitCode)
        {
            var toolRoot = FindRepoRoot() ?? Directory.GetCurrentDirectory();
            var workDir = Path.GetDirectoryName(Path.GetFullPath(file));
            var psi = new ProcessStartInfo("dotnet",
                "mgcb --quiet " +
                $"--workingDir:\"{workDir}\" " +
                $"--outputDir:\"{Path.GetFullPath(outputDir)}\" " +
                $"--platform:{platform} " +
                $"--compress:{compress} " +
                $"--build:\"{Path.GetFileName(file)}\"")
            {
                WorkingDirectory = toolRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            var p = Process.Start(psi);
            var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            exitCode = p.ExitCode;
            return output;
        }
    }
}
