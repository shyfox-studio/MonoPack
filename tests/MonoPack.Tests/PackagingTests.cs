using System.Formats.Tar;
using System.IO.Compression;

namespace MonoPack.Tests;

public sealed class PackagingTests : TestBase
{
    public PackagingTests() : base() { }

    private static void AssertStandardFilePermissions(UnixFileMode permissions, string fileName)
    {
        Assert.True(permissions > 0, $"{fileName}: Permissions should not be zero");
        Assert.True(permissions.HasFlag(UnixFileMode.UserRead), $"{fileName}: Missing {nameof(UnixFileMode.UserRead)}");
        Assert.True(permissions.HasFlag(UnixFileMode.UserWrite), $"{fileName}: Missing {nameof(UnixFileMode.UserWrite)}");
        Assert.True(permissions.HasFlag(UnixFileMode.GroupRead), $"{fileName}: Missing {nameof(UnixFileMode.GroupRead)}");
        Assert.False(permissions.HasFlag(UnixFileMode.GroupWrite), $"{fileName}: Should not have {nameof(UnixFileMode.GroupWrite)}");
        Assert.True(permissions.HasFlag(UnixFileMode.OtherRead), $"{fileName}: Missing {nameof(UnixFileMode.OtherRead)}");
        Assert.False(permissions.HasFlag(UnixFileMode.OtherWrite), $"{fileName}: Should not have {nameof(UnixFileMode.OtherWrite)}");
    }

    private static void AssertExecutablePermissions(UnixFileMode permissions, string fileName)
    {
        AssertStandardFilePermissions(permissions, fileName);

        Assert.True(permissions.HasFlag(UnixFileMode.UserExecute), $"{fileName}: Missing {nameof(UnixFileMode.UserExecute)}, was {permissions.PrintFlags()}");
        Assert.True(permissions.HasFlag(UnixFileMode.GroupExecute), $"{fileName}: Missing {nameof(UnixFileMode.GroupExecute)}, was {permissions.PrintFlags()}");
        Assert.True(permissions.HasFlag(UnixFileMode.OtherExecute), $"{fileName}: Missing {nameof(UnixFileMode.OtherExecute)}, was {permissions.PrintFlags()}");
    }

    private static void AssertDirectoryPermissions(UnixFileMode permissions, string dirName)
    {
        Assert.True(permissions > 0, $"{dirName}: Permissions should not be zero");
        Assert.True(permissions.HasFlag(UnixFileMode.UserRead), $"{dirName}: Missing {nameof(UnixFileMode.UserRead)}");
        Assert.True(permissions.HasFlag(UnixFileMode.UserWrite), $"{dirName}: Missing {nameof(UnixFileMode.UserWrite)}");
        Assert.True(permissions.HasFlag(UnixFileMode.UserExecute), $"{dirName}: Missing {nameof(UnixFileMode.UserExecute)}");
        Assert.True(permissions.HasFlag(UnixFileMode.GroupRead), $"{dirName}: Missing {nameof(UnixFileMode.GroupRead)}");
        Assert.True(permissions.HasFlag(UnixFileMode.GroupExecute), $"{dirName}: Missing {nameof(UnixFileMode.GroupExecute)}");
        Assert.True(permissions.HasFlag(UnixFileMode.OtherRead), $"{dirName}: Missing {nameof(UnixFileMode.OtherRead)}");
        Assert.True(permissions.HasFlag(UnixFileMode.OtherExecute), $"{dirName}: Missing {nameof(UnixFileMode.OtherExecute)}");
    }

    [Theory]
    [InlineData("linux-x64")]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public void ZipPackage_ShouldHaveCorrectFilePermissions(string rid)
    {
        Options options = CreateOptions(rid, useZip: true);
        MonoPackService service = new MonoPackService(options);

        service.Execute();

        string zipPath = Path.Combine(options.OutputDirectory, $"{ProjectName}-{rid}.zip");
        Assert.True(File.Exists(zipPath), $"Zip file not found at : {zipPath}");

        using ZipArchive zipArchive = ZipFile.OpenRead(zipPath);
        Assert.NotEmpty(zipArchive.Entries);

        foreach (ZipArchiveEntry entry in zipArchive.Entries)
        {
            // Skip directories (they have trailing slashes and zero length)
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            UnixFileMode permissions = (UnixFileMode)((entry.ExternalAttributes >> 16) & 0x1FF);
            string fileName = Path.GetFileName(entry.Name);

            if (fileName == ProjectName)
            {
                AssertExecutablePermissions(permissions, entry.FullName);
            }
            else
            {
                AssertStandardFilePermissions(permissions, entry.FullName);
            }

            Assert.True(entry.Length > 0, $"{entry.FullName} should not be empty");
        }
    }

    [Theory]
    [InlineData("linux-x64")]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public void TarGzPackage_ShouldHaveCorrectPermissions(string rid)
    {
        Options options = CreateOptions(rid, useZip: false);
        MonoPackService service = new MonoPackService(options);

        service.Execute();

        string tarPath = Path.Combine(options.OutputDirectory, $"{ProjectName}-{rid}.tar.gz");
        Assert.True(File.Exists(tarPath), $"Tar.gz file not found at: {tarPath}");

        using FileStream fileStream = File.OpenRead(tarPath);
        using GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using TarReader tarReader = new TarReader(gzipStream);

        bool foundEntries = false;
        TarEntry? entry;

        while ((entry = tarReader.GetNextEntry()) != null)
        {
            foundEntries = true;
            UnixFileMode permissions = entry.Mode;

            if (entry.EntryType == TarEntryType.Directory)
            {
                AssertDirectoryPermissions(permissions, entry.Name);
            }
            else if (entry.EntryType == TarEntryType.RegularFile)
            {
                string fileName = Path.GetFileName(entry.Name);

                if (fileName == ProjectName)
                {
                    AssertExecutablePermissions(permissions, entry.Name);
                }
                else
                {
                    AssertStandardFilePermissions(permissions, entry.Name);
                }
            }
        }

        Assert.True(foundEntries, "Tar archive should contain entries");
    }

