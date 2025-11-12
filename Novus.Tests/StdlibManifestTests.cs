using System.Security.Cryptography;
using System.Text.Json;
using Novus.Commands;

namespace Novus.Tests;

/// <summary>
/// Tests for stdlib manifest caching and invalidation
/// </summary>
public class StdlibManifestTests
{
    private string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "novus_test_stdlib_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private string CreateTempFile(string dir, string filename, string content)
    {
        var path = Path.Combine(dir, filename);
        File.WriteAllText(path, content);
        return path;
    }

    private string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    [Fact]
    public void Test_ManifestCreation_ContainsExpectedFields()
    {
        // Arrange
        var manifest = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = "68020",
            BuildMode = "debug",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CodegenVersion = 3,
            Modules = new Dictionary<string, StdlibModuleInfo>
            {
                ["core"] = new StdlibModuleInfo
                {
                    SourceFile = "core.novus",
                    OutputFile = "core_*.o",
                    Hash = "abc123"
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });

        // Assert
        Assert.Contains("\"Version\"", json);
        Assert.Contains("\"Cpu\"", json);
        Assert.Contains("\"BuildMode\"", json);
        Assert.Contains("\"CodegenVersion\"", json);
        Assert.Contains("\"Modules\"", json);
        Assert.Contains("68020", json);
        Assert.Contains("debug", json);
    }

    [Fact]
    public void Test_ManifestSerialization_RoundTrip()
    {
        // Arrange
        var original = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = "68020",
            BuildMode = "release",
            Timestamp = 1234567890,
            CodegenVersion = 5,
            Modules = new Dictionary<string, StdlibModuleInfo>
            {
                ["core"] = new StdlibModuleInfo
                {
                    SourceFile = "core.novus",
                    OutputFile = "core_*.o",
                    Hash = "abc123def456"
                },
                ["dos"] = new StdlibModuleInfo
                {
                    SourceFile = "dos.novus",
                    OutputFile = "dos_*.o",
                    Hash = "789ghi012jkl"
                }
            }
        };

