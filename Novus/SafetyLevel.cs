namespace Novus;

/// <summary>
/// Safety level for runtime checks
/// Controls how many runtime safety checks are inserted by the compiler
/// </summary>
public enum SafetyLevel
{
    /// <summary>
    /// Level 0: No runtime checks - "Footguns Enabled" 🔫
    /// - No bounds checking
    /// - No division-by-zero checks
    /// - No integer overflow checks
    /// - Maximum performance, maximum danger
    /// Use: Performance-critical code where you know what you're doing
    /// </summary>
    Unsafe = 0,

    /// <summary>
    /// Level 1: Basic safety checks (Default for Release)
    /// - Array bounds checking
    /// - Division-by-zero checks
    /// - No integer overflow (expensive)
    /// Use: Production code, good balance of safety and performance
    /// </summary>
    Basic = 1,

    /// <summary>
    /// Level 2: Full safety checks (Default for Debug)
    /// - All Level 1 checks
    /// - Integer overflow detection
    /// - Null pointer checks where possible
    /// Use: Development, catches most bugs
    /// </summary>
    Full = 2,

    /// <summary>
    /// Level 3: Paranoid mode 🛡️
    /// - All Level 2 checks
    /// - Stack canaries (future)
    /// - Additional runtime validation
    /// - Memory poisoning (future)
    /// Use: Finding subtle bugs, security-critical code
    /// </summary>
    Paranoid = 3
}

/// <summary>
/// Extension methods for SafetyLevel
/// </summary>
public static class SafetyLevelExtensions
{
    /// <summary>
    /// Check if bounds checking should be enabled
    /// </summary>
    public static bool EnableBoundsChecking(this SafetyLevel level)
    {
        return level >= SafetyLevel.Basic;
    }

    /// <summary>
    /// Check if division-by-zero checking should be enabled
    /// </summary>
    public static bool EnableDivisionByZeroChecks(this SafetyLevel level)
    {
        return level >= SafetyLevel.Basic;
    }

    /// <summary>
    /// Check if integer overflow checking should be enabled
    /// </summary>
    public static bool EnableOverflowChecks(this SafetyLevel level)
    {
        return level >= SafetyLevel.Full;
    }

    /// <summary>
    /// Check if null pointer checking should be enabled
    /// </summary>
    public static bool EnableNullChecks(this SafetyLevel level)
    {
        return level >= SafetyLevel.Full;
    }

    /// <summary>
    /// Check if paranoid-level checks should be enabled
    /// </summary>
    public static bool EnableParanoidChecks(this SafetyLevel level)
    {
        return level >= SafetyLevel.Paranoid;
    }

    /// <summary>
    /// Get default safety level for a build mode
    /// </summary>
    public static SafetyLevel GetDefaultForBuildMode(BuildMode mode)
    {
        return mode switch
        {
            BuildMode.Debug => SafetyLevel.Full,    // Level 2 for debug
            BuildMode.Release => SafetyLevel.Basic,  // Level 1 for release
            _ => SafetyLevel.Basic
        };
    }

    /// <summary>
    /// Get a friendly description of the safety level
    /// </summary>
    public static string GetDescription(this SafetyLevel level)
    {
        return level switch
        {
            SafetyLevel.Unsafe => "No runtime checks (footguns enabled)",
            SafetyLevel.Basic => "Basic safety checks (bounds, division-by-zero)",
            SafetyLevel.Full => "Full safety checks (includes overflow detection)",
            SafetyLevel.Paranoid => "Paranoid mode (maximum safety, slower)",
            _ => "Unknown"
        };
    }
}
