using MonoPack.Builders;
using MonoPack.Packagers;

namespace MonoPack;

/// <summary>Manages the complete MonoGame project packaging workflow.</summary>
internal sealed class MonoPackService
{
    private readonly Options _options;

    /// <summary>Initializes a new instance of the <see cref="MonoPackService"/> class.</summary>
    /// <param name="options">The configuration options for the packaging process.</param>
    public MonoPackService(Options options)
    {
        _options = options;
    }

    /// <summary>Executes the packaging process for the runtime identifiers specified.</summary>
    public void Execute()
    {
        _options.Validate();

        Directory.CreateDirectory(_options.OutputDirectory);

        // Handle universal macOS build if requested
        if (_options.MacOSUniversal &&
           _options.RuntimeIdentifiers.Contains("osx-x64") &&
           _options.RuntimeIdentifiers.Contains("osx-arm64"))
        {
            PackageUniversalMacOS();
        }
        else
        {
            // Standard packaging for each runtime identifier.
            foreach (string rid in _options.RuntimeIdentifiers)
            {
                PackageForRuntime(rid);
            }
        }
    }

    private void PackageUniversalMacOS()
    {
        // Create intermediate directory for build outputs
        string tempDir = Path.Combine(Path.GetTempPath(), "MonoPack", Guid.NewGuid().ToString());
        string x64BuildDir = Path.Combine(tempDir, "osx-x64");
        string arm64BuildDir = Path.Combine(tempDir, "osx-arm64");

        string projectName = Path.GetFileNameWithoutExtension(_options.ProjectPath);

        try
        {
            // Build both architectures
            Console.WriteLine("Building universal macOS bundle (osx-x64 + osx-arm64)");

            IPlatformBuilder x64Builder = PlatformBuilderFactory.CreateBuilder("osx-x64");
            x64Builder.Build(_options.ProjectPath, x64BuildDir, "osx-x64", _options.ExecutableFileName, _options.VerboseOutput, _options.PublishArgs);

            IPlatformBuilder arm64Builder = PlatformBuilderFactory.CreateBuilder("osx-arm64");
            arm64Builder.Build(_options.ProjectPath, arm64BuildDir, "osx-arm64", _options.ExecutableFileName, _options.VerboseOutput, _options.PublishArgs);

            // Create universal package
            UniversalMacOSPackager packager = new UniversalMacOSPackager(_options.InfoPlistPath!, _options.IcnsPath!, x64BuildDir, arm64BuildDir);

            packager.Package(string.Empty, _options.OutputDirectory, projectName, _options.ExecutableFileName, "universal", _options.UseZipCompression);

            // Process remaining non-macOS runtime identifiers
            foreach(string rid in _options.RuntimeIdentifiers)
            {
                if(rid != "osx-x64" && rid != "osx-arm64")
                {
                    PackageForRuntime(rid);
                }
            }
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine($"Error creating universal macOS package: {ex.Message}");

            if(_options.VerboseOutput)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            throw;
        }
        finally
        {
            // Clean up intermediate directory
            if(Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch(IOException ex)
                {
                    if(_options.VerboseOutput)
                    {
                        Console.WriteLine($"Warning: Failed to clean up temporary directory {tempDir}: {ex.Message}");
                    }
                }
            }
        }
    }

    private void PackageForRuntime(string rid)
    {
        // Create intermediate directory for build output
        string tempDir = Path.Combine(Path.GetTempPath(), "MonoPack", Guid.NewGuid().ToString());
        string buildOutputDir = Path.Combine(tempDir, rid);

        // Extract project name from the project path
        string projectName = Path.GetFileNameWithoutExtension(_options.ProjectPath);

        try
        {
            // Build the project for the specified runtime
            IPlatformBuilder builder = PlatformBuilderFactory.CreateBuilder(rid);
            builder.Build(_options.ProjectPath, buildOutputDir, rid, _options.ExecutableFileName, _options.VerboseOutput, _options.PublishArgs);

            // Package the build artifacts
            IPlatformPackager packager = PlatformPackagerFactory.CreatePackager(rid, _options.InfoPlistPath, _options.IcnsPath);
            packager.Package(buildOutputDir, _options.OutputDirectory, projectName, _options.ExecutableFileName, rid, _options.UseZipCompression);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error packaging for {rid}: {ex.Message}");
            if (_options.VerboseOutput)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            throw;
        }
        finally
        {
            // Clean up intermediate directory
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch (IOException ex)
                {
                    // Log but don't fail if cleanup doesn't work
                    if (_options.VerboseOutput)
                    {
                        Console.WriteLine($"Warning: Failed to clean up temporary directory {tempDir}: {ex.Message}");
                    }
                }
            }
        }
    }
}
