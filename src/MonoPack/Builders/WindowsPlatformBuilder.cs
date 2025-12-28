namespace MonoPack.Builders;

/// <summary>Build strategy for Windows platform.</summary>
internal sealed class WindowsPlatformBuilder : PlatformBuilder
{
    protected override void PostBuildActions(string outputDir, string rid, bool verbose)
    {
        // No specific post-build actions for Windows
    }
}
