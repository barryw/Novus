# Per-CPU NDK Build Cache Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the full Novus NDK (all three `amiga::` tiers plus `std::`) once per target CPU into a persistent user-level cache that survives compiler rebuilds and is reused automatically when compiling applications.

**Architecture:** The per-CPU cache already exists and is keyed correctly; it just lives in the compiler's `bin/` directory and has no way to be populated deliberately. Path resolution moves out of `Program.cs` into a small focused `NdkCachePaths` class rooted at `~/.novus/ndk`, and `StdlibBuildCommand` is rewritten from a stub into a real build that compiles a generated synthetic root importing every stdlib module and harvests the resulting objects.

**Tech Stack:** C# / .NET 10, CommandLine parser verbs, xUnit, VBCC toolchain.

## Global Constraints

- Cache layout is exactly `<root>/<cpu>/<mode>/<fpu>-O<opt>-S<safety>/<abi-hash>/` — do not change the key structure, only the root.
- Default root is `~/.novus/ndk`; `NOVUS_NDK_CACHE` overrides it.
- `cpu` value `auto` resolves to `68020` for keying.
- Variant directories built by `stdlib-build`: debug = `auto-O1-S2`, release = `auto-O1-S1`, read from `compile`'s own defaults rather than hardcoded literals.
- A cache entry is only reused when the manifest `codegenVersion` matches the running compiler **and** the stored `novus_types.h.hash` matches. Stale entries are ignored, never repaired, never linked.
- Every test that touches a cache must point `NOVUS_NDK_CACHE` at a temp directory. No test may read or write the developer's real cache.
- Purge deletes only inside the resolved root, never follows symlinks, and refuses to run against a filesystem root or the bare home directory.

