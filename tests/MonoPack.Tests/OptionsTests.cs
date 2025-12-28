namespace MonoPack.Tests;

public sealed class OptionsTests : TestBase
{

    public OptionsTests() : base() { }

    [Fact]
    public void Options_WithNoProjectPath_ShouldAutoDetect()
    {
        // Arrange
        string outputDir = Path.Combine(_testOutputRoot, "auto-detect");

        Options options = new Options
        {
            ProjectPath = string.Empty, // Will be auto-detected
            OutputDirectory = outputDir,
            VerboseOutput = false
        };

        options.RuntimeIdentifiers.Add("win-x64");

        // Change to the example project directory temporarily
        string originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_exampleProjectPath);

            // Act & Assert
            options.Validate(); // Should auto-detect the project

            Assert.NotEmpty(options.ProjectPath);
            Assert.EndsWith(".csproj", options.ProjectPath);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void Options_WithInvalidProjectPath_ShouldThrow()
    {
        // Arrange
        Options options = new Options
        {
            ProjectPath = "nonexistent.csproj",
            OutputDirectory = _testOutputRoot
        };

        options.RuntimeIdentifiers.Add("win-x64");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => options.Validate());
    }

    [Fact]
    public void Options_MacOSWithoutInfoPlist_ShouldThrow()
    {
        // Arrange
        Options options = new Options
        {
            ProjectPath = Path.Combine(_exampleProjectPath, $"{ProjectName}.csproj"),
            OutputDirectory = _testOutputRoot,
            InfoPlistPath = null, // Missing required file
            IcnsPath = Path.Combine(_exampleProjectPath, "Icon.icns")
        };

        options.RuntimeIdentifiers.Add("osx-x64");

        // Act & Assert - Should throw when trying to validate
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Info.plist", exception.Message);
    }

    [Fact]
    public void Options_MacOSWithoutIcns_ShouldThrow()
    {
        // Arrange
        Options options = new Options
        {
            ProjectPath = Path.Combine(_exampleProjectPath, $"{ProjectName}.csproj"),
            OutputDirectory = _testOutputRoot,
            InfoPlistPath = Path.Combine(_exampleProjectPath, "Info.plist"),
            IcnsPath = null // Missing required file
        };

        options.RuntimeIdentifiers.Add("osx-x64");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains(".icns", exception.Message);
    }
}
