using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace MonoPack;

internal sealed class Options
{
    /// <summary>Gets the path to the project file to be packaged.</summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>Gets the output directory for the packaged applications.</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>Gets the list of runtime identifiers to build for.</summary>
    public Collection<string> RuntimeIdentifiers { get; } = [];

    /// <summary>Gets the path to the Info.plist file for macOS packaging.</summary>
    public string? InfoPlistPath { get; set; }

    /// <summary>Gets the path to the .icns icon file for macOS packaging.</summary>
    public string? IcnsPath { get; set; }

    /// <summary>Gets the name of the executable file.</summary>
    public string? ExecutableFileName { get; set; }

    /// <summary>Indicates whether to use zip compression instead of .tar.gz</summary>
    public bool UseZipCompression { get; set; }

    /// <summary>Indicates whether verbose output is enabled for diagnostic purposes.</summary>
    public bool VerboseOutput { get; set; }

    /// <summary>Indicates whether to create a universal macOS .app bundle when both osx-x64 and osx-arm64 are specified.</summary>
    public bool MacOSUniversal { get; set; }

    /// <summary>
    /// Gets or Sets additional arguments to pass to dotnet publish.
    /// When specified, default publish flags are automatically disabled.
    /// </summary>
    public string? PublishArgs { get; set; }

    /// <summary>Validates and configures the MonoPack options.</summary>
    public void Validate()
    {
        ValidateProjectPath();
        ValidateRuntimeIdentifiers();
        ValidateMacOSRequirements();
        ConfigureOutputDirectory();
    }

    private void ValidateProjectPath()
    {
        if (!string.IsNullOrEmpty(ProjectPath))
        {
            if (!File.Exists(ProjectPath))
            {
                throw new FileNotFoundException($"Unable to find C# Project file: {ProjectPath}");
            }

            if (Path.GetExtension(ProjectPath) != ".csproj")
            {
                throw new InvalidOperationException($"Project path is not a C# Project File (.csproj): {ProjectPath}");
            }
        }

        // Validate that a project path was given.
        // If not, attempt to locate one in the current directory.
        if (string.IsNullOrEmpty(ProjectPath))
        {
            string[] projectFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");

            if (projectFiles.Length == 1)
            {
                ProjectPath = projectFiles[0];
                Console.WriteLine($"No project file specified, using {ProjectPath}");
            }
            else
            {
                throw new InvalidOperationException("Unable to find a unique C# Project File (.csproj)");
            }
        }
        else
        {
            if (!File.Exists(ProjectPath))
            {
                throw new FileNotFoundException($"Unable to find C# Project file: {ProjectPath}");
            }

            if (Path.GetExtension(ProjectPath) != ".csproj")
            {
                throw new InvalidOperationException($"Project path is not a C# Project File (.csproj): {ProjectPath}");
            }
        }
    }

