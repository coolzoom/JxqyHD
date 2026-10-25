using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace XNAConverter
{
    /// <summary>
    /// Cross-platform replacement for the XNA Content Pipeline builder.
    /// Wraps the MonoGame Content Builder (mgcb) dotnet tool to produce .xnb
    /// files. Usage:
    ///   dotnet XNABuilder.dll -o OUTDIR [-p Platform] [-c] [-i file1 file2 ...]
    ///   dotnet XNABuilder.dll -o OUTDIR -d SRCDIR        (all supported files)
    /// Platform defaults to DesktopGL; -c enables compression.
    /// </summary>
    static class Program
    {
        private static readonly HashSet<string> SupportedExts = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".dds", ".jpg", ".png", ".tga", ".fbx", ".fx",
            ".spritefont", ".x", ".xml", ".mp3", ".wav", ".ogg", ".ttf"
        };

        /// <summary>
        /// dotnet-mgcb is a repo-local tool: the command must run from a
        /// directory under the manifest (.config/dotnet-tools.json). Find it
        /// by walking up from this assembly's location.
        /// </summary>
        private static string FindToolRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, ".config", "dotnet-tools.json")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }

        static int Main(string[] args)
        {
            string outDir = null, platform = "DesktopGL", srcDir = null;
            var compress = false;
            var files = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-o": outDir = args[++i]; break;
                    case "-p": platform = args[++i]; break;
                    case "-c": compress = true; break;
                    case "-d": srcDir = args[++i]; break;
                    case "-i":
                        while (++i < args.Length && !args[i].StartsWith("-"))
                            files.Add(args[i]);
                        i--;
                        break;
                }
            }

            if (srcDir != null)
                files.AddRange(Directory.GetFiles(srcDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => SupportedExts.Contains(Path.GetExtension(f))));

            if (outDir == null || files.Count == 0)
            {
                Console.WriteLine("Usage: XNABuilder -o OUTDIR [-p Platform] [-c] (-i files | -d srcdir)");
                return 1;
            }

            Directory.CreateDirectory(outDir);
            var toolRoot = FindToolRoot();
            var failed = 0;
            foreach (var file in files)
            {
                var workDir = Path.GetDirectoryName(Path.GetFullPath(file));
                var psi = new ProcessStartInfo("dotnet",
                    "mgcb --quiet " +
                    $"--workingDir:\"{workDir}\" " +
                    $"--outputDir:\"{Path.GetFullPath(outDir)}\" " +
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
                var ok = p.ExitCode == 0;
                Console.WriteLine((ok ? "OK   " : "FAIL ") + file);
                if (!ok)
                {
                    failed++;
                    if (output.Trim().Length > 0)
                        Console.WriteLine("       " + output.Trim().Replace("\n", "\n       "));
                }
            }
            Console.WriteLine($"Done: {files.Count - failed}/{files.Count} built.");
            return failed == 0 ? 0 : 2;
        }
    }
}
