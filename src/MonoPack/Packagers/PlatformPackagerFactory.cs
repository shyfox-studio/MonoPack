namespace MonoPack.Packagers;

/// <summary>Factory for creating platform-specific packaging strategies.</summary>
internal static class PlatformPackagerFactory
{
    /// <summary>Creates a platform-specific packaging strategy.</summary>
    /// <param name="rid">Runtime identifier for the target platform.</param>
    /// <param name="infoPlistPath">Path to Info.plist (for macOS)/</param>
    /// <param name="icnsPath">Path to .icns icon (for macOS).</param>
    /// <returns>An instance of <see cref="IPlatformPackager"/> for the specified platform.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="rid"/> starts with <c>osx</c> and <paramref name="infoPlistPath"/> is
    /// <see langword="null"/> or <paramref name="icnsPath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="rid"/> is not a supported runtime identifier.
    /// </exception>
    public static IPlatformPackager CreatePackager(string rid, string? infoPlistPath = null, string? icnsPath = null)
    {
        return rid switch
        {
            string s when s.StartsWith("win", StringComparison.InvariantCultureIgnoreCase) => new WindowsPackager(),
            string s when s.StartsWith("linux", StringComparison.InvariantCultureIgnoreCase) => new LinuxPackager(),
            string s when s.StartsWith("osx", StringComparison.InvariantCultureIgnoreCase) => new MacOSPackager(
                infoPlistPath ?? throw new ArgumentNullException(nameof(infoPlistPath), "Info.plist path is required for macOS packaging"),
                icnsPath ?? throw new ArgumentNullException(nameof(icnsPath), "ICNS path is required for macOS packaging")
            ),
            _ => throw new NotSupportedException($"Unsupported runtime identifier: {rid}")
        };
    }
}
