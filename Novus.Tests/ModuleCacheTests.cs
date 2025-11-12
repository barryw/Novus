using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Novus.Compilation;
using Novus.Frontend;
using Novus.Parser;

namespace Novus.Tests;

public class ModuleCacheTests
{
    private string CreateTempFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    private IParseTree ParseSource(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        return parser.compilationUnit();
    }

    [Fact]
    public void Test_CacheHit_ReturnsParseTree()
    {
        // Arrange
        var cache = new ModuleCache();
        var testFile = CreateTempFile("fn main() -> i32 { return 42 }");
        var parseTree = ParseSource("fn main() -> i32 { return 42 }");

        try
        {
            // Act
            cache.Add(testFile, parseTree);
            var result = cache.TryGet(testFile, out var cachedTree);

            // Assert
            Assert.True(result, "Cache should hit for newly added file");
            Assert.Same(parseTree, cachedTree);
            Assert.Equal(1, cache.Count);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public void Test_CacheMiss_ReturnsFalse()
    {
        // Arrange
        var cache = new ModuleCache();
        var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".novus");

        // Act
        var result = cache.TryGet(nonExistentFile, out var cachedTree);

        // Assert
        Assert.False(result, "Cache should miss for non-existent file");
        Assert.Null(cachedTree);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Test_FileModified_InvalidatesCache()
    {
        // Arrange
        var cache = new ModuleCache();
        var testFile = CreateTempFile("fn main() -> i32 { return 42 }");
        var parseTree1 = ParseSource("fn main() -> i32 { return 42 }");

        try
        {
            cache.Add(testFile, parseTree1);
            Assert.True(cache.TryGet(testFile, out var cachedTree1));
            Assert.Same(parseTree1, cachedTree1);

            // Modify the file
            Thread.Sleep(100); // Ensure timestamp difference
            File.WriteAllText(testFile, "fn main() -> i32 { return 99 }");

            // Act
            var result = cache.TryGet(testFile, out var cachedTree2);

            // Assert
            Assert.False(result, "Cache should miss after file modification");
            Assert.Null(cachedTree2);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public void Test_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var cache = new ModuleCache();
        var testFiles = Enumerable.Range(0, 10)
            .Select(i => CreateTempFile($"fn test{i}() -> i32 {{ return {i} }}"))
            .ToList();
        var parseTrees = Enumerable.Range(0, 10)
            .Select(i => ParseSource($"fn test{i}() -> i32 {{ return {i} }}"))
            .ToList();

        try
        {
            // Act - Add files concurrently
            Parallel.For(0, 10, i =>
            {
                cache.Add(testFiles[i], parseTrees[i]);
            });

            // Assert - Retrieve files concurrently
            var results = new bool[10];
            Parallel.For(0, 10, i =>
            {
                results[i] = cache.TryGet(testFiles[i], out var cachedTree);
                Assert.Same(parseTrees[i], cachedTree);
            });

            Assert.All(results, result => Assert.True(result));
            Assert.Equal(10, cache.Count);
        }
        finally
        {
            foreach (var file in testFiles)
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public void Test_MultipleAdds_UpdatesCache()
    {
        // Arrange
        var cache = new ModuleCache();
        var testFile = CreateTempFile("fn main() -> i32 { return 42 }");
        var parseTree1 = ParseSource("fn main() -> i32 { return 42 }");
        var parseTree2 = ParseSource("fn main() -> i32 { return 99 }");

        try
        {
            // Act
            cache.Add(testFile, parseTree1);
            cache.Add(testFile, parseTree2);
            var result = cache.TryGet(testFile, out var cachedTree);

            // Assert
            Assert.True(result);
            Assert.Same(parseTree2, cachedTree);
            Assert.Equal(1, cache.Count);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public void Test_Clear_RemovesAllEntries()
    {
        // Arrange
        var cache = new ModuleCache();
        var testFiles = Enumerable.Range(0, 5)
            .Select(i => CreateTempFile($"fn test{i}() -> i32 {{ return {i} }}"))
            .ToList();
        var parseTrees = Enumerable.Range(0, 5)
            .Select(i => ParseSource($"fn test{i}() -> i32 {{ return {i} }}"))
            .ToList();

        try
        {
            for (int i = 0; i < 5; i++)
            {
                cache.Add(testFiles[i], parseTrees[i]);
            }
            Assert.Equal(5, cache.Count);

            // Act
            cache.Clear();

            // Assert
            Assert.Equal(0, cache.Count);
            foreach (var file in testFiles)
            {
                Assert.False(cache.TryGet(file, out _));
            }
        }
        finally
        {
            foreach (var file in testFiles)
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public void Test_PathNormalization_WorksCorrectly()
    {
        // Arrange
        var cache = new ModuleCache();
        var testFile = CreateTempFile("fn main() -> i32 { return 42 }");
        var parseTree = ParseSource("fn main() -> i32 { return 42 }");

        try
        {
            // Act - Add with absolute path
            cache.Add(testFile, parseTree);

            // Try to get with the same absolute path (should normalize identically)
            var absolutePath = Path.GetFullPath(testFile);
            var absoluteResult = cache.TryGet(absolutePath, out var cachedTree);

            // Assert
            Assert.True(absoluteResult, "Cache should work with normalized paths");
            Assert.Same(parseTree, cachedTree);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public void Test_ContentHashValidation_DetectsRapidChanges()
    {
        // Arrange
        var cache = new ModuleCache();
        var testFile = CreateTempFile("fn main() -> i32 { return 42 }");
        var parseTree1 = ParseSource("fn main() -> i32 { return 42 }");

        try
        {
            // Add to cache
            cache.Add(testFile, parseTree1);
            Assert.True(cache.TryGet(testFile, out var cachedTree1));
            Assert.Same(parseTree1, cachedTree1);

            // Modify file content WITHOUT changing timestamp (simulating rapid edit within filesystem time granularity)
            // We can't actually control timestamps to test this perfectly, but we can verify the hash check exists
            var currentTimestamp = File.GetLastWriteTimeUtc(testFile);
            File.WriteAllText(testFile, "fn main() -> i32 { return 99 }");
            File.SetLastWriteTimeUtc(testFile, currentTimestamp);  // Reset timestamp to simulate race condition

            // Act
            var result = cache.TryGet(testFile, out var cachedTree2);

            // Assert - should detect content change even with same timestamp
            Assert.False(result, "Cache should detect content change via hash even if timestamp unchanged");
            Assert.Null(cachedTree2);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public void Test_DependencyTracking_InvalidatesOnDependencyChange()
    {
        // Arrange
        var cache = new ModuleCache();
        var depFile = CreateTempFile("pub fn helper() -> i32 { return 10 }");
        var mainFile = CreateTempFile("import dep; fn main() -> i32 { return dep::helper() }");

        var depParseTree = ParseSource("pub fn helper() -> i32 { return 10 }");
        var mainParseTree = ParseSource("import dep; fn main() -> i32 { return dep::helper() }");

        try
        {
            // Cache both files, with mainFile depending on depFile
            cache.Add(depFile, depParseTree);
            cache.Add(mainFile, mainParseTree, new[] { depFile });

            // Verify both are cached
            Assert.True(cache.TryGet(depFile, out _));
            Assert.True(cache.TryGet(mainFile, out _));

            // Modify dependency
            Thread.Sleep(100); // Ensure timestamp difference
            File.WriteAllText(depFile, "pub fn helper() -> i32 { return 20 }");

            // Act
            var depResult = cache.TryGet(depFile, out _);
            var mainResult = cache.TryGet(mainFile, out _);

            // Assert - both should be invalidated
            Assert.False(depResult, "Dependency should be invalidated after modification");
            Assert.False(mainResult, "Main file should be invalidated when dependency changes");
        }
        finally
        {
            File.Delete(depFile);
            File.Delete(mainFile);
        }
    }

    [Fact]
    public void Test_DependencyTracking_HandlesDeletedDependency()
    {
        // Arrange
        var cache = new ModuleCache();
        var depFile = CreateTempFile("pub fn helper() -> i32 { return 10 }");
        var mainFile = CreateTempFile("import dep; fn main() -> i32 { return dep::helper() }");

        var depParseTree = ParseSource("pub fn helper() -> i32 { return 10 }");
        var mainParseTree = ParseSource("import dep; fn main() -> i32 { return dep::helper() }");

        try
        {
            // Cache both files with dependency
            cache.Add(depFile, depParseTree);
            cache.Add(mainFile, mainParseTree, new[] { depFile });

            // Delete dependency
            File.Delete(depFile);

            // Act
            var mainResult = cache.TryGet(mainFile, out _);

            // Assert - main file should be invalidated
            Assert.False(mainResult, "Main file should be invalidated when dependency is deleted");
        }
        finally
        {
            if (File.Exists(depFile)) File.Delete(depFile);
            File.Delete(mainFile);
        }
    }

    [Fact]
    public void Test_DependencyTracking_NoDependencies_WorksNormally()
    {
        // Arrange
        var cache = new ModuleCache();
        var testFile = CreateTempFile("fn main() -> i32 { return 42 }");
        var parseTree = ParseSource("fn main() -> i32 { return 42 }");

        try
        {
            // Act - Add with null dependencies
            cache.Add(testFile, parseTree, null);
            var result = cache.TryGet(testFile, out var cachedTree);

            // Assert
            Assert.True(result, "Cache should work without dependencies");
            Assert.Same(parseTree, cachedTree);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public void Test_DependencyTracking_EmptyDependencies_WorksNormally()
    {
        // Arrange
        var cache = new ModuleCache();
        var testFile = CreateTempFile("fn main() -> i32 { return 42 }");
        var parseTree = ParseSource("fn main() -> i32 { return 42 }");

        try
        {
            // Act - Add with empty dependencies list
            cache.Add(testFile, parseTree, Array.Empty<string>());
            var result = cache.TryGet(testFile, out var cachedTree);

            // Assert
            Assert.True(result, "Cache should work with empty dependencies");
            Assert.Same(parseTree, cachedTree);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public void Test_DependencyTracking_ChainedDependencies()
    {
        // Arrange
        var cache = new ModuleCache();
        var fileA = CreateTempFile("pub fn a() -> i32 { return 1 }");
        var fileB = CreateTempFile("import a; pub fn b() -> i32 { return a::a() + 1 }");
        var fileC = CreateTempFile("import b; fn main() -> i32 { return b::b() + 1 }");

        var parseTreeA = ParseSource("pub fn a() -> i32 { return 1 }");
        var parseTreeB = ParseSource("import a; pub fn b() -> i32 { return a::a() + 1 }");
        var parseTreeC = ParseSource("import b; fn main() -> i32 { return b::b() + 1 }");

        try
        {
            // Cache with chained dependencies: C -> B -> A
            cache.Add(fileA, parseTreeA);
            cache.Add(fileB, parseTreeB, new[] { fileA });
            cache.Add(fileC, parseTreeC, new[] { fileB });

            // Verify all cached
            Assert.True(cache.TryGet(fileA, out _));
            Assert.True(cache.TryGet(fileB, out _));
            Assert.True(cache.TryGet(fileC, out _));

            // Modify A
            Thread.Sleep(100);
            File.WriteAllText(fileA, "pub fn a() -> i32 { return 999 }");

            // Act
            var resultA = cache.TryGet(fileA, out _);
            var resultB = cache.TryGet(fileB, out _);
            var resultC = cache.TryGet(fileC, out _);

            // Assert
            Assert.False(resultA, "A should be invalidated");
            Assert.False(resultB, "B should be invalidated when its dependency A changes");
            Assert.False(resultC, "C should be invalidated when its dependency B is invalidated");
        }
        finally
        {
            File.Delete(fileA);
            File.Delete(fileB);
            File.Delete(fileC);
        }
    }
}
