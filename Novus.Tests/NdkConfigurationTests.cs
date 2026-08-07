using Xunit;

namespace Novus.Tests;

/// <summary>
/// The Amiga NDK cannot be redistributed, so Novus ships none of it and has to be told
/// where the user's copy is. These tests pin both halves of that contract: the compiler
/// must not carry AmigaOS system headers, and it must resolve the NDK from an explicit
/// flag, the environment, or the user's config file.
/// </summary>
public class NdkConfigurationTests
{
    private static string MakeFakeNdk()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ndk-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(Path.Combine(root, "Include", "include_h", "exec"));
        File.WriteAllText(Path.Combine(root, "Include", "include_h", "exec", "types.h"), "/* stub */\n");
        return root;
    }

    [Fact]
    public void ExplicitPath_WinsOverEverythingElse()
    {
        var ndk = MakeFakeNdk();
        try
        {
            Assert.Equal(ndk, UserConfig.ResolveNdkPath(ndk));
        }
        finally
        {
            Directory.Delete(ndk, recursive: true);
        }
    }

    [Fact]
    public void LooksLikeNdk_RejectsADirectoryThatIsNotOne()
    {
        var empty = Path.Combine(Path.GetTempPath(), $"notndk-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(empty);
        try
        {
            Assert.False(UserConfig.LooksLikeNdk(empty));
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    [Fact]
    public void LooksLikeNdk_AcceptsAnNdkTree()
    {
        var ndk = MakeFakeNdk();
        try
        {
            Assert.True(UserConfig.LooksLikeNdk(ndk));
        }
        finally
        {
            Directory.Delete(ndk, recursive: true);
        }
    }

    [Fact]
    public void RequireNdkPath_ExplainsHowToFixIt()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"absent-{Path.GetRandomFileName()}");

        var ex = Assert.Throws<DirectoryNotFoundException>(() => UserConfig.RequireNdkPath(missing));

        // The message is the whole point: a user who has never configured an NDK needs to
        // be told the command, not just that something is missing.
        Assert.Contains("novus config set ndk-path", ex.Message);
    }

    [Fact]
    public void RequireNdkPath_RejectsADirectoryThatIsNotAnNdk()
    {
        var empty = Path.Combine(Path.GetTempPath(), $"notndk-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(empty);
        try
        {
            var ex = Assert.Throws<DirectoryNotFoundException>(() => UserConfig.RequireNdkPath(empty));
            Assert.Contains("does not look like an NDK", ex.Message);
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    [Fact]
    public void ShippedToolchain_ContainsNoAmigaOsSystemHeaders()
    {
        // vbcc's own libc headers sit flat in include/, and inline/ is vbcc-generated.
        // Every other subdirectory (exec/, dos/, intuition/, proto/, clib/ ...) is NDK
        // material and must not be redistributed.
        var includeDir = Path.Combine(
            GetProjectRoot(), "Novus", "bin", "Debug", "net10.0",
            "vendor", "vbcc", "targets", "m68k-amigaos", "include");

        Assert.True(Directory.Exists(includeDir), $"vendored vbcc include dir missing: {includeDir}");

        var shippedSubdirectories = Directory.GetDirectories(includeDir)
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(new[] { "inline" }, shippedSubdirectories);
        Assert.False(File.Exists(Path.Combine(includeDir, "exec", "types.h")),
            "exec/types.h comes from the user's NDK and must not be shipped");
    }

    private static string GetProjectRoot()
    {
        var directory = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(directory, "Novus.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName
                ?? throw new InvalidOperationException("Could not find project root");
        }
        return directory;
    }
}
