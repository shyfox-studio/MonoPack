// Copyright (c) ShyFox Studio. All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.


using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;

const string WINDOWS_RID = "win-x64";       // runtime identifier for 64-bit windows build.
const string WINDOWS_ARM_RID = "win-arm64"; // runtime identifier for arm64-bit windows build.
const string LINUX_RID = "linux-x64";        // runtime identifier for 64-bit linux build.
const string OSX_X64_RID = "osx-x64";       // runtime identifier for 64-bit macOS (Intel) build.
const string OSX_ARM64_RID = "osx-arm64";   // runtime identifier for 64-bit macOS (Apple Silicon) build.

string _projectPath = string.Empty;         // Path to the game .csproj file to build.
string _outputDir = string.Empty;           // Path to the output directory.
List<string> _runtimeIdentifiers = new();   // The runtime identifiers to build for
string _infoPlistPath = string.Empty;       // Path to the Info.plist file (required for macOS).
string _icnsPath = string.Empty;            // Path to the Apple Icon (.icns) file (required for macOS).
string _executableFile = null;              // The name of the executable files to set as executable.
bool _useZip = false;                       // Indicates whether to use zip for packaging. Defaults to tar.gz.
bool _verbos = false;                       // Indicates whether verbose output is enabled.

if (Debugger.IsAttached && args.Length == 0)
{
    args = [
        "-p", "../../../../../../examples/ShyFox.MonoPack.Tool.Example/ShyFox.MonoPack.Tool.Example.csproj",
        "-o", "./artifacts/builds",
        "-r", "win-x64",
        "-r", "win-arm64",
        "-r", "osx-x64",
        "-r", "osx-arm64",
        "-r", "linux-x64",
        "-i", "../../../../../../examples/ShyFox.MonoPack.Tool.Example/Info.plist",
        "-c", "../../../../../../examples/ShyFox.MonoPack.Tool.Example/Icon.icns",
        "-e", "ExampleGame",
        "-z",
        "-v"
    ];
}

ParseArguments();                           // Parse the command line arguments given
ValidateArguments();                        // Validate the parsed arguments and set defaults if needed.

Directory.CreateDirectory(_outputDir);      // Ensure output directory is created.

