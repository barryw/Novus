using Tomlyn;
using Tomlyn.Model;

namespace Novus;

/// <summary>
/// Per-user settings, stored in ~/.novus/config.toml.
///
/// The Amiga NDK cannot be redistributed, so Novus cannot carry one. Its location is a
/// property of the machine rather than of the compiler, which means it has to be recorded
/// somewhere durable - otherwise every invocation needs --ndk-path, and every CI job and
/// editor integration needs to know to pass it.
/// </summary>
public static class UserConfig
{
    private const string NdkPathKey = "ndk_path";

    public static string DirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".novus");

    public static string FilePath => Path.Combine(DirectoryPath, "config.toml");

    /// <summary>
    /// The configured NDK path, or null when unset. A configured-but-missing directory is
    /// returned as-is: reporting the stale path the user actually set is more useful than
    /// silently behaving as if nothing were configured.
    /// </summary>
    public static string? NdkPath => Read(NdkPathKey);

    public static void SetNdkPath(string path) => Write(NdkPathKey, path);

    /// <summary>
    /// Where the NDK is, in precedence order: explicit flag, environment, user config,
    /// then the usual install locations. Returns null when nothing plausible is found.
    /// </summary>
    public static string? ResolveNdkPath(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return explicitPath;

        foreach (var variable in new[] { "NDK", "NDK_PATH", "NDK39" })
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
                return value;
        }

        var configured = NdkPath;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var candidate in new[]
                 {
                     Path.Combine(home, "amiga-cc", "NDK3.9"),
                     Path.Combine(home, "amiga-cc", "NDK_3.9"),
                     "/opt/amiga/NDK3.9",
                     @"C:\amiga\NDK3.9",
                 })
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Whether a directory actually looks like an NDK 3.9 tree. Checked when the path is
    /// set rather than only when it is used, so a typo fails at the point the user can
    /// still see what they typed.
    /// </summary>
    public static bool LooksLikeNdk(string path) =>
        File.Exists(Path.Combine(path, "Include", "include_h", "exec", "types.h"));

    /// <summary>The include directory a compile needs from the NDK.</summary>
    public static string IncludeDir(string ndkPath) => Path.Combine(ndkPath, "Include", "include_h");

    /// <summary>
    /// Resolve the NDK or fail with instructions. Call this before doing any work: the
    /// alternative is compiling the whole stdlib and only then reporting a missing NDK.
    /// </summary>
    public static string RequireNdkPath(string? explicitPath = null)
    {
        var path = ResolveNdkPath(explicitPath);

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new DirectoryNotFoundException(
                $"Amiga NDK not found{(string.IsNullOrWhiteSpace(path) ? "" : $" at {path}")}. " +
                "Novus cannot bundle the NDK, so point it at yours:\n" +
                "  novus config set ndk-path /path/to/NDK3.9\n" +
                "(or pass --ndk-path, or set the NDK environment variable)");

        if (!LooksLikeNdk(path))
            throw new DirectoryNotFoundException(
                $"{path} does not look like an NDK 3.9 tree: no Include/include_h/exec/types.h. " +
                "Set the correct path with 'novus config set ndk-path /path/to/NDK3.9'.");

        return path;
    }

    private static string? Read(string key)
    {
        if (!File.Exists(FilePath))
            return null;

        try
        {
            var model = Toml.ToModel(File.ReadAllText(FilePath));
            return model.TryGetValue(key, out var value) ? value as string : null;
        }
        catch (Exception)
        {
            // A corrupt config must not make the compiler unusable; it behaves as unset,
            // and `novus config show` reports where the file is so it can be repaired.
            return null;
        }
    }

    private static void Write(string key, string value)
    {
        TomlTable model;
        try
        {
            model = File.Exists(FilePath)
                ? Toml.ToModel(File.ReadAllText(FilePath))
                : new TomlTable();
        }
        catch (Exception)
        {
            model = new TomlTable();
        }

        model[key] = value;
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, Toml.FromModel(model));
    }
}
