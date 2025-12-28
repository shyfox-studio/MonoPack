using System.Formats.Tar;
using System.IO.Compression;

namespace MonoPack.Packagers;

/// <summary>Base implementation for platform-specific packing strategies</summary>
internal abstract class PlatformPackager : IPlatformPackager
{

    // rwxr-xr-x (0755)
    private const UnixFileMode DefaultDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                                      UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                                      UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    /// <inheritdoc/>
    public abstract void Package(string sourceDir, string outputDir, string projectName, string? executableName, string rid, bool useZip);

    protected static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        string[] files = Directory.GetFiles(sourceDir);

        for (int i = 0; i < files.Length; i++)
        {
            string filePath = files[i];
            string fileName = Path.GetFileName(filePath);
            string destPath = Path.Combine(targetDir, fileName);
            File.Copy(filePath, destPath, overwrite: true);
        }

        string[] directories = Directory.GetDirectories(sourceDir);
        for (int i = 0; i < directories.Length; i++)
        {
            string dirPath = directories[i];
            string dirName = Path.GetFileName(dirPath);
            string destPath = Path.Combine(targetDir, dirName);
            CopyDirectory(dirPath, destPath);
        }
    }

    protected static void DeleteDirectory(string targetDir, bool recursive)
    {
        if (!Directory.Exists(targetDir))
        {
            return;
        }

        const int MaxRetries = 3;
        const int RetryDelayMs = 100;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                Directory.Delete(targetDir, recursive);
                return;
            }
            catch (IOException ex)
            {
                if (attempt == MaxRetries)
                {
                    // Final attempt failed, so we give up
                    throw new InvalidOperationException(
                        $"Failed to delete directory '{targetDir}' after {MaxRetries + 1} attempts. " +
                        "Files may be in use by another process.", ex
                    );
                }

                // Wait and retry
                Thread.Sleep(RetryDelayMs);
            }
        }
    }

    protected static void ZipDirectory(string sourceDir, bool includeBaseDirectory, ZipArchive archive, string? executableName, string projectName, string? archivePathToSkip = null)
    {
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        if (includeBaseDirectory)
        {
            sourceDir = Path.Combine(sourceDir, "..");
        }

        string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string filePath = files[i];

            if (archivePathToSkip != null &&
               Path.GetFullPath(filePath).Equals(Path.GetFullPath(archivePathToSkip), StringComparison.Ordinal))
            {
                continue;
            }


            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
            {
                // Skip hidden files
                continue;
            }

            string entryName = Path.GetRelativePath(sourceDir, filePath);
            ZipArchiveEntry entry = archive.CreateEntryFromFile(filePath, entryName);

            UnixFileMode permissions;
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                permissions = File.GetUnixFileMode(filePath);
            }
            else
            {
                // Default windows permissions
                permissions = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
            }

            string fileName = Path.GetFileName(filePath);
            if (fileName.Equals(executableName, StringComparison.Ordinal) || fileName.Equals(projectName, StringComparison.Ordinal))
            {
                permissions |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            }

            // Need to add S_IFREG (regular file) so that the permissions
            // are preserved when building on windows for mac/linux
            int unixFileMode = (int)permissions | 0x8000;
            entry.ExternalAttributes = unixFileMode << 16;
        }
    }

    protected static void TarDirectory(string sourceDir, bool includeBaseDirectory, GZipStream gzStream, string? executableName, string projectName, string? archivePathToSkip = null)
    {
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        // Pax supports Unix permissions
        using TarWriter writer = new TarWriter(gzStream, TarEntryFormat.Pax);

        if (includeBaseDirectory)
        {
            sourceDir = Path.Combine(sourceDir, "..");
        }

        string[] directories = Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories);
        for (int i = 0; i < directories.Length; i++)
        {
            string dirPath = directories[i];

            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            if (dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
            {
                // Skip hidden directories
                continue;
            }

            // Enforce `/` directory separator.
            // Zip is more forgiving about this, but tar is not
            string entryName = Path.GetRelativePath(sourceDir, dirPath)
                                   .Replace(Path.DirectorySeparatorChar, '/') + '/';

            UnixFileMode dirMode;

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                dirMode = File.GetUnixFileMode(dirPath);
            }
            else
            {

                dirMode = DefaultDirectoryMode;
            }

            PaxTarEntry dirEntry = new PaxTarEntry(TarEntryType.Directory, entryName);
            dirEntry.Mode = dirMode;
            writer.WriteEntry(dirEntry);
        }

        string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string filePath = files[i];

            if (archivePathToSkip != null &&
                Path.GetFullPath(filePath).Equals(Path.GetFullPath(archivePathToSkip), StringComparison.Ordinal))
            {
                continue;
            }

            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
            {
                // Skip hidden files
                continue;
            }

            // Enforce `/` directory separator.
            // Zip is more forgiving about this, but tar is not
            string entryName = Path.GetRelativePath(sourceDir, filePath)
                                   .Replace(Path.DirectorySeparatorChar, '/');

            PaxTarEntry entry = new PaxTarEntry(TarEntryType.RegularFile, entryName);

            UnixFileMode permissions;
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                permissions = File.GetUnixFileMode(filePath);
            }
            else
            {
                // Default windows permissions
                permissions = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
            }

            string fileName = Path.GetFileName(filePath);
            if (fileName.Equals(executableName, StringComparison.Ordinal) || fileName.Equals(projectName, StringComparison.Ordinal))
            {
                permissions |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            }

            entry.Mode = permissions;

            using FileStream fileStream = File.OpenRead(filePath);
            entry.DataStream = fileStream;
            writer.WriteEntry(entry);
        }
    }

    protected static void EnsureArchiveAccessible(string archivePath)
    {
        const int maxAttempts = 50;
        const int delayMs = 10;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                // Try to open with exclusive access
                using (FileStream testStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return;
                }
            }
            catch (IOException)
            {
                // Still locked, wait a bit
                if (attempt < maxAttempts - 1)
                {
                    Thread.Sleep(delayMs);
                }
            }
        }

        // If we get here, file is still locked after 500ms
        throw new InvalidOperationException($"Archive file is still locked after creation: {archivePath}");
    }
}