    private void ValidateRuntimeIdentifiers()
    {
        // Validate that a runtime identifier was specified for the build.
        // If no runtime identifier was specified, default based on the current OS.
        if (RuntimeIdentifiers.Count == 0)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RuntimeIdentifiers.Add("win-x64");
                RuntimeIdentifiers.Add("win-arm64");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                RuntimeIdentifiers.Add("osx-x64");
                RuntimeIdentifiers.Add("osx-arm64");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                RuntimeIdentifiers.Add("linux-x64");
            }
        }
    }

    private void ValidateMacOSRequirements()
    {
        // If osx-x64 and/or osx-arm64 runtime identifiers are specified, then we need
        // to validate that the Info.plist and icns file are given.
        if (RuntimeIdentifiers.Contains("osx-x64") || RuntimeIdentifiers.Contains("osx-arm64"))
        {
            ValidateInfoPlist();
            ValidateIcnsFile();
        }
    }

    private void ValidateInfoPlist()
    {
        // Validate that an Info.plist file was given.
        // if not, attempt to locate one in the current directory.
        if (string.IsNullOrEmpty(InfoPlistPath))
        {
            string[] plistFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.plist");

            if (plistFiles.Length == 1)
            {
                InfoPlistPath = plistFiles[0];
                Console.WriteLine($"No Info.plist specified, using {InfoPlistPath}");
            }
            else
            {
                throw new InvalidOperationException("Unable to find Info.plist for macOS build");
            }
        }
        else
        {
            if (!File.Exists(InfoPlistPath))
            {
                throw new FileNotFoundException($"Unable to find Info.plist file: {InfoPlistPath}");
            }

            if (Path.GetExtension(InfoPlistPath) != ".plist")
            {
                throw new InvalidOperationException($"Info.plist path is not a .plist file: {InfoPlistPath}");
            }
        }
    }

    private void ValidateIcnsFile()
    {
        // Validate that an .icns file was given.
        // if not, attempt to locate one in the current directory.
        if (string.IsNullOrEmpty(IcnsPath))
        {
            string[] icnsFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.icns");

            if (icnsFiles.Length == 1)
            {
                IcnsPath = icnsFiles[0];
                Console.WriteLine($"No .icns specified, using {IcnsPath}");
            }
            else
            {
                throw new InvalidOperationException("Unable to find .icns for macOS build");
            }
        }
        else
        {
            if (!File.Exists(InfoPlistPath))
            {
                throw new FileNotFoundException($"Unable to find Apple Icon file: {IcnsPath}");
            }

            if (Path.GetExtension(IcnsPath) != ".icns")
            {
                throw new InvalidOperationException($"Apple Icon path is not a .icns file: {IcnsPath}");
            }
        }
    }

    private void ConfigureOutputDirectory()
    {
        if (string.IsNullOrEmpty(OutputDirectory))
        {
            OutputDirectory = Path.Combine(Path.GetDirectoryName(ProjectPath) ?? ".", "bin", "MonoPacked");
        }
    }

    public static Options FromArgs(ReadOnlySpan<string> args)
    {
        Options options = new Options();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-p":
                case "--project":
                    if (i + 1 < args.Length)
                    {
                        options.ProjectPath = args[++i];
                    }
                    break;

                case "-o":
                case "--output":
                    if (i + 1 < args.Length)
                    {
                        options.OutputDirectory = args[++i];
                    }
                    break;

                case "-r":
                case "--runtime-identifier":
                    if (i + 1 < args.Length)
                    {
                        options.RuntimeIdentifiers.Add(args[++i]);
                    }
                    break;

                case "-rids":
                    if (i + 1 < args.Length)
                    {
                        string[] rids = args[++i].Split(',');
                        foreach (string rid in rids)
                        {
                            options.RuntimeIdentifiers.Add(rid);
                        }
                    }
                    break;

                case "-i":
                case "--info-plist":
                    if (i + 1 < args.Length)
                    {
                        options.InfoPlistPath = args[++i];
                    }
                    break;

                case "-c":
                case "--icns":
                    if (i + 1 < args.Length)
                    {
                        options.IcnsPath = args[++i];
                    }
                    break;

                case "-e":
                case "--executable":
                    if (i + 1 < args.Length)
                    {
                        options.ExecutableFileName = args[++i];
                    }
                    break;

                case "-z":
                case "--zip":
                    options.UseZipCompression = true;
                    break;

                case "-v":
                case "--verbose":
                    options.VerboseOutput = true;
                    break;

                case "--macos-universal":
                    options.MacOSUniversal = true;
                    break;

                case "--publish-args":
                    if (i + 1 < args.Length)
                    {
                        options.PublishArgs = args[++i];
                    }
                    break;

                case "-h":
                case "--help":
                    DisplayHelp();
                    Environment.Exit(0);
                    break;

                default:
                    Console.WriteLine($"Unknown argument: {args[i]}");
                    DisplayHelp();
                    Environment.Exit(1);
                    break;
            }
        }

        return options;
    }

    /// <summary>Displays help information for the MonoPack tool.</summary>
    public static void DisplayHelp()
    {
        Console.WriteLine(
            """
            MonoPack - MonoGame Project Packer
            Usage: monopack [options] [project-path]

            Options:
                -p --project <path>             Path to the .csproj file (optional if only one .csproj in current directory)
                -o --output <dir>               Output directory (default: [project]/bin/Packed)
                -r --runtime-identifier <rid>   Specify target runtime identifier(s) to build for.
                -rids <rids>                    Comma separated list of runtime identifiers to build for (e.g --rids win-x64,linux-x64)
                -i --info-plist <path>          Path to Info.plist file (required when packaging for macOS)
                -c --icns <path>                Path to .icns file (required when packaging for macOS)
                -e --executable <n>             Name of the executable file to set as executable
                -z --zip                        Use zip for packaging instead of tar.gz (only for Linux and MacOS)
                -v --verbose                    Enable verbose output
                --macos-universal               Create a universal .app bundle when both osx-x64 and osx-arm64 are specified
                --publish-args <args>           Custom arguments to pass to dotnet publish (disables default flags)
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

                When --macos-universal is specified and both osx-x64 and osx-arm64 runtime identifiers are
                provided, a single universal .app bundle will be created instead of separate packages for
                each architecture. On macOS, this uses lipo to create true universal binaries. On other
                platforms, it creates a launcher script that selects the correct architecture at runtime.

            Examples:
                monopack
                monopack -h
                monopack -p ./src/MyGame.csproj -o ./artifacts/builds -r win-x64 -r osx-x64 -r osx-arm64 -r linux-x64 -i ./Info.plist -c ./Icon.icns
                monopack -p ./src/MyGame.csproj -o ./artifacts/builds -rids win-x64,osx-x64,osx-arm64,linux-x64 -i ./Info.plist -c ./Icon.icns
                monopack -p ./src/MyGame.csproj -o ./artifacts/builds -rids osx-x64,osx-arm64 -i ./Info.plist -c ./Icon.icns --macos-universal
                monopack -p ./src/MyGame.csproj --publish-args "-p:PublishAot=true --self-contained"
            """
        );
    }
}
