using Novus.Compilation;
using Novus.Diagnostics;
using Novus.Parser;
using Antlr4.Runtime;

namespace Novus.Tests;

/// <summary>
/// Integration tests for the complete compilation pipeline including
/// module caching, circular import detection, and error reporting.
/// </summary>
public class CompilationPipelineTests
{
    private string CreateTempFile(string name, string content)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "novus_test_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, name);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private void CleanupTempDir(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (dir != null && Directory.Exists(dir) && dir.Contains("novus_test_"))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public void Test_CompileWithCache_FasterThanWithout()
    {
        // Arrange
        var source = @"
fn fibonacci(n: i32) -> i32 {
    if n <= 1 {
        return n
    }
    return fibonacci(n - 1) + fibonacci(n - 2)
}

fn main() -> i32 {
    return fibonacci(10)
}";

        var testFile = CreateTempFile("fibonacci.novus", source);
        var cache = new ModuleCache();

        try
        {
            // Act - First parse (no cache)
            var startCold = DateTime.UtcNow;
            for (int i = 0; i < 10; i++)
            {
                var inputStream = new AntlrInputStream(source);
                var lexer = new NovusLexer(inputStream);
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new NovusParser(tokenStream);
                parser.compilationUnit();
            }
            var coldTime = DateTime.UtcNow - startCold;

            // Add to cache
            var inputStreamCached = new AntlrInputStream(source);
            var lexerCached = new NovusLexer(inputStreamCached);
            var tokenStreamCached = new CommonTokenStream(lexerCached);
            var parserCached = new NovusParser(tokenStreamCached);
            var parseTree = parserCached.compilationUnit();
            cache.Add(testFile, parseTree);

            // Act - Second parse (with cache)
            var startHot = DateTime.UtcNow;
            for (int i = 0; i < 10; i++)
            {
                cache.TryGet(testFile, out var cachedTree);
            }
            var hotTime = DateTime.UtcNow - startHot;

            // Assert - Cache should be significantly faster
            Assert.True(hotTime < coldTime,
                $"Cache should be faster: Cold={coldTime.TotalMilliseconds}ms, Hot={hotTime.TotalMilliseconds}ms");
        }
        finally
        {
            CleanupTempDir(testFile);
        }
    }

    [Fact]
    public void Test_CircularImport_ReportsError()
    {
        // Arrange
        var moduleA = CreateTempFile("moduleA.novus", "from moduleB import *");
        var moduleB = CreateTempFile("moduleB.novus", "from moduleA import *");

        var diagnostics = new DiagnosticBag();
        var detector = new CircularImportDetector(diagnostics);

        try
        {
            // Act
            detector.EnterModule(moduleA);
            detector.RecordDependency(moduleA, moduleB);
            detector.ExitModule();

            detector.EnterModule(moduleB);
            var result = detector.RecordDependency(moduleB, moduleA);
            detector.ExitModule();

            // Assert
            Assert.False(result, "Should detect circular import");
            Assert.True(diagnostics.HasErrors);

            var errorMessage = diagnostics.FormatDiagnostics();
            Assert.Contains("circular module dependency", errorMessage);
            Assert.Contains("moduleA.novus", errorMessage);
            Assert.Contains("moduleB.novus", errorMessage);
        }
        finally
        {
            CleanupTempDir(moduleA);
            CleanupTempDir(moduleB);
        }
    }

    [Fact]
    public void Test_ParseError_IntegratesWithDiagnostics()
    {
        // Arrange
        var source = "fn main() -> i32 { let x: i32 = }"; // Missing value
        var testFile = CreateTempFile("syntax_error.novus", source);
        var diagnostics = new DiagnosticBag();

        try
        {
            // Act
            var inputStream = new AntlrInputStream(source);
            var lexer = new NovusLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new NovusParser(tokenStream);

            parser.RemoveErrorListeners();
            parser.AddErrorListener(new NovusErrorListener(diagnostics, testFile, source));

            parser.compilationUnit();

            // Assert
            Assert.True(diagnostics.HasErrors, "Should have parse errors");

            var formatted = diagnostics.FormatDiagnostics();
            Assert.Contains("syntax_error.novus", formatted);
            Assert.Contains("E0001", formatted);
        }
        finally
        {
            CleanupTempDir(testFile);
        }
    }

    [Fact]
    public void Test_ComplexDependencyGraph_CachedCorrectly()
    {
        // Arrange - Create a diamond dependency: A -> B,C -> D
        var moduleD = CreateTempFile("moduleD.novus", "pub fn utility() -> i32 { return 42 }");
        var moduleB = CreateTempFile("moduleB.novus", "from moduleD import utility\npub fn helper1() -> i32 { return utility() }");
        var moduleC = CreateTempFile("moduleC.novus", "from moduleD import utility\npub fn helper2() -> i32 { return utility() }");
        var moduleA = CreateTempFile("moduleA.novus", "from moduleB import helper1\nfrom moduleC import helper2\nfn main() -> i32 { return helper1() + helper2() }");

        var cache = new ModuleCache();
        var diagnostics = new DiagnosticBag();

        try
        {
            // Act - Parse all modules and cache them
            foreach (var module in new[] { moduleD, moduleB, moduleC, moduleA })
            {
                var source = File.ReadAllText(module);
                var inputStream = new AntlrInputStream(source);
                var lexer = new NovusLexer(inputStream);
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new NovusParser(tokenStream);

                parser.RemoveErrorListeners();
                parser.AddErrorListener(new NovusErrorListener(diagnostics, module, source));

                var parseTree = parser.compilationUnit();
                cache.Add(module, parseTree);
            }

            // Assert - All modules should be cached
            Assert.Equal(4, cache.Count);
            Assert.False(diagnostics.HasErrors, "Diamond dependency should be valid");

            // Verify cache hits
            Assert.True(cache.TryGet(moduleA, out _));
            Assert.True(cache.TryGet(moduleB, out _));
            Assert.True(cache.TryGet(moduleC, out _));
            Assert.True(cache.TryGet(moduleD, out _));
        }
        finally
        {
            CleanupTempDir(moduleA);
            CleanupTempDir(moduleB);
            CleanupTempDir(moduleC);
            CleanupTempDir(moduleD);
        }
    }

