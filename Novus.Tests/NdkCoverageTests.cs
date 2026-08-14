using Novus.Tools;

namespace Novus.Tests;

public sealed class NdkCoverageTests
{
    [Fact]
    public void InventoryAndVerifierCoverFunctionsTypesConstantsAndMacros()
    {
        var root = Path.Combine(Path.GetTempPath(), $"novus-coverage-{Guid.NewGuid():N}");
        try
        {
            var ndk = Path.Combine(root, "NDK3.9");
            var raw = Path.Combine(root, "raw");
            Directory.CreateDirectory(Path.Combine(ndk, "Include", "sfd"));
            Directory.CreateDirectory(Path.Combine(ndk, "Include", "fd"));
            Directory.CreateDirectory(Path.Combine(ndk, "Include", "include_h", "clib"));
            Directory.CreateDirectory(Path.Combine(ndk, "Include", "include_h", "devices"));
            Directory.CreateDirectory(Path.Combine(ndk, "Include", "include_h", "resources"));
            Directory.CreateDirectory(raw);
            File.WriteAllText(Path.Combine(ndk, "README"), "Native Developer Kit for AmigaOS 3.9\n");
            File.WriteAllText(Path.Combine(ndk, "Include", "sfd", "demo_lib.sfd"), """
                ==base _DemoBase
                ==libname demo.library
                ==bias 30
                ==public
                ULONG DemoCall(struct Demo *value) (a0)
                """);
            File.WriteAllText(Path.Combine(ndk, "Include", "include_h", "clib", "alib_protos.h"), "");
            File.WriteAllText(Path.Combine(ndk, "Include", "include_h", "devices", "demo.h"), """
                struct Demo { ULONG value; };
                #define DEMO_FLAG 4
                #define DEMO_VALUE(x) ((x)->value)
                """);
            File.WriteAllText(Path.Combine(raw, "demo.novus"), "extern pub fn DemoCall(value: *Demo) -> u32\n");
            File.WriteAllText(Path.Combine(raw, "structs.novus"), "pub struct Demo { value: u32 }\n");
            File.WriteAllText(Path.Combine(raw, "consts.novus"), "pub const DEMO_FLAG: u32 = 4\n");

            var manifest = NdkCoverage.Generate(ndk, raw);

            Assert.Contains(manifest.Symbols, symbol => symbol.Category == "function" && symbol.Name == "DemoCall" && symbol.Status == "DIRECTLY_SUPPORTED");
            Assert.Contains(manifest.Symbols, symbol => symbol.Category == "type" && symbol.Name == "Demo" && symbol.Status == "DIRECTLY_SUPPORTED");
            Assert.Contains(manifest.Symbols, symbol => symbol.Category == "constant" && symbol.Name == "DEMO_FLAG" && symbol.Status == "DIRECTLY_SUPPORTED");
            Assert.Contains(manifest.Symbols, symbol => symbol.Category == "macro" && symbol.Name == "DEMO_VALUE" && symbol.Status == "NOVUS_EQUIVALENT");
            Assert.Empty(NdkCoverage.Verify(manifest, raw, ndk));

            File.AppendAllText(Path.Combine(raw, "demo.novus"), "extern pub fn DemoCall(value: *Demo) -> u32\n");
            Assert.Contains(NdkCoverage.Verify(manifest, raw), error => error.Contains("duplicate raw binding", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void VerifierRejectsAnUnaccountedBinding()
    {
        var raw = Path.Combine(Path.GetTempPath(), $"novus-coverage-raw-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(raw);
            File.WriteAllText(Path.Combine(raw, "demo.novus"), "extern pub fn Expected()\nextern pub fn Extra()\n");
            File.WriteAllText(Path.Combine(raw, "structs.novus"), "pub struct ExtraType {}\n");
            File.WriteAllText(Path.Combine(raw, "consts.novus"), "pub const EXTRA_CONSTANT: u32 = 1\n");
            File.WriteAllText(Path.Combine(raw, "ndk_unsupported_macros.txt"), "UNCLASSIFIED = something\n");
            var manifest = new NdkCoverageManifest
            {
                Baseline = new NdkBaseline { Platform = "classic-68k-amigaos" },
                Symbols =
                [
                    new NdkSymbol
                    {
                        Category = "function", Interface = "demo.library", Name = "Expected",
                        NovusModule = "amiga::raw::demo", Status = "DIRECTLY_SUPPORTED"
                    }
                ]
            };

            var errors = NdkCoverage.Verify(manifest, raw);
            Assert.Contains(errors, error => error.Contains("raw binding is outside pinned baseline", StringComparison.Ordinal));
            Assert.Contains(errors, error => error.Contains("raw type is outside pinned baseline", StringComparison.Ordinal));
            Assert.Contains(errors, error => error.Contains("raw constant is outside pinned baseline", StringComparison.Ordinal));
            Assert.Contains(errors, error => error.Contains("macro is not classified", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(raw)) Directory.Delete(raw, recursive: true);
        }
    }
}
