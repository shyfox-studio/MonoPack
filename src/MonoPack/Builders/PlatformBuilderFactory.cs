namespace MonoPack.Builders;

/// <summary>Factory for creating platform-specific build strategies.</summary>
internal static class PlatformBuilderFactory
{
    /// <summary>Creates a platform-specific build strategy.</summary>
    /// <param name="rid">Runtime identifier for the target platform.</param>
    /// <returns>An instance of <see cref="IPlatformBuilder"/> for the specified platform.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="rid"/> is not a supported runtime identifier.
    /// </exception>
    public static IPlatformBuilder CreateBuilder(string rid)
    {
        return rid switch
        {
            string s when s.StartsWith("win", StringComparison.InvariantCultureIgnoreCase) => new WindowsPlatformBuilder(),
            string s when s.StartsWith("linux", StringComparison.InvariantCultureIgnoreCase) => new LinuxPlatformBuilder(),
            string s when s.StartsWith("osx", StringComparison.InvariantCultureIgnoreCase) => new MacOSPlatformBuilder(),
            _ => throw new NotSupportedException($"Unsupported runtime identifier: {rid}")
        };
    }
}