try
{
    // Build and package for Windows OS if specified
    if (_runtimeIdentifiers.Contains(WINDOWS_RID))
    {
        BuildWindows();
        PackageWindows();
    }

    if (_runtimeIdentifiers.Contains(WINDOWS_ARM_RID))
    {
        BuildWindowsArm();
        PackageWindows(WINDOWS_ARM_RID);
    }

    // Build and pacakge for macOS if specified.
    if (_runtimeIdentifiers.Contains(OSX_X64_RID) && _runtimeIdentifiers.Contains(OSX_ARM64_RID))
    {
        BuildOSXUniversal();
        PackageOSXUniversal();
    }
    else if (_runtimeIdentifiers.Contains(OSX_X64_RID))
    {
        BuildOSXIntel();
        PackageOSXIntel();
    }
    else if (_runtimeIdentifiers.Contains(OSX_ARM64_RID))
    {
        BuildOSXAppleSilicon();
        PackageOSXAppleSilicon();
    }

    // Build and pakage for Linux OS if specified.
    if (_runtimeIdentifiers.Contains(LINUX_RID))
    {
        BuildLinux();
        PackageLinux();
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");

    if (_verbos)
    {
        Console.Error.WriteLine(ex.StackTrace);
    }

    return 1;
}

void BuildWindows()
{
    string outputDir = Path.Combine(_outputDir, WINDOWS_RID);
    Build(outputDir, WINDOWS_RID);
}

void BuildWindowsArm()
{
    string outputDir = Path.Combine(_outputDir, WINDOWS_ARM_RID);
    Build(outputDir, WINDOWS_ARM_RID);
}

void BuildLinux()
{
    string outputDir = Path.Combine(_outputDir, LINUX_RID);
    Build(outputDir, LINUX_RID);
}

void BuildOSXUniversal()
{
    BuildOSXIntel();
    BuildOSXAppleSilicon();
}

void BuildOSXIntel()
{
    string outputDir = Path.Combine(_outputDir, OSX_X64_RID);
    Build(outputDir, OSX_X64_RID);
}

void BuildOSXAppleSilicon()
{
    string outputDir = Path.Combine(_outputDir, OSX_ARM64_RID);
    Build(outputDir, OSX_ARM64_RID);
}

void Build(string outputDir, string rid)
{
    Console.WriteLine($"Building {rid}");

    ProcessStartInfo startInfo = new()
    {
        FileName = "dotnet",
        Arguments = $"publish \"{_projectPath}\" -c Release -r {rid} -p:PublishReadyToRun=false -p:TieredCompilation=false -p:PublishSingleFile=true --self-contained -o \"{outputDir}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using Process process = new() { StartInfo = startInfo };

    process.OutputDataReceived += (sender, args) =>
{
    if (args.Data is not null && _verbos)
    {
        Console.WriteLine(args.Data);
    }
};

    process.ErrorDataReceived += (sender, args) =>
    {
        if (args.Data is not null && _verbos)
        {
            Console.WriteLine(args.Data);
        }
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();
}

void PackageWindows(string rid = WINDOWS_RID)
{
    string projectName = Path.GetFileNameWithoutExtension(_projectPath);
    string sourceDir = Path.Combine(_outputDir, rid);
    string zipPath = Path.Combine(_outputDir, $"{_executableFile ?? projectName}-{rid}.zip");

    if (File.Exists(zipPath))
    {
        File.Delete(zipPath);
    }

    ZipFile.CreateFromDirectory(sourceDir, zipPath);
    Directory.Delete(sourceDir, true);

    Console.WriteLine($"Created archive: {zipPath}");
}

void PackageLinux()
{
    string projectName = Path.GetFileNameWithoutExtension(_projectPath);
    string sourceDir = Path.Combine(_outputDir, LINUX_RID);
    string tarPath = Path.Combine(_outputDir, $"{_executableFile ?? projectName}-{LINUX_RID}.{(_useZip ? "zip" : "tar.gz")}");

    if (File.Exists(tarPath))
    {
        File.Delete(tarPath);
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Console.WriteLine(
            $"""

            --------
            Warning: Building for Linux on Windows.
            Perform the following once the archive has been extracted on Linux to make it executable:
                1. Open a terminal in the directory where the archive was unpacked to.
                2. Execute the command "chmod +x ./{_executableFile ?? projectName}"
            --------

            """
        );
    }
    else
    {
        Chmod(Path.Combine(sourceDir, _executableFile ?? projectName));
    }

    using FileStream fs = new(tarPath, FileMode.Create, FileAccess.Write);
    if (_useZip)
    {
        using ZipArchive zip = new(fs, ZipArchiveMode.Create, leaveOpen: true);
        ZipDirectory(sourceDir, zip, _executableFile, projectName);
    }
    else
    {
        using GZipStream gz = new(fs, CompressionMode.Compress, leaveOpen: true);
        TarDirectory(sourceDir, false, gz, _executableFile, projectName);
    }
    
    Directory.Delete(sourceDir, true);

    Console.WriteLine($"Created archive: {tarPath}");
}

void PackageOSXIntel()
{
    string projectName = Path.GetFileNameWithoutExtension(_projectPath);
    string sourceDir = Path.Combine(_outputDir, OSX_X64_RID);

    // Create the .app directory strucgture
    string appDir = Path.Combine(_outputDir, $"{_executableFile ?? projectName}.app");
    string contentsDir = Path.Combine(appDir, "Contents");
    string macOSDir = Path.Combine(contentsDir, "MacOS");
    string resourcesDir = Path.Combine(contentsDir, "Resources");
    string contentsResourceDir = Path.Combine(resourcesDir, "Content");

    // Create all required directories
    Directory.CreateDirectory(contentsDir);
    Directory.CreateDirectory(macOSDir);
    Directory.CreateDirectory(resourcesDir);

    // Copy the Info.plist
    File.Copy(_infoPlistPath, Path.Combine(contentsDir, "Info.plist"), true);

    // Copy the icns file
    File.Copy(_icnsPath, Path.Combine(resourcesDir, Path.GetFileName(_icnsPath)), true);

    // Copy the osx-x64 files
    CopyDirectory(sourceDir, macOSDir);

    // Move the game Content directory to the resources directory
    string gameContentDir = Path.Combine(macOSDir, "Content");
    if (Directory.Exists(contentsResourceDir))
    {
        Directory.Delete(contentsResourceDir, recursive: true);
    }
    Directory.Move(gameContentDir, contentsResourceDir);

    // Set file as executable. Only works on Linux and mac
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Console.WriteLine(
            $"""

            --------
            Warning: Building for macOS on Windows.
            Perform the following once the archive has been extracted on macOs to make it executable:
                1. Open a terminal in the directory where the archive was unpacked to.
                2. Execute the command "chmod +x ./{_executableFile ?? projectName}.app/Contents/MacOS/{_executableFile ?? projectName}"
            --------

            """
        );
    }
    else
    {
        Chmod(Path.Combine(sourceDir, _executableFile ?? projectName));
    }

    string tarPath = Path.Combine(_outputDir, $"{_executableFile ?? projectName}-{OSX_X64_RID}.{(_useZip ? "zip" : "tar.gz")}");

    if (File.Exists(tarPath))
    {
        File.Delete(tarPath);
    }

    using FileStream fs = new(tarPath, FileMode.Create, FileAccess.Write);
    if (_useZip)
    {
        using ZipArchive zip = new(fs, ZipArchiveMode.Create, leaveOpen: true);
        ZipDirectory(sourceDir, zip, _executableFile, projectName);
    }
    else
    {
        using GZipStream gz = new(fs, CompressionMode.Compress, leaveOpen: true);
        TarDirectory(sourceDir, false, gz, _executableFile, projectName);
    }
    
    // Cleanup
    Directory.Delete(sourceDir, true);

    Console.WriteLine($"Created archive: {tarPath}");
}

void PackageOSXAppleSilicon()
{
    string projectName = Path.GetFileNameWithoutExtension(_projectPath);
    string sourceDir = Path.Combine(_outputDir, OSX_ARM64_RID);

    // Create the .app directory structure
    string appDir = Path.Combine(_outputDir, $"{_executableFile ?? projectName}.app");
    string contentsDir = Path.Combine(appDir, "Contents");
    string macOSDir = Path.Combine(contentsDir, "MacOS");
    string resourcesDir = Path.Combine(contentsDir, "Resources");
    string contentsResourceDir = Path.Combine(resourcesDir, "Content");

    // Create all required directories
    Directory.CreateDirectory(contentsDir);
    Directory.CreateDirectory(macOSDir);
    Directory.CreateDirectory(resourcesDir);
    Directory.CreateDirectory(contentsResourceDir);

    // Copy the Info.plist
    File.Copy(_infoPlistPath, Path.Combine(contentsDir, "Info.plist"), true);

    // Copy the icns file
    File.Copy(_icnsPath, Path.Combine(resourcesDir, Path.GetFileName(_icnsPath)), true);

    // Copy the osx-arm64 files
    CopyDirectory(sourceDir, macOSDir);

    // Move the game Content directory to the resources directory
    string gameContentDir = Path.Combine(macOSDir, "Content");
    if (Directory.Exists(contentsResourceDir))
    {
        Directory.Delete(contentsResourceDir, recursive: true);
    }
    Directory.Move(gameContentDir, contentsResourceDir);

    // Set file as executable. Only works on Linux and mac
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Console.WriteLine(
            $"""

            --------
            Warning: Building for macOS on Windows.
            Perform the following once the archive has been extracted on macOs to make it executable:
                1. Open a terminal in the directory where the archive was unpacked to.
                2. Execute the command "chmod +x ./{_executableFile ?? projectName}.app/Contents/MacOS/{_executableFile ?? projectName}"
            --------

            """
        );
    }
    else
    {
        Chmod(Path.Combine(sourceDir, _executableFile ?? projectName));
    }

    string tarPath = Path.Combine(_outputDir, $"{_executableFile ?? projectName}-{OSX_X64_RID}.{(_useZip ? "zip" : "tar.gz")}");

    if (File.Exists(tarPath))
    {
        File.Delete(tarPath);
    }

    using FileStream fs = new(tarPath, FileMode.Create, FileAccess.Write);
    if (_useZip)
    {
        using ZipArchive zip = new(fs, ZipArchiveMode.Create, leaveOpen: true);
        ZipDirectory(sourceDir, zip, _executableFile, projectName);
    }
    else
    {
        using GZipStream gz = new(fs, CompressionMode.Compress, leaveOpen: true);
        TarDirectory(sourceDir, true, gz, _executableFile, projectName);
    }

    // Cleanup
    Directory.Delete(sourceDir, true);

    Console.WriteLine($"Created archive: {tarPath}");
}

void PackageOSXUniversal()
{
    string projectName = Path.GetFileNameWithoutExtension(_projectPath);
    string amd64SourceDir = Path.Combine(_outputDir, OSX_X64_RID);
    string arm64SourceDir = Path.Combine(_outputDir, OSX_ARM64_RID);

    // Create the .app directory structure
    string appDir = Path.Combine(_outputDir, $"{_executableFile ?? projectName}.app");
    string contentsDir = Path.Combine(appDir, "Contents");
    string macOSDir = Path.Combine(contentsDir, "MacOS");
    string resourcesDir = Path.Combine(contentsDir, "Resources");
    string amd64Dir = Path.Combine(macOSDir, "amd64");
    string arm64Dir = Path.Combine(macOSDir, "arm64");
    string contentsResourceDir = Path.Combine(resourcesDir, "Content");

    // Create all required directories
    Directory.CreateDirectory(contentsDir);
    Directory.CreateDirectory(macOSDir);
    Directory.CreateDirectory(resourcesDir);

    // These directories are only needed if packaging is being done not on macOS
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        Directory.CreateDirectory(amd64Dir);
        Directory.CreateDirectory(arm64Dir);
    }

    // Copy the Info.plist
    File.Copy(_infoPlistPath, Path.Combine(contentsDir, "Info.plist"), true);

    // Copy the icns file
    File.Copy(_icnsPath, Path.Combine(resourcesDir, Path.GetFileName(_icnsPath)), true);

    // If this is running on macOS, then we can use lipo to create a universal
    // binary; otherwise, we have to do the traditional method of copying both
    // the x64 and arm64 directories
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        // Copy the osx-amd64 files
        CopyDirectory(amd64SourceDir, macOSDir);

        // Combine the osx-arm64 executable using lip
        string amd64ExecutablePath = Path.Combine(macOSDir, _executableFile ?? projectName);
        string arm64ExecutablePath = Path.Combine(arm64SourceDir, _executableFile ?? projectName);
        string universalExecutablePath = Path.Combine(macOSDir, _executableFile ?? projectName);
        Lipo(amd64ExecutablePath, arm64ExecutablePath, universalExecutablePath);

        // Move the game Content directory to the resources directory
        string gameContentDir = Path.Combine(macOSDir, "Content");
        if (Directory.Exists(contentsResourceDir))
        {
            Directory.Delete(contentsResourceDir, recursive: true);
        }
        Directory.Move(gameContentDir, contentsResourceDir);
    }
    else
    {
        // Copy the osx-amd64 files
        CopyDirectory(amd64SourceDir, amd64Dir);

        // Copy the osx-arm64 files
        CopyDirectory(arm64SourceDir, arm64Dir);
    }

    // If not running on macOS, then we couldn't use lipo to combine the
    // executables, so we need to make an launch script instead.
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        string launchScriptPath = Path.Combine(macOSDir, _executableFile ?? projectName);
        string scriptContent =
        $"""
        #!/bin/bash

        cd "$(dirname $BASH_SOURCE)/../Resources"
        if [[ $(uname -p) == 'arm' ]]; then
          ./../MacOS/arm64/{_executableFile ?? projectName}
        else
          ./../MacOS/amd64/{_executableFile ?? projectName}
        fi
        """;

        // Replace Windows (CRLF) line endings with Unix (LF) line endings
        scriptContent = scriptContent.ReplaceLineEndings("\n");
        File.WriteAllText(launchScriptPath, scriptContent);
    }

    // Set file as executable. Only works on Linux and mac
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Console.WriteLine(
            $"""

            --------
            Warning: Building for macOS on Windows.
            Perform the following once the archive has been extracted on macOs to make it executable:
                1. Open a terminal in the directory where the archive was unpacked to.
                2. Execute the command "chmod +x ./{_executableFile ?? projectName}.app/Contents/MacOS/{_executableFile ?? projectName}"
            --------

            """
        );
    }
    else
    {
        Chmod(Path.Combine(macOSDir, _executableFile ?? projectName));
    }

    string tarPath = Path.Combine(_outputDir, $"{_executableFile ?? projectName}-universal.{(_useZip ? "zip" : "tar.gz")}");

    if (File.Exists(tarPath))
    {
        File.Delete(tarPath);
    }

    using FileStream fs = new(tarPath, FileMode.Create, FileAccess.Write);
    if (_useZip)
    {
        using ZipArchive zip = new(fs, ZipArchiveMode.Create, leaveOpen: true);
        ZipDirectory(appDir, zip, _executableFile, projectName);
    }
    else
    {
        using GZipStream gz = new(fs, CompressionMode.Compress, leaveOpen: true);
        TarDirectory(appDir, true, gz, _executableFile, projectName);
    }

    // Cleanup
    Directory.Delete(amd64SourceDir, true);
    Directory.Delete(arm64SourceDir, true);

    Console.WriteLine($"Created archive: {tarPath}");
}

