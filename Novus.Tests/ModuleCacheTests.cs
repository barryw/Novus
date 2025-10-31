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
}
