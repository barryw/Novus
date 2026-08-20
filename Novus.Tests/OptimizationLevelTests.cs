using Novus;
using Novus.Commands;
using Novus.Toolchain;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// vbcc used to miscompile at -O=3. Its emit-level peephole collapsed a
/// "move.l d0,a1 / move.l a1,d0" round trip and removed the second move - the only
/// instruction that set the condition codes. The preceding tst had already been elided
/// on the strength of it, so the branch read whatever the callee happened to leave:
///
///     jsr     _ReadArgs
///     move.l  d0,a1          ; MOVEA - sets no flags
///     add.w   #12,a7         ; ADDQ to An - sets no flags
///     bne     l44            ; branched on stale flags
///
/// Args::parse took the failure path under vamos and the success path on a real 68040,
/// from the same binary. Fixed in vendor/vbcc by widening the peephole's guard, which
/// only covered cc_set naming the address register, to also cover it naming the data
/// register the removed move writes.
///
/// The fix only reaches builds because the toolchain pins PATH to the vendored vbcc;
/// see VbccToolchain.RunTool.
/// </summary>
public class OptimizationLevelTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SupportedLevelsAreAccepted(int level)
    {
        CompilerOptions.ValidateOptimizationLevel(level);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void OutOfRangeLevelsAreRejected(int level)
    {
        Assert.Throws<ArgumentException>(() => CompilerOptions.ValidateOptimizationLevel(level));
    }

    [Fact]
    public void DefaultIsOne()
    {
        // -O=2 was the old default and is the largest level measured; -O=1 is both
        // smaller and correct. -O=3 is smaller still and now correct, but is not the
        // default until it has been exercised more widely than one repro.
        var options = new CompilerOptions();
        Assert.Equal(1, options.OptimizationLevel);
        Assert.InRange(options.OptimizationLevel, 0, CompilerOptions.MaxOptimizationLevel);
    }

    [Fact]
    public void ReleaseBuildDefaultsToWholeProgramOptimizationLevel()
    {
        Assert.Equal(3, BuildCommand.ReleaseOptimizationLevel);
    }

    [Fact]
    public void WholeProgramCompileUsesFinalSizeOptimizationAndKeepsEveryInput()
    {
        var args = VbccToolchain.BuildWholeProgramCompileArguments(
            new[] { "/tmp/a.c", "/tmp/b.c" }, "/tmp/all.o", "68020", 3,
            "/ndk/include", new[] { "/tmp" }, enableFpu: false);

        Assert.Contains("-final", args);
        Assert.Contains("-size", args);
        Assert.Contains("-sec-per-obj", args);
        Assert.Contains("-O3", args);
        Assert.Equal(new[] { "/tmp/a.c", "/tmp/b.c" }, args.TakeLast(2));
    }

    [Fact]
    public void WholeProgramCacheKeyCoversEveryInputAndBuildSetting()
    {
        var inputs = new[] { ("/tmp/a.c", "aaa"), ("/tmp/b.c", "bbb") };
        var key = Program.ComputeWholeProgramCacheKey(inputs, "types", "68020", "soft", 3);

        Assert.Equal(key, Program.ComputeWholeProgramCacheKey(inputs.Reverse(), "types", "68020", "soft", 3));
        Assert.NotEqual(key, Program.ComputeWholeProgramCacheKey(
            new[] { ("/tmp/a.c", "changed"), ("/tmp/b.c", "bbb") }, "types", "68020", "soft", 3));
        Assert.NotEqual(key, Program.ComputeWholeProgramCacheKey(inputs, "types", "68040", "soft", 3));
    }

    [Fact]
    public void GraphicsA5InlineCallsPreserveTheForcedFramePointer()
    {
        var root = PathUtility.FindProjectRoot()
            ?? throw new InvalidOperationException("Novus project root not found");
        var header = File.ReadAllText(Path.Combine(
            root, "vendor", "vbcc", "targets", "m68k-amigaos",
            "include", "inline", "graphics_protos.h"));

        foreach (var function in new[] { "LockLayerRom", "UnlockLayerRom", "AttemptLockLayerRom" })
        {
            var declaration = header.Split('\n').Single(line => line.StartsWith($"VOID __{function}(") ||
                line.StartsWith($"BOOL __{function}("));
            Assert.Contains("__reg(\"a0\") struct Layer * layer", declaration);
            Assert.Contains("move.l\\ta5,-(sp)\\n\\tmove.l\\ta0,a5", declaration);
            Assert.Contains("move.l\\t(sp)+,a5", declaration);
            Assert.DoesNotContain("__reg(\"a5\")", declaration);
        }
    }

    [Fact]
    public void TestCommandDefaultsMatchBuildMode()
    {
        Assert.Equal(0, new TestOptions().GetOptimizationLevel());
        Assert.Equal(1, new TestOptions { Release = true }.GetOptimizationLevel());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void TestCommandHonorsExplicitOptimizationLevel(int level)
    {
        Assert.Equal(level, new TestOptions { Release = true, OptimizationLevel = level }.GetOptimizationLevel());
    }

    [Fact]
    public void TestCommandRejectsInvalidOptimizationLevel()
    {
        Assert.Throws<ArgumentException>(() =>
            new TestOptions { OptimizationLevel = 4 }.GetOptimizationLevel());
    }
}
