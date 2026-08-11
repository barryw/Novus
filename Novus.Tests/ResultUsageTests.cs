using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

public class ResultUsageTests
{
    private static string StdLibPath => PathUtility.FindStdLibPath()
        ?? throw new InvalidOperationException("Novus standard library not found");

    private static DiagnosticBag Analyze(string source)
    {
        var input = new AntlrInputStream(source);
        var lexer = new NovusLexer(input);
        var tokens = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokens);
        var tree = parser.compilationUnit();
        var analyzer = new SemanticAnalyzer("test.novus", source, StdLibPath);
        analyzer.Analyze(tree);
        return analyzer.Diagnostics;
    }

    private static DiagnosticBag Build(string source)
    {
        var input = new AntlrInputStream(source);
        var lexer = new NovusLexer(input);
        var tokens = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokens);
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: false);
        builder.SetStdLibPath(StdLibPath);
        builder.SetInputFilePath("test.novus");
        builder.BuildModule(tree);
        return builder.Diagnostics;
    }

    private const string FallibleFunction = """
        from std::core import Result

        fn fallible() -> Result<(), i32> {
            Result::Err(1)
        }
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IgnoredResult_IsRejectedByBothAnalysisPaths(bool useIrBuilder)
    {
        var source = FallibleFunction + """

            fn ignore_it() {
                fallible()
            }
            """;

        var diagnostics = useIrBuilder ? Build(source) : Analyze(source);

        Assert.Contains(diagnostics.Diagnostics, diagnostic => diagnostic.Code == ErrorCodes.UnusedResult);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitDiscard_IsAcceptedByBothAnalysisPaths(bool useIrBuilder)
    {
        var source = FallibleFunction + """

            fn discard_it() {
                let _ = fallible()
            }
            """;

        var diagnostics = useIrBuilder ? Build(source) : Analyze(source);

        Assert.DoesNotContain(diagnostics.Diagnostics, diagnostic => diagnostic.Code == ErrorCodes.UnusedResult);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PropagatedAndImplicitlyReturnedResults_AreAccepted(bool useIrBuilder)
    {
        var source = FallibleFunction + """

            fn propagate() -> Result<(), i32> {
                fallible()?
                Result::Ok(())
            }

            fn return_implicitly() -> Result<(), i32> {
                fallible()
            }
            """;

        var diagnostics = useIrBuilder ? Build(source) : Analyze(source);

        Assert.DoesNotContain(diagnostics.Diagnostics, diagnostic => diagnostic.Code == ErrorCodes.UnusedResult);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchedResult_IsAcceptedByBothAnalysisPaths(bool useIrBuilder)
    {
        var source = FallibleFunction + """

            fn handle_it() -> i32 {
                match fallible() {
                    Result::Ok(_) => 0,
                    Result::Err(_) => 1,
                }
            }
            """;

        var diagnostics = useIrBuilder ? Build(source) : Analyze(source);

        Assert.DoesNotContain(diagnostics.Diagnostics, diagnostic => diagnostic.Code == ErrorCodes.UnusedResult);
    }
}
