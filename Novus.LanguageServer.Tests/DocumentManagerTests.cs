using Xunit;
using Novus.LanguageServer;

namespace Novus.LanguageServer.Tests;

/// <summary>
/// Unit tests for the DocumentManager class.
/// Tests document lifecycle (open, update, close) and parsing behavior.
/// </summary>
public class DocumentManagerTests
{
    private static string GetTestStdLibPath()
    {
        // Try to find the project root and use the actual std path
        var stdPath = Novus.PathUtility.FindStdLibPath();
        if (stdPath != null)
            return stdPath;

        // Fallback: walk up from current directory to find Novus.sln
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "Novus.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        if (currentDir != null)
        {
            return Path.Combine(currentDir, "Novus", "std");
        }

        // Final fallback - tests will fail with clear error message
        return Path.Combine(AppContext.BaseDirectory, "std");
    }

    private static readonly string TestStdLibPath = GetTestStdLibPath();

    [Fact]
    public void Open_ValidDocument_CreatesDocumentState()
    {
        // Arrange
        var manager = new DocumentManager(TestStdLibPath);
        var uri = "file:///test.novus";
        var text = "pub fn main() -> i32 { return 0 }";
        var version = 1;

        // Act
        manager.Open(uri, text, version);
        var state = manager.Get(uri);

        // Assert
        Assert.NotNull(state);
        Assert.Equal(uri, state.Uri);
        Assert.Equal(text, state.Text);
        Assert.Equal(version, state.Version);
        Assert.NotNull(state.ParseTree);
        Assert.NotNull(state.Diagnostics);
    }

    [Fact]
    public void Open_DocumentWithSyntaxError_CollectsDiagnostics()
    {
        // Arrange
        var manager = new DocumentManager(TestStdLibPath);
        var uri = "file:///test.novus";
        var text = "pub fn main() -> i32 { return 0"; // Missing closing brace
        var version = 1;

        // Act
        manager.Open(uri, text, version);
        var state = manager.Get(uri);

        // Assert
        Assert.NotNull(state);
        Assert.NotNull(state.Diagnostics);
        Assert.True(state.Diagnostics.HasErrors, "Expected syntax errors");
    }

    [Fact]
    public void Open_DocumentWithTypeError_CollectsDiagnostics()
    {
        // Arrange
        var manager = new DocumentManager(TestStdLibPath);
        var uri = "file:///test.novus";
        var text = @"
pub fn main() -> i32 {
    let x = 42
    let y = ""hello""
    return x + y
}";
        var version = 1;

        // Act
        manager.Open(uri, text, version);
        var state = manager.Get(uri);

        // Assert
        Assert.NotNull(state);
        Assert.NotNull(state.Diagnostics);
        Assert.True(state.Diagnostics.HasErrors, "Expected type error for i32 + String");
    }

    [Fact]
    public void Update_ExistingDocument_UpdatesContentAndReparses()
    {
        // Arrange
        var manager = new DocumentManager(TestStdLibPath);
        var uri = "file:///test.novus";
        var initialText = "pub fn main() -> i32 { return 0 }";
        var updatedText = "pub fn main() -> i32 { return 42 }";
        var version = 1;

        manager.Open(uri, initialText, version);

        // Act
        manager.Update(uri, updatedText, version + 1);
        var state = manager.Get(uri);

        // Assert
        Assert.NotNull(state);
        Assert.Equal(updatedText, state.Text);
        Assert.Equal(version + 1, state.Version);
    }

    [Fact]
    public void Update_NonExistentDocument_DoesNothing()
    {
        // Arrange
        var manager = new DocumentManager(TestStdLibPath);
        var uri = "file:///test.novus";
        var text = "pub fn main() -> i32 { return 0 }";
        var version = 1;

        // Act (update without opening first)
        manager.Update(uri, text, version);
        var state = manager.Get(uri);

        // Assert
        Assert.Null(state);
    }

