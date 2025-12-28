using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MonoPack.Builders;

/// <summary>Build strategy for macOS platform.</summary>
internal sealed class MacOSPlatformBuilder : PlatformBuilder
{
    protected override void PostBuildActions(string outputDir, string rid, bool verbose)
    {
        // TODO: If we're setting executable permissions during the tar/zip
        //       packaging, do we need to do it at the file level here?

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Executable permissions will be set during tar/zip packaging
            // using UnixFileMode attributes, which works cross-platform.
            return;
        }

        string[] files = Directory.GetFiles(outputDir);

        foreach (string file in files)
        {
            // Skip library files and pdbs
            if (file.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) ||
               file.EndsWith(".pdb", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "chmod",
                Arguments = $"+x \"{file}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process chmodProcess = new Process() { StartInfo = startInfo };

            chmodProcess.OutputDataReceived += (sender, args) =>
            {
                if (args.Data is not null && verbose)
                {
                    Console.WriteLine(args.Data);
                }
            };

            chmodProcess.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data is not null && verbose)
                {
                    Console.Error.WriteLine(args.Data);
                }
            };


            chmodProcess.Start();
            chmodProcess.WaitForExit();

            if (chmodProcess.ExitCode != 0)
            {
                Console.WriteLine($"Warning: Failed to set executable permissions for {file}");
            }
        }
    }
}