void ZipDirectory(string sourceDirectory, ZipArchive archive, params string[] executableFiles)
{
    foreach (string filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
    {
        ZipArchiveEntry entry = archive.CreateEntryFromFile(filePath, Path.GetRelativePath(sourceDirectory, filePath));
        UnixFileMode permissions = UnixFileMode.None;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            permissions = File.GetUnixFileMode(filePath);
        }
        else 
        {
            permissions = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;// Default permissions for Windows
            if (executableFiles.Contains(Path.GetFileName(Path.GetFileNameWithoutExtension(filePath))))
            {
                permissions |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            }
        }
        entry.ExternalAttributes = (int)permissions << 16; // Set Unix file permissions
    }
}

void TarDirectory(string sourceDirectory, bool includeBaseDirectory, Stream stream, params string[] executableFiles)
{
    using TarWriter writer = new(stream, TarEntryFormat.Pax); // Pax supports Unix permissions
    foreach (string filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
    {
        FileInfo fi = new(filePath);
        if (fi.Attributes.HasFlag(FileAttributes.Hidden))
        {
            continue;
        }
        string relativePath = Path.GetRelativePath(includeBaseDirectory ? Path.Combine(sourceDirectory, "..") : sourceDirectory, filePath);
        PaxTarEntry entry = new(TarEntryType.RegularFile, relativePath);

        //Preserve Unix file permissions (if on Linux/macOS)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            entry.Mode = File.GetUnixFileMode(filePath);
        }
        else
        {
            entry.Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;// Default permissions for Windows
            if (executableFiles.Contains(Path.GetFileName(Path.GetFileNameWithoutExtension(filePath))))
            {
                entry.Mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            }
        }

        // Attach file data
        entry.DataStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        writer.WriteEntry(entry);
    }
}

