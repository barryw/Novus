using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.Tests;

public class VolatileMemoryTests
{
    private static readonly string Source = """
        pub fn read_register(address: *u16) -> u16 {
            return unsafe { read_volatile(address) }
        }

        pub fn write_register(address: *u16, value: u16) {
            unsafe { write_volatile(address, value) }
            memory_fence()
        }

        fn main() -> i32 {
            let address = 14675970u32 as *u16
            write_register(address, 1u16)
            return read_register(address) as i32
        }
        """;

    private static IrModule BuildIr(string source)
    {
        var parser = new NovusParser(new AngleBracketTokenStream(
            new NovusLexer(new AntlrInputStream(source))));
        var tree = parser.compilationUnit();
        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        return new IrBuilder(skipAutoImports: true).BuildModule(tree);
    }

    [Fact]
    public void VolatileAccess_EmitsVolatileCAndFence()
    {
        var analyzer = new SemanticAnalyzer("test.novus", Source, "std");
        analyzer.Analyze(CompilerTestHelper.Parse(Source));
        Assert.False(analyzer.GetResult().Diagnostics.HasErrors);

        var module = BuildIr(Source);
        var read = Assert.IsType<IrDereferenceValue>(
            Assert.IsType<IrReturn>(module.GetFunction("read_register")!.BasicBlocks
                .SelectMany(block => block.Instructions).Single(instruction => instruction is IrReturn)).Value);
        Assert.True(read.IsVolatile);

        var write = module.GetFunction("write_register")!.BasicBlocks
            .SelectMany(block => block.Instructions).OfType<IrDereferenceStore>().Single();
        Assert.True(write.IsVolatile);

        var code = new CCodeGenerator(module, [], "68020", "soft", BuildMode.Release).Generate();
        Assert.Contains("volatile uint16_t*", code);
        Assert.Contains("\\tnop\\n", code);
    }

    [Theory]
    [InlineData("fn main() -> u16 {\nlet p = 0 as *u16\nreturn read_volatile(p)\n}")]
    [InlineData("fn main() {\nlet p = 0 as *u16\nwrite_volatile(p, 1u16)\n}")]
    public void VolatileAccess_RequiresUnsafe(string source)
    {
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(CompilerTestHelper.Parse(source));
        Assert.Contains(analyzer.GetResult().Diagnostics.Diagnostics,
            diagnostic => diagnostic.Code == "E1001");
    }

    [Fact]
    public void VolatileAccess_ValidatesPointerAndValueTypes()
    {
        const string source = "fn main() { unsafe { write_volatile(1u32, 2u16) } }";
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(CompilerTestHelper.Parse(source));
        Assert.True(analyzer.GetResult().Diagnostics.HasErrors);
    }
}
