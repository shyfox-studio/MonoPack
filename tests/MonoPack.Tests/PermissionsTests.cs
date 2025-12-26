using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using NUnit.Framework;
using System.Text;
using System.IO;

namespace MonoPack.Tests;

public class PermissionsTests
{
    static bool RunMonoPack(string[] args)
    {
        // This method would run the MonoPack command with the provided arguments
        // and return the output for verification.
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "ShyFox.MonoPack.exe" : "ShyFox.MonoPack",
                Arguments = string.Join(" ", args),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        process.WaitForExit();
        string output = process.StandardOutput.ReadToEnd();
        if (process.ExitCode != 0)
        {
            Console.WriteLine($"Error: {output}");
            return false;
        }
        return true;
    }

    static string GetExampleProjectPath()
    {
        // This method would return the path to the example project.
        return Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "..", "examples", "ShyFox.MonoPack.Tool.Example");
    }

    static string GetOutputPath()
    {
        // This method would return the path to the output directory.
        return Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
    }

    [Test]
    [TestCase("linux-x64", "linux-x64", "ExampleGame")]
    [TestCase("osx-x64", "osx-x64", "ExampleGame")]
    [TestCase("osx-arm64", "osx-arm64", "ExampleGame")]
    [TestCase("osx-x64,osx-arm64", "universal", "ExampleGame")]
    public void TestZipFileHasCorrectPermissions(string rid, string packageRid, string projectName)
    {
        var output = Path.Combine(GetOutputPath(), "zip");
        var projectPath = GetExampleProjectPath();
        // Run monopack to get a zip file
        Assert.That(RunMonoPack(new[] { "-p", Path.Combine(projectPath, "ShyFox.MonoPack.Tool.Example.csproj"), "-rids", rid, "-c", Path.Combine(projectPath, "Icon.icns"), "-i", Path.Combine(projectPath, "Info.plist"), "-z", "-e", projectName, "-o", output }));
        // Check the permissions of the zip file
        // Check the permissions of the files inside the zip file
        using ZipArchive zipArchive = ZipFile.OpenRead(Path.Combine(output, $"{projectName}-{packageRid}.zip"));
        foreach (ZipArchiveEntry entry in zipArchive.Entries)
        {
            UnixFileMode permissions = (UnixFileMode)((entry.ExternalAttributes >> 16) & 0x1FF);
            Assert.That(permissions > 0);
            Assert.That(permissions.HasFlag(UnixFileMode.UserRead));
            Assert.That(permissions.HasFlag(UnixFileMode.UserWrite));
            Assert.That(permissions.HasFlag(UnixFileMode.GroupRead));
            Assert.That(!permissions.HasFlag(UnixFileMode.GroupWrite));
            Assert.That(permissions.HasFlag(UnixFileMode.OtherRead));
            Assert.That(!permissions.HasFlag(UnixFileMode.OtherWrite));
            if (Path.GetFileName(entry.Name) == projectName)
            {
                Assert.That(permissions.HasFlag(UnixFileMode.UserExecute), $"{projectName} should have UserExecute but was {permissions.PrintFlags()}");
                Assert.That(permissions.HasFlag(UnixFileMode.GroupExecute), $"{projectName} should have GroupExecute but was {permissions.PrintFlags()}");
                Assert.That(permissions.HasFlag(UnixFileMode.OtherExecute), $"{projectName} should have OtherExecute but was {permissions.PrintFlags()}");

            }
            Assert.That(entry.Length > 0);
        }
    }

    [Test]
    public void TestTarFileHasCorrectPermissions()
    {
        var output = Path.Combine(GetOutputPath(), "tar");
        var projectPath = GetExampleProjectPath();
        var projectName = "ExampleGame";
        Assert.That(RunMonoPack(new[] { "-p", Path.Combine (projectPath, "ShyFox.MonoPack.Tool.Example.csproj"), "-r", "linux-x64", "-e", projectName, "-o", output }));
        using (FileStream fileStream = new FileStream(Path.Combine(output, $"{projectName}-linux-x64.tar.gz"), FileMode.Open, FileAccess.Read))
        using (GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
        using (TarReader tarReader = new TarReader(gzipStream))
        {
            TarEntry? entry;
            while ((entry = tarReader.GetNextEntry()) != null)
            {
                var permissions = (UnixFileMode)entry.Mode;
                Assert.That(permissions > 0);
                Assert.That(permissions.HasFlag(UnixFileMode.UserRead));
                Assert.That(permissions.HasFlag(UnixFileMode.UserWrite));
                Assert.That(permissions.HasFlag(UnixFileMode.GroupRead));
                Assert.That(!permissions.HasFlag(UnixFileMode.GroupWrite));
                Assert.That(permissions.HasFlag(UnixFileMode.OtherRead));
                Assert.That(!permissions.HasFlag(UnixFileMode.OtherWrite));
                if (Path.GetFileName(entry.Name) == projectName)
                {
                    Assert.That(permissions.HasFlag(UnixFileMode.UserExecute), $"{entry.Name} should have UserExecute but was {permissions.PrintFlags()}");
                    Assert.That(permissions.HasFlag(UnixFileMode.GroupExecute), $"{entry.Name} should have GroupExecute but was {permissions.PrintFlags()}");
                    Assert.That(permissions.HasFlag(UnixFileMode.OtherExecute), $"{entry.Name} should have OtherExecute but was {permissions.PrintFlags()}");
                }
            }
        }
    }
}

public static class EnumExtensions
{
    public static string PrintFlags<T>(this T value) where T : Enum
    {
        var sb = new StringBuilder();
        foreach (T flag in Enum.GetValues(typeof(T)))
        {
            if (Convert.ToInt64(flag) != 0 && value.HasFlag(flag))
            {
                sb.Append(flag.ToString() + ",");
            }
        }
        return sb.ToString().TrimEnd(',');
    }
}
