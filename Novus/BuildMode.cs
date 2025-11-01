namespace Novus;

/// <summary>
/// Build mode for compilation (Debug or Release)
/// </summary>
public enum BuildMode
{
    /// <summary>
    /// Debug mode: No optimization, debug symbols, bounds checking, assertions
    /// </summary>
    Debug,

    /// <summary>
    /// Release mode: Full optimization, no debug symbols, no bounds checking
    /// </summary>
    Release
}
