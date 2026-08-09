namespace Novus.Commands;

/// <summary>
/// Clean all cached artifacts to force fresh rebuilds.
/// This is the nuclear option for when you suspect stale code issues.
/// </summary>
public static class CleanCommand
{
    public static int Run(CleanOptions options)
    {
        var compilerDir = AppContext.BaseDirectory;
        int totalDeleted = 0;

        Console.WriteLine("Novus Clean - Removing all cached artifacts\n");

        // 1. Clean stdlib cache
        if (!options.KeepStdlibCache)
        {
            totalDeleted += CleanStdlibCache(compilerDir, options.Verbose);
        }

        // 2. Clean bin/std/ copy (force MSBuild to re-copy)
        if (!options.KeepBinStd)
        {
            totalDeleted += CleanBinStd(compilerDir, options.Verbose);
        }

        // 3. Clean user cache (~/.novus-cache)
        if (!options.KeepUserCache)
        {
            totalDeleted += CleanUserCache(options.Verbose);
        }

        // 4. Clean temp build directories
        if (!options.KeepTempDirs)
        {
            totalDeleted += CleanTempBuildDirs(options.Verbose);
        }

        Console.WriteLine($"\n✓ Clean complete: removed {totalDeleted} item(s)");
        return 0;
    }

    private static int CleanStdlibCache(string compilerDir, bool verbose)
    {
        var stdlibCacheDir = Path.Combine(compilerDir, "stdlib");
        if (!Directory.Exists(stdlibCacheDir))
        {
            if (verbose) Console.WriteLine("  [skip] stdlib cache not found");
            return 0;
        }

        var count = Directory.GetFiles(stdlibCacheDir, "*", SearchOption.AllDirectories).Length;
        Directory.Delete(stdlibCacheDir, recursive: true);

        Console.WriteLine($"  ✓ Stdlib cache: deleted {count} file(s)");
        return count;
    }

    private static int CleanBinStd(string compilerDir, bool verbose)
    {
        var binStdDir = Path.Combine(compilerDir, "std");
        if (!Directory.Exists(binStdDir))
        {
            if (verbose) Console.WriteLine("  [skip] bin/std not found");
            return 0;
        }

        int count = 0;
        try
        {
            var files = Directory.GetFiles(binStdDir, "*", SearchOption.AllDirectories);
            count = files.Length;
            Directory.Delete(binStdDir, recursive: true);
            Console.WriteLine($"  ✓ bin/std copy: deleted {count} file(s)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ bin/std: {ex.Message}");
        }

        return count;
    }

    private static int CleanUserCache(bool verbose)
    {
        var userCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".novus-cache");

        if (!Directory.Exists(userCacheDir))
        {
            if (verbose) Console.WriteLine("  [skip] user cache not found");
            return 0;
        }

        int count = 0;
        try
        {
            var files = Directory.GetFiles(userCacheDir, "*", SearchOption.AllDirectories);
            count = files.Length;
            Directory.Delete(userCacheDir, recursive: true);
            Console.WriteLine($"  ✓ User cache (~/.novus-cache): deleted {count} file(s)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ User cache: {ex.Message}");
        }

        return count;
    }

    private static int CleanTempBuildDirs(bool verbose)
    {
        var tempPath = Path.GetTempPath();
        var buildDirs = Directory.GetDirectories(tempPath, "novus-build-*");

        int count = 0;
        foreach (var dir in buildDirs)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
                count++;
            }
            catch
            {
                // Skip dirs that are locked
            }
        }

        if (count > 0)
        {
            Console.WriteLine($"  ✓ Temp build dirs: deleted {count} directory(ies)");
        }
        else if (verbose)
        {
            Console.WriteLine("  [skip] No temp build directories found");
        }

        return count;
    }
}
