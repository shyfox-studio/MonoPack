using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;
using System.Xml.XPath;

namespace MonoPack.Packagers;

/// <summary>Packaging strategy for macOS platform.</summary>
internal sealed class MacOSPackager : PlatformPackager
{
    private readonly string _infoPlistPath;
    private readonly string _icnsPath;

    public MacOSPackager(string infoPlistPath, string icnsPath)
    {
        _infoPlistPath = infoPlistPath;
        _icnsPath = icnsPath;
    }

    /// <inheritdoc/>
    public override void Package(string sourceDir, string outputDir, string projectName, string? executableName, string rid, bool useZip)
    {
        string appName = executableName ?? projectName;
        string appDir = Path.Combine(outputDir, $"{appName}.app");
        string contentsDir = Path.Combine(appDir, "Contents");
        string macOSDir = Path.Combine(contentsDir, "MacOS");
        string macOSContentDir = Path.Combine(macOSDir, "Content");
        string resourcesDir = Path.Combine(contentsDir, "Resources");
        string resourcesContentDir = Path.Combine(resourcesDir, "Content");


        // Create app bundle directories
        Directory.CreateDirectory(contentsDir);
        Directory.CreateDirectory(macOSDir);
        Directory.CreateDirectory(resourcesDir);

        // Copy Info.plist
        string infoPlistDest = Path.Combine(contentsDir, "Info.plist");
        File.Copy(_infoPlistPath, infoPlistDest, overwrite: true);

        // Copy the icns file
        string icnsDest = Path.Combine(resourcesDir, Path.GetFileName(_icnsPath));
        File.Copy(_icnsPath, icnsDest, overwrite: true);

        CopyDirectory(sourceDir, macOSDir);

        // Move the monogame "Content" directory that was just copied to the
        // MacOS directory to the Resources directory
        DeleteDirectory(resourcesContentDir, recursive: true);
        Directory.Move(macOSContentDir, resourcesContentDir);

        // Validate Info.plist configuration
        ValidateInfoPlist(infoPlistDest, macOSDir);

        string archivePath = Path.Combine(outputDir, $"{executableName ?? projectName}-{rid}.{(useZip ? "zip" : "tar.gz")}");
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using FileStream fileStream = File.OpenWrite(archivePath);

        if (useZip)
        {
            using ZipArchive zip = new ZipArchive(fileStream, ZipArchiveMode.Create);
            ZipDirectory(appDir, true, zip, executableName, projectName, archivePath);
        }
        else
        {
            using GZipStream gzStream = new GZipStream(fileStream, CompressionMode.Compress);
            TarDirectory(appDir, true, gzStream, executableName, projectName, archivePath);
        }

        DeleteDirectory(sourceDir, recursive: true);
        Console.WriteLine($"Created macOS archive: {archivePath}");
    }

    private static void ValidateInfoPlist(string infoPlistPath, string macOSDir)
    {
        try
        {
            XDocument plist = XDocument.Load(infoPlistPath);
            XElement? dict = plist.Root?.Element("dict");

            if (dict == null)
            {
                Console.WriteLine("Warning: Could not parse Info.plist structure");
                return;
            }

            // Find CFBundleExecute value
            string? bundleExecutable = null;
            XElement[] elements = dict.Elements().ToArray();

            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i].Name == "key" && elements[i].Value == "CFBundleExecutable")
                {
                    bundleExecutable = elements[i + 1].Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(bundleExecutable))
            {
                Console.WriteLine("Warning: CFBundleExecutable not found in Info.plist");
                return;
            }

            // Check if the executable exists in the MacOS directory
            string expectedExecutablePath = Path.Combine(macOSDir, bundleExecutable);

            if (!File.Exists(expectedExecutablePath))
            {
                // List available executables by excluding known library/data file extensions.
                // We can't just use Path.GetExtension() along because it treats names like "MonoPack.Example"
                // as having extension ".Example", when it's actually an executable.
                string[] knownNonExecutableExtensions =
                [
                    ".dll", ".pdb", ".dylib", ".so", ".a",
                    ".config", ".json", ".xml", ".yml", ".md", ".txt", ".dat",
                    ".xnb"
                ];

                // List available executables
                string?[] availableExecutables = Directory.GetFiles(macOSDir)
                                                          .Where(f =>
                                                          {
                                                              string ext = Path.GetExtension(f).ToLowerInvariant();
                                                              return !knownNonExecutableExtensions.Contains(ext);
                                                          })

                                                          .Select(Path.GetFileName)
                                                          .ToArray();

                Console.WriteLine();
                Console.WriteLine("ERROR: Info.plist validation failed!");
                Console.WriteLine($"  CFBundleExecutable specifies: {bundleExecutable}");
                Console.WriteLine($"  But this file does not exist in the app bundle.");
                Console.WriteLine();

                if (availableExecutables.Length > 0)
                {
                    Console.WriteLine("  Available executables in the MacOS directory");
                    foreach (string? exec in availableExecutables)
                    {
                        Console.WriteLine($"    - {exec}");
                    }
                    Console.WriteLine();
                    Console.WriteLine("  Update your Info.plist to set CFBundleExecutable to one of these names,");
                    Console.WriteLine("  or use the -e/--executable option to specify the correct name.");
                }
                else
                {
                    Console.WriteLine("  No executable files found in the MacOS directory.");
                }

                Console.WriteLine();
                Console.WriteLine("  The app bundle will be created, but will NOT launch on macOS.");
                Console.WriteLine("  Users will see: \"You can't open the application because it may be damaged or incomplete.\"");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not validate Info.plist: {ex.Message}");
        }
    }
}
