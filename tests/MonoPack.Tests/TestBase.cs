namespace MonoPack.Tests;

public abstract class TestBase : IDisposable
{
    protected const string ProjectName = "MonoPack.Example";

    protected readonly string _testOutputRoot;
    protected readonly string _exampleProjectPath;

    protected TestBase()
    {
        _testOutputRoot = Path.Combine(Path.GetTempPath(), "MonoPack.Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testOutputRoot);

        string currentDir = Directory.GetCurrentDirectory();
        string? solutionDir = FindSolutionDirectory(currentDir);

        if (solutionDir == null)
        {
            throw new InvalidOperationException("Could not locate solution directory");
        }

        _exampleProjectPath = Path.Combine(solutionDir, "examples", "MonoPack.Example");

        if (!Directory.Exists(_exampleProjectPath))
        {

        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_testOutputRoot))
        {
            try
            {
                Directory.Delete(_testOutputRoot, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    // Finds the solution directory by searching upward from the current directory.
    private static string? FindSolutionDirectory(string startPath)
    {
        DirectoryInfo? dir = new DirectoryInfo(startPath);

        while (dir != null)
        {
            if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return null;
    }

    internal Options CreateOptions(string rid, bool useZip = false)
    {
        string outputDir = Path.Combine(_testOutputRoot, rid);

        Options options = new Options
        {
            ProjectPath = Path.Combine(_exampleProjectPath, $"{ProjectName}.csproj"),
            OutputDirectory = outputDir,
            ExecutableFileName = ProjectName,
            UseZipCompression = useZip,
            VerboseOutput = true
        };

        options.RuntimeIdentifiers.Add(rid);

        if (rid.StartsWith("osx", StringComparison.Ordinal))
        {
            options.InfoPlistPath = Path.Combine(_exampleProjectPath, "Info.plist");
            options.IcnsPath = Path.Combine(_exampleProjectPath, "Icon.icns");
        }

        return options;
    }
}
