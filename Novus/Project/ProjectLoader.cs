using Tomlyn;
using Tomlyn.Model;

namespace Novus.Project;

/// <summary>
/// Loads and parses novus.toml project files
/// </summary>
public class ProjectLoader
{
    public static NovusProject LoadFromFile(string projectFilePath)
    {
        if (!File.Exists(projectFilePath))
        {
            throw new FileNotFoundException($"Project file not found: {projectFilePath}");
        }

        var tomlContent = File.ReadAllText(projectFilePath);
        var model = Toml.ToModel(tomlContent);

        var project = new NovusProject();

        // Parse [package] section
        if (model.TryGetValue("package", out var packageObj) && packageObj is TomlTable packageTable)
        {
            project.Package.Name = GetString(packageTable, "name") ?? "";
            project.Package.Version = GetString(packageTable, "version") ?? "0.1.0";
            project.Package.Description = GetString(packageTable, "description");
            project.Package.Entry = GetString(packageTable, "entry");
            project.Package.License = GetString(packageTable, "license");

            if (packageTable.TryGetValue("authors", out var authorsObj) && authorsObj is TomlArray authorsArray)
            {
                project.Package.Authors = authorsArray.Select(a => a?.ToString() ?? "").ToArray();
            }
        }

        // Parse [build] section
        if (model.TryGetValue("build", out var buildObj) && buildObj is TomlTable buildTable)
        {
            project.Build.TargetCpu = GetString(buildTable, "target_cpu") ?? "68020";
            project.Build.Fpu = GetString(buildTable, "fpu") ?? "auto";
            project.Build.Output = GetString(buildTable, "output") ?? "build";
            project.Build.OptimizationLevel = GetInt(buildTable, "optimization_level") ?? 0;
            project.Build.EmitAsm = GetBool(buildTable, "emit_asm") ?? false;
        }

        // Parse [paths] section
        if (model.TryGetValue("paths", out var pathsObj) && pathsObj is TomlTable pathsTable)
        {
            project.Paths.Src = GetString(pathsTable, "src") ?? ".";

            if (pathsTable.TryGetValue("lib", out var libObj) && libObj is TomlArray libArray)
            {
                project.Paths.Lib = libArray.Select(l => l?.ToString() ?? "").ToArray();
            }
        }

        // Parse [dependencies] section
        if (model.TryGetValue("dependencies", out var depsObj) && depsObj is TomlTable depsTable)
        {
            foreach (var (name, value) in depsTable)
            {
                var dep = new DependencySection();

                if (value is string versionStr)
                {
                    // Simple version dependency: foo = "1.0.0"
                    dep.Version = versionStr;
                }
                else if (value is TomlTable depTable)
                {
                    // Complex dependency with multiple fields
                    dep.Version = GetString(depTable, "version");
                    dep.Path = GetString(depTable, "path");
                    dep.Git = GetString(depTable, "git");
                    dep.Branch = GetString(depTable, "branch");
                    dep.Tag = GetString(depTable, "tag");
                }

                project.Dependencies[name] = dep;
            }
        }

        return project;
    }

    public static NovusProject? TryLoadFromDirectory(string directory)
    {
        var projectFile = Path.Combine(directory, "novus.toml");
        if (!File.Exists(projectFile))
        {
            return null;
        }

        try
        {
            return LoadFromFile(projectFile);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(TomlTable table, string key)
    {
        if (table.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }
        return null;
    }

    private static int? GetInt(TomlTable table, string key)
    {
        if (table.TryGetValue(key, out var value) && value is long longValue)
        {
            return (int)longValue;
        }
        return null;
    }

    private static bool? GetBool(TomlTable table, string key)
    {
        if (table.TryGetValue(key, out var value) && value is bool boolValue)
        {
            return boolValue;
        }
        return null;
    }
}