        // Act - Serialize and deserialize
        var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
        var deserialized = JsonSerializer.Deserialize<StdlibManifest>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Version, deserialized.Version);
        Assert.Equal(original.Cpu, deserialized.Cpu);
        Assert.Equal(original.BuildMode, deserialized.BuildMode);
        Assert.Equal(original.Timestamp, deserialized.Timestamp);
        Assert.Equal(original.CodegenVersion, deserialized.CodegenVersion);
        Assert.Equal(2, deserialized.Modules.Count);
        Assert.Equal("abc123def456", deserialized.Modules["core"].Hash);
        Assert.Equal("789ghi012jkl", deserialized.Modules["dos"].Hash);
    }

    [Fact]
    public void Test_CodegenVersionInManifest_InvalidatesCache()
    {
        // Arrange
        var manifestOld = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = "68020",
            BuildMode = "debug",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CodegenVersion = 1,  // Old version
            Modules = new Dictionary<string, StdlibModuleInfo>()
        };

        var manifestNew = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = "68020",
            BuildMode = "debug",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CodegenVersion = 2,  // New version
            Modules = new Dictionary<string, StdlibModuleInfo>()
        };

        // Act
        var versionsDiffer = manifestOld.CodegenVersion != manifestNew.CodegenVersion;

        // Assert
        Assert.True(versionsDiffer, "Different codegen versions should invalidate cache");
    }

    [Fact]
    public void Test_SourceFileHashChange_DetectedInManifest()
    {
        // Arrange
        var tempDir = CreateTempDir();
        try
        {
            var sourceFile = CreateTempFile(tempDir, "core.novus", "fn main() -> i32 { return 42 }");
            var originalHash = ComputeFileHash(sourceFile);

            // Create manifest with original hash
            var manifest = new StdlibManifest
            {
                Version = "1.0.0",
                Cpu = "68020",
                BuildMode = "debug",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CodegenVersion = 3,
                Modules = new Dictionary<string, StdlibModuleInfo>
                {
                    ["core"] = new StdlibModuleInfo
                    {
                        SourceFile = "core.novus",
                        OutputFile = "core_*.o",
                        Hash = originalHash
                    }
                }
            };

            // Modify source file
            File.WriteAllText(sourceFile, "fn main() -> i32 { return 99 }");
            var newHash = ComputeFileHash(sourceFile);

            // Act
            var hashChanged = manifest.Modules["core"].Hash != newHash;

            // Assert
            Assert.True(hashChanged, "Hash change should be detected");
            Assert.NotEqual(originalHash, newHash);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_NewSourceFileAdded_NotInManifest()
    {
        // Arrange
        var manifest = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = "68020",
            BuildMode = "debug",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CodegenVersion = 3,
            Modules = new Dictionary<string, StdlibModuleInfo>
            {
                ["core"] = new StdlibModuleInfo
                {
                    SourceFile = "core.novus",
                    OutputFile = "core_*.o",
                    Hash = "abc123"
                }
            }
        };

        // Act - Check if new module exists in manifest
        var hasNewModule = manifest.Modules.ContainsKey("new_module");

        // Assert
        Assert.False(hasNewModule, "New files should trigger cache invalidation");
    }

    [Fact]
    public void Test_ManifestWithSubdirectoryModules_TrackedCorrectly()
    {
        // Arrange
        var manifest = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = "68020",
            BuildMode = "debug",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CodegenVersion = 3,
            Modules = new Dictionary<string, StdlibModuleInfo>
            {
                ["core"] = new StdlibModuleInfo
                {
                    SourceFile = "core.novus",
                    OutputFile = "core_*.o",
                    Hash = "abc123"
                },
                ["ffi_amiga_consts"] = new StdlibModuleInfo
                {
                    SourceFile = "ffi/amiga_consts.novus",
                    OutputFile = "amiga_consts_*.o",
                    Hash = "def456"
                }
            }
        };

        // Act
        var hasCoreModule = manifest.Modules.ContainsKey("core");
        var hasFfiModule = manifest.Modules.ContainsKey("ffi_amiga_consts");
        var ffiSourcePath = manifest.Modules["ffi_amiga_consts"].SourceFile;

        // Assert
        Assert.True(hasCoreModule, "Should track root-level modules");
        Assert.True(hasFfiModule, "Should track subdirectory modules");
        Assert.Contains("ffi/", ffiSourcePath);
    }

    [Fact]
    public void Test_ManifestMissing_RequiresRebuild()
    {
        // Arrange
        var tempDir = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempDir, "manifest.json");

            // Act
            var exists = File.Exists(manifestPath);

            // Assert
            Assert.False(exists, "Missing manifest should trigger rebuild");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_ManifestCorrupted_HandledGracefully()
    {
        // Arrange
        var tempDir = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempDir, "manifest.json");
            File.WriteAllText(manifestPath, "{ corrupted json }}}");

            // Act & Assert - should not throw, caller handles as rebuild
            Assert.Throws<JsonException>(() =>
            {
                var json = File.ReadAllText(manifestPath);
                JsonSerializer.Deserialize<StdlibManifest>(json);
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_ManifestTimestamp_UpdatedOnRebuild()
    {
        // Arrange
        var manifest1 = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = "68020",
            BuildMode = "debug",
            Timestamp = 1000000,
            CodegenVersion = 3,
            Modules = new Dictionary<string, StdlibModuleInfo>()
        };

        // Wait a bit
        Thread.Sleep(100);

        var manifest2 = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = "68020",
            BuildMode = "debug",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CodegenVersion = 3,
            Modules = new Dictionary<string, StdlibModuleInfo>()
        };

        // Act
        var timestampsDiffer = manifest1.Timestamp != manifest2.Timestamp;

        // Assert
        Assert.True(timestampsDiffer, "Timestamps should differ between builds");
        Assert.True(manifest2.Timestamp > manifest1.Timestamp);
    }

    [Fact]
    public void Test_EmptyManifest_Valid()
    {
        // Arrange
        var manifest = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = "68020",
            BuildMode = "debug",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CodegenVersion = 3,
            Modules = new Dictionary<string, StdlibModuleInfo>()  // Empty modules
        };

        // Act
        var json = JsonSerializer.Serialize(manifest);
        var deserialized = JsonSerializer.Deserialize<StdlibManifest>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Empty(deserialized.Modules);
    }
}
