using System.IO.Compression;

namespace MonoPack.Packagers;

/// <summary>Packaging strategy for Windows platform.</summary>
internal sealed class WindowsPackager : PlatformPackager
{
    /// <inheritdoc/>
    public override void Package(string sourceDir, string outputDir, string projectName, string? executableName, string rid, bool useZip)
    {
        string archivePath = Path.Combine(outputDir, $"{executableName ?? projectName}-{rid}.zip");

        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using FileStream fileStream = File.OpenWrite(archivePath);

        // Zip only for windows
        using ZipArchive zip = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: true);
        ZipDirectory(sourceDir, false, zip, executableName, projectName);

        DeleteDirectory(sourceDir, recursive: true);
        Console.WriteLine($"Created Windows archive: {archivePath}");
    }
}
