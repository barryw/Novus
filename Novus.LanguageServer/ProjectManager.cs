using System.Collections.Concurrent;
using Tomlyn;
using Tomlyn.Model;

namespace Novus.LanguageServer;

/// <summary>
/// Manages workspace structure and tracks project.toml files.
/// Provides access to project configuration and workspace organization.
/// </summary>
public class ProjectManager
{
    private readonly ConcurrentDictionary<string, TomlProjectState> _projects = new();
    private string? _workspaceRoot;

    /// <summary>
    /// Gets the workspace root path.
    /// </summary>
    public string? WorkspaceRoot => _workspaceRoot;

    /// <summary>
    /// Sets the workspace root from the LSP initialize request.
    /// </summary>
    /// <param name="rootUri">The workspace root URI from the client</param>
    public void SetWorkspaceRoot(string? rootUri)
    {
        if (rootUri != null)
        {
            _workspaceRoot = UriToFilePath(rootUri);
            Console.Error.WriteLine($"[LSP] ProjectManager workspace root set to: {_workspaceRoot}");
        }
    }

    /// <summary>
    /// Registers a TOML project file and parses it.
    /// </summary>
    /// <param name="uri">The URI of the project.toml file</param>
    /// <param name="text">The TOML file content</param>
    /// <param name="version">The document version</param>
    public void RegisterProject(string uri, string text, int version)
    {
        var filePath = UriToFilePath(uri);
        var state = new TomlProjectState(uri, text, version);

        // Parse the TOML content
        ParseToml(state);

        _projects[uri] = state;
        Console.Error.WriteLine($"[LSP] Registered project: {filePath}");
    }

    /// <summary>
    /// Updates an existing project file with new content.
    /// </summary>
    /// <param name="uri">The URI of the project.toml file</param>
    /// <param name="text">The new TOML file content</param>
    /// <param name="version">The new document version</param>
    public void UpdateProject(string uri, string text, int version)
    {
        if (_projects.TryGetValue(uri, out var state))
        {
            state.Text = text;
            state.Version = version;
            ParseToml(state);
            Console.Error.WriteLine($"[LSP] Updated project: {UriToFilePath(uri)}");
        }
    }

    /// <summary>
    /// Unregisters a project file.
    /// </summary>
    /// <param name="uri">The URI of the project.toml file</param>
    public void UnregisterProject(string uri)
    {
        _projects.TryRemove(uri, out _);
        Console.Error.WriteLine($"[LSP] Unregistered project: {UriToFilePath(uri)}");
    }

    /// <summary>
    /// Gets the project state for a given URI.
    /// </summary>
    /// <param name="uri">The URI of the project.toml file</param>
    /// <returns>The project state, or null if not found</returns>
    public TomlProjectState? GetProject(string uri)
    {
        _projects.TryGetValue(uri, out var state);
        return state;
    }

    /// <summary>
    /// Gets all registered projects.
    /// </summary>
    /// <returns>An enumerable of all project states</returns>
    public IEnumerable<TomlProjectState> GetAllProjects()
    {
        return _projects.Values;
    }

    private void ParseToml(TomlProjectState state)
    {
        try
        {
            // Parse TOML using Tomlyn
            var tomlTable = Toml.ToModel(state.Text);
            state.TomlModel = tomlTable;
            state.ParseError = null;

            // Extract target_cpu from [build] section if present
            if (tomlTable.TryGetValue("build", out var buildObj) && buildObj is TomlTable buildTable)
            {
                if (buildTable.TryGetValue("target_cpu", out var cpuObj) && cpuObj is string cpuValue)
                {
                    state.TargetCpu = cpuValue;
                    Console.Error.WriteLine($"[LSP] Extracted target_cpu: {cpuValue} from {UriToFilePath(state.Uri)}");
                }
            }

            // Also check [workspace.build] for workspace.toml files
            if (tomlTable.TryGetValue("workspace", out var workspaceObj) && workspaceObj is TomlTable workspaceTable)
            {
                if (workspaceTable.TryGetValue("build", out var wsBuildObj) && wsBuildObj is TomlTable wsBuildTable)
                {
                    if (wsBuildTable.TryGetValue("target_cpu", out var cpuObj) && cpuObj is string cpuValue)
                    {
                        state.TargetCpu = cpuValue;
                        Console.Error.WriteLine($"[LSP] Extracted workspace target_cpu: {cpuValue} from {UriToFilePath(state.Uri)}");
                    }
                }
            }

            Console.Error.WriteLine($"[LSP] Successfully parsed TOML for {UriToFilePath(state.Uri)}");
        }
        catch (Exception ex)
        {
            state.ParseError = ex.Message;
            state.TomlModel = null;
            Console.Error.WriteLine($"[LSP] Failed to parse TOML for {UriToFilePath(state.Uri)}: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the target CPU from the project/workspace configuration.
    /// Searches for the nearest project.toml or workspace.toml that specifies a target_cpu.
    /// </summary>
    /// <param name="documentUri">The URI of the document to find the project for</param>
    /// <returns>The target CPU string (e.g., "68020"), or null if not specified</returns>
    public string? GetTargetCpuForDocument(string documentUri)
    {
        // First try to find a project.toml in the same directory or parent directories
        var documentPath = UriToFilePath(documentUri);
        var directory = System.IO.Path.GetDirectoryName(documentPath);

        while (!string.IsNullOrEmpty(directory))
        {
            // Check for project.toml
            var projectTomlPath = System.IO.Path.Combine(directory, "project.toml");
            var projectTomlUri = $"file://{projectTomlPath}";

            foreach (var project in _projects.Values)
            {
                if (UriToFilePath(project.Uri) == projectTomlPath && project.TargetCpu != null)
                {
                    return project.TargetCpu;
                }
            }

            // Check for workspace.toml
            var workspaceTomlPath = System.IO.Path.Combine(directory, "workspace.toml");

            foreach (var project in _projects.Values)
            {
                if (UriToFilePath(project.Uri) == workspaceTomlPath && project.TargetCpu != null)
                {
                    return project.TargetCpu;
                }
            }

            // Move to parent directory
            directory = System.IO.Path.GetDirectoryName(directory);
        }

        return null;
    }

    /// <summary>
    /// Converts a URI to a file path.
    /// </summary>
    private static string UriToFilePath(string uri)
    {
        if (uri.StartsWith("file://"))
        {
            return Uri.UnescapeDataString(uri.Substring("file://".Length));
        }
        return uri;
    }
}

/// <summary>
/// Represents the state of a TOML project file.
/// </summary>
public class TomlProjectState
{
    /// <summary>
    /// Gets the URI of the TOML file.
    /// </summary>
    public string Uri { get; }

    /// <summary>
    /// Gets or sets the current text content.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the document version number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the parsed TOML model.
    /// Null if parsing failed.
    /// </summary>
    public TomlTable? TomlModel { get; set; }

    /// <summary>
    /// Gets or sets the parse error message.
    /// Null if parsing succeeded.
    /// </summary>
    public string? ParseError { get; set; }

    /// <summary>
    /// Gets or sets the target CPU extracted from the [build] section.
    /// Valid values: "68000", "68010", "68020", "68030", "68040", "68060", "68080", "auto"
    /// Null if not specified.
    /// </summary>
    public string? TargetCpu { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TomlProjectState"/> class.
    /// </summary>
    public TomlProjectState(string uri, string text, int version)
    {
        Uri = uri;
        Text = text;
        Version = version;
    }
}
