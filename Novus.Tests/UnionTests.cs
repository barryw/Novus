using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.Tests;

public class UnionTests
{
    private const string Source = """
        pub union RegisterValue {
            raw: u32,
            words: [u16; 2],
            bytes: [u8; 4],
        }

        pub fn read_low() -> u16 {
            let value = RegisterValue { raw: 305419896 }
            return unsafe { value.raw } as u16
        }

        pub fn main() -> i32 { return read_low() as i32 }
        """;

    private static DiagnosticBag Analyze(string source)
    {
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(CompilerTestHelper.Parse(source));
        return analyzer.GetResult().Diagnostics;
    }

    private static IrModule BuildIr(string source)
    {
        var parser = new NovusParser(new AngleBracketTokenStream(
            new NovusLexer(new AntlrInputStream(source))));
        var tree = parser.compilationUnit();
        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var builder = new IrBuilder(skipAutoImports: true);
        var module = builder.BuildModule(tree);
        Assert.False(builder.Diagnostics.HasErrors,
            string.Join(Environment.NewLine, builder.Diagnostics.Diagnostics.Select(d => d.Message)));
        return module;
    }

    [Fact]
    public void Union_UsesLargestFieldAndZeroOffsets()
    {
        var module = BuildIr(Source);
        var union = module.GetStruct("RegisterValue")!;

        Assert.True(union.IsUnion);
        Assert.Equal(4, union.SizeInBytes);
        Assert.All(union.Fields, field => Assert.Equal(0, field.Offset));
    }

    [Fact]
    public void Union_EmitsNativeCUnion()
    {
        var module = BuildIr(Source);
        var registry = new TypeRegistry();
        registry.RegisterModule(module);
        var code = CCodeGenerator.GenerateSharedTypesHeader(registry);

        Assert.Contains("union RegisterValue {", code);
        Assert.Contains("typedef union RegisterValue RegisterValue;", code);

        var union = module.GetStruct("RegisterValue")!;
        var literal = new CCodeGenerator(module, [], "68020", "soft", BuildMode.Release)
            .EmitStructLiteral(new IrStructLiteral(union,
            new Dictionary<string, IrValue> { ["raw"] = new IrConstant(1u, IrIntType.U32) }));
        Assert.Contains("(RegisterValue){ .raw =", literal);
    }

    [Fact]
    public void ImportedNestedNdkUnion_PreservesAbiSize()
    {
        const string source = """
            from amiga::raw::structs import CopIns
            pub fn size() -> u32 { return @sizeof(CopIns) }
            """;
        var parser = new NovusParser(new AngleBracketTokenStream(
            new NovusLexer(new AntlrInputStream(source))));
        var builder = new IrBuilder(skipAutoImports: true);
        builder.SetStdLibPath(PathUtility.FindStdLibPath()!);
        var module = builder.BuildModule(parser.compilationUnit());

        Assert.False(builder.Diagnostics.HasErrors,
            string.Join(Environment.NewLine, builder.Diagnostics.Diagnostics.Select(d => d.Message)));
        var copIns = module.GetStruct("CopIns")!;
        Assert.Equal(6, copIns.SizeInBytes);
        Assert.Equal("6", new CCodeGenerator(module, [], "68020", "soft", BuildMode.Release)
            .EmitSizeOf(new IrSizeOf(copIns, IrIntType.U32)));
    }

    [Fact]
    public void Union_ExactlyOneInitialField_IsValid()
    {
        Assert.False(Analyze(Source).HasErrors);
    }

    [Fact]
    public void Union_MultipleInitialFields_AreRejected()
    {
        const string source = """
            union Value { word: u16, raw: u32 }
            fn main() { let value = Value { word: 1, raw: 2 } }
            """;

        Assert.True(Analyze(source).HasErrors);
    }

    [Fact]
    public void Union_FieldAccess_RequiresUnsafe()
    {
        const string source = """
            union Value { word: u16, raw: u32 }
            fn main() -> u32 {
                let value = Value { raw: 2 }
                return value.raw
            }
            """;

        Assert.True(Analyze(source).HasErrors);
    }
}
