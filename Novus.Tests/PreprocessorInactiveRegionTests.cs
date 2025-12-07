using Novus.Diagnostics;
using Novus.Preprocessing;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for the Preprocessor's inactive region tracking.
/// These regions are used by the language server to gray out code
/// that won't be compiled based on current preprocessor settings.
/// </summary>
public class PreprocessorInactiveRegionTests
{
    private Preprocessor CreatePreprocessor(Dictionary<string, bool> constants)
    {
        var diagnostics = new DiagnosticBag();
        return new Preprocessor(constants, diagnostics, "test.novus");
    }

    [Fact]
    public void NoConditionals_NoInactiveRegions()
    {
        var constants = new Dictionary<string, bool> { ["DEBUG"] = true };
        var preprocessor = CreatePreprocessor(constants);

        var result = preprocessor.Process(@"
fn main() {
    return 0
}
");

        Assert.Empty(preprocessor.InactiveRegions);
    }

    [Fact]
    public void TrueCondition_NoInactiveRegions()
    {
        var constants = new Dictionary<string, bool> { ["M68020_PLUS"] = true };
        var preprocessor = CreatePreprocessor(constants);

        var result = preprocessor.Process(@"
#if M68020_PLUS
fn optimized() {
    // 68020+ code
}
#endif
");

        Assert.Empty(preprocessor.InactiveRegions);
    }

    [Fact]
    public void FalseCondition_RecordsInactiveRegion()
    {
        var constants = new Dictionary<string, bool> { ["M68020_PLUS"] = false };
        var preprocessor = CreatePreprocessor(constants);

        var result = preprocessor.Process(@"
#if M68020_PLUS
fn optimized() {
    // 68020+ code
}
#endif
");

        Assert.Single(preprocessor.InactiveRegions);
        var region = preprocessor.InactiveRegions[0];
        Assert.Equal(3, region.StartLine);  // Line after #if
        Assert.Equal(5, region.EndLine);    // Line before #endif
        Assert.Equal("M68020_PLUS", region.Reason);
    }

    [Fact]
    public void ElseBranch_RecordsCorrectInactiveRegion()
    {
        var constants = new Dictionary<string, bool> { ["M68040_PLUS"] = true };
        var preprocessor = CreatePreprocessor(constants);

        var result = preprocessor.Process(@"
#if M68040_PLUS
fn fast() {
    // 68040+ optimized
}
#else
fn slow() {
    // Fallback for older CPUs
}
#endif
");

        // The #else branch should be inactive since M68040_PLUS is true
        Assert.Single(preprocessor.InactiveRegions);
        var region = preprocessor.InactiveRegions[0];
        Assert.Equal(7, region.StartLine);  // Line after #else
        Assert.Equal(9, region.EndLine);    // Line before #endif
        Assert.Contains("previous branch taken", region.Reason);
    }

    [Fact]
    public void IfBranch_WhenElseIsActive()
    {
        var constants = new Dictionary<string, bool> { ["M68040_PLUS"] = false };
        var preprocessor = CreatePreprocessor(constants);

        var result = preprocessor.Process(@"
#if M68040_PLUS
fn fast() {
    // 68040+ optimized
}
#else
fn slow() {
    // Fallback for older CPUs
}
#endif
");

        // The #if branch should be inactive since M68040_PLUS is false
        Assert.Single(preprocessor.InactiveRegions);
        var region = preprocessor.InactiveRegions[0];
        Assert.Equal(3, region.StartLine);  // Line after #if
        Assert.Equal(5, region.EndLine);    // Line before #else
        Assert.Equal("M68040_PLUS", region.Reason);
    }

    [Fact]
    public void NestedConditionals_RecordsMultipleRegions()
    {
        var constants = new Dictionary<string, bool>
        {
            ["M68020_PLUS"] = true,
            ["M68040_PLUS"] = false
        };
        var preprocessor = CreatePreprocessor(constants);

        var result = preprocessor.Process(@"
#if M68020_PLUS
fn outer() {
    #if M68040_PLUS
    // 68040+ only
    #endif
}
#endif
");

        // Only the inner #if M68040_PLUS should be inactive
        Assert.Single(preprocessor.InactiveRegions);
        var region = preprocessor.InactiveRegions[0];
        Assert.Equal(5, region.StartLine);
        Assert.Equal(5, region.EndLine);
        Assert.Equal("M68040_PLUS", region.Reason);
    }

    [Fact]
    public void Elif_RecordsCorrectInactiveRegions()
    {
        var constants = new Dictionary<string, bool>
        {
            ["M68000"] = false,
            ["M68020"] = true,
            ["M68040"] = false
        };
        var preprocessor = CreatePreprocessor(constants);

        var result = preprocessor.Process(@"
#if M68000
fn base_68000() {}
#elif M68020
fn for_68020() {}
#elif M68040
fn for_68040() {}
#endif
");

        // The M68000 branch (lines 3) and M68040 branch (line 7) should be inactive
        Assert.Equal(2, preprocessor.InactiveRegions.Count);

        var region1 = preprocessor.InactiveRegions[0];
        Assert.Equal(3, region1.StartLine);
        Assert.Equal(3, region1.EndLine);
        Assert.Equal("M68000", region1.Reason);

        var region2 = preprocessor.InactiveRegions[1];
        Assert.Equal(7, region2.StartLine);
        Assert.Equal(7, region2.EndLine);
        Assert.Equal("M68040", region2.Reason);
    }

    [Fact]
    public void CpuHierarchy_68020Default_GraysOut68040Code()
    {
        // Simulate default 68020 target
        var constants = new Dictionary<string, bool>
        {
            ["M68020_PLUS"] = true,
            ["M68040_PLUS"] = false
        };
        var preprocessor = CreatePreprocessor(constants);

        var result = preprocessor.Process(@"
fn equals(a: *u8, b: *u8, len: u32) -> bool {
    #if M68020_PLUS
    // Use cmpm.l for fast comparison
    asm { ... }
    #endif

    #if M68040_PLUS
    // Use cache hints for 68040
    asm { ... }
    #endif

    return true
}
");

        // Only the M68040_PLUS block should be inactive
        Assert.Single(preprocessor.InactiveRegions);
        var region = preprocessor.InactiveRegions[0];
        Assert.Equal("M68040_PLUS", region.Reason);
    }

    [Fact]
    public void EmptyBlock_NoRegionIfNoContent()
    {
        var constants = new Dictionary<string, bool> { ["DEBUG"] = false };
        var preprocessor = CreatePreprocessor(constants);

        var result = preprocessor.Process(@"
#if DEBUG
#endif
");

        // An empty block should still be recorded
        // The region spans from line 3 to line 2, which is empty (startLine > endLine)
        // Our preprocessor records startLine = lineNumber + 1 (after #if)
        // and endLine = lineNumber - 1 (before #endif)
        // When they're consecutive, startLine > endLine, meaning no actual content
        Assert.Empty(preprocessor.InactiveRegions);
    }
}
