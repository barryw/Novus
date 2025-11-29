using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.HIR;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Novus.Transforms;
using Novus.Transforms.Passes;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for Copper and Blitter DSL parsing, semantic analysis, and lowering.
/// The Copper and Blitter are Amiga custom chip co-processors.
/// </summary>
public class CopperBlitterDslTests
{
    private (DiagnosticBag Diagnostics, SemanticAnalyzer Analyzer) Analyze(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(tree);
        return (analyzer.Diagnostics, analyzer);
    }

    private IrModule BuildIR(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        return builder.BuildModule(tree);
    }

    #region Copper DSL Parsing Tests

    [Fact]
    public void CopperDsl_SimpleCopperList_Parses()
    {
        var source = @"
fn test() {
    unsafe {
        let copper_list = copper {
            wait(0, 100)
            move($180, $F00)
        }
    }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
        Assert.Single(module.Functions);
        // Copper list creates an HIR instruction
        Assert.Single(module.HirInstructions);
        Assert.IsType<HirCopperList>(module.HirInstructions[0]);
    }

    [Fact]
    public void CopperDsl_WithSkipInstruction_Parses()
    {
        var source = @"
fn test() {
    unsafe {
        let copper_list = copper {
            wait(0, 100)
            skip(50, 150)
            move($180, $00F)
        }
    }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);

        var copperList = Assert.Single(module.HirInstructions) as HirCopperList;
        Assert.NotNull(copperList);
        Assert.Equal(3, copperList.Instructions.Count);
        Assert.IsType<HirCopperInstruction.Wait>(copperList.Instructions[0]);
        Assert.IsType<HirCopperInstruction.Skip>(copperList.Instructions[1]);
        Assert.IsType<HirCopperInstruction.Move>(copperList.Instructions[2]);
    }

    [Fact]
    public void CopperDsl_MultipleWaitsAndMoves_Parses()
    {
        var source = @"
fn test() {
    unsafe {
        let copper_list = copper {
            wait(0, 44)
            move($180, $000)
            wait(0, 100)
            move($180, $F00)
            wait(0, 150)
            move($180, $0F0)
            wait(0, 200)
            move($180, $00F)
        }
    }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);

        var copperList = Assert.Single(module.HirInstructions) as HirCopperList;
        Assert.NotNull(copperList);
        Assert.Equal(8, copperList.Instructions.Count);
    }

    [Fact]
    public void CopperDsl_RequiresUnsafeBlock()
    {
        var source = @"
fn test() {
    let copper_list = copper {
        wait(0, 100)
    }
}";
        var (diagnostics, _) = Analyze(source);
        // Should report an error about requiring unsafe
        Assert.True(diagnostics.HasErrors);
    }

    #endregion

    #region Blitter DSL Parsing Tests

    [Fact]
    public void BlitterDsl_SimpleCopy_Parses()
    {
        // Note: blitter fields are comma-separated per grammar
        var source = @"
fn test(src: *u8, dst: *u8) {
    unsafe {
        blitter {
            source: src,
            dest: dst,
            width: 16,
            height: 16,
            minterm: $F0
        }
    }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
        Assert.Single(module.Functions);
        Assert.Single(module.HirInstructions);
        Assert.IsType<HirBlitterJob>(module.HirInstructions[0]);
    }

    [Fact]
    public void BlitterDsl_WithAllFields_Parses()
    {
        // Note: blitter fields are comma-separated per grammar
        var source = @"
fn test(a: *u8, b: *u8, c: *u8, d: *u8) {
    unsafe {
        blitter {
            sourcea: a,
            sourceb: b,
            sourcec: c,
            dest: d,
            width: 32,
            height: 64,
            minterm: $CA,
            shift: 4
        }
    }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);

        var blitterJob = Assert.Single(module.HirInstructions) as HirBlitterJob;
        Assert.NotNull(blitterJob);
        Assert.True(blitterJob.Fields.ContainsKey("sourcea"));
        Assert.True(blitterJob.Fields.ContainsKey("sourceb"));
        Assert.True(blitterJob.Fields.ContainsKey("sourcec"));
        Assert.True(blitterJob.Fields.ContainsKey("dest"));
    }

    [Fact]
    public void BlitterDsl_RequiresUnsafeBlock()
    {
        // Note: blitter fields are comma-separated per grammar, minterm required
        var source = @"
fn test(src: *u8, dst: *u8) {
    blitter {
        source: src,
        dest: dst,
        width: 16,
        height: 16,
        minterm: $F0
    }
}";
        var (diagnostics, _) = Analyze(source);
        // Should report an error about requiring unsafe
        Assert.True(diagnostics.HasErrors);
    }

    #endregion

    #region Copper Lowering Tests

    [Fact]
    public void CopperLowering_GeneratesStaticData()
    {
        var source = @"
fn test() {
    unsafe {
        let copper_list = copper {
            wait(0, 100)
            move($180, $F00)
        }
    }
}";
        var module = BuildIR(source);

        // Run the copper lowering pass
        var pass = new CopperLoweringPass();
        bool changed = pass.Transform(module);

        Assert.True(changed);
        // HIR instruction should be removed
        Assert.Empty(module.HirInstructions);
        // Static variable should be created
        Assert.Single(module.StaticVariables);
        Assert.StartsWith("__copper_list_", module.StaticVariables[0].Name);
    }

    [Fact]
    public void CopperLowering_GeneratesCorrectWaitFormat()
    {
        var source = @"
fn test() {
    unsafe {
        let copper_list = copper {
            wait(0, 100)
        }
    }
}";
        var module = BuildIR(source);
        var pass = new CopperLoweringPass();
        pass.Transform(module);

        var staticVar = module.StaticVariables[0];
        var arrayLiteral = staticVar.InitialValue as IrArrayLiteral;
        Assert.NotNull(arrayLiteral);

        // WAIT(0, 100) should generate: VP=100 (0x64), HP=0, wait bit=1
        // Word 1: (100 << 8) | (0 << 1) | 1 = 0x6401
        // Word 2: 0xFFFE (mask)
        Assert.True(arrayLiteral.Elements.Count >= 4); // 2 words + 2 terminator words

        var word1 = (arrayLiteral.Elements[0] as IrConstant)?.Value;
        var word2 = (arrayLiteral.Elements[1] as IrConstant)?.Value;
        Assert.Equal(0x6401L, word1);
        Assert.Equal(0xFFFEL, word2);
    }

    [Fact]
    public void CopperLowering_GeneratesCorrectMoveFormat()
    {
        var source = @"
fn test() {
    unsafe {
        let copper_list = copper {
            move($180, $F00)
        }
    }
}";
        var module = BuildIR(source);

        // Verify the HIR instruction has the right values before lowering
        Assert.Single(module.HirInstructions);
        var copperList = module.HirInstructions[0] as HirCopperList;
        Assert.NotNull(copperList);
        Assert.Single(copperList.Instructions);
        var moveInstr = copperList.Instructions[0] as HirCopperInstruction.Move;
        Assert.NotNull(moveInstr);

        // Check the Register value is an IrConstant with value $180
        var regConstant = moveInstr.Register as IrConstant;
        Assert.NotNull(regConstant);
        Assert.Equal(0x180L, regConstant.Value);

        // Check the Value value is an IrConstant with value $F00
        var valConstant = moveInstr.Value as IrConstant;
        Assert.NotNull(valConstant);
        Assert.Equal(0xF00L, valConstant.Value);

        var pass = new CopperLoweringPass();
        pass.Transform(module);

        var staticVar = module.StaticVariables[0];
        var arrayLiteral = staticVar.InitialValue as IrArrayLiteral;
        Assert.NotNull(arrayLiteral);

        // MOVE($180, $F00) should generate: register offset $180, value $F00
        var word1 = (arrayLiteral.Elements[0] as IrConstant)?.Value;
        var word2 = (arrayLiteral.Elements[1] as IrConstant)?.Value;
        Assert.Equal(0x0180L, word1);
        Assert.Equal(0x0F00L, word2);
    }

    [Fact]
    public void CopperLowering_GeneratesTerminator()
    {
        var source = @"
fn test() {
    unsafe {
        let copper_list = copper {
            wait(0, 100)
        }
    }
}";
        var module = BuildIR(source);
        var pass = new CopperLoweringPass();
        pass.Transform(module);

        var staticVar = module.StaticVariables[0];
        var arrayLiteral = staticVar.InitialValue as IrArrayLiteral;
        Assert.NotNull(arrayLiteral);

        // Last two words should be terminator: 0xFFFF, 0xFFFE
        int lastIdx = arrayLiteral.Elements.Count - 1;
        var termWord1 = (arrayLiteral.Elements[lastIdx - 1] as IrConstant)?.Value;
        var termWord2 = (arrayLiteral.Elements[lastIdx] as IrConstant)?.Value;
        Assert.Equal(0xFFFFL, termWord1);
        Assert.Equal(0xFFFEL, termWord2);
    }

    #endregion

    #region Blitter Lowering Tests

    [Fact]
    public void BlitterLowering_RemovesHirInstruction()
    {
        // Note: blitter fields are comma-separated per grammar
        var source = @"
fn test(src: *u8, dst: *u8) {
    unsafe {
        blitter {
            source: src,
            dest: dst,
            width: 16,
            height: 16,
            minterm: $F0
        }
    }
}";
        var module = BuildIR(source);

        // Run the blitter lowering pass
        var pass = new BlitterLoweringPass();
        bool changed = pass.Transform(module);

        Assert.True(changed);
        // HIR instruction should be removed
        Assert.Empty(module.HirInstructions);
    }

    [Fact]
    public void BlitterLowering_ValidatesDestinationRequired()
    {
        // Note: blitter fields are comma-separated per grammar
        var source = @"
fn test(src: *u8) {
    unsafe {
        blitter {
            source: src,
            width: 16,
            height: 16
        }
    }
}";
        var module = BuildIR(source);

        // Run the blitter lowering pass - should throw because no destination
        var pass = new BlitterLoweringPass();
        Assert.Throws<InvalidOperationException>(() => pass.Transform(module));
    }

    #endregion

    #region Transform Pipeline Tests

    [Fact]
    public void TransformPipeline_IncludesLoweringPasses()
    {
        var source = @"
fn test() {
    unsafe {
        let copper_list = copper {
            wait(0, 100)
            move($180, $F00)
        }
    }
}";
        var module = BuildIR(source);

        // Run the full transform pipeline
        var pipeline = TransformPipeline.CreatePipeline(enableInlining: false, verbose: false);
        bool changed = pipeline.Run(module);

        Assert.True(changed);
        // All HIR instructions should be lowered
        Assert.Empty(module.HirInstructions);
    }

    #endregion
}
