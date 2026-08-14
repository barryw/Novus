using Novus.Diagnostics;
using Novus.Frontend;
using Novus.LanguageServer;
using Novus.Parser;
using Novus.Preprocessing;
using Novus.SemanticAnalysis;
using Tomlyn;
using Xunit;

namespace Novus.Tests.LanguageServer;

[Trait("Category", "CorpusCompilation")]
public class RepositoryCorpusTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Std = Path.Combine(Root, "Novus", "std");

    public static IEnumerable<object[]> NovusFiles() => Files("*.novus");
    public static IEnumerable<object[]> TomlFiles() => Files("*.toml");
    public static IEnumerable<object[]> HdPartSources() =>
        Directory.GetFiles(Path.Combine(Root, "ports", "hdpart-novus", "src"), "*.novus")
            .OrderBy(path => path)
            .Select(path => new object[] { path });

    [Theory]
    [MemberData(nameof(NovusFiles))]
    public void EveryNovusFileParsesInLanguageServer(string path)
    {
        var source = File.ReadAllText(path);
        var diagnostics = new DiagnosticBag();
        var constants = IrBuilderConfiguration.GetDefaultPreprocessorConstants();
        source = new Preprocessor(constants, diagnostics, path).Process(source);
        var parser = NovusParserFactory.CreateParser(
            source, diagnostics, new Uri(path).AbsoluteUri, NovusParserFactory.ParseMode.LanguageServer);

        Assert.NotNull(parser.compilationUnit());
        AssertNoErrors(path, diagnostics.Diagnostics);
    }

    [Theory]
    [MemberData(nameof(TomlFiles))]
    public void EveryTomlFileParsesWithoutLanguageServerDiagnostics(string path)
    {
        var source = File.ReadAllText(path);
        var model = Toml.ToModel(source);
        var diagnostics = Path.GetFileName(path) switch
        {
            "project.toml" or "novus.toml" => TomlSchemaValidator.ValidateProjectToml(model, source, path),
            "workspace.toml" => TomlSchemaValidator.ValidateWorkspaceToml(model, source, path),
            _ => []
        };

        Assert.True(diagnostics.Count == 0,
            $"{Relative(path)} has language-server diagnostics:\n" +
            string.Join('\n', diagnostics.Select(d => $"  {d.Severity}: {d.Message}")));
    }

    [Theory]
    [MemberData(nameof(HdPartSources))]
    public void HdPartSourceHasNoLanguageServerErrors(string path)
    {
        var source = File.ReadAllText(path);
        var constants = IrBuilderConfiguration.GetDefaultPreprocessorConstants();
        var diagnostics = new DiagnosticBag();
        source = new Preprocessor(constants, diagnostics, path).Process(source);
        var parser = NovusParserFactory.CreateParser(
            source, diagnostics, new Uri(path).AbsoluteUri, NovusParserFactory.ParseMode.LanguageServer);
        var analyzer = new SemanticAnalyzer(path, source, Std, constants);
        analyzer.Analyze(parser.compilationUnit());
        foreach (var diagnostic in analyzer.Diagnostics.Diagnostics)
            diagnostics.Add(diagnostic);

        AssertNoErrors(path, diagnostics.Diagnostics);
    }

    private static IEnumerable<object[]> Files(string pattern) =>
        Directory.GetFiles(Root, pattern, SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj" or "build" or "publish" or "publish-x64" or
                    "node_modules" or ".novus-cache" or "dist"))
            .OrderBy(path => path)
            .Select(path => new object[] { path });

    private static void AssertNoErrors(string path, IEnumerable<Diagnostic> diagnostics)
    {
        var errors = diagnostics.Where(diagnostic => diagnostic.IsError).ToList();
        Assert.True(errors.Count == 0,
            $"{Relative(path)} has language-server errors:\n" +
            string.Join('\n', errors.Select(error =>
                $"  {error.Location.Line}:{error.Location.Column} {error.Code}: {error.Message}")));
    }

    private static string Relative(string path) => Path.GetRelativePath(Root, path);

    private static string FindRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(directory, "Novus.sln")))
            directory = Directory.GetParent(directory)?.FullName
                ?? throw new InvalidOperationException("Could not find repository root");
        return directory;
    }
}