    [Fact]
    public void Test_CacheInvalidation_AfterFileModification()
    {
        // Arrange
        var source1 = "fn main() -> i32 { return 42 }";
        var testFile = CreateTempFile("test.novus", source1);
        var cache = new ModuleCache();

        try
        {
            // Act - Parse and cache
            var inputStream1 = new AntlrInputStream(source1);
            var lexer1 = new NovusLexer(inputStream1);
            var tokenStream1 = new CommonTokenStream(lexer1);
            var parser1 = new NovusParser(tokenStream1);
            var parseTree1 = parser1.compilationUnit();
            cache.Add(testFile, parseTree1);

            Assert.True(cache.TryGet(testFile, out var cached1));
            Assert.Same(parseTree1, cached1);

            // Modify file
            Thread.Sleep(100); // Ensure timestamp difference
            var source2 = "fn main() -> i32 { return 99 }";
            File.WriteAllText(testFile, source2);

            // Try to get from cache (should miss)
            var cacheHit = cache.TryGet(testFile, out var cached2);

            // Assert
            Assert.False(cacheHit, "Cache should miss after file modification");
            Assert.Null(cached2);
        }
        finally
        {
            CleanupTempDir(testFile);
        }
    }

    [Fact]
    public void Test_MultipleParseErrors_AllCaptured()
    {
        // Arrange
        var source = @"
fn bad1() -> i32 {
    let x: i32 =
}

fn bad2() -> i32 {
    return @
}
";
        var testFile = CreateTempFile("multiple_errors.novus", source);
        var diagnostics = new DiagnosticBag();

        try
        {
            // Act
            var inputStream = new AntlrInputStream(source);
            var lexer = new NovusLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new NovusParser(tokenStream);

            parser.RemoveErrorListeners();
            parser.AddErrorListener(new NovusErrorListener(diagnostics, testFile, source));

            parser.compilationUnit();

            // Assert
            Assert.True(diagnostics.HasErrors);

            var formatted = diagnostics.FormatDiagnostics();
            Assert.Contains("multiple_errors.novus", formatted);
        }
        finally
        {
            CleanupTempDir(testFile);
        }
    }

    [Fact]
    public void Test_CircularImportChain_ReportsFullPath()
    {
        // Arrange - A -> B -> C -> D -> A
        var moduleA = CreateTempFile("moduleA.novus", "from moduleB import *");
        var moduleB = CreateTempFile("moduleB.novus", "from moduleC import *");
        var moduleC = CreateTempFile("moduleC.novus", "from moduleD import *");
        var moduleD = CreateTempFile("moduleD.novus", "from moduleA import *");

        var diagnostics = new DiagnosticBag();
        var detector = new CircularImportDetector(diagnostics);

        try
        {
            // Act
            detector.EnterModule(moduleA);
            detector.RecordDependency(moduleA, moduleB);
            detector.ExitModule();

            detector.EnterModule(moduleB);
            detector.RecordDependency(moduleB, moduleC);
            detector.ExitModule();

            detector.EnterModule(moduleC);
            detector.RecordDependency(moduleC, moduleD);
            detector.ExitModule();

            detector.EnterModule(moduleD);
            var result = detector.RecordDependency(moduleD, moduleA);
            detector.ExitModule();

            // Assert
            Assert.False(result, "Should detect long circular chain");
            Assert.True(diagnostics.HasErrors);

            var errorMessage = diagnostics.FormatDiagnostics();
            // All modules should be mentioned
            Assert.Contains("moduleA.novus", errorMessage);
            Assert.Contains("moduleB.novus", errorMessage);
            Assert.Contains("moduleC.novus", errorMessage);
            Assert.Contains("moduleD.novus", errorMessage);

            // Should show import chain
            Assert.Contains("->", errorMessage);
        }
        finally
        {
            CleanupTempDir(moduleA);
            CleanupTempDir(moduleB);
            CleanupTempDir(moduleC);
            CleanupTempDir(moduleD);
        }
    }

    [Fact]
    public void Test_ValidProgram_NoErrorsOrWarnings()
    {
        // Arrange
        var source = @"
fn fibonacci(n: i32) -> i32 {
    if n <= 1 {
        return n
    }
    return fibonacci(n - 1) + fibonacci(n - 2)
}

fn main() -> i32 {
    return fibonacci(10)
}
";
        var testFile = CreateTempFile("valid.novus", source);
        var diagnostics = new DiagnosticBag();
        var cache = new ModuleCache();

        try
        {
            // Act
            var inputStream = new AntlrInputStream(source);
            var lexer = new NovusLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new NovusParser(tokenStream);

            parser.RemoveErrorListeners();
            parser.AddErrorListener(new NovusErrorListener(diagnostics, testFile, source));

            var parseTree = parser.compilationUnit();
            cache.Add(testFile, parseTree);

            // Assert
            Assert.False(diagnostics.HasErrors, "Valid program should have no errors");
            Assert.False(diagnostics.HasWarnings, "Valid program should have no warnings");
            Assert.Equal(1, cache.Count);
            Assert.True(cache.TryGet(testFile, out var cached));
            Assert.Same(parseTree, cached);
        }
        finally
        {
            CleanupTempDir(testFile);
        }
    }
}
