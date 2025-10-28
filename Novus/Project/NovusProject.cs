namespace Novus.Project;

/// <summary>
/// Represents a novus.toml project file
/// </summary>
public class NovusProject
{
    public PackageSection Package { get; set; } = new();
    public BuildSection Build { get; set; } = new();
    public PathsSection Paths { get; set; } = new();
    public Dictionary<string, DependencySection> Dependencies { get; set; } = new();
}

public class PackageSection
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "0.1.0";
    public string? Description { get; set; }
    public string? Entry { get; set; }  // Entry point file (defaults to main.novus or <name>.novus)
    public string[] Authors { get; set; } = Array.Empty<string>();
    public string? License { get; set; }
}

public class BuildSection
{
    public string TargetCpu { get; set; } = "68020";  // 68000, 68020, 68040, 68060, auto
    public string Fpu { get; set; } = "auto";          // soft, 68881, 68040, auto
    public string Output { get; set; } = "build";      // Output directory
    public int OptimizationLevel { get; set; } = 0;    // 0-3
    public bool EmitAsm { get; set; } = false;         // Emit assembly files
}

public class PathsSection
{
    public string Src { get; set; } = ".";             // Source directory
    public string[] Lib { get; set; } = Array.Empty<string>();  // Additional library paths
}

public class DependencySection
{
    public string? Version { get; set; }
    public string? Path { get; set; }      // Local path dependency
    public string? Git { get; set; }       // Git repository URL
    public string? Branch { get; set; }    // Git branch
    public string? Tag { get; set; }       // Git tag
}
