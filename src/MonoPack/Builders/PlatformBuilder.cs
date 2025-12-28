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
    public void Build(string projectPath, string outputDir, string rid, string? executableName, bool verbose)
    {
        Console.WriteLine($"Building {rid}");

        string arguments = $"publish \"{projectPath}\" -c Release -r {rid} " +
                            "-p:PublishReadyToRun=false " +
                            "-p:TieredCompilation=false " +
                            "-p:PublishSingleFile=true " +
                            "--self-contained ";


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
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Build failed for {rid}");
        }

        // Platform-specific post build actions
        PostBuildActions(outputDir, rid, verbose);
    }

    /// <summary>Performs platform-specific post-build actions.</summary>
    /// <param name="outputDir">Directory containing build artifacts.</param>
    /// <param name="rid">Runtime identifier of the build.</param>
    /// <param name="verbose">Indicates whether to display verbose output.</param>
    protected abstract void PostBuildActions(string outputDir, string rid, bool verbose);
}
