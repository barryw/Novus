using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;

namespace Novus.Tests;

public class InterruptTests
{
    private static IrModule BuildIr(string source)
    {
        var parser = new NovusParser(new AngleBracketTokenStream(
            new NovusLexer(new AntlrInputStream(source))));
        var tree = parser.compilationUnit();
        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        return new IrBuilder(skipAutoImports: true).BuildModule(tree);
    }

    [Fact]
    public void InterruptAttributes_EmitDistinctReturnConventions()
    {
        const string source = """
            @interrupt
            amiga fn server(data: *u8 in a1) -> u32 in d0 { return 0u32 }

            @interrupt_vector
            fn bus_error() {}

            fn main() -> i32 {
                let result = server(0 as *u8)
                bus_error()
                return result as i32
            }
            """;

        var module = BuildIr(source);
        Assert.True(module.GetFunction("server")!.IsInterruptHandler);
        Assert.False(module.GetFunction("server")!.IsInterruptVector);
        Assert.True(module.GetFunction("bus_error")!.IsInterruptVector);

        var code = new CCodeGenerator(module, [], "68020", "soft", BuildMode.Release).Generate();
        Assert.Contains("__amigainterrupt __reg(\"d0\") uint32_t server", code);
        Assert.Contains("__interrupt void bus_error", code);
    }

    [Theory]
    [InlineData("Signal", false)]
    [InlineData("PutMsg", false)]
    [InlineData("ReplyMsg", false)]
    [InlineData("GetMsg", false)]
    [InlineData("FindTask", false)]
    [InlineData("Wait", true)]
    [InlineData("AllocMem", true)]
    public void InterruptSafety_UsesExecInterruptContract(string callee, bool shouldFail)
    {
        var module = new IrModule();
        var handler = new IrFunction("server", IrVoidType.Instance)
        {
            Attributes = new Novus.SemanticAnalysis.AttributeCollection([
                new Novus.SemanticAnalysis.AttributeInfo(
                    Novus.SemanticAnalysis.KnownAttributes.Interrupt,
                    new Novus.Diagnostics.SourceLocation("test.novus", 1, 1, 0, ""))
            ])
        };
        var block = new IrBasicBlock("entry");
        block.Instructions.Add(new IrCall(callee, IrVoidType.Instance, null));
        block.Instructions.Add(new IrReturn());
        handler.BasicBlocks.Add(block);
        module.AddFunction(handler);

        var result = new InterruptSafetyValidator().Validate(module);
        Assert.Equal(shouldFail, !result.IsValid);
    }
}
