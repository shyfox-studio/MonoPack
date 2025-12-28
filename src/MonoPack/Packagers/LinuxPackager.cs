using System.IO.Compression;

namespace MonoPack.Packagers;

/// <summary>Packaging strategy for linux platform.</summary>
internal sealed class LinuxPackager : PlatformPackager
{
    /// <inheritdoc/>
    public override void Package(string sourceDir, string outputDir, string projectName, string? executableName, string rid, bool useZip)
    {
        string archivePath = Path.Combine(outputDir, $"{executableName ?? projectName}-{rid}.{(useZip ? "zip" : "tar.gz")}");

        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using FileStream fileStream = File.OpenWrite(archivePath);

        if (useZip)
        {
            using ZipArchive zip = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: true);
            ZipDirectory(sourceDir, false, zip, executableName, projectName);
        }
        else
        {
            using GZipStream gzStream = new GZipStream(fileStream, CompressionMode.Compress, leaveOpen: true);
            TarDirectory(sourceDir, false, gzStream, executableName, projectName);
        }

        DeleteDirectory(sourceDir, recursive: true);
        Console.WriteLine($"Created Linux archive: {archivePath}");
    }
}
