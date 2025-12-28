namespace MonoPack.Builders;

/// <summary>Provides strategies for building applications for different platforms.</summary>
internal interface IPlatformBuilder
{
    /// <summary>Builds the application for a specific runtime identifier.</summary>
    /// <param name="projectPath">Path to the project file.</param>
    /// <param name="outputDir">Directory to place build artifacts.</param>
    /// <param name="rid">Runtime identifier for the target platform.</param>
    /// <param name="executableName">Optional custom name for the executable.</param>
    /// <param name="verbose">Indicates whether to display verbose output.</param>
    void Build(string projectPath, string outputDir, string rid, string? executableName, bool verbose);
}
