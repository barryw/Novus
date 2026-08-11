using Novus.Commands;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Templates are copied out of a directory on disk, not out of git. Anyone who runs a
/// build inside templates/ leaves gitignored build output behind, and a wholesale copy
/// ships it into every project created from then on. That is how a 468-byte skeleton
/// executable ended up sitting in the bins/ directory of new gui and workbench
/// workspaces next to the real binaries.
/// </summary>
public class TemplateScaffoldingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "novus-template-tests-" + Guid.NewGuid().ToString("N"));

    private string MakeDirtyTemplate()
    {
        var template = Path.Combine(_root, "template");
        Directory.CreateDirectory(Path.Combine(template, "app", "src"));
        File.WriteAllText(Path.Combine(template, "workspace.toml"), "[workspace]\nname = \"x\"\n");
        File.WriteAllText(Path.Combine(template, "app", "project.toml"), "[package]\nname = \"x-app\"\n");
        File.WriteAllText(Path.Combine(template, "app", "src", "main.novus"), "pub fn main() -> i32 { return 0 }\n");

        // Leftovers from someone having run a build in the template directory.
        foreach (var junk in new[] { "target", "build", ".novus-cache", "usercache", "bin", "obj" })
        {
            var dir = Path.Combine(template, junk, "debug", "bins");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "modern"), new byte[] { 0x00, 0x00, 0x03, 0xF3 });
            File.WriteAllText(Path.Combine(dir, "modern_main.c"), "int main(void) { return 0; }\n");
        }
        File.WriteAllText(Path.Combine(template, ".DS_Store"), "junk");
        return template;
    }

    [Fact]
    public void CopyDirectory_DoesNotCopyBuildOutputFromTemplate()
    {
        var dest = Path.Combine(_root, "dest");
        NewCommand.CopyDirectory(MakeDirtyTemplate(), dest, true);

        foreach (var junk in NewCommand.BuildOutputDirectories)
        {
            Assert.False(Directory.Exists(Path.Combine(dest, junk)),
                $"'{junk}/' is build output and must not be copied out of a template");
        }

        // The specific symptom: a stray executable in the new project's bins/ directory.
        Assert.Empty(Directory.GetFiles(dest, "modern", SearchOption.AllDirectories));
    }

    [Fact]
    public void CopyDirectory_CopiesTheActualTemplateContent()
    {
        var dest = Path.Combine(_root, "dest");
        NewCommand.CopyDirectory(MakeDirtyTemplate(), dest, true);

        // Excluding build output must not cost us any real template files.
        Assert.True(File.Exists(Path.Combine(dest, "workspace.toml")));
        Assert.True(File.Exists(Path.Combine(dest, "app", "project.toml")));
        Assert.True(File.Exists(Path.Combine(dest, "app", "src", "main.novus")));
    }

    [Fact]
    public void CopyDirectory_SkipsDsStore()
    {
        var dest = Path.Combine(_root, "dest");
        NewCommand.CopyDirectory(MakeDirtyTemplate(), dest, true);

        Assert.False(File.Exists(Path.Combine(dest, ".DS_Store")));
    }

    [Fact]
    public void BundledTemplates_ContainNoBuildOutput()
    {
        // Guards the source tree itself: if build output is committed or left in
        // templates/, it reaches users through the release archive.
        var templates = FindTemplatesDirectory();
        if (templates == null) return; // not running from a source checkout

        foreach (var junk in NewCommand.BuildOutputDirectories)
        {
            var hits = Directory.GetDirectories(templates, junk, SearchOption.AllDirectories);
            Assert.True(hits.Length == 0,
                $"templates/ contains build output: {string.Join(", ", hits)}. " +
                "Run a build outside the template tree, or delete these.");
        }
    }

    [Fact]
    public void ModernGuiTemplateUsesStaticUi()
    {
        var templates = FindTemplatesDirectory();
        if (templates == null) return;

        var source = File.ReadAllText(Path.Combine(templates, "gui", "modern", "src", "main.novus"));
        Assert.Contains("StaticGadToolsUi", source);
        Assert.DoesNotContain("GadToolsBuilder", source);
    }

    [Fact]
    public void HandlerUsesPacketStartupAndShipsTemplate()
    {
        Assert.Equal("novus_handler_startup", Novus.Program.GetStartupStub("handler"));
        Assert.Equal("novus_startup", Novus.Program.GetStartupStub("cli"));

        var templates = FindTemplatesDirectory();
        if (templates == null) return;
        Assert.True(File.Exists(Path.Combine(templates, "handler", "handler", "src", "main.novus")));
        Assert.True(File.Exists(Path.Combine(templates, "resource", "resource", "src", "resource.novus")));
    }

    private static string? FindTemplatesDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "templates");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(dir.FullName, "Novus.sln")))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
