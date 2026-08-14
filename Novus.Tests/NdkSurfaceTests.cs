using Xunit;

namespace Novus.Tests;

public class NdkSurfaceTests
{
    private static readonly string ProjectRoot = FindProjectRoot();
    private static readonly InProcessCompiler Compiler = new(Path.Combine(ProjectRoot, "Novus", "std"));

    public static IEnumerable<object[]> GeneratedModules()
    {
        var map = Path.Combine(ProjectRoot, "Novus", "std", "amiga", "raw", "ndk_headers.txt");
        return File.ReadLines(map)
            .Select(line => line.Split('|')[0])
            .Select(CanonicalModule)
            .Append("consts")
            .Append("structs")
            .Append("amiga_lib")
            .Append("hdwrench")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new object[] { name });
    }

    [Fact]
    public void CriticalDeviceAndResourceAbiIsPresent()
    {
        var raw = Path.Combine(ProjectRoot, "Novus", "std", "amiga", "raw");
        var structs = File.ReadAllText(Path.Combine(raw, "structs.novus"));
        var constants = File.ReadAllText(Path.Combine(raw, "consts.novus"));

        foreach (var type in new[] { "IOClipReq", "IOExtPar", "IOExtSer", "GamePortTrigger", "narrator_rb", "SCSICmd", "IOExtTD", "FileSysResource" })
            Assert.Contains($"pub struct {type}", structs);
        foreach (var constant in new[] { "CBD_CHANGEHOOK", "CD_READ", "CMD_READ", "CMD_WRITE", "GPD_READEVENT", "HD_SCSICMD", "PRD_QUERY", "SDCMD_QUERY", "TD_GETGEOMETRY" })
            Assert.Contains($"pub const {constant}:", constants);
    }

    [Fact]
    public void HdwrenchMetadataRequiresV44WithoutIncludingItsBrokenPrototypeHeader()
    {
        var raw = Path.Combine(ProjectRoot, "Novus", "std", "amiga", "raw");
        var metadata = Assert.IsType<Compilation.FfiModuleMetadata>(
            Compilation.FfiModuleMetadata.TryRead(Path.Combine(raw, "hdwrench.novus")));

        Assert.Equal(44, metadata.MinimumVersion);
        Assert.Equal(44, metadata.FunctionVersions["QueryCapacity"]);
        Assert.DoesNotContain("libraries/hdwrench.h", metadata.Headers);
    }

    private static string CanonicalModule(string module) => module switch
    {
        _ when module.EndsWith("_device", StringComparison.Ordinal) => $"devices::{module[..^7]}",
        _ when module.EndsWith("_resource", StringComparison.Ordinal) => $"resources::{module[..^9]}",
        _ => module
    };

    [Theory]
    [Trait("Category", "CorpusCompilation")]
    [MemberData(nameof(GeneratedModules))]
    public async Task EveryGeneratedNdkModuleIsImportable(string moduleName)
    {
        var source = Path.Combine(Path.GetTempPath(), $"novus-ndk-{moduleName}-{Guid.NewGuid():N}.novus");
        try
        {
            await File.WriteAllTextAsync(source, $$"""
                from amiga::raw::{{moduleName}} import *

                pub fn main() -> i32 {
                    return 0
                }
                """);

            var result = await Compiler.CompileAndVerifyAsync(source);
            Assert.True(result.Success, $"amiga::raw::{moduleName} is not importable:\n{result.ErrorMessage}");
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
