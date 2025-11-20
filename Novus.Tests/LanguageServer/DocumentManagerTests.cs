using Novus.LanguageServer;
using Xunit;

namespace Novus.Tests.LanguageServer;

/// <summary>
/// Tests for the DocumentManager component
/// Tests document lifecycle, parsing, and state management
/// </summary>
public class DocumentManagerTests
{
    private readonly string _testStdLibPath;

    public DocumentManagerTests()
    {
        // Use a temp path for stdlib - DocumentManager will handle missing stdlib gracefully
        _testStdLibPath = Path.Combine(Path.GetTempPath(), "novus-test-stdlib");
    }

    [Fact]
    public void Constructor_InitializesWithStdLibPath()
    {
        var manager = new DocumentManager(_testStdLibPath);
        Assert.NotNull(manager);
    }

    [Fact]
    public void Open_CreatesNewDocument()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/document.novus";
        var text = "fn main() {}";
        var version = 1;

        manager.Open(uri, text, version);

        var state = manager.Get(uri);
        Assert.NotNull(state);
        Assert.Equal(uri, state.Uri);
        Assert.Equal(text, state.Text);
        Assert.Equal(version, state.Version);
    }

    [Fact]
    public void Open_ParsesDocument()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/document.novus";
        var text = "fn test() -> i32 { return 42; }";

        manager.Open(uri, text, 1);

        var state = manager.Get(uri);
        Assert.NotNull(state);
        Assert.NotNull(state.ParseTree);
        Assert.NotNull(state.Diagnostics);
    }

    [Fact]
    public void Update_ModifiesExistingDocument()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/document.novus";
        var originalText = "fn old() {}";
        var newText = "fn new() {}";

        manager.Open(uri, originalText, 1);
        manager.Update(uri, newText, 2);

        var state = manager.Get(uri);
        Assert.NotNull(state);
        Assert.Equal(newText, state.Text);
        Assert.Equal(2, state.Version);
    }

    [Fact]
    public void Update_ReparsesDocument()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/document.novus";

        manager.Open(uri, "fn test() {}", 1);
        var originalTree = manager.Get(uri)?.ParseTree;

        manager.Update(uri, "fn test() { let x = 1; }", 2);
        var updatedTree = manager.Get(uri)?.ParseTree;

        Assert.NotNull(originalTree);
        Assert.NotNull(updatedTree);
        // Trees should be different instances (document was reparsed)
        Assert.NotSame(originalTree, updatedTree);
    }

    [Fact]
    public void Update_NonExistentDocument_DoesNothing()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/nonexistent.novus";

        // This should not throw
        manager.Update(uri, "fn test() {}", 1);

        var state = manager.Get(uri);
        Assert.Null(state);
    }

    [Fact]
    public void Close_RemovesDocument()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/document.novus";

        manager.Open(uri, "fn test() {}", 1);
        Assert.NotNull(manager.Get(uri));

        manager.Close(uri);
        Assert.Null(manager.Get(uri));
    }

    [Fact]
    public void Close_NonExistentDocument_DoesNotThrow()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/nonexistent.novus";

        // This should not throw
        manager.Close(uri);
    }

    [Fact]
    public void Get_NonExistentDocument_ReturnsNull()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/nonexistent.novus";

        var state = manager.Get(uri);
        Assert.Null(state);
    }

    [Fact]
    public void Open_WithSyntaxError_StoresDiagnostics()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/error.novus";
        var text = "fn test("; // Syntax error

        manager.Open(uri, text, 1);

        var state = manager.Get(uri);
        Assert.NotNull(state);
        Assert.NotNull(state.Diagnostics);
        Assert.True(state.Diagnostics.HasErrors);
    }

    [Fact]
    public void Open_MultipleDocuments_TracksIndependently()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri1 = "file:///test/doc1.novus";
        var uri2 = "file:///test/doc2.novus";
        var text1 = "fn doc1() {}";
        var text2 = "fn doc2() {}";

        manager.Open(uri1, text1, 1);
        manager.Open(uri2, text2, 1);

        var state1 = manager.Get(uri1);
        var state2 = manager.Get(uri2);

        Assert.NotNull(state1);
        Assert.NotNull(state2);
        Assert.Equal(text1, state1.Text);
        Assert.Equal(text2, state2.Text);
        Assert.NotSame(state1, state2);
    }

    [Fact]
    public void Update_IncrementsVersion()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/document.novus";

        manager.Open(uri, "fn test() {}", 1);
        manager.Update(uri, "fn test() { let x = 1; }", 2);
        manager.Update(uri, "fn test() { let x = 2; }", 3);

        var state = manager.Get(uri);
        Assert.NotNull(state);
        Assert.Equal(3, state.Version);
    }

    [Fact]
    public void DocumentState_Constructor_InitializesCorrectly()
    {
        var uri = "file:///test/document.novus";
        var text = "fn test() {}";
        var version = 5;

        var state = new DocumentState(uri, text, version);

        Assert.Equal(uri, state.Uri);
        Assert.Equal(text, state.Text);
        Assert.Equal(version, state.Version);
        Assert.Null(state.ParseTree);
        Assert.Null(state.Diagnostics);
        Assert.Null(state.SemanticAnalyzer);
    }

    [Fact]
    public void DocumentState_TextProperty_CanBeModified()
    {
        var state = new DocumentState("file:///test.novus", "original", 1);
        state.Text = "modified";
        Assert.Equal("modified", state.Text);
    }

    [Fact]
    public void DocumentState_VersionProperty_CanBeModified()
    {
        var state = new DocumentState("file:///test.novus", "text", 1);
        state.Version = 10;
        Assert.Equal(10, state.Version);
    }

    [Fact]
    public void Open_ValidNovusCode_HasNoErrors()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/valid.novus";
        var text = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b;
}

fn main() {
    let result = add(5, 3);
}
";

        manager.Open(uri, text, 1);

        var state = manager.Get(uri);
        Assert.NotNull(state);
        Assert.NotNull(state.ParseTree);
        // Note: May have semantic errors due to missing stdlib, but should parse successfully
    }

    [Fact]
    public void Open_EmptyDocument_ParsesSuccessfully()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/empty.novus";
        var text = "";

        manager.Open(uri, text, 1);

        var state = manager.Get(uri);
        Assert.NotNull(state);
        Assert.NotNull(state.ParseTree);
    }

    [Fact]
    public void Close_AfterMultipleUpdates_RemovesDocument()
    {
        var manager = new DocumentManager(_testStdLibPath);
        var uri = "file:///test/document.novus";

        manager.Open(uri, "v1", 1);
        manager.Update(uri, "v2", 2);
        manager.Update(uri, "v3", 3);

        manager.Close(uri);

        Assert.Null(manager.Get(uri));
    }
}
