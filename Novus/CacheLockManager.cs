using System.Diagnostics;

namespace Novus;

/// <summary>
/// File-based locking manager for cross-process cache coordination.
/// Uses FileStream with exclusive access for portable file locking that works
/// across multiple processes (e.g., parallel test execution).
///
/// Key design decisions:
/// - Uses FileShare.None for exclusive access (cross-process safe)
/// - FileOptions.DeleteOnClose for automatic cleanup
/// - Supports timeout-based lock acquisition with polling
/// - Disposable pattern for automatic lock release
/// </summary>
public class CacheLockManager : IDisposable
{
    private readonly string _lockDir;
    private readonly Dictionary<string, LockHandle> _activeLocks = new();
    private bool _disposed;

    public CacheLockManager(string cacheDir)
    {
        _lockDir = Path.Combine(cacheDir, ".locks");
        Directory.CreateDirectory(_lockDir);
    }

    /// <summary>
    /// Acquire an exclusive lock for the given lock name.
    /// Returns a disposable handle that releases the lock when disposed.
    /// </summary>
    /// <param name="lockName">Name of the lock (e.g., "stdlib-build")</param>
    /// <param name="timeout">Maximum time to wait for lock acquisition</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Disposable lock handle, or null if acquisition failed</returns>
    public async Task<IDisposable?> AcquireLockAsync(
        string lockName,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var lockFile = Path.Combine(_lockDir, $"{lockName}.lock");
        var stopwatch = Stopwatch.StartNew();
        var pollInterval = TimeSpan.FromMilliseconds(50);

        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Try to create/open lock file with exclusive access
                // FileShare.None ensures no other process can access the file
                var fs = new FileStream(
                    lockFile,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None); // Don't use DeleteOnClose - we manage cleanup

                // Write process info for debugging purposes
                try
                {
                    fs.SetLength(0);
                    using var writer = new StreamWriter(fs, leaveOpen: true);
                    await writer.WriteLineAsync($"PID: {Environment.ProcessId}");
                    await writer.WriteLineAsync($"Time: {DateTime.UtcNow:O}");
                    await writer.WriteLineAsync($"Thread: {Environment.CurrentManagedThreadId}");
                    await writer.FlushAsync(cancellationToken);
                }
                catch
                {
                    // Ignore write errors - lock is still valid
                }

                var handle = new LockHandle(lockFile, fs, this);
                lock (_activeLocks)
                {
                    _activeLocks[lockName] = handle;
                }
                return handle;
            }
            catch (IOException)
            {
                // Lock is held by another process - wait and retry
                await Task.Delay(pollInterval, cancellationToken);

                // Exponential backoff (up to 500ms)
                pollInterval = TimeSpan.FromMilliseconds(
                    Math.Min(pollInterval.TotalMilliseconds * 1.5, 500));
            }
        }

        // Timeout - could not acquire lock
        return null;
    }

    /// <summary>
    /// Try to acquire a lock without waiting.
    /// Returns immediately with lock handle or null.
    /// </summary>
    public IDisposable? TryAcquireLock(string lockName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var lockFile = Path.Combine(_lockDir, $"{lockName}.lock");

        try
        {
            var fs = new FileStream(
                lockFile,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.None);

            var handle = new LockHandle(lockFile, fs, this);
            lock (_activeLocks)
            {
                _activeLocks[lockName] = handle;
            }
            return handle;
        }
        catch (IOException)
        {
            // Lock is held by another process
            return null;
        }
    }

    internal void ReleaseLock(string lockFile, LockHandle handle)
    {
        var lockName = Path.GetFileNameWithoutExtension(lockFile);
        lock (_activeLocks)
        {
            if (_activeLocks.TryGetValue(lockName, out var current) && current == handle)
            {
                _activeLocks.Remove(lockName);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Release all active locks
        List<LockHandle> locks;
        lock (_activeLocks)
        {
            locks = _activeLocks.Values.ToList();
            _activeLocks.Clear();
        }

        foreach (var handle in locks)
        {
            try
            {
                handle.Dispose();
            }
            catch
            {
                // Best effort cleanup
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Represents an active lock. Disposing releases the lock.
    /// </summary>
    public sealed class LockHandle : IDisposable
    {
        private readonly string _lockFile;
        private readonly FileStream _stream;
        private readonly CacheLockManager _manager;
        private bool _disposed;

        internal LockHandle(string lockFile, FileStream stream, CacheLockManager manager)
        {
            _lockFile = lockFile;
            _stream = stream;
            _manager = manager;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _stream.Dispose();
            }
            catch
            {
                // Best effort
            }

            try
            {
                // Clean up lock file
                if (File.Exists(_lockFile))
                {
                    File.Delete(_lockFile);
                }
            }
            catch
            {
                // Best effort - file might be locked by another process
            }

            _manager.ReleaseLock(_lockFile, this);
        }
    }
}

/// <summary>
/// Provides atomic cache write operations using temp directory + rename pattern.
/// This prevents other processes from seeing partially-written cache entries.
/// </summary>
public static class AtomicCacheWriter
{
    /// <summary>
    /// Write files to a cache directory atomically.
    /// Uses a temporary directory and atomic rename to ensure all-or-nothing semantics.
    /// </summary>
    /// <param name="targetDir">The final cache directory</param>
    /// <param name="writeAction">Action that writes files to the temp directory</param>
    /// <param name="completionMarkerName">Name of the completion marker file (default: ".complete")</param>
    public static async Task WriteAtomicallyAsync(
        string targetDir,
        Func<string, Task> writeAction,
        string completionMarkerName = ".complete")
    {
        // Create unique temp directory
        var parentDir = Path.GetDirectoryName(targetDir) ?? Path.GetTempPath();
        var tempDir = Path.Combine(parentDir, $".tmp_{Path.GetFileName(targetDir)}_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);

            // Write all files to temp directory
            await writeAction(tempDir);

            // Write completion marker
            var completionMarker = Path.Combine(tempDir, completionMarkerName);
            await File.WriteAllTextAsync(completionMarker,
                $"Completed: {DateTime.UtcNow:O}\nPID: {Environment.ProcessId}");

            // Atomic move: if target exists, remove it first
            if (Directory.Exists(targetDir))
            {
                // Remove old directory
                Directory.Delete(targetDir, recursive: true);
            }

            // Move temp to target
            Directory.Move(tempDir, targetDir);
        }
        catch
        {
            // Clean up temp directory on failure
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
            throw;
        }
    }

    /// <summary>
    /// Write a single file atomically using temp file + rename pattern.
    /// </summary>
    public static async Task WriteFileAtomicallyAsync(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? ".";
        var tempPath = Path.Combine(directory, $".tmp_{Path.GetFileName(targetPath)}_{Guid.NewGuid():N}");

        try
        {
            await File.WriteAllTextAsync(tempPath, content);

            // Delete target if exists, then move
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(tempPath, targetPath);
        }
        catch
        {
            // Clean up temp file on failure
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best effort
            }
            throw;
        }
    }

    /// <summary>
    /// Write a file atomically by copying bytes, using temp file + rename pattern.
    /// </summary>
    public static async Task WriteFileAtomicallyAsync(string targetPath, byte[] content)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? ".";
        var tempPath = Path.Combine(directory, $".tmp_{Path.GetFileName(targetPath)}_{Guid.NewGuid():N}");

        try
        {
            await File.WriteAllBytesAsync(tempPath, content);

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(tempPath, targetPath);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch { }
            throw;
        }
    }

    /// <summary>
    /// Copy a file atomically to target path.
    /// </summary>
    public static void CopyFileAtomically(string sourcePath, string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? ".";
        var tempPath = Path.Combine(directory, $".tmp_{Path.GetFileName(targetPath)}_{Guid.NewGuid():N}");

        try
        {
            File.Copy(sourcePath, tempPath, overwrite: true);

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(tempPath, targetPath);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch { }
            throw;
        }
    }

    /// <summary>
    /// Check if a cache directory is complete (has completion marker).
    /// </summary>
    public static bool IsCacheComplete(string cacheDir, string completionMarkerName = ".complete")
    {
        if (!Directory.Exists(cacheDir))
            return false;

        var completionMarker = Path.Combine(cacheDir, completionMarkerName);
        return File.Exists(completionMarker);
    }
}
