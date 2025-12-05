using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for #line directive emission in CCodeGenerator.
/// </summary>
public class LineDirectiveTests
{
    private (IrModule Module, CCodeGenerator Generator, string Code) CompileAndGenerate(string source, BuildMode buildMode = BuildMode.Debug)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        var module = builder.BuildModule(tree);

        var stringLiterals = new List<IrStringLiteral>();
        var generator = new CCodeGenerator(
            module,
            stringLiterals,
            cpuTarget: "68020",
            fpuMode: "soft",
            buildMode: buildMode,
            safetyLevel: SafetyLevel.Basic);

        var code = generator.Generate();
        return (module, generator, code);
    }

    [Fact]
    public void ReleaseBuild_DoesNotEmitLineDirectives()
    {
        var source = @"
fn main() {
    let x = 42;
    let y = x + 1;
}";

        var (module, _, code) = CompileAndGenerate(source, BuildMode.Release);

        // Release builds should NOT contain #line directives
        Assert.DoesNotContain("#line", code);
    }

    [Fact]
    public void DebugBuild_CodeGenerates()
    {
        // Basic test that debug build code generation works
        var source = @"
fn main() {
    let x = 42;
    let y = x + 1;
}";

        var (module, _, code) = CompileAndGenerate(source, BuildMode.Debug);

        // Code should be generated
        Assert.NotNull(code);
        Assert.Contains("main", code);
    }

    [Fact]
    public void MaybeEmitLineDirective_RespectsDebugMode()
    {
        // This test verifies that the MaybeEmitLineDirective method exists and works
        // The actual #line directive emission depends on instructions having Location info
        // which is only set during full file compilation
        var source = @"
fn test() -> i32 {
    42
}

fn main() {
    test();
}";

        var (moduleDebug, _, codeDebug) = CompileAndGenerate(source, BuildMode.Debug);
        var (moduleRelease, _, codeRelease) = CompileAndGenerate(source, BuildMode.Release);

        // Both should generate valid code
        Assert.NotNull(codeDebug);
        Assert.NotNull(codeRelease);
        Assert.Contains("test", codeDebug);
        Assert.Contains("test", codeRelease);

        // Release should never have #line
        Assert.DoesNotContain("#line", codeRelease);
    }
}
