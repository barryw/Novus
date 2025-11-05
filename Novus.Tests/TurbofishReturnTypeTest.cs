using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace Novus.Tests;

public class TurbofishReturnTypeTest
{
    private readonly ITestOutputHelper _output;

    public TurbofishReturnTypeTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TurbofishMethodCall_ShouldSubstituteReturnType()
    {
        var source = @"
pub enum Option<T> {
    Some(T),
    None,
}

pub struct Vec<T> {
    data: *T,
}

impl<T> Vec<T> {
    pub fn with_capacity(n: u32) -> Option<Vec<T>> {
        return Option::None
    }
}

pub fn main() {
    // This should resolve to Option<Vec<u8>>, not Option<Vec<T>>
    let vec_opt = Vec::<u8>::with_capacity(10)
}
";

        var diagnostics = new DiagnosticBag();
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);

        var errorListener = new NovusErrorListener(diagnostics, "test.novus", source);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);

        var tree = parser.compilationUnit();

        Assert.False(diagnostics.HasErrors,
            $"Parse errors: {string.Join("\n", diagnostics.Diagnostics.Where(d => d.IsError).Select(d => d.Message))}");

        // Use a path that looks like a std library file to avoid auto-import
        var semanticAnalyzer = new SemanticAnalyzer("/fake/std/test.novus", source, ".");
        semanticAnalyzer.Analyze(tree);

        if (semanticAnalyzer.Diagnostics.HasErrors)
        {
            foreach (var diag in semanticAnalyzer.Diagnostics.Diagnostics.Where(d => d.IsError))
            {
                _output.WriteLine(diag.Message);
            }
        }

        Assert.False(semanticAnalyzer.Diagnostics.HasErrors,
            "Semantic analysis should succeed");
    }
}
