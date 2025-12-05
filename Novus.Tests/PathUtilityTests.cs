using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for the PathUtility class.
/// Ensures cross-platform path handling works correctly.
/// </summary>
public class PathUtilityTests
{
    [Theory]
    [InlineData("foo/bar/baz.cs", "foo/bar/baz.cs")]
    [InlineData("foo\\bar\\baz.cs", "foo/bar/baz.cs")]
    [InlineData("foo\\bar/baz.cs", "foo/bar/baz.cs")]
    [InlineData("C:\\Users\\test\\file.cs", "C:/Users/test/file.cs")]
    public void NormalizeForStorage_ConvertsToForwardSlashes(string input, string expected)
    {
        var result = PathUtility.NormalizeForStorage(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("foo/bar", "foo/bar", true)]
    [InlineData("foo\\bar", "foo/bar", true)]
    [InlineData("foo/bar/baz", "foo\\bar\\baz", true)]
    [InlineData("foo/bar", "foo/baz", false)]
    public void PathsEqual_ComparesIgnoringSeparatorStyle(string path1, string path2, bool expected)
    {
        var result = PathUtility.PathsEqual(path1, path2);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/path/to/target/file.c", "target", true)]
    [InlineData("C:\\path\\to\\target\\file.c", "target", true)]
    [InlineData("/path/to/build/file.c", "build", true)]
    [InlineData("/path/to/src/file.c", "target", false)]
    [InlineData("/path/to/src/file.c", "build", false)]
    [InlineData("target/file.c", "target", true)]
    [InlineData("/path/target", "target", true)]
    public void ContainsDirectory_ChecksBothSeparatorStyles(string path, string dirName, bool expected)
    {
        var result = PathUtility.ContainsDirectory(path, dirName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetVbccPath_ReturnsNonEmptyPath()
    {
        // This test verifies the method doesn't throw and returns something
        var result = PathUtility.GetVbccPath();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void GetNdkPath_ReturnsNonEmptyPath()
    {
        // This test verifies the method doesn't throw and returns something
        var result = PathUtility.GetNdkPath();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void FindProjectRoot_FromProjectDirectory_ReturnsRoot()
    {
        // Should find the project root from the test execution directory
        var result = PathUtility.FindProjectRoot();

        // Should find a directory containing Novus.sln
        Assert.NotNull(result);
        Assert.True(File.Exists(Path.Combine(result!, "Novus.sln")),
            $"Expected Novus.sln in {result}");
    }

    [Fact]
    public void FindStdLibPath_ReturnsValidPath()
    {
        var result = PathUtility.FindStdLibPath();

        // In the test environment, we should be able to find the std lib
        Assert.NotNull(result);
        Assert.True(Directory.Exists(result),
            $"Expected std lib directory to exist at {result}");
    }

    [Fact]
    public void NormalizeForPlatform_UsesCurrentPlatformSeparator()
    {
        var input = "foo/bar\\baz";
        var result = PathUtility.NormalizeForPlatform(input);

        // Result should only contain the current platform's separator
        if (Path.DirectorySeparatorChar == '/')
        {
            Assert.DoesNotContain("\\", result);
            Assert.Equal("foo/bar/baz", result);
        }
        else
        {
            Assert.DoesNotContain("/", result);
            Assert.Equal("foo\\bar\\baz", result);
        }
    }
}
