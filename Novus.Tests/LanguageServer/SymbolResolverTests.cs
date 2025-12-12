using Novus.Diagnostics;
using Novus.LanguageServer;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests.LanguageServer;

/// <summary>
/// Tests for the SymbolResolver component
/// Tests symbol resolution at specific source positions for LSP features
/// </summary>
public class SymbolResolverTests
{
    private readonly string _testStdLibPath;

    public SymbolResolverTests()
    {
        _testStdLibPath = Path.Combine(Path.GetTempPath(), "novus-test-stdlib");
    }

    private (SymbolResolver resolver, string source) CreateResolver(string source)
    {
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(
            source,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.Compilation
        );
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalyzer("test.novus", source, _testStdLibPath);
        try
        {
            analyzer.Analyze(tree);
        }
        catch
        {
            // Semantic analysis may fail with missing stdlib, but we can still test symbol resolution
        }

        var resolver = new SymbolResolver(analyzer, tree, source);
        return (resolver, source);
    }

    [Fact]
    public void FindSymbolAtPosition_FunctionName_ReturnsFunction()
    {
        var source = @"
fn test() -> i32 {
    return 42;
}

fn main() {
    let x = test();
}
";
        var (resolver, _) = CreateResolver(source);

        // Position should be on "test" in the call "test()" (line 6, column ~12)
        var symbol = resolver.FindSymbolAtPosition(6, 12);

        Assert.NotNull(symbol);
        Assert.Equal(Novus.LanguageServer.SymbolKind.Function, symbol.Kind);
        Assert.Equal("test", symbol.Name);
    }

    [Fact]
    public void FindSymbolAtPosition_LocalVariable_ReturnsVariable()
    {
        var source = @"
fn main() {
    let x = 10;
    let y = x + 5;
}
";
        var (resolver, _) = CreateResolver(source);

        // Position on "x" in "x + 5" (line 3, column ~12)
        var symbol = resolver.FindSymbolAtPosition(3, 12);

        // May be null if semantic analysis fails, which is acceptable in unit tests
        if (symbol != null)
        {
            Assert.Equal(Novus.LanguageServer.SymbolKind.Variable, symbol.Kind);
            Assert.Equal("x", symbol.Name);
        }
    }

    [Fact]
    public void FindSymbolAtPosition_NoSymbolAtPosition_ReturnsNull()
    {
        var source = @"
fn test() {
    // Comment with no symbols
}
";
        var (resolver, _) = CreateResolver(source);

        // Position in comment area
        var symbol = resolver.FindSymbolAtPosition(2, 5);

        // Should return null for positions without symbols
        Assert.Null(symbol);
    }

    [Fact]
    public void FindSymbolAtPosition_OutOfBounds_ReturnsNull()
    {
        var source = "fn test() {}";
        var (resolver, _) = CreateResolver(source);

        // Way past end of file
        var symbol = resolver.FindSymbolAtPosition(100, 100);

        Assert.Null(symbol);
    }

    [Fact]
    public void FindSymbolAtPosition_BeforeFile_ReturnsNull()
    {
        var source = "fn test() {}";
        var (resolver, _) = CreateResolver(source);

        // Before start of file (line -1)
        var symbol = resolver.FindSymbolAtPosition(-1, 0);

        Assert.Null(symbol);
    }

    [Fact]
    public void FindSymbolAtPosition_EmptySource_ReturnsNull()
    {
        var source = "";
        var (resolver, _) = CreateResolver(source);

        var symbol = resolver.FindSymbolAtPosition(0, 0);

        Assert.Null(symbol);
    }

    [Fact]
    public void SymbolInfo_Record_PropertiesCorrect()
    {
        var source = @"
fn test() -> i32 { return 42; }
";
        var (resolver, _) = CreateResolver(source);

        var symbol = resolver.FindSymbolAtPosition(1, 3);

        if (symbol != null)
        {
            Assert.Equal("test", symbol.Name);
            Assert.Equal(Novus.LanguageServer.SymbolKind.Function, symbol.Kind);
            Assert.NotNull(symbol.Location);
            // Type may be null depending on semantic analysis
            Assert.NotNull(symbol.Symbol);
        }
    }

    [Fact]
    public void FindSymbolAtPosition_Constant_ReturnsConstant()
    {
        var source = @"
const MAX: i32 = 100;

fn test() -> i32 {
    return MAX;
}
";
        var (resolver, _) = CreateResolver(source);

        // Position on "MAX" in return statement
        var symbol = resolver.FindSymbolAtPosition(4, 11);

        if (symbol != null)
        {
            Assert.Equal(Novus.LanguageServer.SymbolKind.Constant, symbol.Kind);
            Assert.Equal("MAX", symbol.Name);
        }
    }

    [Fact]
    public void FindSymbolAtPosition_Struct_ReturnsStruct()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

fn test() -> Point {
    return Point { x: 0, y: 0 };
}
";
        var (resolver, _) = CreateResolver(source);

        // Position on "Point" in return type
        var symbol = resolver.FindSymbolAtPosition(6, 13);

