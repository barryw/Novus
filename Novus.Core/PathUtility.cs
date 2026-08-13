namespace Novus;

/// <summary>
/// Cross-platform path utilities for the Novus compiler.
/// Handles path normalization, environment variable resolution, and platform-specific concerns.
/// </summary>
public static class PathUtility
{
    /// <summary>
    /// Normalizes a path to use forward slashes for consistent comparison across platforms.
    /// This is useful for cache keys and manifest storage.
    /// </summary>
    public static string NormalizeForStorage(string path)
    {
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// Normalizes a path to use the current platform's directory separator.
    /// </summary>
    public static string NormalizeForPlatform(string path)
    {
        if (Path.DirectorySeparatorChar == '/')
            return path.Replace('\\', '/');
        else
            return path.Replace('/', '\\');
    }

    /// <summary>
    /// Compares two paths for equality, ignoring platform-specific separators.
    /// </summary>
    public static bool PathsEqual(string path1, string path2)
    {
        return NormalizeForStorage(path1) == NormalizeForStorage(path2);
    }

    /// <summary>
    /// Checks if a path contains a specific directory component, regardless of separator style.
    /// </summary>
    public static bool ContainsDirectory(string path, string directoryName)
    {
        var normalized = NormalizeForStorage(path);
        return normalized.Contains($"/{directoryName}/") ||
               normalized.StartsWith($"{directoryName}/") ||
               normalized.EndsWith($"/{directoryName}");
    }

    public static string GetModuleDisplayName(string path)
    {
        var normalized = NormalizeForStorage(path);
        var marker = normalized.LastIndexOf("/std/", StringComparison.Ordinal);
        if (marker < 0)
            return Path.GetFileNameWithoutExtension(path);

        var module = Path.ChangeExtension(normalized[(marker + 5)..], null)!
            .Replace("/", "::", StringComparison.Ordinal);
        return module.StartsWith("amiga::", StringComparison.Ordinal) ? module : $"std::{module}";
    }

    /// <summary>
    /// Gets the VBCC installation path. Vendored VBCC takes priority over system VBCC
    /// because it contains Novus-specific configuration (e.g., aos68k_fpu for FPU support).
    /// </summary>
    public static string GetVbccPath()
    {
        // PRIORITY 1: Try vendor directory relative to compiler location
        // Vendored VBCC takes priority because it contains Novus-specific config files
        var compilerDir = AppContext.BaseDirectory;
        var vendorVbcc = Path.Combine(compilerDir, "vendor", "vbcc");
        if (Directory.Exists(vendorVbcc))
            return vendorVbcc;

        // Try to walk up to find vendor directory (for dev builds)
        var current = compilerDir;
        for (int i = 0; i < 6; i++)
        {
            var parent = Path.GetDirectoryName(current);
            if (parent == null) break;
            current = parent;

            vendorVbcc = Path.Combine(current, "vendor", "vbcc");
            if (Directory.Exists(vendorVbcc))
                return vendorVbcc;
        }

        // PRIORITY 2: Check VBCC environment variable (system installation)
        var envPath = Environment.GetEnvironmentVariable("VBCC");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

        // Check VBCC_PATH as alternative
        envPath = Environment.GetEnvironmentVariable("VBCC_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            // VBCC_PATH might point to the vc binary, get directory
            var dir = Path.GetDirectoryName(envPath);
            if (dir != null)
            {
                var parentDir = Path.GetDirectoryName(dir);
                if (parentDir != null && Directory.Exists(parentDir))
                    return parentDir;
            }
        }

        // Platform-specific common locations
        if (OperatingSystem.IsMacOS())
        {
            var macPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "amiga-cc", "vbcc"),
                "/opt/amiga/vbcc",
                "/usr/local/amiga/vbcc"
            };
            foreach (var path in macPaths)
            {
                if (Directory.Exists(path))
                    return path;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var linuxPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "amiga-cc", "vbcc"),
                "/opt/amiga/vbcc",
                "/usr/local/amiga/vbcc"
            };
            foreach (var path in linuxPaths)
            {
                if (Directory.Exists(path))
                    return path;
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            var winPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "amiga-cc", "vbcc"),
                @"C:\amiga\vbcc",
                @"C:\Program Files\vbcc"
            };
            foreach (var path in winPaths)
            {
                if (Directory.Exists(path))
                    return path;
            }
        }

        // Return a reasonable default path (won't exist, but makes error message clearer)
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "amiga-cc", "vbcc");
    }

    /// <summary>
    /// Gets the NDK installation path from environment variable or returns a default.
    /// </summary>
    public static string GetNdkPath()
    {
        // Check environment variable first
        var envPath = Environment.GetEnvironmentVariable("NDK_PATH");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

        // Check NDK39 as alternative
        envPath = Environment.GetEnvironmentVariable("NDK39");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

        // Platform-specific common locations
        if (OperatingSystem.IsMacOS())
        {
            var macPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "amiga-cc", "NDK3.9"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "amiga-cc", "NDK_3.9"),
                "/opt/amiga/NDK3.9"
            };
            foreach (var path in macPaths)
            {
                if (Directory.Exists(path))
                    return path;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var linuxPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "amiga-cc", "NDK3.9"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "amiga-cc", "NDK_3.9"),
                "/opt/amiga/NDK3.9"
            };
            foreach (var path in linuxPaths)
            {
                if (Directory.Exists(path))
                    return path;
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            var winPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "amiga-cc", "NDK3.9"),
                @"C:\amiga\NDK3.9",
                @"C:\Program Files\NDK3.9"
            };
            foreach (var path in winPaths)
            {
                if (Directory.Exists(path))
                    return path;
            }
        }

        // Return a reasonable default path
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "amiga-cc", "NDK3.9");
    }

    /// <summary>
    /// Finds the standard library path relative to a base directory (compiler location or project root).
    /// </summary>
    public static string? FindStdLibPath(string? baseDirectory = null)
    {
        baseDirectory ??= AppContext.BaseDirectory;

        // Possible locations to check
        var possiblePaths = new[]
        {
            Path.Combine(baseDirectory, "std"),
            Path.Combine(baseDirectory, "..", "..", "..", "..", "Novus", "std"),
            Path.Combine(baseDirectory, "..", "..", "..", "Novus", "std"),
            Path.Combine(baseDirectory, "..", "Novus", "std"),
            Path.Combine(baseDirectory, "Novus", "std")
        };

        foreach (var path in possiblePaths)
        {
            var normalizedPath = Path.GetFullPath(path);
            if (Directory.Exists(normalizedPath))
                return normalizedPath;
        }

        return null;
    }

    /// <summary>
    /// Gets the compiler's runtime resource directory.
    ///
    /// MSBuild copies runtime/ next to the compiler binary in both the dev layout
    /// (Novus/bin/&lt;cfg&gt;/&lt;tfm&gt;/) and the published layout, so it always sits directly
    /// beside the executable. Resolving it by walking a fixed number of parent
    /// directories only works for one of those layouts.
    /// </summary>
    public static string GetRuntimeDir(string? baseDirectory = null)
    {
        return Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "runtime");
    }

    /// <summary>
    /// Finds a file in the compiler's runtime directory, or returns null if it is missing.
    ///
    /// Callers must treat null as a fatal installation error. Skipping a missing runtime
    /// file silently produces a program that compiles but cannot link, and the failure
    /// surfaces as an undefined symbol in generated C rather than as a Novus error.
    /// </summary>
    public static string? FindRuntimeFile(string fileName, string? baseDirectory = null)
    {
        var path = Path.Combine(GetRuntimeDir(baseDirectory), fileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Finds the project root directory by looking for Novus.sln.
    /// </summary>
    public static string? FindProjectRoot(string? startDirectory = null)
    {
        var directory = startDirectory ?? Directory.GetCurrentDirectory();

        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "Novus.sln")))
                return directory;

            var parent = Directory.GetParent(directory);
            if (parent == null)
                break;
            directory = parent.FullName;
        }

        return null;
    }
}
