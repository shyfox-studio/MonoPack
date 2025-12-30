using System.Diagnostics;

namespace MonoPack.Builders;

/// <summary>Base implementation for platform-specific build strategies</summary>
internal abstract class PlatformBuilder : IPlatformBuilder
{
    /// <summary>Builds the application using dotnet publish.</summary>
    /// <param name="projectPath">Path to the project file.</param>
    /// <param name="outputDir">Directory to place build artifacts.</param>
    /// <param name="rid">Runtime identifier for the target platform.</param>
    /// <param name="executableName">Optional custom name for the executable.</param>
    /// <param name="verbose">Indicates whether to display verbose output.</param>
    /// <param name="publishArgs">Custom arguments to pass to dotnet publish. When specified, default flags are not applied.</param>
    public void Build(string projectPath, string outputDir, string rid, string? executableName, bool verbose, string? publishArgs)
    {
        Console.WriteLine($"Building {rid}");

        string arguments = $"publish \"{projectPath}\" -c Release -r {rid} --self-contained ";

        if(!string.IsNullOrEmpty(publishArgs))
        {
            arguments += $"{publishArgs} ";
        }

        // Use custom assembly name if executable name was given
        if(!string.IsNullOrEmpty(executableName))
        {
            arguments += $"-p:AssemblyName=\"{executableName}\" ";
        }

        arguments += $"-o \"{outputDir}\"";

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = new() { StartInfo = startInfo };

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data is not null && verbose)
            {
                Console.WriteLine(args.Data);
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data is not null && verbose)
            {
                Console.Error.WriteLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Build failed for {rid}");
        }

        // // Rename executable if custom name was specified
        // if (!string.IsNullOrEmpty(executableName))
        // {
        //     RenameExecutable(projectPath, outputDir, rid, executableName, verbose);
        // }

        // Platform-specific post build actions
        PostBuildActions(outputDir, rid, verbose);
    }

    private static void RenameExecutable(string projectPath, string outputDir, string rid, string executableName, bool verbose)
    {
        string projectName = Path.GetFileNameWithoutExtension(projectPath);
        string extension = rid.StartsWith("win", StringComparison.OrdinalIgnoreCase) ? ".exe" : string.Empty;

        string oldExecutablePath = Path.Combine(outputDir, $"{projectName}{extension}");
        string newExecutablePath = Path.Combine(outputDir, $"{executableName}{extension}");

        if (File.Exists(oldExecutablePath) && !oldExecutablePath.Equals(newExecutablePath, StringComparison.Ordinal))
        {
            if (File.Exists(newExecutablePath))
            {
                File.Delete(newExecutablePath);
            }

            File.Move(oldExecutablePath, newExecutablePath);

            if (verbose)
            {
                Console.WriteLine($"Renamed executable: {projectName}{extension} -> {executableName}{extension}");
            }
        }
    }

    /// <summary>Performs platform-specific post-build actions.</summary>
    /// <param name="outputDir">Directory containing build artifacts.</param>
    /// <param name="rid">Runtime identifier of the build.</param>
    /// <param name="verbose">Indicates whether to display verbose output.</param>
    protected abstract void PostBuildActions(string outputDir, string rid, bool verbose);
}
