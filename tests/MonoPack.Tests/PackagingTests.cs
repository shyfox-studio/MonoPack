using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.Serialization;

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
    [InlineData("linux-arm64")]
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
    [InlineData("linux-arm64")]
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

        service.Execute();

        string zipPath = Path.Combine(options.OutputDirectory, $"{customName}-win-x64.zip");
        Assert.True(File.Exists(zipPath), "Archive should be named with custom executable name");
    }

    [Fact]
    public void UniversalMacOS_ShouldCreateUniversalAppBundle()
    {
        string outputDir = Path.Combine(_testOutputRoot, "universal-macos");

        Options options = new Options
        {
            ProjectPath = Path.Combine(_exampleProjectPath, $"{ProjectName}.csproj"),
            OutputDirectory = outputDir,
            InfoPlistPath = Path.Combine(_exampleProjectPath, "Info.plist"),
            IcnsPath = Path.Combine(_exampleProjectPath, "Icon.icns"),
            UseZipCompression = true,
            VerboseOutput = true,
            MacOSUniversal = true
        };

        options.RuntimeIdentifiers.Add("osx-x64");
        options.RuntimeIdentifiers.Add("osx-arm64");

        MonoPackService service = new MonoPackService(options);

        service.Execute();

        string zipPath = Path.Combine(outputDir, $"{ProjectName}-universal.zip");
        Assert.True(File.Exists(zipPath), $"Universal archive not found at: {zipPath}");

        using ZipArchive zipArchive = ZipFile.OpenRead(zipPath);
        Assert.NotEmpty(zipArchive.Entries);

        // Verify .app bundle structure
        string appBundleDir = $"{ProjectName}.app";
        bool allEntriesInAppDirectory = zipArchive.Entries.All(e => e.FullName.StartsWith(appBundleDir, StringComparison.Ordinal));
        Assert.True(allEntriesInAppDirectory, "All entries should be within .app bundle");

        string macOsDir = Path.Combine(appBundleDir, "Contents", "MacOS");
        string amd64Dir = Path.Combine(macOsDir, "amd64");
        string arm64Dir = Path.Combine(macOsDir, "arm64");
        string executablePath = Path.Combine(macOsDir, ProjectName);

        if (OperatingSystem.IsMacOS())
        {
            // On macOS, lipo should create a true universal binary
            bool hasExecutable = zipArchive.Entries.Any(e => e.FullName.Equals($"{executablePath}", StringComparison.Ordinal));
            Assert.True(hasExecutable, $"Should contain universal executable at {executablePath}");

            // Should NOT have architecture subdirectories
            bool hasAmd64Dir = zipArchive.Entries.Any(e => e.FullName.Contains($"{amd64Dir}", StringComparison.Ordinal));
            Assert.False(hasAmd64Dir, "Should not have amd64 subdirectory (lip creates true universal binary)");

            bool hasArm64Dir = zipArchive.Entries.Any(e => e.FullName.Contains($"{arm64Dir}", StringComparison.Ordinal));
            Assert.False(hasArm64Dir, "Should not have arm64 subdirectory (lip creates true universal binary)");
        }
        else
        {
            // On non-macOS, should create architecture subdirectories with launcher script
            bool hasAmd64Dir = zipArchive.Entries.Any(e => e.FullName.StartsWith(amd64Dir, StringComparison.Ordinal));
            Assert.True(hasAmd64Dir, "Should contain amd64 subdirectory (script based universal binary)");

            bool hasArm64Dir = zipArchive.Entries.Any(e => e.FullName.StartsWith(arm64Dir, StringComparison.Ordinal));
            Assert.True(hasArm64Dir, "Should contain arm64 subdirectory (script based universal binary)");

            // Should have launcher script
            bool hasLauncher = zipArchive.Entries.Any(e => e.FullName.Equals(executablePath, StringComparison.Ordinal));
            Assert.True(hasLauncher, $"Should contain launcher script at {executablePath}");

            // Verify executables exist in architecture subdirectories
            string amd64ExecutablePath = Path.Combine(amd64Dir, ProjectName);
            bool hasAmd64Executable = zipArchive.Entries.Any(e => e.FullName.Equals(amd64ExecutablePath, StringComparison.Ordinal));
            Assert.True(hasAmd64Executable, "Should contain amd64 executable");

            string arm64ExecutablePath = Path.Combine(arm64Dir, ProjectName);
            bool hasArm64Executable = zipArchive.Entries.Any(e => e.FullName.Equals(arm64ExecutablePath, StringComparison.Ordinal));
            Assert.True(hasArm64Executable, "Should contain arm64 executable");
        }
    }

    [Fact]
    public void UniversalMacOS_WithCustomName_ShouldUseCustomName()
    {
        string customName = "CustomUniversalApp";
        string outputDir = Path.Combine(_testOutputRoot, "universal-custom-name");

        Options options = new Options
        {
            ProjectPath = Path.Combine(_exampleProjectPath, $"{ProjectName}.csproj"),
            OutputDirectory = outputDir,
            ExecutableFileName = customName,
            InfoPlistPath = Path.Combine(_exampleProjectPath, "Info.plist"),
            IcnsPath = Path.Combine(_exampleProjectPath, "Icon.icns"),
            UseZipCompression = true,
            VerboseOutput = true,
            MacOSUniversal = true
        };

        options.RuntimeIdentifiers.Add("osx-x64");
        options.RuntimeIdentifiers.Add("osx-arm64");

        MonoPackService service = new MonoPackService(options);

        service.Execute();

        string zipPath = Path.Combine(outputDir, $"{customName}-universal.zip");
        Assert.True(File.Exists(zipPath), $"Universal archive should be named {customName}-universal.zip");

        using ZipArchive zipArchive = ZipFile.OpenRead(zipPath);

        // Verify .app bundle uses custom name
        string appBundleDir = $"{customName}.app";
        bool allEntriesInAppDirectory = zipArchive.Entries.All(e => e.FullName.StartsWith(appBundleDir, StringComparison.Ordinal));
        Assert.True(allEntriesInAppDirectory, $"All entries should be within {customName}.app bundle");

        string macOsDir = Path.Combine(appBundleDir, "Contents", "MacOS");
        string executablePath = Path.Combine(macOsDir, customName);

        // On macOS, should have the custom named universal binary
        // On non-macOS, should have the custom named launcher script
        bool hasExecutable = zipArchive.Entries.Any(e => e.FullName.Equals(executablePath, StringComparison.Ordinal));
        Assert.True(hasExecutable, $"Should contain universal executable named {customName}");
    }

    [Theory]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public void MacOSSingleArchitecture_ShouldDetermineExecutableName(string rid)
    {
        // Don't set ExecutableFileName, let it be determined from MSBuild
        string outputDir = Path.Combine(_testOutputRoot, $"macos-auto-name-{rid}");

        Options options = new Options
        {
            ProjectPath = Path.Combine(_exampleProjectPath, $"{ProjectName}.csproj"),
            OutputDirectory = outputDir,
            ExecutableFileName = null,
            InfoPlistPath = Path.Combine(_exampleProjectPath, "Info.plist"),
            IcnsPath = Path.Combine(_exampleProjectPath, "Icon.icns"),
            UseZipCompression = true,
            VerboseOutput = true
        };

        options.RuntimeIdentifiers.Add(rid);

        MonoPackService service = new MonoPackService(options);

        service.Execute();

        // The executable name should be determined from MSBuild (which will be ProjectName for the example project)
        string zipPath = Path.Combine(outputDir, $"{ProjectName}-{rid}.zip");
        Assert.True(File.Exists(zipPath), $"Archive not found at: {zipPath}");

        using ZipArchive zipArchive = ZipFile.OpenRead(zipPath);

        string appBundleDir = $"{ProjectName}.app";
        string macOSDir = Path.Combine(appBundleDir, "Contents", "MacOS");
        string executablePath = Path.Combine(macOSDir, ProjectName);
        bool hasExecutable = zipArchive.Entries.Any(e => e.FullName.Equals(executablePath, StringComparison.Ordinal));
        Assert.True(hasExecutable, $"Should contain executable named {ProjectName} (determined from MSBuild)");
    }

    [Theory]
    [InlineData("linux-x64")]
    [InlineData("linux-arm64")]
    [InlineData("win-x64")]
    public void NonMacOSPlatform_ShouldDetermineExecutableName(string rid)
    {
        // Don't set ExecutableFileName, let it be determined from MSBuild
        string outputDir = Path.Combine(_testOutputRoot, $"auto-name-{rid}");

        Options options = new Options
        {
            ProjectPath = Path.Combine(_exampleProjectPath, $"{ProjectName}.csproj"),
            OutputDirectory = outputDir,
            ExecutableFileName = null,
            UseZipCompression = true,
            VerboseOutput = true
        };

        options.RuntimeIdentifiers.Add(rid);

        MonoPackService service = new MonoPackService(options);

        service.Execute();

        // The executable name should be determined from MSBuild
        string archivePath = Path.Combine(outputDir, $"{ProjectName}-{rid}.zip");
        Assert.True(File.Exists(archivePath), $"Archive not found at: {archivePath}");

        using ZipArchive zipArchive = ZipFile.OpenRead(archivePath);

        // Verify the executable is in the archive with correct name
        string expectedFileName = rid.StartsWith("win", StringComparison.Ordinal) ? $"{ProjectName}.exe" : ProjectName;
        bool hasExecutable = zipArchive.Entries.Any(e => Path.GetFileName(e.Name).Equals(expectedFileName, StringComparison.Ordinal));
        Assert.True(hasExecutable, $"Should contain executable named {expectedFileName} (determined from MSBuild)");

        // Verify executable has correct permissions (for non-Windows)
        if (!rid.StartsWith("win", StringComparison.Ordinal))
        {
            ZipArchiveEntry? executableEntry = zipArchive.Entries.FirstOrDefault(e => Path.GetFileName(e.Name).Equals(expectedFileName, StringComparison.Ordinal));
            Assert.NotNull(executableEntry);

            UnixFileMode permissions = (UnixFileMode)((executableEntry.ExternalAttributes >> 16) & 0x1FF);
            Assert.True(permissions.HasFlag(UnixFileMode.UserExecute), $"Executable {expectedFileName} should have execute permissions");
        }
    }
}