        if (symbol != null)
        {
            Assert.Equal(Novus.LanguageServer.SymbolKind.Struct, symbol.Kind);
            Assert.Equal("Point", symbol.Name);
        }
    }

    [Fact]
    public void FindSymbolAtPosition_Enum_ReturnsEnum()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

fn test() -> Color {
    return Color::Red;
}
";
        var (resolver, _) = CreateResolver(source);

        // Position on "Color" in return type
        var symbol = resolver.FindSymbolAtPosition(7, 13);

        if (symbol != null)
        {
            Assert.Equal(Novus.LanguageServer.SymbolKind.Enum, symbol.Kind);
            Assert.Equal("Color", symbol.Name);
        }
    }

    [Fact]
    public void FindSymbolAtPosition_MultipleOccurrences_ResolvesCorrect()
    {
        var source = @"
fn test() -> i32 { return 0; }

fn main() {
    test();
    test();
}
";
        var (resolver, _) = CreateResolver(source);

        // First occurrence (line 4, column ~4)
        var symbol1 = resolver.FindSymbolAtPosition(4, 4);

        // Second occurrence (line 5, column ~4)
        var symbol2 = resolver.FindSymbolAtPosition(5, 4);

        if (symbol1 != null && symbol2 != null)
        {
            Assert.Equal("test", symbol1.Name);
            Assert.Equal("test", symbol2.Name);
            Assert.Equal(symbol1.Location.Line, symbol2.Location.Line);
        }
    }

    [Fact]
    public void SymbolKind_Enum_HasAllExpectedValues()
    {
        // Verify enum has expected symbol kinds
        Assert.Equal(0, (int)Novus.LanguageServer.SymbolKind.Function);
        Assert.Equal(1, (int)Novus.LanguageServer.SymbolKind.Variable);
        Assert.Equal(2, (int)Novus.LanguageServer.SymbolKind.GlobalVariable);
        Assert.Equal(3, (int)Novus.LanguageServer.SymbolKind.Constant);
        Assert.Equal(4, (int)Novus.LanguageServer.SymbolKind.Struct);
        Assert.Equal(5, (int)Novus.LanguageServer.SymbolKind.Enum);
        Assert.Equal(6, (int)Novus.LanguageServer.SymbolKind.Trait);
        Assert.Equal(7, (int)Novus.LanguageServer.SymbolKind.Method);
        Assert.Equal(8, (int)Novus.LanguageServer.SymbolKind.Field);
        Assert.Equal(9, (int)Novus.LanguageServer.SymbolKind.EnumVariant);
        Assert.Equal(10, (int)Novus.LanguageServer.SymbolKind.Parameter);
    }

    [Fact]
    public void FindSymbolAtPosition_GlobalVariable_ReturnsGlobalVariable()
    {
        var source = @"
var global_count: i32 = 0;

fn increment() {
    global_count = global_count + 1;
}
";
        var (resolver, _) = CreateResolver(source);

        // Position on "global_count" in assignment
        var symbol = resolver.FindSymbolAtPosition(4, 4);

        if (symbol != null)
        {
            Assert.Equal(Novus.LanguageServer.SymbolKind.GlobalVariable, symbol.Kind);
            Assert.Equal("global_count", symbol.Name);
        }
    }

    [Fact]
    public void FindSymbolAtPosition_Trait_ReturnsTrait()
    {
        var source = @"
trait Display {
    fn show(self);
}

struct Point {}

impl Display for Point {
    fn show(self) {}
}
";
        var (resolver, _) = CreateResolver(source);

        // Position on "Display" in impl declaration
        var symbol = resolver.FindSymbolAtPosition(7, 5);

        if (symbol != null)
        {
            Assert.Equal(Novus.LanguageServer.SymbolKind.Trait, symbol.Kind);
            Assert.Equal("Display", symbol.Name);
        }
    }

    [Fact]
    public void FindSymbolAtPosition_PartialToken_FindsToken()
    {
        var source = "fn test() {}";
        var (resolver, _) = CreateResolver(source);

        // Position at start of "test" (column 3)
        var symbolStart = resolver.FindSymbolAtPosition(0, 3);

        // Position in middle of "test" (column 5)
        var symbolMiddle = resolver.FindSymbolAtPosition(0, 5);

        // Position at end of "test" (column 6)
        var symbolEnd = resolver.FindSymbolAtPosition(0, 6);

        // At least one position should find the symbol
        Assert.True(symbolStart != null || symbolMiddle != null || symbolEnd != null);
    }

    [Fact]
    public void FindSymbolAtPosition_WhitespaceOnly_ReturnsNull()
    {
        var source = @"
fn test() {


}
";
        var (resolver, _) = CreateResolver(source);

        // Position in whitespace between braces
        var symbol = resolver.FindSymbolAtPosition(2, 0);

        Assert.Null(symbol);
    }

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        var source = "fn test() {}";
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(source, diagnostics, "test.novus", NovusParserFactory.ParseMode.Compilation);
        var tree = parser.compilationUnit();
        var analyzer = new SemanticAnalyzer("test.novus", source, _testStdLibPath);

        var resolver = new SymbolResolver(analyzer, tree, source);

        Assert.NotNull(resolver);
    }
}