void Chmod(string filePath)
{
    ProcessStartInfo startInfo = new()
    {
        FileName = "chmod",
        Arguments = $"+x {filePath}",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using Process process = new() { StartInfo = startInfo };

    process.OutputDataReceived += (sender, args) =>
    {
        if (args.Data is not null && _verbos)
        {
            Console.WriteLine(args.Data);
        }
    };

    process.ErrorDataReceived += (sender, args) =>
    {
        if (args.Data is not null && _verbos)
        {
            Console.WriteLine(args.Data);
        }
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();
}

void Lipo(string source1, string source2, string destination)
{
    ProcessStartInfo startInfo = new()
    {
        FileName = "lipo",
        Arguments = $"-create {source1} {source2} -output {destination}",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using Process process = new() { StartInfo = startInfo };

    process.OutputDataReceived += (sender, args) =>
    {
        if (args.Data is not null && _verbos)
        {
            Console.WriteLine(args.Data);
        }
    };

    process.ErrorDataReceived += (sender, args) =>
    {
        if (args.Data is not null && _verbos)
        {
            Console.WriteLine(args.Data);
        }
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();
}

void CopyDirectory(string sourceDir, string targetDir)
{
    Directory.CreateDirectory(targetDir);

    foreach (string filePath in Directory.GetFiles(sourceDir))
    {
        string fileName = Path.GetFileName(filePath);
        string destPath = Path.Combine(targetDir, fileName);
        File.Copy(filePath, destPath, true);
    }

    foreach (string dirPath in Directory.GetDirectories(sourceDir))
    {
        string dirName = Path.GetFileName(dirPath);
        string destPath = Path.Combine(targetDir, dirName);
        CopyDirectory(dirPath, destPath);
    }
}

void ParseArguments()
{
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-p":
            case "--project":
                if (i + 1 < args.Length)
                {
                    _projectPath = args[++i];
                }
                break;

            case "-o":
            case "--output":
                if (i + 1 < args.Length)
                {
                    _outputDir = args[++i];
                }
                break;

            case "-r":
            case "--runtime-identifier":
                if (i + 1 < args.Length)
                {
                    string rid = args[++i];
                    _runtimeIdentifiers.Add(rid);
                }
                break;

            case "-i":
            case "--info-plist":
                if (i + 1 < args.Length)
                {
                    _infoPlistPath = args[++i];
                }
                break;

            case "-c":
            case "--icns":
                if (i + 1 < args.Length)
                {
                    _icnsPath = args[++i];
                }
                break;
            case "-e":
            case "--executable":
                if (i + 1 < args.Length)
                {
                    _executableFile = args[++i];
                }
                break;
            case "-z":
            case "--zip":
                _useZip = true;
                break;
            case "-v":
            case "--verbos":
                _verbos = true;
                break;

            case "-h":
            case "--help":
                DisplayHelp();
                Environment.Exit(1);
                break;

            default:
                Console.WriteLine($"Unknown argument: {args[i]}");
                DisplayHelp();
                Environment.Exit(1);
                break;
        }
    }
}

void ValidateArguments()
{
    // Validate that a project path was given.  If not, attempt to locate one in
    // the current directory.
    if (string.IsNullOrEmpty(_projectPath))
    {
        string[] projectFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");

        if (projectFiles.Length >= 1)
        {
            _projectPath = projectFiles[0];
            Console.WriteLine($"No project file specified, using {_projectPath}");
        }
        else
        {
            Console.Error.WriteLine("Unable to find C# Project FIle (.csproj).");
            DisplayHelp();
            Environment.Exit(1);
        }
    }

    // Validate that the project path is a .csproj file
    if (Path.GetExtension(_projectPath) != ".csproj")
    {
        Console.Error.WriteLine($"Project path specified is not a C# Project File (.csproj): {_projectPath}");
        DisplayHelp();
        Environment.Exit(1);
    }

    // Validate that a runtime identifier was specified for the build.
    // If no runtime identifier was specified, default to the current OS.
    if (_runtimeIdentifiers.Count == 0)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _runtimeIdentifiers.Add("win-x64");
            _runtimeIdentifiers.Add("win-arm64");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _runtimeIdentifiers.Add("osx-x64");
            _runtimeIdentifiers.Add("osx-arm64");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _runtimeIdentifiers.Add("linux-x64");
        }
    }

    // If osx-x64 and/or osx-arm64 runtime identifiers are specified the we need
    // to validate that the Info.plist and icns file were given
    if (_runtimeIdentifiers.Contains("osx-x64") || _runtimeIdentifiers.Contains("osx-arm64"))
    {
        // Validate that an Info.plist file was given. If not, attempt to locate
        // one in the current directory.
        if (string.IsNullOrEmpty(_infoPlistPath))
        {
            string[] plistFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.plist");

            if (plistFiles.Length == 1)
            {
                _infoPlistPath = plistFiles[0];
                Console.WriteLine($"macOS build specified but no Info.plist was specified, using {_infoPlistPath}");
            }
            else
            {
                Console.Error.WriteLine("macOS build specified but unable to find Info.plist");
                DisplayHelp();
                Environment.Exit(1);
            }
        }

        // Validate that the Info.plist file is a .plist file
        if (Path.GetExtension(_infoPlistPath) != ".plist")
        {
            Console.Error.WriteLine($"Info.plist path specified is not a .plist file: {_infoPlistPath}");
            DisplayHelp();
            Environment.Exit(1);
        }

        // Validate that a .icns file was given.  If not, attempt to locate
        // one in the current directory
        if (string.IsNullOrEmpty(_icnsPath))
        {
            string[] icnsFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.icns");

            if (icnsFiles.Length == 1)
            {
                _icnsPath = icnsFiles[0];
                Console.WriteLine($"macOS build specified but no Apple Icons (.icns) was specified, using {_icnsPath}");
            }
            else
            {
                Console.Error.WriteLine("macOS build specified but unable to find Apply Icons (.icns) file");
                DisplayHelp();
                Environment.Exit(1);
            }
        }

        // Validate that the .icns fle is a .icns file
        if (Path.GetExtension(_icnsPath) != ".icns")
        {
            Console.Error.WriteLine($"Apply Icon (.icns) path specified is not a .icns file: {_icnsPath}");
            DisplayHelp();
            Environment.Exit(1);
        }
    }

    // Validate that an output directory was given.  If not, set it to the
    // default for MonoPack.
    if (string.IsNullOrEmpty(_outputDir))
    {
        _outputDir = Path.Combine(Path.GetDirectoryName(_projectPath) ?? ".", "bin", "MonoPacked");
    }

    if (_verbos)
    {
        Console.WriteLine(
            $"""
                The following arguments were validated:
                Project Path:           {_projectPath}
                Output Directory:       {_outputDir}
                Runtime Identifiers:    {string.Join(", ", _runtimeIdentifiers)}
                Info.plist Path:        {_infoPlistPath}
                Icns Path:              {_icnsPath}
                Verbos:                 {_verbos}
                """
        );
    }
}

