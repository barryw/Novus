using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Novus.Compilation;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for persistent compilation cache (incremental compilation)
/// </summary>
public class CompilationCacheTests : IDisposable
{
    [Fact]
    public void CompilerConfigHashIncludesSafetyAndPackageMetadata()
    {
        var options = new CompilerOptions { SafetyLevelOption = 1, PackageName = "one" };
        var first = Program.ComputeCompilationConfigHash(options);

        options.SafetyLevelOption = 2;
        Assert.NotEqual(first, Program.ComputeCompilationConfigHash(options));

        options.SafetyLevelOption = 1;
        options.PackageName = "two";
        Assert.NotEqual(first, Program.ComputeCompilationConfigHash(options));
    }

    private readonly string _testCacheDir;
    private readonly CompilationCache _cache;
    private const int TestCompilerVersion = 1;

    public CompilationCacheTests()
    {
        // Create a temporary cache directory for testing
        _testCacheDir = Path.Combine(Path.GetTempPath(), $"novus-cache-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testCacheDir);
        _cache = new CompilationCache(_testCacheDir, TestCompilerVersion);
    }

    public void Dispose()
    {
        // Clean up test cache directory
        if (Directory.Exists(_testCacheDir))
        {
            try
            {
                Directory.Delete(_testCacheDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ConfigHash_ShouldBeDifferentForDifferentSettings()
    {
        var hash1 = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");
        var hash2 = CompilationCache.ComputeConfigHash("68000", "auto", 2, "debug");
        var hash3 = CompilationCache.ComputeConfigHash("68020", "soft", 2, "debug");
        var hash4 = CompilationCache.ComputeConfigHash("68020", "auto", 3, "debug");
        var hash5 = CompilationCache.ComputeConfigHash("68020", "auto", 2, "release");

        Assert.NotEqual(hash1, hash2); // Different CPU
        Assert.NotEqual(hash1, hash3); // Different FPU
        Assert.NotEqual(hash1, hash4); // Different optimization level
        Assert.NotEqual(hash1, hash5); // Different build mode
    }

    [Fact]
    public void ConfigHash_ShouldBeSameForSameSettings()
    {
        var hash1 = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");
        var hash2 = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void NeedsRecompilation_NewFile_ShouldReturnTrue()
    {
        var testFile = CreateTestFile("fn main() { }");
        var configHash = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");

        var needsRecompilation = _cache.NeedsRecompilation(testFile, configHash);

        Assert.True(needsRecompilation);
    }

    [Fact]
    public void CacheHit_UnchangedFile_ShouldReturnCachedModule()
    {
        var testFile = CreateTestFile("fn main() { }");
        var configHash = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");

        // Create mock compilation artifacts
        var parseTree = CreateMockParseTree();
        var irModule = new IrModule();
        var stringLiterals = new System.Collections.Generic.List<IrStringLiteral>();
        var importedModules = new System.Collections.Generic.List<string>();

        // Cache the compilation result
        _cache.CacheCompilationResult(
            testFile,
            parseTree,
            irModule,
            stringLiterals,
            importedModules,
            configHash,
            hadErrors: false);

        // Verify cache hit
        var (cachedModule, cachedStringLiterals, cachedImports) =
            _cache.GetCachedIrModule(testFile, configHash);

        Assert.NotNull(cachedModule);
        Assert.NotNull(cachedStringLiterals);
        Assert.NotNull(cachedImports);
    }

    [Fact]
    public void CacheMiss_ModifiedFile_ShouldReturnNull()
    {
        var testFile = CreateTestFile("fn main() { }");
        var configHash = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");

        // Cache initial compilation
        var parseTree = CreateMockParseTree();
        var irModule = new IrModule();
        var stringLiterals = new System.Collections.Generic.List<IrStringLiteral>();
        var importedModules = new System.Collections.Generic.List<string>();

        _cache.CacheCompilationResult(
            testFile,
            parseTree,
            irModule,
            stringLiterals,
            importedModules,
            configHash,
            hadErrors: false);

        // Modify the file
        System.Threading.Thread.Sleep(100); // Ensure timestamp changes
        File.WriteAllText(testFile, "fn main() { let x = 42; }");

        // Verify cache miss
        var (cachedModule, _, _) = _cache.GetCachedIrModule(testFile, configHash);
        Assert.Null(cachedModule);
    }

    [Fact]
    public void CacheMiss_DifferentConfig_ShouldReturnNull()
    {
        var testFile = CreateTestFile("fn main() { }");
        var configHash1 = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");
        var configHash2 = CompilationCache.ComputeConfigHash("68000", "auto", 2, "debug");

        // Cache compilation with config1
        var parseTree = CreateMockParseTree();
        var irModule = new IrModule();
        var stringLiterals = new System.Collections.Generic.List<IrStringLiteral>();
        var importedModules = new System.Collections.Generic.List<string>();

        _cache.CacheCompilationResult(
            testFile,
            parseTree,
            irModule,
            stringLiterals,
            importedModules,
            configHash1,
            hadErrors: false);

        // Try to retrieve with different config
        var (cachedModule, _, _) = _cache.GetCachedIrModule(testFile, configHash2);
        Assert.Null(cachedModule);
    }

    [Fact]
    public void CacheMiss_DifferentCompilerVersion_ShouldReturnNull()
    {
        var testFile = CreateTestFile("fn main() { }");
        var configHash = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");

        // Cache with current compiler version
        var parseTree = CreateMockParseTree();
        var irModule = new IrModule();
        var stringLiterals = new System.Collections.Generic.List<IrStringLiteral>();
        var importedModules = new System.Collections.Generic.List<string>();

        _cache.CacheCompilationResult(
            testFile,
            parseTree,
            irModule,
            stringLiterals,
            importedModules,
            configHash,
            hadErrors: false);

        // Create cache with different compiler version
        var cacheWithDifferentVersion = new CompilationCache(_testCacheDir, TestCompilerVersion + 1);

        // Verify cache miss due to version mismatch
        var (cachedModule, _, _) = cacheWithDifferentVersion.GetCachedIrModule(testFile, configHash);
        Assert.Null(cachedModule);
    }

    [Fact]
    public void DependencyInvalidation_ChangedDependency_ShouldInvalidateDependent()
    {
        var dependencyFile = CreateTestFile("pub fn helper() -> i32 { 42 }", "dependency.novus");
        var mainFile = CreateTestFile("fn main() { }", "main.novus");
        var configHash = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");

        // Cache both files, with main depending on dependency
        var parseTree = CreateMockParseTree();
        var irModule = new IrModule();
        var stringLiterals = new System.Collections.Generic.List<IrStringLiteral>();

        // Cache dependency
        _cache.CacheCompilationResult(
            dependencyFile,
            parseTree,
            irModule,
            stringLiterals,
            new System.Collections.Generic.List<string>(),
            configHash,
            hadErrors: false);

        // Cache main file with dependency
        _cache.CacheCompilationResult(
            mainFile,
            parseTree,
            irModule,
            stringLiterals,
            new System.Collections.Generic.List<string> { dependencyFile },
            configHash,
            hadErrors: false);

        // Modify dependency
        System.Threading.Thread.Sleep(100); // Ensure timestamp changes
        File.WriteAllText(dependencyFile, "pub fn helper() -> i32 { 100 }");

        // Verify main file needs recompilation
        var needsRecompilation = _cache.NeedsRecompilation(mainFile, configHash);
        Assert.True(needsRecompilation);
    }

    [Fact]
    public void Invalidate_ShouldRemoveFileFromCache()
    {
        var testFile = CreateTestFile("fn main() { }");
        var configHash = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");

        // Cache the file
        var parseTree = CreateMockParseTree();
        var irModule = new IrModule();
        var stringLiterals = new System.Collections.Generic.List<IrStringLiteral>();
        var importedModules = new System.Collections.Generic.List<string>();

        _cache.CacheCompilationResult(
            testFile,
            parseTree,
            irModule,
            stringLiterals,
            importedModules,
            configHash,
            hadErrors: false);

        // Invalidate the cache
        _cache.Invalidate(testFile);

        // Verify cache miss
        var (cachedModule, _, _) = _cache.GetCachedIrModule(testFile, configHash);
        Assert.Null(cachedModule);
    }

    [Fact]
    public void GetStats_ShouldTrackHitsAndMisses()
    {
        var testFile = CreateTestFile("fn main() { }");
        var configHash = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");

        // Initial stats should be zero
        var (parseHits1, parseMisses1, irHits1, irMisses1, _) = _cache.GetStats();
        Assert.Equal(0, parseHits1);
        Assert.Equal(0, parseMisses1);
        Assert.Equal(0, irHits1);
        Assert.Equal(0, irMisses1);

        // Cache miss (file not cached)
        _cache.GetCachedIrModule(testFile, configHash);
        var (_, _, _, irMisses2, _) = _cache.GetStats();
        Assert.Equal(1, irMisses2);

        // Cache the file
        var parseTree = CreateMockParseTree();
        var irModule = new IrModule();
        var stringLiterals = new System.Collections.Generic.List<IrStringLiteral>();
        var importedModules = new System.Collections.Generic.List<string>();

        _cache.CacheCompilationResult(
            testFile,
            parseTree,
            irModule,
            stringLiterals,
            importedModules,
            configHash,
            hadErrors: false);

        // Cache hit
        _cache.GetCachedIrModule(testFile, configHash);
        var (_, _, irHits3, _, _) = _cache.GetStats();
        Assert.Equal(1, irHits3);
    }

    [Fact]
    public void Persistence_ShouldSurviveMultipleInstances()
    {
        var testFile = CreateTestFile("fn main() { }");
        var configHash = CompilationCache.ComputeConfigHash("68020", "auto", 2, "debug");

        // Cache with first instance
        var parseTree = CreateMockParseTree();
        var irModule = new IrModule();
        var stringLiterals = new System.Collections.Generic.List<IrStringLiteral>();
        var importedModules = new System.Collections.Generic.List<string>();

        _cache.CacheCompilationResult(
            testFile,
            parseTree,
            irModule,
            stringLiterals,
            importedModules,
            configHash,
            hadErrors: false);

        // Wait for async save to complete (uses proper synchronization instead of sleep)
        _cache.Flush();

        // Create new cache instance (simulates new compiler invocation)
        var cache2 = new CompilationCache(_testCacheDir, TestCompilerVersion);

        // Verify file still needs no recompilation
        var needsRecompilation = cache2.NeedsRecompilation(testFile, configHash);
        Assert.False(needsRecompilation);
    }

    // Helper methods

    private string CreateTestFile(string content, string? fileName = null)
    {
        fileName ??= $"test-{Guid.NewGuid()}.novus";
        var filePath = Path.Combine(_testCacheDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private Antlr4.Runtime.Tree.IParseTree CreateMockParseTree()
    {
        // Create a minimal mock parse tree
        // For testing purposes, we just need a valid IParseTree instance
        var input = new Antlr4.Runtime.AntlrInputStream("fn main() { }");
        var lexer = new NovusLexer(input);
        var tokens = new Antlr4.Runtime.CommonTokenStream(lexer);
        var parser = new NovusParser(tokens);
        return parser.compilationUnit();
    }
}
