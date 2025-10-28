using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class LeaOptimizationTests
{
    private string GenerateAssembly(string source, string cpuTarget = "68000")
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        var module = builder.BuildModule(tree);

        var codegen = new M68kCodeGenerator(module, builder.StringLiterals, cpuTarget, "/tmp");
        return codegen.Generate();
    }

    [Fact]
    public void IntegerAddition_DoesNotUseLea()
    {
        var source = @"
fn test(x: u32) -> u32 {
    return x + 16
}";
        var asm = GenerateAssembly(source);

        // Integer addition should use ADD, not LEA
        Assert.DoesNotContain("lea\t", asm);
        Assert.Contains("add.l\td1,d0", asm);
    }

    [Fact]
    public void IntegerAdditionWithLargeConstant_DoesNotUseLea()
    {
        var source = @"
fn test(x: u32) -> u32 {
    return x + 100000
}";
        var asm = GenerateAssembly(source);

        // Integer addition should use ADD even for large constants
        Assert.DoesNotContain("lea\t", asm);
        Assert.Contains("add.l\td1,d0", asm);
    }
}