void DisplayHelp()
{
    Console.WriteLine(
        """
        MonoPack - MonoGame Project Packer
        Usage: monopack [options] [project-path]

        Options:
            -p --project <path>             Path to the .csproj file (optional if only one .csproj in current directory)
            -o --output <dir>               Output directory (default: [project]/bin/Packed)
            -r --runtime-identifier <rid>   Specify target runtime identifier(s) to build for.
            -i --info-plist <path>          Path to Info.plist file (required when packaging for macOS)
            -c --icns <path>                Path to .icns file (required when packaging for macOS)
            -e --executable <name>          Name of the executable file to set as executable
            -z --zip                        Use zip for packaging instead of tar.gz (only for Linux and MacOS)
            -v --verbose                    Enable verbose output
            -h --help                       Show this help message

        Available runtime identifiers:
            win-x64     Windows
            win-arm64   Windows Arm64
            linux-x64   Linux
            osx-x64     macOS 64-bit Intel
            osx-arm64   macOS Apple Silicon


        Notes:
            The --project path is not specified, an attempt will be made to locate the .csproj within
            the current directory.

            If a --runtime-identifier is not specified, a default one will be chosen based on the
            operating system.

            When the --runtime-identifier specified is osx-x64 and/or osx-arm64, paths to the Info.plist
            and the .icns file must be specified.  If they are not specified, an attempt is made to locate
            them in the current directory.

        Examples:
            monopack
            monopack -h
            monopack -p ./src/MyGame.csproj -o ./artifacts/builds -r win-x64 -r osx-x64 -r osx-armd64 -r linux-x64 -i ./Info.plist -c ./Icon.icns
        """
    );
}
