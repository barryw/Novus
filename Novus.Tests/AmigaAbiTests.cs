using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.Tests;

public class AmigaAbiTests
{
    private static IrModule BuildIr(string source)
    {
        var lexer = new NovusLexer(new AntlrInputStream(source));
        var parser = new NovusParser(new AngleBracketTokenStream(lexer));
        var tree = parser.compilationUnit();
        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        return new IrBuilder(skipAutoImports: true).BuildModule(tree);
    }

    [Fact]
    public void AmigaFunction_EmitsRegisterBoundSignature()
    {
        const string source = """
            type HookEntry = amiga fn(*u8 in a0, *u8 in a2, *u8 in a1) -> u32 in d0

            amiga fn hook_entry(hook: *u8 in a0, object: *u8 in a2, message: *u8 in a1) -> u32 in d0 {
                return 1u32
            }

            pub fn main() -> i32 {
                let entry: HookEntry = hook_entry
                return entry(0 as *u8, 0 as *u8, 0 as *u8) as i32
            }
            """;

        var module = BuildIr(source);
        var callback = Assert.IsType<IrFunctionPointerType>(
            module.GetFunction("main")!.LocalVariables.Single(variable => variable.Name == "entry").Type);
        Assert.Equal(IrCallingConvention.Amiga, callback.CallingConvention);
        Assert.Equal(new[] { "a0", "a2", "a1" }, callback.ParameterRegisters);
        Assert.Equal("d0", callback.ReturnRegister);

        var hook = module.GetFunction("hook_entry")!;
        Assert.Equal(IrCallingConvention.Amiga, hook.CallingConvention);
        Assert.Equal(new[] { "a0", "a2", "a1" }, hook.Parameters.Select(parameter => parameter.Register));
        Assert.Equal("d0", hook.ReturnRegister);

        var code = new CCodeGenerator(module, [], "68020", "soft", BuildMode.Release).Generate();
        Assert.Contains("__reg(\"d0\") uint32_t hook_entry", code);
        Assert.Contains("__reg(\"a0\") uint8_t* hook", code);
        Assert.Contains("__reg(\"a2\") uint8_t* object", code);
        Assert.Contains("__reg(\"a1\") uint8_t* message", code);
    }

    [Fact]
    public void FunctionPointerAbi_IsPartOfTypeIdentity()
    {
        var interner = new TypeInterner();
        var ordinary = interner.GetFunctionPointerType([IrIntType.U32], IrIntType.U32);
        var amiga = interner.GetFunctionPointerType([IrIntType.U32], IrIntType.U32,
            IrCallingConvention.Amiga, ["d0"], "d0");

        Assert.NotSame(ordinary, amiga);
        Assert.False(TypeChecker.TypesAreEqual(ordinary, amiga));
    }

    [Theory]
    [InlineData("amiga fn bad(value: u32 in a7) -> u32 in d0 { return value }")]
    [InlineData("amiga fn bad(left: u32 in d0, right: u32 in d0) -> u32 in d0 { return left }")]
    [InlineData("fn bad(value: u32 in d0) -> u32 { return value }")]
    public void InvalidRegisterBindings_AreRejected(string declaration)
    {
        var source = declaration + "\nfn main() {}";
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(CompilerTestHelper.Parse(source));

        Assert.True(analyzer.GetResult().Diagnostics.HasErrors);
    }
}
