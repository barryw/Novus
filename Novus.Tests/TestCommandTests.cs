using Novus.Commands;

namespace Novus.Tests;

public class TestCommandTests
{
    [Fact]
    public async Task NoMatchingFilterRemovesStaleTestExecutable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"novus-test-{Guid.NewGuid():N}");
        var output = Path.Combine(root, "out");
        Directory.CreateDirectory(output);
        try
        {
            var source = Path.Combine(root, "sample.novus");
            await File.WriteAllTextAsync(source, "@test(\"sample\")\npub fn test_sample() {}\n");
            await File.WriteAllTextAsync(Path.Combine(output, "tests"), "stale");
            await File.WriteAllTextAsync(Path.Combine(output, "tests.novus-build"), "stale");
            await File.WriteAllTextAsync(Path.Combine(output, "_test_runner.novus"), "stale");

            var options = new TestOptions
            {
                Path = source,
                OutputDir = output,
                Filter = "does_not_match",
                CacheDirectory = "shared-test-cache",
            };
            var result = await TestCommand.Run(options);

            Assert.Equal(0, result);
            Assert.True(Path.IsPathFullyQualified(options.CacheDirectory));
            Assert.False(File.Exists(Path.Combine(output, "tests")));
            Assert.False(File.Exists(Path.Combine(output, "tests.novus-build")));
            Assert.False(File.Exists(Path.Combine(output, "_test_runner.novus")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
