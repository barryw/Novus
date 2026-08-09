using Xunit;

namespace Novus.Tests;

public class NdkSurfaceTests
{
    private static readonly string ProjectRoot = FindProjectRoot();
    private static readonly InProcessCompiler Compiler = new(Path.Combine(ProjectRoot, "Novus", "std"));

    public static IEnumerable<object[]> GeneratedModules()
    {
        var map = Path.Combine(ProjectRoot, "Novus", "std", "ffi", "ndk_headers.txt");
        return File.ReadLines(map)
            .Select(line => line.Split('|')[0])
            .Append("amiga_consts")
            .Append("amiga_structs")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new object[] { name });
    }

    [Theory]
    [MemberData(nameof(GeneratedModules))]
    public async Task EveryGeneratedNdkModuleIsImportable(string moduleName)
    {
        var source = Path.Combine(Path.GetTempPath(), $"novus-ndk-{moduleName}-{Guid.NewGuid():N}.novus");
        try
        {
            await File.WriteAllTextAsync(source, $$"""
                from std::ffi::{{moduleName}} import *

                pub fn main() -> i32 {
                    return 0
                }
                """);

            var result = await Compiler.CompileAndVerifyAsync(source);
            Assert.True(result.Success, $"std::ffi::{moduleName} is not importable:\n{result.ErrorMessage}");
        }
        finally
        {
            File.Delete(source);
        }
    }

    private static string FindProjectRoot()
    {
        var directory = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(directory, "Novus.sln")))
            directory = Directory.GetParent(directory)?.FullName
                ?? throw new DirectoryNotFoundException("Could not find Novus.sln");
        return directory;
    }
}
