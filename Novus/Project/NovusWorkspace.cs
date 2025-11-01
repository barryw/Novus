namespace Novus.Project;

/// <summary>
/// Represents a Novus.toml workspace/solution file (capital N)
/// Contains multiple projects
/// </summary>
public class NovusWorkspace
{
    public WorkspaceSection Workspace { get; set; } = new();
}

public class WorkspaceSection
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "0.1.0";
    public string? Description { get; set; }
    public string[] Authors { get; set; } = Array.Empty<string>();
    public string[] Members { get; set; } = Array.Empty<string>();  // List of project directories
    public WorkspaceBuildSection? Build { get; set; }
}

public class WorkspaceBuildSection
{
    public string TargetCpu { get; set; } = "68020";
    public string Fpu { get; set; } = "auto";
    public int OptimizationLevel { get; set; } = 0;
}
