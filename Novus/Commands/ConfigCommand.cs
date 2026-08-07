namespace Novus.Commands;

/// <summary>
/// Reads and writes ~/.novus/config.toml.
///
/// This exists for one reason: the NDK cannot ship with the compiler, so the user has to
/// tell Novus where theirs is, once, rather than on every command line.
/// </summary>
public static class ConfigCommand
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 1;

    public static int Run(ConfigOptions options)
    {
        var action = (options.Action ?? "show").ToLowerInvariant();

        return action switch
        {
            "show" or "list" or "get" => Show(),
            "set" => Set(options.Key, options.Value),
            "path" => Path(),
            _ => Usage($"unknown action '{options.Action}'"),
        };
    }

    private static int Show()
    {
        Console.WriteLine($"config file: {UserConfig.FilePath}" +
                          (File.Exists(UserConfig.FilePath) ? "" : " (not created yet)"));

        var configured = UserConfig.NdkPath;
        var resolved = UserConfig.ResolveNdkPath();

        Console.WriteLine($"ndk-path   : {configured ?? "(unset)"}");

        if (resolved == null)
        {
            Console.WriteLine();
            Console.WriteLine("No Amiga NDK found. Novus cannot bundle one, so set yours with:");
            Console.WriteLine("  novus config set ndk-path /path/to/NDK3.9");
            return ExitSuccess;
        }

        // Distinguish "you configured this" from "we guessed", so a surprising build is
        // traceable to where the path came from.
        if (configured == null)
            Console.WriteLine($"           -> using {resolved} (auto-detected)");
        else if (!Directory.Exists(resolved))
            Console.WriteLine($"           -> WARNING: {resolved} does not exist");
        else if (!UserConfig.LooksLikeNdk(resolved))
            Console.WriteLine($"           -> WARNING: {resolved} has no Include/include_h/exec/types.h");

        return ExitSuccess;
    }

    private static int Path()
    {
        Console.WriteLine(UserConfig.FilePath);
        return ExitSuccess;
    }

    private static int Set(string? key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            return Usage("usage: novus config set ndk-path <path>");

        if (key.ToLowerInvariant() is not ("ndk-path" or "ndk_path"))
            return Usage($"unknown setting '{key}' (known settings: ndk-path)");

        var full = System.IO.Path.GetFullPath(value);

        if (!Directory.Exists(full))
        {
            Console.Error.WriteLine($"error: no such directory: {full}");
            return ExitUsage;
        }

        // Refuse a directory that is not an NDK. Accepting it here would turn a typo into
        // a confusing "file 'exec/types.h' not found" on the next compile instead.
        if (!UserConfig.LooksLikeNdk(full))
        {
            Console.Error.WriteLine($"error: {full} does not look like an NDK 3.9 tree");
            Console.Error.WriteLine("       expected it to contain Include/include_h/exec/types.h");
            return ExitUsage;
        }

        UserConfig.SetNdkPath(full);
        Console.WriteLine($"ndk-path set to {full}");
        Console.WriteLine($"written to {UserConfig.FilePath}");
        return ExitSuccess;
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("usage:");
        Console.Error.WriteLine("  novus config show");
        Console.Error.WriteLine("  novus config set ndk-path <path>");
        Console.Error.WriteLine("  novus config path");
        return ExitUsage;
    }
}