**Sequencing note (deviation from the spec's order):** the spec listed the synthetic-root spike first because it could change the build mechanism. Tasks 1 and 2 below are independent of that outcome and deliver the main pain fix (the cache surviving compiler rebuilds), so they ship first. The spike is Task 3, before any command is built around it.

**Simplification vs the spec:** the spec said the build should stop after object generation and skip linking. Linking the synthetic root anyway is simpler — the harvest already happens as a side effect of a normal compile, so no new "stop after objects" plumbing is needed — and costs one link per variant (8 total for 4 CPUs × 2 modes). Task 4 uses a normal compile and discards the output binary.

---

## File Structure

| File | Responsibility |
| --- | --- |
| `Novus/NdkCachePaths.cs` (create) | Resolve cache root, variant string, key path, CPU normalisation, purge guard. Pure path logic, no I/O beyond existence checks. |
| `Novus/Program.cs` (modify, ~line 2424) | Use `NdkCachePaths.Root` instead of `Path.Combine(compilerDir, "stdlib")`. |
| `Novus/Commands/CleanCommand.cs` (modify) | Also delete the new root; keep deleting the old one. |
| `Novus/NdkRootGenerator.cs` (create) | Generate the synthetic root source that imports every stdlib module. |
| `Novus/Commands/StdlibBuildCommand.cs` (rewrite `BuildForTarget`) | Compile the synthetic root per variant and harvest objects. |
| `Novus/StdlibBuildOptions.cs` (modify) | Add `--overwrite` and `--purge`. |
| `Novus.Tests/NdkCachePathsTests.cs` (create) | Unit tests for path logic and the purge guard. |
| `Novus.Tests/NdkCacheBuildTests.cs` (create) | Integration tests for build, reuse, non-reuse, overwrite, purge. |

---

### Task 1: Cache path resolution

**Files:**
- Create: `Novus/NdkCachePaths.cs`
- Test: `Novus.Tests/NdkCachePathsTests.cs`

**Interfaces:**
- Consumes: `Novus.BuildMode` (enum, `Debug` / `Release`, namespace `Novus`).
- Produces: `NdkCachePaths.EnvironmentVariable` (const string), `NdkCachePaths.Root` (string property), `NdkCachePaths.ResolveCpu(string)`, `NdkCachePaths.Variant(string fpu, int optimizationLevel, int safetyLevel)`, `NdkCachePaths.ForKey(string cpu, BuildMode mode, string variant, string abiHash)`, `NdkCachePaths.CanPurge(out string reason)`.

- [ ] **Step 1: Write the failing test**

Create `Novus.Tests/NdkCachePathsTests.cs`:

```csharp
using System;
using System.IO;
using Xunit;

namespace Novus.Tests;

public class NdkCachePathsTests : IDisposable
{
    private readonly string? _saved = Environment.GetEnvironmentVariable(NdkCachePaths.EnvironmentVariable);

    public void Dispose() =>
        Environment.SetEnvironmentVariable(NdkCachePaths.EnvironmentVariable, _saved);

    [Fact]
    public void Root_DefaultsToUserNovusNdk()
    {
        Environment.SetEnvironmentVariable(NdkCachePaths.EnvironmentVariable, null);
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".novus", "ndk");
        Assert.Equal(expected, NdkCachePaths.Root);
    }

    [Fact]
    public void Root_HonoursEnvironmentOverride()
    {
        var temp = Path.Combine(Path.GetTempPath(), "novus-ndk-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(NdkCachePaths.EnvironmentVariable, temp);
        Assert.Equal(Path.GetFullPath(temp), NdkCachePaths.Root);
    }

    [Theory]
    [InlineData("auto", "68020")]
    [InlineData("68020", "68020")]
    [InlineData("68040", "68040")]
    public void ResolveCpu_MapsAutoToBaseline(string input, string expected) =>
        Assert.Equal(expected, NdkCachePaths.ResolveCpu(input));

    [Fact]
    public void Variant_ComposesFpuOptimisationAndSafety() =>
        Assert.Equal("auto-O1-S2", NdkCachePaths.Variant("auto", 1, 2));

    [Fact]
    public void ForKey_BuildsFullCachePath()
    {
        var temp = Path.Combine(Path.GetTempPath(), "novus-ndk-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(NdkCachePaths.EnvironmentVariable, temp);
        var actual = NdkCachePaths.ForKey("68040", BuildMode.Release, "auto-O1-S1", "abc123");
        Assert.Equal(
            Path.Combine(Path.GetFullPath(temp), "68040", "release", "auto-O1-S1", "abc123"),
            actual);
    }

    [Fact]
    public void CanPurge_AllowsExplicitTempRoot()
    {
        var temp = Path.Combine(Path.GetTempPath(), "novus-ndk-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(NdkCachePaths.EnvironmentVariable, temp);
        Assert.True(NdkCachePaths.CanPurge(out _));
    }

    [Fact]
    public void CanPurge_RefusesFilesystemRoot()
    {
        Environment.SetEnvironmentVariable(NdkCachePaths.EnvironmentVariable, Path.GetPathRoot(Path.GetTempPath()));
        Assert.False(NdkCachePaths.CanPurge(out var reason));
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void CanPurge_RefusesBareHomeDirectory()
    {
        Environment.SetEnvironmentVariable(
            NdkCachePaths.EnvironmentVariable,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Assert.False(NdkCachePaths.CanPurge(out var reason));
        Assert.NotEmpty(reason);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Novus.Tests/Novus.Tests.csproj -c Debug --filter "FullyQualifiedName~NdkCachePathsTests"`
Expected: build failure — `NdkCachePaths` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `Novus/NdkCachePaths.cs`:

```csharp
using System;
using System.IO;

namespace Novus;

/// <summary>
/// Resolves where built NDK objects live. The cache is keyed by every compilation input that
/// can change generated code or object ABI, and it deliberately lives outside the compiler's
/// own output directory so that rebuilding the compiler does not discard it.
/// </summary>
public static class NdkCachePaths
{
    public const string EnvironmentVariable = "NOVUS_NDK_CACHE";

    /// <summary>Baseline CPU used when the target is left as `auto`.</summary>
    public const string BaselineCpu = "68020";

    public static string Root
    {
        get
        {
            var custom = Environment.GetEnvironmentVariable(EnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(custom))
                return Path.GetFullPath(custom);
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".novus", "ndk");
        }
    }

    public static string ResolveCpu(string cpu) =>
        string.Equals(cpu, "auto", StringComparison.OrdinalIgnoreCase) ? BaselineCpu : cpu;

    public static string Variant(string fpu, int optimizationLevel, int safetyLevel) =>
        $"{fpu}-O{optimizationLevel}-S{safetyLevel}";

    public static string ModeDirectory(BuildMode mode) =>
        mode == BuildMode.Release ? "release" : "debug";

    public static string ForKey(string cpu, BuildMode mode, string variant, string abiHash) =>
        Path.Combine(Root, ResolveCpu(cpu), ModeDirectory(mode), variant, abiHash);

    /// <summary>
    /// A recursive delete must never be aimed at a filesystem root or a whole home directory,
    /// however the root was configured. A mistyped environment variable is the realistic way
    /// that happens.
    /// </summary>
    public static bool CanPurge(out string reason)
    {
        var root = Root;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.Equals(root, Path.GetPathRoot(root), StringComparison.Ordinal))
        {
            reason = $"refusing to purge a filesystem root: {root}";
            return false;
        }

        if (string.Equals(root.TrimEnd(Path.DirectorySeparatorChar), home.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.Ordinal))
        {
            reason = $"refusing to purge the home directory: {root}";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Novus.Tests/Novus.Tests.csproj -c Debug --filter "FullyQualifiedName~NdkCachePathsTests"`
Expected: PASS, 9 tests.

- [ ] **Step 5: Commit**

```bash
git add Novus/NdkCachePaths.cs Novus.Tests/NdkCachePathsTests.cs
git commit -m "feat(cache): resolve NDK cache paths outside the compiler output directory"
```

---

### Task 2: Re-root the lazy cache and teach clean about it

**Files:**
- Modify: `Novus/Program.cs:2424`
- Modify: `Novus/Commands/CleanCommand.cs:44-59`

**Interfaces:**
- Consumes: `NdkCachePaths.Root` from Task 1.
- Produces: no new API. After this task the lazy compile path reads and writes `~/.novus/ndk`.

- [ ] **Step 1: Change the cache root in Program.cs**

At `Novus/Program.cs:2424`, replace:

```csharp
            var stdlibCacheRootDir = Path.Combine(compilerDir, "stdlib");
```

with:

```csharp
            // The cache lives outside the compiler's own output directory: `compilerDir` is
            // bin/<config>/<tfm>, which a rebuild can delete, and the cache key embeds the
            // compiler assembly id, so a rebuild would discard every cached NDK anyway.
            var stdlibCacheRootDir = NdkCachePaths.Root;
```

Leave everything below it untouched — `stdlibVariant`, `stdlibPrecompiledDir`, and the lock manager already compose the rest of the key correctly.

- [ ] **Step 2: Add the new root to clean**

In `Novus/Commands/CleanCommand.cs`, replace the body of `CleanStdlibCache` with a version that clears both locations:

```csharp
    private static int CleanStdlibCache(string compilerDir, bool verbose)
    {
        // The old cache lived in the compiler's output directory. Keep removing it so an
        // upgrade does not strand an orphan, then remove the current one.
        var roots = new[] { Path.Combine(compilerDir, "stdlib"), NdkCachePaths.Root };
        var total = 0;

        foreach (var stdlibCacheDir in roots)
        {
            if (!Directory.Exists(stdlibCacheDir))
            {
                if (verbose) Console.WriteLine($"  [skip] stdlib cache not found: {stdlibCacheDir}");
                continue;
            }

            var count = Directory.GetFiles(stdlibCacheDir, "*", SearchOption.AllDirectories).Length;
            Directory.Delete(stdlibCacheDir, recursive: true);
            Console.WriteLine($"  ✓ Stdlib cache ({stdlibCacheDir}): deleted {count} file(s)");
            total += count;
        }

        return total;
    }
```

- [ ] **Step 3: Verify a compile populates the new root**

```bash
export NOVUS_NDK_CACHE=$(mktemp -d)
cat > /tmp/ndk-smoke.novus <<'EOF'
fn main() -> i32 {
    return 0
}
EOF
dotnet build Novus/Novus.csproj -c Debug
dotnet Novus/bin/Debug/net10.0/Novus.dll compile /tmp/ndk-smoke.novus -o /tmp/ndk-smoke.out
find "$NOVUS_NDK_CACHE" -name '*.o' | wc -l
```

Expected: a non-zero object count, and the path under `$NOVUS_NDK_CACHE` matching `<cpu>/<mode>/<variant>/<hash>/`.

- [ ] **Step 4: Run the full host suite for regressions**

Run: `dotnet test Novus.Tests/Novus.Tests.csproj -c Debug`
Expected: PASS, no failures.

- [ ] **Step 5: Commit**

```bash
git add Novus/Program.cs Novus/Commands/CleanCommand.cs
git commit -m "fix(cache): move the NDK object cache to a user-level root"
```

---

### Task 3: Synthetic root generator and collision spike

**Files:**
- Create: `Novus/NdkRootGenerator.cs`
- Test: `Novus.Tests/NdkCacheBuildTests.cs` (first test only)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `NdkRootGenerator.EnumerateModules(string stdRoot)` returning `IReadOnlyList<string>` of module paths like `std::core`, and `NdkRootGenerator.GenerateRoot(string stdRoot)` returning the synthetic program source as a string.

This task ends in a spike whose result decides whether Task 4 needs one root or several. **Report the outcome before starting Task 4.**

- [ ] **Step 1: Write the failing test**

Create `Novus.Tests/NdkCacheBuildTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Novus.Tests;

public class NdkCacheBuildTests
{
    private static readonly string ProjectRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string StdRoot => Path.Combine(ProjectRoot, "Novus", "std");

    [Fact]
    public void EnumerateModules_CoversAllThreeAmigaTiers()
    {
        var modules = NdkRootGenerator.EnumerateModules(StdRoot);

        Assert.Contains("std::core", modules);
        Assert.Contains("amiga::dos", modules);              // tier 1
        Assert.Contains("amiga::sys::dos::filesystem", modules); // tier 2
        Assert.Contains("amiga::raw::exec", modules);        // tier 3
    }

    [Fact]
    public void GenerateRoot_ImportsEveryModuleAndDeclaresMain()
    {
        var modules = NdkRootGenerator.EnumerateModules(StdRoot);
        var source = NdkRootGenerator.GenerateRoot(StdRoot);

        Assert.Contains("fn main() -> i32", source);
        foreach (var module in modules)
            Assert.Contains($"from {module} import ", source);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Novus.Tests/Novus.Tests.csproj -c Debug --filter "FullyQualifiedName~NdkCacheBuildTests"`
Expected: build failure — `NdkRootGenerator` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `Novus/NdkRootGenerator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Novus;

/// <summary>
/// Builds a synthetic program that pulls the whole standard library into one compilation.
/// Novus has no bare module import, so each module is reached by importing one public symbol
/// from it; importing any symbol causes the whole module's functions to be emitted, which is
/// what populates the object cache.
/// </summary>
public static class NdkRootGenerator
{
    private static readonly Regex PublicSymbol = new(
        @"^pub\s+(?:fn|struct|enum|trait|const|type)\s+([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static IReadOnlyList<string> EnumerateModules(string stdRoot)
    {
        var root = Path.GetFullPath(stdRoot);
        var modules = new List<string>();

        foreach (var path in Directory.EnumerateFiles(root, "*.novus", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            // Tests and the prelude are not part of the library surface.
            var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            if (relative.StartsWith("tests/", StringComparison.Ordinal) ||
                relative == "prelude.novus")
                continue;
            if (FirstPublicSymbol(path) == null)
                continue;

            modules.Add(ModulePathFor(relative));
        }

        return modules;
    }

    public static string GenerateRoot(string stdRoot)
    {
        var root = Path.GetFullPath(stdRoot);
        var builder = new StringBuilder();
        builder.AppendLine("// Generated by novus stdlib-build. Not part of the library.");

        foreach (var path in Directory.EnumerateFiles(root, "*.novus", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            if (relative.StartsWith("tests/", StringComparison.Ordinal) || relative == "prelude.novus")
                continue;

            var symbol = FirstPublicSymbol(path);
            if (symbol == null)
                continue;

            builder.AppendLine($"from {ModulePathFor(relative)} import {symbol}");
        }

        builder.AppendLine();
        builder.AppendLine("fn main() -> i32 {");
        builder.AppendLine("    return 0");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string ModulePathFor(string relativePath)
    {
        var withoutExtension = relativePath[..^".novus".Length];
        return withoutExtension.Replace("/", "::");
    }

    private static string? FirstPublicSymbol(string path)
    {
        var match = PublicSymbol.Match(File.ReadAllText(path));
        return match.Success ? match.Groups[1].Value : null;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Novus.Tests/Novus.Tests.csproj -c Debug --filter "FullyQualifiedName~NdkCacheBuildTests"`
Expected: PASS, 2 tests. If `EnumerateModules` misses a tier, fix the filtering before continuing.

- [ ] **Step 5: Run the spike and record what breaks**

```bash
export NOVUS_NDK_CACHE=$(mktemp -d)
dotnet build Novus/Novus.csproj -c Debug
dotnet fsi --use:/dev/null 2>/dev/null || true   # not needed; generate via a scratch program instead
```

Generate the root and compile it:

```bash
cat > /tmp/gen-root.csx <<'EOF'
EOF
# Simplest path: add a temporary hidden switch is NOT needed — call the generator from a test.
dotnet test Novus.Tests/Novus.Tests.csproj -c Debug --filter "FullyQualifiedName~GenerateRoot_ImportsEveryModuleAndDeclaresMain" -v n
```

Then write the generated root to disk from a scratch xUnit fact (delete afterwards) or, more simply, compile it through the command once Task 4 exists. For the spike, add this temporary test to `NdkCacheBuildTests.cs`, run it, then remove it:

```csharp
    [Fact]
    public void Spike_WriteGeneratedRoot()
    {
        File.WriteAllText("/tmp/ndk-root.novus", NdkRootGenerator.GenerateRoot(StdRoot));
    }
```

```bash
dotnet test Novus.Tests/Novus.Tests.csproj -c Debug --filter "FullyQualifiedName~Spike_WriteGeneratedRoot"
dotnet Novus/bin/Debug/net10.0/Novus.dll compile /tmp/ndk-root.novus -o /tmp/ndk-root.out 2>&1 | tail -40
```

Expected outcomes, one of:
- **Clean compile.** Task 4 uses a single root. Record the object count from `find "$NOVUS_NDK_CACHE" -name '*.o' | wc -l`.
- **Name collisions reported.** Record every colliding symbol and the modules involved. Task 4 then splits modules into groups, one root per group, retrying a module in its own root when it collides. Report the collision list before proceeding.

**Do not work around a collision by silently dropping the module** — a module missing from the root is a module missing from the cache, and that is the failure this whole feature exists to prevent.

- [ ] **Step 6: Remove the spike test and commit**

```bash
git add Novus/NdkRootGenerator.cs Novus.Tests/NdkCacheBuildTests.cs
git commit -m "feat(cache): generate a synthetic root covering every stdlib module"
```

---

### Task 4: Rewrite stdlib-build to compile and harvest

**Files:**
- Modify: `Novus/Commands/StdlibBuildCommand.cs` (replace the body of `BuildForTarget`)
- Test: `Novus.Tests/NdkCacheBuildTests.cs` (add build test)

**Interfaces:**
- Consumes: `NdkRootGenerator.GenerateRoot` (Task 3), `NdkCachePaths.ForKey` / `Variant` (Task 1).
- Produces: `StdlibBuildCommand.BuildForTarget(string cpu, BuildMode mode, string? vbccPath, string? ndkPath, bool verbose, int codegenVersion, bool overwrite)` returning `Task<int>` — `0` on success. Note the added `overwrite` parameter; update the call site at `Novus/Program.cs:660`.

- [ ] **Step 1: Write the failing test**

Add to `Novus.Tests/NdkCacheBuildTests.cs`:

```csharp
    [Fact]
    public async Task BuildForTarget_PopulatesCacheForTheRequestedKey()
    {
        var temp = Path.Combine(Path.GetTempPath(), "novus-ndk-" + Guid.NewGuid().ToString("N"));
        var saved = Environment.GetEnvironmentVariable(NdkCachePaths.EnvironmentVariable);
        Environment.SetEnvironmentVariable(NdkCachePaths.EnvironmentVariable, temp);
        try
        {
            var exit = await Novus.Commands.StdlibBuildCommand.BuildForTarget(
                "68020", BuildMode.Release, null, null, verbose: false,
                codegenVersion: 1, overwrite: false);

            Assert.Equal(0, exit);
            var objects = Directory.EnumerateFiles(temp, "*.o", SearchOption.AllDirectories).ToList();
            Assert.NotEmpty(objects);
            Assert.NotEmpty(Directory.EnumerateFiles(temp, "manifest.json", SearchOption.AllDirectories));
        }
        finally
        {
            Environment.SetEnvironmentVariable(NdkCachePaths.EnvironmentVariable, saved);
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }
```

Add `using System.Threading.Tasks;` to the file's usings.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Novus.Tests/Novus.Tests.csproj -c Debug --filter "FullyQualifiedName~BuildForTarget_PopulatesCacheForTheRequestedKey"`
Expected: FAIL — the stub writes a manifest but no objects, so `Assert.NotEmpty(objects)` fails.

- [ ] **Step 3: Write the implementation**

Replace the body of `BuildForTarget` in `Novus/Commands/StdlibBuildCommand.cs` so that it, in order:

1. Skips work when `!overwrite` and a valid manifest already exists for the key (reuse the existing `NeedsRebuild` helper, passing the cache directory from `NdkCachePaths.ForKey`).
2. Writes `NdkRootGenerator.GenerateRoot(stdlibSourceDir)` to a temp file.
3. Invokes the normal compile path for that root with the variant's options — `--cpu <cpu>`, the mode's default `fpu`/`-O`/`--safety-level`, output to a temp binary — which populates the cache as a side effect.
4. Deletes the temp root and temp binary.
5. Returns the compile's exit code.

```csharp
    public static async Task<int> BuildForTarget(
        string cpu, BuildMode buildMode, string? vbccPath, string? ndkPath,
        bool verbose, int codegenVersion, bool overwrite)
    {
        var stdlibSourceDir = ResolveStdlibSourceDir();
        var variant = NdkCachePaths.Variant(
            DefaultFpu, DefaultOptimizationLevel, DefaultSafetyLevel(buildMode));

        if (!overwrite && !NeedsRebuild(AppContext.BaseDirectory, cpu, buildMode, codegenVersion,
                out var reason, cacheDir: NdkCachePaths.ForKey(cpu, buildMode, variant, CurrentAbiHash())))
        {
            Console.WriteLine($"  ✓ {cpu}/{NdkCachePaths.ModeDirectory(buildMode)}: already built");
            return 0;
        }

        var rootPath = Path.Combine(Path.GetTempPath(), $"novus-ndk-root-{Guid.NewGuid():N}.novus");
        var outputPath = Path.Combine(Path.GetTempPath(), $"novus-ndk-root-{Guid.NewGuid():N}.out");
        await File.WriteAllTextAsync(rootPath, NdkRootGenerator.GenerateRoot(stdlibSourceDir));

        try
        {
            return await CompileSyntheticRoot(
                rootPath, outputPath, cpu, buildMode, vbccPath, ndkPath, verbose);
        }
        finally
        {
            if (File.Exists(rootPath)) File.Delete(rootPath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }
```

`CompileSyntheticRoot` builds a `CompilerOptions` for the variant and calls the same entry point `RunCompile` uses in `Program.cs`. `DefaultFpu`, `DefaultOptimizationLevel`, `DefaultSafetyLevel` and `CurrentAbiHash` must read the same defaults `compile` uses rather than duplicating literals — locate them in `CompilerOptions` and the types-header hashing in `Program.cs` and call through.

- [ ] **Step 4: Update the call site**

In `Novus/Program.cs:660`, pass the new argument:

```csharp
                var result = await Commands.StdlibBuildCommand.BuildForTarget(
                    cpu!,
                    mode,
                    options.VbccPath,
                    options.NdkPath,
                    options.Verbose,
                    CompilerCacheVersion,
                    options.Overwrite);
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Novus.Tests/Novus.Tests.csproj -c Debug --filter "FullyQualifiedName~BuildForTarget_PopulatesCacheForTheRequestedKey"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Novus/Commands/StdlibBuildCommand.cs Novus/Program.cs Novus.Tests/NdkCacheBuildTests.cs
git commit -m "feat(cache): build the full NDK per CPU instead of writing an empty manifest"
```

---

### Task 5: Overwrite and purge flags

**Files:**
- Modify: `Novus/StdlibBuildOptions.cs`
- Modify: `Novus/Program.cs:642-680` (`RunStdlibBuild`)
- Test: `Novus.Tests/NdkCacheBuildTests.cs`

**Interfaces:**
- Consumes: `NdkCachePaths.CanPurge`, `NdkCachePaths.Root`, `NdkCachePaths.ModeDirectory` (Task 1); `BuildForTarget(..., overwrite)` (Task 4).
- Produces: `StdlibBuildOptions.Overwrite` (bool), `StdlibBuildOptions.Purge` (bool).

- [ ] **Step 1: Write the failing test**

Add to `Novus.Tests/NdkCacheBuildTests.cs`:

```csharp
    [Fact]
    public void Purge_RemovesOnlyTheSelectedCpuAndMode()
    {
        var temp = Path.Combine(Path.GetTempPath(), "novus-ndk-" + Guid.NewGuid().ToString("N"));
        var saved = Environment.GetEnvironmentVariable(NdkCachePaths.EnvironmentVariable);
        Environment.SetEnvironmentVariable(NdkCachePaths.EnvironmentVariable, temp);
        try
        {
            var keep = NdkCachePaths.ForKey("68040", BuildMode.Release, "auto-O1-S1", "hash");
            var drop = NdkCachePaths.ForKey("68020", BuildMode.Release, "auto-O1-S1", "hash");
            Directory.CreateDirectory(keep);
            Directory.CreateDirectory(drop);
            File.WriteAllText(Path.Combine(keep, "a.o"), "");
            File.WriteAllText(Path.Combine(drop, "a.o"), "");

            var removed = Novus.Commands.StdlibBuildCommand.Purge(
                new[] { "68020" }, new[] { BuildMode.Release }, verbose: false);

            Assert.True(removed > 0);
            Assert.False(Directory.Exists(drop));
            Assert.True(Directory.Exists(keep));
        }
        finally
        {
            Environment.SetEnvironmentVariable(NdkCachePaths.EnvironmentVariable, saved);
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Novus.Tests/Novus.Tests.csproj -c Debug --filter "FullyQualifiedName~Purge_RemovesOnlyTheSelectedCpuAndMode"`
Expected: build failure — `StdlibBuildCommand.Purge` does not exist.

- [ ] **Step 3: Add the options**

In `Novus/StdlibBuildOptions.cs`, add:

```csharp
    [Option("overwrite", Required = false, HelpText = "Rebuild even when a valid cache entry already exists")]
    public bool Overwrite { get; set; }

    [Option("purge", Required = false, HelpText = "Delete cached NDK builds matching --cpu/--mode, then exit")]
    public bool Purge { get; set; }
```

- [ ] **Step 4: Implement Purge**

Add to `Novus/Commands/StdlibBuildCommand.cs`:

```csharp
    /// <summary>
    /// Delete cached builds for the selected CPUs and modes. Only ever deletes inside the
    /// resolved cache root, and never follows a symlinked entry out of it.
    /// </summary>
    public static int Purge(IReadOnlyList<string> cpus, IReadOnlyList<BuildMode> modes, bool verbose)
    {
        if (!NdkCachePaths.CanPurge(out var reason))
        {
            Console.Error.WriteLine($"✗ {reason}");
            return -1;
        }

        var removed = 0;
        foreach (var cpu in cpus)
        {
            foreach (var mode in modes)
            {
                var directory = Path.Combine(
                    NdkCachePaths.Root, NdkCachePaths.ResolveCpu(cpu), NdkCachePaths.ModeDirectory(mode));
                if (!Directory.Exists(directory))
                {
                    if (verbose) Console.WriteLine($"  [skip] {directory}");
                    continue;
                }

                var info = new DirectoryInfo(directory);
                if (info.LinkTarget != null)
                {
                    Console.Error.WriteLine($"  [skip] refusing to follow symlink: {directory}");
                    continue;
                }

                var count = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length;
                Directory.Delete(directory, recursive: true);
                Console.WriteLine($"  ✓ Purged {cpu}/{NdkCachePaths.ModeDirectory(mode)}: {count} file(s)");
                removed += count;
            }
        }

        return removed;
    }
```

- [ ] **Step 5: Wire purge into RunStdlibBuild**

In `Novus/Program.cs`, immediately after `cpus` and `modes` are computed in `RunStdlibBuild`, add:

```csharp
        if (options.Purge)
        {
            var purged = Commands.StdlibBuildCommand.Purge(
                cpus.Select(c => c!).ToArray(),
                modes,
                options.Verbose);
            return purged < 0 ? 1 : 0;
        }
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test Novus.Tests/Novus.Tests.csproj -c Debug --filter "FullyQualifiedName~Purge_RemovesOnlyTheSelectedCpuAndMode"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Novus/StdlibBuildOptions.cs Novus/Commands/StdlibBuildCommand.cs Novus/Program.cs Novus.Tests/NdkCacheBuildTests.cs
git commit -m "feat(cache): add --overwrite and scoped --purge to stdlib-build"
```

---

### Task 6: Reuse and non-reuse integration tests

**Files:**
- Test: `Novus.Tests/NdkCacheBuildTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-5.
- Produces: no API.

- [ ] **Step 1: Write the failing tests**

Add to `Novus.Tests/NdkCacheBuildTests.cs`:

```csharp
    private static async Task<string> CompileHelloAsync(string cacheRoot, params string[] extraArgs)
    {
        var source = Path.Combine(Path.GetTempPath(), $"novus-hello-{Guid.NewGuid():N}.novus");
        var output = Path.Combine(Path.GetTempPath(), $"novus-hello-{Guid.NewGuid():N}.out");
        await File.WriteAllTextAsync(source, "fn main() -> i32 {\n    return 0\n}\n");

        var compiler = Path.Combine(ProjectRoot, "Novus", "bin", "Debug", "net10.0", "Novus.dll");
        var arguments = new List<string> { compiler, "compile", source, "-o", output };
        arguments.AddRange(extraArgs);

        var info = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        info.Environment[NdkCachePaths.EnvironmentVariable] = cacheRoot;

        using var process = System.Diagnostics.Process.Start(info)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        File.Delete(source);
        if (File.Exists(output)) File.Delete(output);
        return stdout;
    }

    [Fact]
    public async Task CompileReusesAPrebuiltCacheAndSkipsRebuildingStdlib()
    {
        var temp = Path.Combine(Path.GetTempPath(), "novus-ndk-" + Guid.NewGuid().ToString("N"));
        try
        {
            var first = await CompileHelloAsync(temp);
            Assert.Contains("Compiling stdlib modules", first, StringComparison.Ordinal);

            var second = await CompileHelloAsync(temp);
            Assert.DoesNotContain("Compiling stdlib modules", second, StringComparison.Ordinal);
            Assert.Contains("pre-compiled stdlib", second, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public async Task CompileWithADifferentSafetyLevelDoesNotReuseTheCache()
    {
        var temp = Path.Combine(Path.GetTempPath(), "novus-ndk-" + Guid.NewGuid().ToString("N"));
        try
        {
            await CompileHelloAsync(temp);
            var different = await CompileHelloAsync(temp, "--safety-level", "3");
            Assert.Contains("Compiling stdlib modules", different, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }
```

- [ ] **Step 2: Run tests to verify they fail or pass honestly**

Run: `dotnet test Novus.Tests/Novus.Tests.csproj -c Debug --filter "FullyQualifiedName~NdkCacheBuildTests"`
Expected: both new tests PASS if Tasks 2 and 4 are correct. If `CompileReusesAPrebuiltCacheAndSkipsRebuildingStdlib` fails on the second compile, the cache key is not stable across runs — fix that before continuing; a key that changes per invocation defeats the entire feature.

- [ ] **Step 3: Run the full host suite**

Run: `dotnet test Novus.Tests/Novus.Tests.csproj -c Debug`
Expected: PASS, no regressions.

- [ ] **Step 4: Verify the end-to-end command**

```bash
export NOVUS_NDK_CACHE=$(mktemp -d)
dotnet Novus/bin/Debug/net10.0/Novus.dll stdlib-build --cpu 68020 --mode release -v
find "$NOVUS_NDK_CACHE" -name '*.o' | wc -l          # expect > 0
dotnet Novus/bin/Debug/net10.0/Novus.dll stdlib-build --cpu 68020 --mode release   # expect "already built"
dotnet Novus/bin/Debug/net10.0/Novus.dll stdlib-build --cpu 68020 --mode release --overwrite   # expect a rebuild
dotnet Novus/bin/Debug/net10.0/Novus.dll stdlib-build --cpu 68020 --mode release --purge
find "$NOVUS_NDK_CACHE" -name '*.o' | wc -l          # expect 0
```

- [ ] **Step 5: Commit**

```bash
git add Novus.Tests/NdkCacheBuildTests.cs
git commit -m "test(cache): cover NDK cache reuse, key isolation, overwrite and purge"
```

---

## Self-Review

**Spec coverage.** Cache location → Task 1 + 2. Command surface → Task 4 + 5. Build mechanism → Task 3 + 4. Reuse/invalidation → Task 2 (re-root, existing validation preserved) + Task 6 (proven). Purge → Task 5, guard in Task 1. Migration (none, `clean` removes old) → Task 2. Testing → Tasks 1, 3, 4, 5, 6. No spec requirement is unimplemented.

**Placeholders.** Task 4 Step 3 names `DefaultFpu`, `DefaultOptimizationLevel`, `DefaultSafetyLevel` and `CurrentAbiHash` as helpers to be located in existing code rather than showing their bodies, because their values must be read from `compile`'s defaults rather than duplicated — the exact call is discovered during implementation. This is the one place the plan defers detail, and it is deliberate: hardcoding those literals would violate a Global Constraint. Task 3 Step 5 intentionally has two possible outcomes; that is a decision point, not a placeholder.

**Type consistency.** `BuildForTarget` gains `bool overwrite` in Task 4 and is called with it in Task 4 Step 4 and Task 5. `NdkCachePaths.ModeDirectory`, `ResolveCpu`, `ForKey`, `Variant`, `CanPurge` are defined in Task 1 and used with matching signatures in Tasks 2, 4 and 5. `NdkRootGenerator.EnumerateModules` / `GenerateRoot` defined in Task 3, used in Task 4.
