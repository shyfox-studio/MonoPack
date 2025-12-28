namespace MonoPack.Packagers;

/// <summary>Defines contract for platform-specific packaging strategies.</summary>
internal interface IPlatformPackager
{
    /// <summary>Packages the build application for a specific platform.</summary>
    /// <param name="sourceDir">Directory containing build artifacts.</param>
    /// <param name="outputDir">Directory to place the packaged application.</param>
    /// <param name="projectName">Name of the project being packaged.</param>
    /// <param name="executableName">Name of the executable file.</param>
    /// <param name="rid">Runtime identifier for the platform.</param>
    /// <param name="useZip">Indicates whether to use zip compression.</param>
    public abstract void Package(string sourceDir, string outputDir, string projectName, string? executableName, string rid, bool useZip);
}