    [Theory]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public void MacOSPackage_ShouldContainAppBundle(string rid)
    {
        Options options = CreateOptions(rid, useZip: true);
        MonoPackService service = new MonoPackService(options);

        service.Execute();

        string zipPath = Path.Combine(options.OutputDirectory, $"{ProjectName}-{rid}.zip");
        using ZipArchive zipArchive = ZipFile.OpenRead(zipPath);

        // Verify app bundle structure
        string appBundleDir = $"{ProjectName}.app";
        bool allEntriesInAppDirectory = zipArchive.Entries.All(e => e.FullName.StartsWith(appBundleDir, StringComparison.Ordinal));
        Assert.True(allEntriesInAppDirectory, "Root entry should be .app bundle only");

        string contentsDir = $"{appBundleDir}{Path.DirectorySeparatorChar}Contents{Path.DirectorySeparatorChar}";
        bool hasContents = zipArchive.Entries.Any(e => e.FullName.StartsWith(contentsDir, StringComparison.Ordinal));
        Assert.True(hasContents, "Should contain Contents directory");

        string macOSDir = $"{contentsDir}MacOS{Path.DirectorySeparatorChar}";
        bool hasMacOS = zipArchive.Entries.Any(e => e.FullName.StartsWith(macOSDir, StringComparison.Ordinal));
        Assert.True(hasMacOS, "Should contain Contents/MacOS directory");

        string resourcesDir = $"{contentsDir}Resources{Path.DirectorySeparatorChar}";
        bool hasResources = zipArchive.Entries.Any(e => e.FullName.StartsWith(resourcesDir, StringComparison.Ordinal));
        Assert.True(hasResources, "Should contain Contents/Resources directory");

        string infoPlistPath = $"{contentsDir}Info.plist";
        bool hasInfoPlist = zipArchive.Entries.Any(e => e.FullName.StartsWith(infoPlistPath, StringComparison.Ordinal));
        Assert.True(hasInfoPlist, "Should contain Contents/Info.plist");

        bool hasIcon = zipArchive.Entries.Any(e => e.FullName.Contains(".icns", StringComparison.Ordinal));
        Assert.True(hasIcon, "Should contain .icns icon file");
    }

    [Fact]
    public void WindowsPackage_ShouldCreateZipArchive()
    {
        Options options = CreateOptions("win-x64", useZip: true);
        MonoPackService service = new MonoPackService(options);

        service.Execute();

        string zipPath = Path.Combine(options.OutputDirectory, $"{ProjectName}-win-x64.zip");
        Assert.True(File.Exists(zipPath), $"Zip file not found at: {zipPath}");

        using ZipArchive zipArchive = ZipFile.OpenRead(zipPath);
        Assert.NotEmpty(zipArchive.Entries);

        bool hasExecutable = zipArchive.Entries.Any(e =>
            Path.GetFileNameWithoutExtension(e.Name).Equals(ProjectName, StringComparison.OrdinalIgnoreCase) &&
            Path.GetExtension(e.Name).Equals(".exe", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasExecutable, "Should contain the .exe file");
    }

    [Fact]
    public void MultipleRuntimeIdentifiers_ShouldCreateMultiplePackages()
    {
        string outputDir = Path.Combine(_testOutputRoot, "multi-rid");

        Options options = new Options
        {
            ProjectPath = Path.Combine(_exampleProjectPath, $"{ProjectName}.csproj"),
            OutputDirectory = outputDir,
            ExecutableFileName = ProjectName,
            UseZipCompression = true,
            VerboseOutput = false
        };

        options.RuntimeIdentifiers.Add("win-x64");
        options.RuntimeIdentifiers.Add("linux-x64");

        MonoPackService service = new MonoPackService(options);

        service.Execute();

        string windowsZip = Path.Combine(outputDir, $"{ProjectName}-win-x64.zip");
        string linuxZip = Path.Combine(outputDir, $"{ProjectName}-linux-x64.zip");

        Assert.True(File.Exists(windowsZip), "Windows package should exist");
        Assert.True(File.Exists(linuxZip), "Linux package should exist");
    }

    [Fact]
    public void CustomExecutableName_ShouldAppearInPackage()
    {
        // Arrange
        string customName = "CustomGameName";
        Options options = new Options
        {
            ProjectPath = Path.Combine(_exampleProjectPath, $"{ProjectName}.csproj"),
            OutputDirectory = Path.Combine(_testOutputRoot, "custom-name"),
            ExecutableFileName = customName,
            UseZipCompression = true,
            VerboseOutput = false
        };

        options.RuntimeIdentifiers.Add("win-x64");

        MonoPackService service = new MonoPackService(options);

        // Act
        service.Execute();

        // Assert
        string zipPath = Path.Combine(options.OutputDirectory, $"{customName}-win-x64.zip");
        Assert.

        True(File.Exists(zipPath), "Archive should be named with custom executable name");
    }

}
