using System.Diagnostics;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// The compiler ships its own resources (runtime/, std/, stubs/) beside its binary.
/// Resolving them by walking a fixed number of parent directories only works for the
/// layout that count was written against, so an installed compiler silently loses
/// files the dev build finds. A missing runtime object does not fail the compile —
/// it fails the link, with an undefined symbol in generated C.
///
/// These tests pin the resolution to the binary's own directory and prove a compiler
/// still works when its directory sits at a different depth.
/// </summary>
public class CompilerResourceResolutionTests
{
    [Fact]
    public void FindRuntimeFile_LocatesFileBesideBinary_AtAnyDepth()
    {
        var deep = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "a", "b", "c", "d");
        var shallow = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        foreach (var baseDir in new[] { deep, shallow })
        {
            Directory.CreateDirectory(Path.Combine(baseDir, "runtime"));
            File.WriteAllText(Path.Combine(baseDir, "runtime", "novus_io.s"), "; stub\n");

            var found = PathUtility.FindRuntimeFile("novus_io.s", baseDir);

            Assert.NotNull(found);
            Assert.True(File.Exists(found));
        }
    }

    [Fact]
    public void FindRuntimeFile_ReturnsNull_WhenMissing()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(baseDir, "runtime"));

        Assert.Null(PathUtility.FindRuntimeFile("novus_io.s", baseDir));
    }

    [Fact]
    public void RuntimeAssembly_ShipsBesideCompilerBinary()
    {
        // novus_io.s defines _write, which std::io::file declares as `extern fn write`.
        // Every program that prints needs it, so its absence breaks the whole toolchain.
        var compilerDir = Path.GetDirectoryName(typeof(CompilerResourceResolutionTests).Assembly.Location)!;
        var runtimeAsm = Path.Combine(compilerDir, "runtime", "novus_io.s");

        Assert.True(File.Exists(runtimeAsm),
            $"novus_io.s must be copied next to the compiler binary; not found at {runtimeAsm}");
    }

    [Fact]
    public void VendoredToolchain_ShipsBesideCompilerBinary()
    {
        // Without its own vbcc, an installed compiler falls back to whatever is on the
        // machine. A stock vbcc has no aos68k_fpu config and none of Novus's optimizer
        // patches, so it either fails outright or miscompiles.
        var compilerDir = Path.Combine(GetProjectRoot(), "Novus", "bin", "Debug", "net10.0");
        var vbccDir = Path.Combine(compilerDir, "vendor", "vbcc");

        Assert.True(File.Exists(Path.Combine(vbccDir, "bin", "vc")),
            $"vendored vbcc must ship beside the compiler; no bin/vc under {vbccDir}");
        Assert.True(File.Exists(Path.Combine(vbccDir, "config", "aos68k_fpu")),
            "vendored vbcc must include the aos68k_fpu target config");
        Assert.True(File.Exists(Path.Combine(vbccDir, "targets", "m68k-amigaos", "lib", "startup.o")),
            "vendored vbcc must include the m68k-amigaos target lib");
    }

    /// <summary>
    /// End-to-end guard: the compiler is copied to a temp directory at an unrelated
    /// depth (as an installed/published one would be) and must still build a program
    /// with no toolchain on the machine and no NDK.
    /// </summary>
    [Fact]
    [Trait("Category", "FullCompilation")]
    public void CompilerRelocatedToDifferentDepth_StillLinks()
    {
        var projectRoot = GetProjectRoot();
        var devOutput = Path.Combine(projectRoot, "Novus", "bin", "Debug", "net10.0");
        Assert.True(File.Exists(Path.Combine(devOutput, "Novus.dll")),
            $"Compiler not built at {devOutput}. Build the project first.");

        var relocated = Path.Combine(Path.GetTempPath(), $"novus-relocated-{Path.GetRandomFileName()}");
        try
        {
            CopyDirectory(devOutput, relocated);

            var source = Path.Combine(relocated, "print_smoke.novus");
            File.WriteAllText(source,
                "from std::io::file import print\n\n" +
                "pub fn main() -> i32 {\n" +
                "    print(\"ok\\n\")\n" +
                "    return 0\n" +
                "}\n");

            var output = Path.Combine(relocated, "print_smoke");
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                // No --vbcc-path and no NDK: the compiler carries its own toolchain in
                // vendor/vbcc, so a relocated copy must build a program entirely on its own.
                Arguments = $"\"{Path.Combine(relocated, "Novus.dll")}\" compile \"{source}\" -o \"{output}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Clear every toolchain hint, so a vbcc that happens to be installed on the
            // build machine cannot stand in for the one the compiler is supposed to ship.
            foreach (var hint in new[] { "VBCC", "VBCC_PATH", "NDK", "NDK_PATH", "NDK39" })
            {
                startInfo.Environment.Remove(hint);
            }

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0,
                $"Relocated compiler failed with exit code {process.ExitCode}.\nOutput:\n{stdout}\nErrors:\n{stderr}");
            Assert.True(File.Exists(output),
                $"Relocated compiler produced no executable.\nOutput:\n{stdout}\nErrors:\n{stderr}");
        }
        finally
        {
            if (Directory.Exists(relocated))
                Directory.Delete(relocated, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
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
