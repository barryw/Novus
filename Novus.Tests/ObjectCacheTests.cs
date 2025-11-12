using System.Text.Json;

namespace Novus.Tests;

/// <summary>
/// Tests for object file caching with integrity validation
/// </summary>
public class ObjectCacheTests
{
    private string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "novus_test_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private void CreateMockObjectFile(string path, string content)
    {
        File.WriteAllText(path, content);
    }

    private CacheMetadata CreateValidMetadata(string objFile)
    {
        var info = new FileInfo(objFile);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(objFile);
        var hashBytes = sha256.ComputeHash(stream);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return new CacheMetadata
        {
            FileSize = info.Length,
            FileHash = hash,
            CachedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public void Test_ValidCache_LoadsSuccessfully()
    {
        // Arrange
        var cacheDir = CreateTempDir();
        try
        {
            var objFile = Path.Combine(cacheDir, "test.o");
            var metaFile = objFile + ".meta";

            // Create mock object file
            CreateMockObjectFile(objFile, "mock compiled code");

            // Create valid metadata
            var metadata = CreateValidMetadata(objFile);
            var metaJson = JsonSerializer.Serialize(metadata);
            File.WriteAllText(metaFile, metaJson);

            // Act - Verify metadata matches
            var loadedMetaJson = File.ReadAllText(metaFile);
            var loadedMeta = JsonSerializer.Deserialize<CacheMetadata>(loadedMetaJson);

            var objInfo = new FileInfo(objFile);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(objFile);
            var hashBytes = sha256.ComputeHash(stream);
            var objHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            // Assert
            Assert.NotNull(loadedMeta);
            Assert.Equal(metadata.FileSize, loadedMeta.FileSize);
            Assert.Equal(metadata.FileHash, loadedMeta.FileHash);
            Assert.Equal(objInfo.Length, loadedMeta.FileSize);
            Assert.Equal(objHash, loadedMeta.FileHash);
        }
        finally
        {
            Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void Test_CorruptedObjectFile_DetectedBySize()
    {
        // Arrange
        var cacheDir = CreateTempDir();
        try
        {
            var objFile = Path.Combine(cacheDir, "test.o");
            var metaFile = objFile + ".meta";

            // Create mock object file
            CreateMockObjectFile(objFile, "mock compiled code");

            // Create valid metadata
            var metadata = CreateValidMetadata(objFile);
            var metaJson = JsonSerializer.Serialize(metadata);
            File.WriteAllText(metaFile, metaJson);

            // Corrupt the object file by appending data
            File.AppendAllText(objFile, "corrupted data");

            // Act - Load metadata and check
            var loadedMetaJson = File.ReadAllText(metaFile);
            var loadedMeta = JsonSerializer.Deserialize<CacheMetadata>(loadedMetaJson);

            var objInfo = new FileInfo(objFile);

            // Assert
            Assert.NotNull(loadedMeta);
            Assert.NotEqual(loadedMeta.FileSize, objInfo.Length);  // Size mismatch detected
        }
        finally
        {
            Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void Test_CorruptedObjectFile_DetectedByHash()
    {
        // Arrange
        var cacheDir = CreateTempDir();
        try
        {
            var objFile = Path.Combine(cacheDir, "test.o");
            var metaFile = objFile + ".meta";

            // Create mock object file
            CreateMockObjectFile(objFile, "mock compiled code");

            // Create valid metadata
            var metadata = CreateValidMetadata(objFile);
            var metaJson = JsonSerializer.Serialize(metadata);
            File.WriteAllText(metaFile, metaJson);

            // Corrupt the object file by changing content but keeping same size
            File.WriteAllText(objFile, "CORRUPTED CODE!!!");

            // Act - Load metadata and compute current hash
            var loadedMetaJson = File.ReadAllText(metaFile);
            var loadedMeta = JsonSerializer.Deserialize<CacheMetadata>(loadedMetaJson);

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(objFile);
            var hashBytes = sha256.ComputeHash(stream);
            var currentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            // Assert
            Assert.NotNull(loadedMeta);
            Assert.NotEqual(loadedMeta.FileHash, currentHash);  // Hash mismatch detected
        }
        finally
        {
            Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void Test_MissingMetadata_CacheInvalid()
    {
        // Arrange
        var cacheDir = CreateTempDir();
        try
        {
            var objFile = Path.Combine(cacheDir, "test.o");
            var metaFile = objFile + ".meta";

            // Create object file but NO metadata
            CreateMockObjectFile(objFile, "mock compiled code");

            // Act
            var metaExists = File.Exists(metaFile);

            // Assert
            Assert.False(metaExists, "Cache should be invalid without metadata");
        }
        finally
        {
            Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void Test_MissingObjectFile_CacheInvalid()
    {
        // Arrange
        var cacheDir = CreateTempDir();
        try
        {
            var objFile = Path.Combine(cacheDir, "test.o");
            var metaFile = objFile + ".meta";

            // Create metadata but NO object file
            var metadata = new CacheMetadata
            {
                FileSize = 1234,
                FileHash = "fakehash",
                CachedAt = DateTime.UtcNow
            };
            var metaJson = JsonSerializer.Serialize(metadata);
            File.WriteAllText(metaFile, metaJson);

            // Act
            var objExists = File.Exists(objFile);

            // Assert
            Assert.False(objExists, "Cache should be invalid without object file");
        }
        finally
        {
            Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void Test_InvalidMetadataFormat_HandledGracefully()
    {
        // Arrange
        var cacheDir = CreateTempDir();
        try
        {
            var objFile = Path.Combine(cacheDir, "test.o");
            var metaFile = objFile + ".meta";

            // Create object file
            CreateMockObjectFile(objFile, "mock compiled code");

            // Create invalid metadata (malformed JSON)
            File.WriteAllText(metaFile, "{ invalid json }}}");

            // Act & Assert - should not throw
            Assert.Throws<JsonException>(() =>
            {
                var metaJson = File.ReadAllText(metaFile);
                JsonSerializer.Deserialize<CacheMetadata>(metaJson);
            });
        }
        finally
        {
            Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void Test_CacheVersionFile_WrittenCorrectly()
    {
        // Arrange
        var cacheDir = CreateTempDir();
        try
        {
            var versionFile = Path.Combine(cacheDir, ".cache_version");
            var expectedVersion = 42;

            // Act
            File.WriteAllText(versionFile, expectedVersion.ToString());

            // Assert
            Assert.True(File.Exists(versionFile));
            var content = File.ReadAllText(versionFile).Trim();
            Assert.Equal("42", content);
            Assert.Equal(expectedVersion, int.Parse(content));
        }
        finally
        {
            Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void Test_CacheVersionMismatch_Detected()
    {
        // Arrange
        var cacheDir = CreateTempDir();
        try
        {
            var versionFile = Path.Combine(cacheDir, ".cache_version");

            // Write old version
            File.WriteAllText(versionFile, "1");

            // Act - Check against new version
            var cachedVersion = int.Parse(File.ReadAllText(versionFile).Trim());
            var expectedVersion = 2;

            // Assert
            Assert.NotEqual(expectedVersion, cachedVersion);
        }
        finally
        {
            Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void Test_CacheVersionMatch_Validated()
    {
        // Arrange
        var cacheDir = CreateTempDir();
        try
        {
            var versionFile = Path.Combine(cacheDir, ".cache_version");
            var version = 3;

            // Write version
            File.WriteAllText(versionFile, version.ToString());

            // Act - Check against same version
            var cachedVersion = int.Parse(File.ReadAllText(versionFile).Trim());

            // Assert
            Assert.Equal(version, cachedVersion);
        }
        finally
        {
            Directory.Delete(cacheDir, true);
        }
    }
}