    [Fact]
    public void Update_FixesSyntaxError_ClearsDiagnostics()
    {
        // Arrange
        var manager = new DocumentManager(TestStdLibPath);
        var uri = "file:///test.novus";
        var brokenText = "pub fn main() -> i32 { return 0"; // Missing brace
        var fixedText = "pub fn main() -> i32 { return 0 }"; // Fixed
        var version = 1;

        manager.Open(uri, brokenText, version);
        var brokenState = manager.Get(uri);
        Assert.NotNull(brokenState);
        Assert.NotNull(brokenState.Diagnostics);
        Assert.True(brokenState.Diagnostics.HasErrors);

        // Act
        manager.Update(uri, fixedText, version + 1);
        var fixedState = manager.Get(uri);

        // Assert
        Assert.NotNull(fixedState);
        Assert.NotNull(fixedState.Diagnostics);
        Assert.False(fixedState.Diagnostics.HasErrors, "Expected no errors after fix");
    }

    [Fact]
    public void Close_ExistingDocument_RemovesFromTracking()
    {
        // Arrange
        var manager = new DocumentManager(TestStdLibPath);
        var uri = "file:///test.novus";
        var text = "pub fn main() -> i32 { return 0 }";
        var version = 1;

        manager.Open(uri, text, version);
        Assert.NotNull(manager.Get(uri));

        // Act
        manager.Close(uri);
        var state = manager.Get(uri);

        // Assert
        Assert.Null(state);
    }

    [Fact]
    public void Close_NonExistentDocument_DoesNotThrow()
    {
        // Arrange
        var manager = new DocumentManager(TestStdLibPath);
        var uri = "file:///test.novus";

        // Act & Assert (should not throw)
        manager.Close(uri);
    }

    [Fact]
    public void Get_NonExistentDocument_ReturnsNull()
    {
        // Arrange
        var manager = new DocumentManager(TestStdLibPath);
        var uri = "file:///test.novus";

        // Act
        var state = manager.Get(uri);

        // Assert
        Assert.Null(state);
    }

    [Fact]
    public void MultipleDocuments_TrackedIndependently()
    {
        // Arrange
        var manager = new DocumentManager(TestStdLibPath);
        var uri1 = "file:///test1.novus";
        var uri2 = "file:///test2.novus";
        var text1 = "pub fn test1() -> i32 { return 1 }";
        var text2 = "pub fn test2() -> i32 { return 2 }";

        // Act
        manager.Open(uri1, text1, 1);
        manager.Open(uri2, text2, 1);

        var state1 = manager.Get(uri1);
        var state2 = manager.Get(uri2);

        // Assert
        Assert.NotNull(state1);
        Assert.NotNull(state2);
        Assert.Equal(text1, state1.Text);
        Assert.Equal(text2, state2.Text);
        Assert.NotEqual(state1, state2);
    }

    [Fact]
    public void DocumentState_InitialState_HasCorrectValues()
    {
        // Arrange
        var uri = "file:///test.novus";
        var text = "test content";
        var version = 42;

        // Act
        var state = new DocumentState(uri, text, version);

        // Assert
        Assert.Equal(uri, state.Uri);
        Assert.Equal(text, state.Text);
        Assert.Equal(version, state.Version);
        Assert.Null(state.ParseTree);
        Assert.Null(state.Diagnostics);
    }

    [Fact]
    public void DocumentState_TextProperty_CanBeModified()
    {
        // Arrange
        var state = new DocumentState("file:///test.novus", "initial", 1);
        var newText = "updated";

        // Act
        state.Text = newText;

        // Assert
        Assert.Equal(newText, state.Text);
    }

    [Fact]
    public void DocumentState_VersionProperty_CanBeModified()
    {
        // Arrange
        var state = new DocumentState("file:///test.novus", "content", 1);
        var newVersion = 5;

        // Act
        state.Version = newVersion;

        // Assert
        Assert.Equal(newVersion, state.Version);
    }
}
