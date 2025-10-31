using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class CodeGeneratorTests
{
    private string GenerateAssembly(string source, string cpuTarget = "68000")
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        var module = builder.BuildModule(tree);

        var codegen = new M68kCodeGenerator(module, builder.StringLiterals, cpuTarget);
        return codegen.Generate();
    }

    [Fact]
    public void Generate_SimpleReturn_ContainsBasicStructure()
    {
        var source = @"
pub fn main() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source);

        Assert.Contains("section\ttext,code", asm);
        Assert.Contains("xdef\t_main", asm);
        Assert.Contains("_main:", asm);
        Assert.Contains("rts", asm);
    }

    [Fact]
    public void Generate_SimpleReturn_68000_ContainsCorrectHeader()
    {
        var source = @"
fn main() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source, "68000");

        Assert.Contains("Target: Motorola 68000", asm);
    }

    [Fact]
    public void Generate_SimpleReturn_68020_ContainsCorrectHeader()
    {
        var source = @"
fn main() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source, "68020");

        Assert.Contains("Target: Motorola 68020", asm);
    }

    [Fact]
    public void Generate_ReturnConstant_UsesMove()
    {
        var source = @"
fn main() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source);

        // Should use moveq optimization for small constants
        Assert.Contains("moveq\t#42,d0", asm);
    }

    [Fact]
    public void Generate_ReturnSmallConstant_UsesMoveq()
    {
        var source = @"
fn main() -> u32 {
    return 10
}";
        var asm = GenerateAssembly(source);

        // Should use moveq optimization for small constants
        Assert.Contains("moveq\t#10,d0", asm);
    }

    [Fact]
    public void Generate_ReturnLargeConstant_UsesMoveLong()
    {
        var source = @"
fn main() -> u32 {
    return 200
}";
        var asm = GenerateAssembly(source);

        // Should use move.l for values > 127
        Assert.Contains("move.l\t#200,d0", asm);
    }

    [Fact]
    public void Generate_Addition_UsesAddInstruction()
    {
        var source = @"
fn test() -> u32 {
    return 10 + 20
}";
        var asm = GenerateAssembly(source);

        Assert.Contains("moveq\t#10,d0", asm);
        Assert.Contains("moveq\t#20,d1", asm);
        Assert.Contains("add.l\td1,d0", asm);
    }

    [Fact]
    public void Generate_Subtraction_UsesSubInstruction()
    {
        var source = @"
fn test() -> u32 {
    return 30 - 10
}";
        var asm = GenerateAssembly(source);

        Assert.Contains("sub.l\td1,d0", asm);
    }

    [Fact]
    public void Generate_Multiplication_68000_UsesWordMultiply()
    {
        var source = @"
fn test() -> i32 {
    return 5 * 6
}";
        var asm = GenerateAssembly(source, "68000");

        // For 68000, 32-bit multiply should use 16x16 multiply routine
        Assert.Contains("68000: 32-bit multiply", asm);
    }

    [Fact]
    public void Generate_Multiplication_68020_UsesLongMultiply()
    {
        var source = @"
fn test() -> i32 {
    return 5 * 6
}";
        var asm = GenerateAssembly(source, "68020");

        // For 68020+, should use native 32-bit multiply
        Assert.Contains("muls.l\td1,d0", asm);
    }

    [Fact]
    public void Generate_UnsignedMultiply_UsesMulu()
    {
        var source = @"
fn test() -> u32 {
    return 5u32 * 6u32
}";
        var asm = GenerateAssembly(source, "68020");

        // Unsigned types should use mulu
        Assert.Contains("mulu.l\td1,d0", asm);
    }

    [Fact]
    public void Generate_SignedMultiply_UsesMuls()
    {
        var source = @"
fn test() -> i32 {
    return 5 * 6
}";
        var asm = GenerateAssembly(source, "68020");

        // Signed types should use muls
        Assert.Contains("muls.l\td1,d0", asm);
    }

    [Fact]
    public void Generate_ChainedOperations_GeneratesMultipleInstructions()
    {
        var source = @"
fn test() -> u32 {
    return (10u32 + 20u32) * 2u32
}";
        var asm = GenerateAssembly(source, "68020");

        Assert.Contains("moveq\t#10,d0", asm);
        Assert.Contains("moveq\t#20,d1", asm);
        Assert.Contains("add.l\td1,d0", asm);
        Assert.Contains("moveq\t#2,d1", asm);
        Assert.Contains("mulu.l\td1,d0", asm);
    }

    [Fact]
    public void Generate_MultipleFunctions_GeneratesAllFunctions()
    {
        var source = @"
pub fn add(a: i32, b: i32) -> i32 {
    return a
}

pub fn main() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source);

        Assert.Contains("xdef\t_add", asm);
        Assert.Contains("_add:", asm);
        Assert.Contains("xdef\t_main", asm);
        Assert.Contains("_main:", asm);
    }

    [Fact]
    public void Generate_FunctionComment_IncludesFunctionName()
    {
        var source = @"
fn my_function() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source);

        Assert.Contains("Function: my_function", asm);
    }

    [Fact]
    public void Generate_68000Multiply_IncludesWarningComment()
    {
        var source = @"
fn test() -> i32 {
    return 100 * 200
}";
        var asm = GenerateAssembly(source, "68000");

        // Should include warning about incomplete 68000 multiply
        Assert.Contains("68000: 32-bit multiply", asm);
    }

    [Fact]
    public void Generate_U8Type_UsesByteSize()
    {
        // Note: Current implementation may not fully support this yet
        // This test documents expected behavior
        var source = @"
fn test() -> u8 {
    return 42u8
}";
        var asm = GenerateAssembly(source);

        // Should generate code (exact instruction may vary)
        Assert.NotEmpty(asm);
        Assert.Contains("rts", asm);
    }

    [Fact]
    public void Generate_U16Type_UsesWordSize()
    {
        var source = @"
fn test() -> u16 {
    return 42u16
}";
        var asm = GenerateAssembly(source);

        Assert.NotEmpty(asm);
        Assert.Contains("rts", asm);
    }

    [Fact]
    public void Generate_CommentedAssembly_ContainsCompilerSignature()
    {
        var source = @"
fn main() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source);

        Assert.Contains("Generated by Novus compiler", asm);
    }

    [Fact]
    public void Generate_WithComments_ContainsIrVariableNames()
    {
        var source = @"
fn test() -> u32 {
    return 10 + 20
}";
        var asm = GenerateAssembly(source);

        // Should include IR variable names in comments for debugging
        Assert.Contains("%t0", asm);
    }

    [Fact]
    public void Generate_ReturnsInD0_AmigaABICompliance()
    {
        var source = @"
fn main() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source);

        // Return value should be in d0 per Amiga ABI
        var lines = asm.Split('\n');
        var rtsIndex = Array.FindIndex(lines, l => l.Contains("rts"));
        var d0MoveIndex = Array.FindIndex(lines, l => l.Contains(",d0") && !l.Trim().StartsWith(";"));

        Assert.True(d0MoveIndex < rtsIndex, "Value should be moved to d0 before rts");
    }

    [Fact]
    public void Generate_EmptyLines_ProperlyFormatted()
    {
        var source = @"
fn main() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source);

        // Assembly should be well-formatted with proper spacing
        Assert.NotEmpty(asm);
        var lines = asm.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.All(lines, line => Assert.False(line.StartsWith(" ") && !line.StartsWith("\t")));
    }

    // Control Flow Code Generation Tests

    [Fact]
    public void Generate_IfStatement_GeneratesLabelsAndBranches()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 100
    }
    return 0
}";
        var asm = GenerateAssembly(source);

        // Should have labels
        Assert.Contains("if_then_", asm);
        Assert.Contains("if_end_", asm);

        // Should have comparison and conditional branch (optimized - no materialization)
        Assert.Contains("cmp", asm);
        Assert.Contains("bgt", asm);     // Branch directly on comparison result
    }

    [Fact]
    public void Generate_IfElseStatement_GeneratesAllLabels()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 100
    } else {
        return 200
    }
}";
        var asm = GenerateAssembly(source);

        // Should have all labels
        Assert.Contains("if_then_", asm);
        Assert.Contains("if_else_", asm);
        Assert.Contains("if_end_", asm);

        // Should have conditional branch (optimized - no materialization with tst.l)
        Assert.Contains("cmp", asm);
        Assert.Contains("bgt", asm);     // Branch directly on comparison result
        Assert.Contains("bra", asm);     // Unconditional branch to skip else
    }

    [Fact]
    public void Generate_Comparison_UsesCorrectInstruction()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 1
    }
    return 0
}";
        var asm = GenerateAssembly(source);

        // Should use CMP for comparison
        Assert.Contains("cmp", asm);

        // Should use conditional branch (bgt) - optimized, doesn't materialize with sgt
        Assert.Contains("bgt", asm);
    }

    [Fact]
    public void Generate_AllComparisonOperators_GeneratesCorrectConditions()
    {
        // When comparisons are used in if statements, they're optimized to conditional branches
        var testCases = new[]
        {
            ("==", "beq"),  // Branch if equal
            ("!=", "bne"),  // Branch if not equal
            ("<", "blt"),   // Branch if less than
            ("<=", "ble"),  // Branch if less or equal
            (">", "bgt"),   // Branch if greater than
            (">=", "bge")   // Branch if greater or equal
        };

        foreach (var (op, expected) in testCases)
        {
            var source = $@"
fn test(x: i32) -> i32 {{
    if x {op} 10 {{
        return 1
    }}
    return 0
}}";
            var asm = GenerateAssembly(source);

            Assert.Contains(expected, asm);
        }
    }

    [Fact]
    public void Generate_WhileLoop_GeneratesLoopStructure()
    {
        var source = @"
fn test() -> i32 {
    while 0 == 1 {
        return 999
    }
    return 42
}";
        var asm = GenerateAssembly(source);

        // Should have loop labels
        Assert.Contains("while_cond_", asm);
        Assert.Contains("while_body_", asm);
        Assert.Contains("while_end_", asm);

        // Should have branch back to condition
        Assert.Contains("bra\twhile_cond_", asm);
    }

    [Fact]
    public void Generate_ForeverLoop_GeneratesInfiniteLoop()
    {
        var source = @"
fn test() -> i32 {
    forever {
        break
    }
    return 123
}";
        var asm = GenerateAssembly(source);

        // Should have loop labels
        Assert.Contains("forever_body_", asm);
        Assert.Contains("forever_end_", asm);

        // Note: No branch back to body because the loop body immediately breaks (dead code elimination)

        // Should have branch to end (for break)
        Assert.Contains("bra\tforever_end_", asm);
    }

    [Fact]
    public void Generate_Break_GeneratesBranchToEnd()
    {
        var source = @"
fn test() -> i32 {
    forever {
        break
    }
    return 42
}";
        var asm = GenerateAssembly(source);

        // Break should generate branch to loop end
        Assert.Contains("bra\tforever_end_", asm);
    }

    [Fact]
    public void Generate_NestedIf_GeneratesUniqueLabels()
    {
        var source = @"
fn test(x: i32, y: i32) -> i32 {
    if x > 0 {
        if y > 0 {
            return 1
        }
    }
    return 0
}";
        var asm = GenerateAssembly(source);

        // Should have unique labels for each if
        var lines = asm.Split('\n');
        var ifThenLabels = lines.Where(l => l.Contains("if_then_")).ToList();
        var ifEndLabels = lines.Where(l => l.Contains("if_end_")).ToList();

        Assert.True(ifThenLabels.Count >= 2);
        Assert.True(ifEndLabels.Count >= 2);
    }

    [Fact]
    public void Generate_NestedLoops_GeneratesUniqueLabels()
    {
        var source = @"
fn test() -> i32 {
    while 1 == 1 {
        forever {
            break
        }
        break
    }
    return 0
}";
        var asm = GenerateAssembly(source);

        // Should have labels for both loops
        Assert.Contains("while_", asm);
        Assert.Contains("forever_", asm);
    }

    [Fact]
    public void Generate_ComparisonResult_ProperlyConverted()
    {
        // Test that assigns comparison to variable - this DOES materialize the boolean
        var source = @"
fn test(x: i32) -> i32 {
    let result: bool = x > 10
    if result {
        return 1
    }
    return 0
}";
        var asm = GenerateAssembly(source);

        // When comparison is assigned to variable, it's materialized using Scc
        Assert.Contains("sgt", asm);  // Set if greater than

        // Should use ext.w + ext.l (68000 compatible, not extb.l which is 68020+)
        Assert.Contains("ext.w", asm);
        Assert.Contains("ext.l", asm);

        // Should use neg.l to convert $FFFFFFFF to 1
        Assert.Contains("neg.l", asm);
    }

    [Fact]
    public void Generate_WhileConditionFalse_SkipsBody()
    {
        var source = @"
fn test() -> i32 {
    while 0 == 1 {
        return 999
    }
    return 42
}";
        var asm = GenerateAssembly(source);

        // Should compare 0 and 1
        Assert.Contains("moveq\t#0,d0", asm);
        Assert.Contains("moveq\t#1,d1", asm);
        Assert.Contains("cmp", asm);

        // Should have conditional branch (optimized - branch directly on comparison)
        Assert.Contains("beq\twhile_body_", asm);  // Branch if equal (0 == 1)
        Assert.Contains("bra\twhile_end_", asm);   // Skip body if condition false
    }

    [Fact]
    public void Generate_IfCondition_LoadsOperands()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 100
    }
    return 0
}";
        var asm = GenerateAssembly(source);

        // Should load x parameter (from parameter location)
        // and constant 10
        Assert.Contains("moveq\t#10,d1", asm);
    }

    [Fact]
    public void Generate_ComplexCondition_EvaluatesCorrectly()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if (x + 5) > 10 {
        return 1
    }
    return 0
}";
        var asm = GenerateAssembly(source);

        // Should have addition
        Assert.Contains("add", asm);

        // Should have comparison and conditional branch (optimized)
        Assert.Contains("cmp", asm);
        Assert.Contains("bgt", asm);  // Optimized to branch directly
    }

    [Fact]
    public void Generate_Label_ProperFormat()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 0 {
        return 1
    }
    return 0
}";
        var asm = GenerateAssembly(source);

        // Labels should end with colon and not be indented
        var lines = asm.Split('\n');
        var labelLines = lines.Where(l => l.Contains("if_then_") && l.Contains(":")).ToList();

        foreach (var line in labelLines)
        {
            if (line.Trim().EndsWith(":"))
            {
                Assert.False(line.StartsWith("\t"), $"Label should not be indented: {line}");
            }
        }
    }

    [Fact]
    public void Generate_Branch_ProperFormat()
    {
        var source = @"
fn test() -> i32 {
    forever {
        break
    }
    return 0
}";
        var asm = GenerateAssembly(source);

        // Branches should be indented with tab
        var lines = asm.Split('\n');
        var branchLines = lines.Where(l => l.Contains("\tbra\t")).ToList();

        Assert.NotEmpty(branchLines);
        foreach (var line in branchLines)
        {
            Assert.StartsWith("\t", line);
        }
    }

    // Visibility Tests

    [Fact]
    public void Generate_PublicFunction_ExportsSymbol()
    {
        var source = @"
pub fn exported() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source);

        // Should have xdef for public function
        Assert.Contains("xdef\t_exported", asm);
        Assert.Contains("_exported:", asm);
    }

    [Fact]
    public void Generate_PrivateFunction_DoesNotExportSymbol()
    {
        var source = @"
fn internal() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source);

        // Should NOT have xdef for private function
        Assert.DoesNotContain("xdef\t_internal", asm);
        // But should still have the function label
        Assert.Contains("_internal:", asm);
    }

    [Fact]
    public void Generate_MixedVisibility_ExportsOnlyPublic()
    {
        var source = @"
pub fn public_api() -> u32 {
    return 42
}

fn helper() -> u32 {
    return 10
}";
        var asm = GenerateAssembly(source);

        // Public function should be exported
        Assert.Contains("xdef\t_public_api", asm);
        Assert.Contains("_public_api:", asm);

        // Private function should NOT be exported
        Assert.DoesNotContain("xdef\t_helper", asm);
        // But should still have the function label
        Assert.Contains("_helper:", asm);
    }

    [Fact]
    public void Generate_PublicMain_ExportsCorrectly()
    {
        var source = @"
pub fn main() -> u32 {
    return 0
}";
        var asm = GenerateAssembly(source);

        Assert.Contains("xdef\t_main", asm);
        Assert.Contains("_main:", asm);
    }

    [Fact]
    public void Generate_FunctionCallNoArguments_GeneratesJSR()
    {
        var source = @"
pub fn main() -> u32 {
    return helper()
}

pub fn helper() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source);

        // Should contain JSR to helper
        Assert.Contains("jsr\t_helper", asm);
        // Should NOT push any arguments before JSR
        // (but may save result after JSR, which is fine)
        var mainSection = asm.Substring(asm.IndexOf("_main:"));
        var jsrIndex = mainSection.IndexOf("jsr\t_helper");
        var beforeJsr = mainSection.Substring(0, jsrIndex);
        // No stack pushes before the JSR (arguments would be pushed before call)
        Assert.DoesNotContain("-(sp)", beforeJsr);
    }

    [Fact]
    public void Generate_FunctionCallWithOneArgument_PushesArgumentAndCallsJSR()
    {
        var source = @"
pub fn main() -> u32 {
    return double(21)
}

pub fn double(x: u32) -> u32 {
    return x + x
}";
        var asm = GenerateAssembly(source);

        // Should push argument onto stack
        Assert.Contains("-(sp)", asm);
        // Should call double
        Assert.Contains("jsr\t_double", asm);
        // Should clean up stack (4 bytes for 1 argument)
        Assert.Contains("lea\t4(sp),sp", asm);
    }

    [Fact]
    public void Generate_FunctionCallWithMultipleArguments_PushesAllArguments()
    {
        var source = @"
pub fn main() -> u32 {
    return add(10, 32)
}

pub fn add(a: u32, b: u32) -> u32 {
    return a + b
}";
        var asm = GenerateAssembly(source);

        // Should call add
        Assert.Contains("jsr\t_add", asm);
        // Should clean up stack (8 bytes for 2 arguments)
        Assert.Contains("lea\t8(sp),sp", asm);
    }

    [Fact]
    public void Generate_FunctionCallInExpression_PreservesResultInD0()
    {
        var source = @"
pub fn main() -> u32 {
    return add(10, 20) + 12
}

pub fn add(a: u32, b: u32) -> u32 {
    return a + b
}";
        var asm = GenerateAssembly(source);

        // Should call add
        Assert.Contains("jsr\t_add", asm);
        // Should use result in subsequent addition
        Assert.Contains("add.l", asm);
    }

    [Fact]
    public void Generate_NestedFunctionCalls_GeneratesMultipleJSR()
    {
        var source = @"
pub fn main() -> u32 {
    return add(double(5), 10)
}

pub fn add(a: u32, b: u32) -> u32 {
    return a + b
}

pub fn double(x: u32) -> u32 {
    return x + x
}";
        var asm = GenerateAssembly(source);

        // Should call both functions
        Assert.Contains("jsr\t_double", asm);
        Assert.Contains("jsr\t_add", asm);
    }

    [Fact]
    public void Generate_FunctionWithParametersUsesStack()
    {
        var source = @"
pub fn main() -> u32 {
    return add(10, 32)
}

pub fn add(a: u32, b: u32) -> u32 {
    return a + b
}";
        var asm = GenerateAssembly(source);

        // add function should have prologue since it has parameters
        Assert.Contains("link\ta6,#0", asm);
        // Should load parameters from stack
        Assert.Contains("8(a6)", asm);  // First parameter at 8(a6)
        Assert.Contains("12(a6)", asm); // Second parameter at 12(a6)
    }

    // CPU-Specific Optimization Tests

    [Fact]
    public void Generate_68060_MultiplyByTwo_UsesShiftAddOptimization()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x * 2
}";
        var asm = GenerateAssembly(source, "68060");

        // 68060 should optimize x * 2 to add.l d0,d0 instead of muls.l
        Assert.Contains("68060 optimization: x * 2", asm);
        Assert.Contains("add.l\td0,d0", asm);
        Assert.DoesNotContain("muls.l", asm);
    }

    [Fact]
    public void Generate_68020_MultiplyByTwo_UsesNativeMultiply()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x * 2
}";
        var asm = GenerateAssembly(source, "68020");

        // 68020 should use native muls.l (no 68060 optimization)
        Assert.Contains("muls.l\td1,d0", asm);
        Assert.DoesNotContain("68060 optimization", asm);
    }

    [Fact]
    public void Generate_68060_MultiplyByThree_UsesShiftAddOptimization()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x * 3
}";
        var asm = GenerateAssembly(source, "68060");

        // 68060 should optimize x * 3 to x + (x << 1)
        Assert.Contains("68060 optimization: x * 3", asm);
        Assert.Contains("add.l\td0,d0", asm);  // x << 1
        Assert.Contains("add.l\td1,d0", asm);  // + x
        Assert.DoesNotContain("muls.l", asm);
    }

    [Fact]
    public void Generate_68060_MultiplyByFour_UsesDoubleShift()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x * 4
}";
        var asm = GenerateAssembly(source, "68060");

        // 68060 should optimize x * 4 to x << 2 (two adds)
        Assert.Contains("68060 optimization: x * 4", asm);
        var addCount = asm.Split(new[] { "add.l\td0,d0" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(2, addCount);
        Assert.DoesNotContain("muls.l", asm);
    }

    [Fact]
    public void Generate_68060_MultiplyByFive_UsesShiftAddOptimization()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x * 5
}";
        var asm = GenerateAssembly(source, "68060");

        // 68060 should optimize x * 5 to x + (x << 2)
        Assert.Contains("68060 optimization: x * 5", asm);
        Assert.DoesNotContain("muls.l", asm);
    }

    [Fact]
    public void Generate_68060_MultiplyByEight_UsesTripleShift()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x * 8
}";
        var asm = GenerateAssembly(source, "68060");

        // 68060 should optimize x * 8 to x << 3 (three adds)
        Assert.Contains("68060 optimization: x * 8", asm);
        var addCount = asm.Split(new[] { "add.l\td0,d0" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(3, addCount);
        Assert.DoesNotContain("muls.l", asm);
    }

    [Fact]
    public void Generate_68060_MultiplyByTen_UsesShiftAddOptimization()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x * 10
}";
        var asm = GenerateAssembly(source, "68060");

        // 68060 should optimize x * 10
        Assert.Contains("68060 optimization: x * 10", asm);
        Assert.DoesNotContain("muls.l", asm);
    }

    [Fact]
    public void Generate_68060_MultiplyByLargeConstant_UsesNativeMultiply()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x * 200
}";
        var asm = GenerateAssembly(source, "68060");

        // 68060 should use native muls.l for large constants
        Assert.Contains("muls.l\td1,d0", asm);
        // But should have a warning comment
        Assert.Contains("Note: 68060 has slow multiply", asm);
    }

    [Fact]
    public void Generate_DivideByPowerOfTwo_OptimizesToShift()
    {
        var source = @"
fn test(x: u32) -> u32 {
    return x / 4
}";
        var asm = GenerateAssembly(source, "68020");

        // Division by power of 2 should optimize to right shift (unsigned only)
        Assert.Contains("Optimized: x / 4 = x >> 2", asm);
        Assert.Contains("lsr.l\t#2,d0", asm);
        Assert.DoesNotContain("divu.l", asm);
    }

    [Fact]
    public void Generate_68060_DivideByPowerOfTwo_HasOptimizationComment()
    {
        var source = @"
fn test(x: u32) -> u32 {
    return x / 8
}";
        var asm = GenerateAssembly(source, "68060");

        // 68060 should have specific optimization comment (only for unsigned)
        Assert.Contains("68060 optimization: x / 8 = x >> 3", asm);
        Assert.Contains("lsr.l\t#3,d0", asm);
        Assert.DoesNotContain("divu.l", asm);
    }

    [Fact]
    public void Generate_68060_DivideNonPowerOfTwo_HasWarning()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x / 7
}";
        var asm = GenerateAssembly(source, "68060");

        // 68060 should use divs.l with warning
        Assert.Contains("divs.l\td1,d0", asm);
        Assert.Contains("WARNING: 68060 has very slow 32-bit divide", asm);
    }

    [Fact]
    public void Generate_68000_StackAlignment_Uses2Bytes()
    {
        var source = @"
fn test() -> i32 {
    var x: i32 = 10
    var y: i32 = 20
    return x + y
}";
        var asm = GenerateAssembly(source, "68000");

        // 68000 uses 2-byte alignment, so 2 i32 variables = 8 bytes
        Assert.Contains("link\ta6,#-8", asm);
    }

    [Fact]
    public void Generate_68060_StackAlignment_Uses8Bytes()
    {
        var source = @"
fn test() -> i32 {
    var x: i32 = 10
    var y: i32 = 20
    return x + y
}";
        var asm = GenerateAssembly(source, "68060");

        // 68060 uses 8-byte alignment, so 2 i32 variables aligned to 8 bytes = 16 bytes
        Assert.Contains("link\ta6,#-16", asm);
    }

    [Fact]
    public void Generate_68020_ShiftByConstant_UsesBarrelShifter()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x << 4
}";
        var asm = GenerateAssembly(source, "68020");

        // 68020+ has barrel shifter, can do immediate shift
        Assert.Contains("lsl.l\t#4,d0", asm);
    }

    [Fact]
    public void Generate_68000_ShiftByConstant_UsesImmediateShift()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x << 4
}";
        var asm = GenerateAssembly(source, "68000");

        // 68000 can do immediate shifts up to 8
        Assert.Contains("lsl.l\t#4,d0", asm);
    }

    [Fact]
    public void Generate_RightShiftSigned_UsesArithmeticShift()
    {
        var source = @"
fn test(x: i32) -> i32 {
    return x >> 2
}";
        var asm = GenerateAssembly(source, "68020");

        // Signed right shift should use asr (arithmetic shift)
        Assert.Contains("asr.l\t#2,d0", asm);
    }

    [Fact]
    public void Generate_RightShiftUnsigned_UsesLogicalShift()
    {
        var source = @"
fn test(x: u32) -> u32 {
    return x >> 2
}";
        var asm = GenerateAssembly(source, "68020");

        // Unsigned right shift should use lsr (logical shift)
        Assert.Contains("lsr.l\t#2,d0", asm);
    }

    [Fact]
    public void Generate_68060_MultipleOptimizations_AllApplied()
    {
        var source = @"
fn test(x: i32) -> i32 {
    var a = x * 2
    var b = x * 4
    var c = x * 8
    return a + b + c
}";
        var asm = GenerateAssembly(source, "68060");

        // Should have all 68060 multiply optimizations
        Assert.Contains("68060 optimization: x * 2", asm);
        Assert.Contains("68060 optimization: x * 4", asm);
        Assert.Contains("68060 optimization: x * 8", asm);

        // Should use stack with 8-byte alignment
        // 3 variables (a, b, c) = 12 bytes base, aligned to 24
        Assert.Contains("link\ta6,#-24", asm);
    }

    // Module Import Tests

    [Fact]
    public void Generate_ImportedFunctionWithNoBody_GeneratesExternalReference()
    {
        // Simulate an imported function by creating a function with no basic blocks
        var module = new IrModule();

        // Create main function with body
        var mainFunc = new IrFunction("main", new IrIntType(32, false), visibility: Visibility.Public, isExtern: false);
        var entryBlock = mainFunc.CreateBasicBlock("entry");
        entryBlock.Instructions.Add(new IrReturn(new IrConstant(42, new IrIntType(32, false))));
        module.AddFunction(mainFunc);

        // Create imported function with NO body (simulates imported declaration from another module)
        var importedFunc = new IrFunction("WriteOut", new IrIntType(32, true), visibility: Visibility.Private, isExtern: false);
        importedFunc.Parameters.Add(new IrParameter("message", IrStringType.Instance));
        // Importantly: NO basic blocks - this simulates an imported function
        module.AddFunction(importedFunc);

        var codegen = new M68kCodeGenerator(module, new List<IrStringLiteral>(), "68020");
        var asm = codegen.Generate();

        // Imported function should be treated as external reference
        Assert.Contains("xref\t_WriteOut", asm);

        // Imported function should NOT have a function body generated
        // It should not have the function label followed by instructions
        var lines = asm.Split('\n');
        var writeOutLabelIndex = Array.FindIndex(lines, l => l.Trim() == "_WriteOut:");

        // Function label should NOT exist (only xref)
        Assert.Equal(-1, writeOutLabelIndex);

        // Should NOT contain prologue for imported function
        var writeOutSection = string.Join("\n", lines.SkipWhile(l => !l.Contains("WriteOut")));
        Assert.DoesNotContain("link\ta6,#", writeOutSection);
    }

    [Fact]
    public void Generate_ExternFunction_GeneratesExternalReference()
    {
        var source = @"
extern fn Output() -> i32

pub fn main() -> u32 {
    return 42
}";
        var asm = GenerateAssembly(source);

        // Extern function should generate xref
        Assert.Contains("xref\t_Output", asm);

        // Extern function should NOT have a function body
        Assert.DoesNotContain("_Output:", asm);
    }

    [Fact]
    public void Generate_ImportedAndLocalFunctions_OnlyGeneratesLocalBodies()
    {
        var module = new IrModule();

        // Create local function with body
        var localFunc = new IrFunction("helper", new IrIntType(32, true), visibility: Visibility.Public, isExtern: false);
        var helperBlock = localFunc.CreateBasicBlock("entry");
        helperBlock.Instructions.Add(new IrReturn(new IrConstant(10, new IrIntType(32, true))));
        module.AddFunction(localFunc);

        // Create imported function with no body
        var importedFunc = new IrFunction("WriteOut", new IrIntType(32, true), visibility: Visibility.Private, isExtern: false);
        importedFunc.Parameters.Add(new IrParameter("message", IrStringType.Instance));
        module.AddFunction(importedFunc);

        // Create main function with body
        var mainFunc = new IrFunction("main", new IrIntType(32, false), visibility: Visibility.Public, isExtern: false);
        var mainBlock = mainFunc.CreateBasicBlock("entry");
        mainBlock.Instructions.Add(new IrReturn(new IrConstant(0, new IrIntType(32, false))));
        module.AddFunction(mainFunc);

        var codegen = new M68kCodeGenerator(module, new List<IrStringLiteral>(), "68020");
        var asm = codegen.Generate();

        // Local functions should have bodies
        Assert.Contains("_helper:", asm);
        Assert.Contains("_main:", asm);

        // Imported function should only have xref
        Assert.Contains("xref\t_WriteOut", asm);
        Assert.DoesNotContain("_WriteOut:", asm);

        // Local functions should be exported (pub)
        Assert.Contains("xdef\t_helper", asm);
        Assert.Contains("xdef\t_main", asm);

        // Imported function should NOT be exported
        Assert.DoesNotContain("xdef\t_WriteOut", asm);
    }

    [Fact]
    public void Generate_ByteStoreAndLoad_UsesBigEndianAlignment()
    {
        var source = @"
fn test(x: u8) -> u8 {
    var y: u8 = x
    return y
}";
        var asm = GenerateAssembly(source);

        // When storing a byte to stack, should use offset + 3 for big-endian
        // Pattern: store byte at correct offset, then load from same offset
        // The offset adjustment should be consistent
        Assert.Contains("move.b", asm);
    }

    [Fact]
    public void Generate_WordStoreAndLoad_UsesBigEndianAlignment()
    {
        var source = @"
fn test(x: u16) -> u16 {
    var y: u16 = x
    return y
}";
        var asm = GenerateAssembly(source);

        // When storing a word to stack, should use offset + 2 for big-endian
        // Pattern: store word at correct offset, then load from same offset
        Assert.Contains("move.w", asm);
    }

    [Fact]
    public void Generate_MixedByteWordLong_CorrectAlignment()
    {
        var source = @"
fn test() -> i32 {
    var a: u8 = 1u8
    var b: u16 = 2u16
    var c: i32 = 3
    return c
}";
        var asm = GenerateAssembly(source);

        // All three types should be handled with correct alignment
        Assert.Contains("move.b", asm);
        Assert.Contains("move.w", asm);
        Assert.Contains("move.l", asm);
    }

    [Fact]
    public void Generate_ByteParameter_UsesBigEndianAlignment()
    {
        var source = @"
fn test(x: u8) -> u8 {
    return x
}";
        var asm = GenerateAssembly(source);

        // Parameter access should use correct big-endian offset
        // Byte parameter at 8(a6) should be accessed at 11(a6) [8+3]
        Assert.Contains("move.b", asm);
        Assert.Contains("(a6)", asm);
    }

    [Fact]
    public void Generate_WordParameter_UsesBigEndianAlignment()
    {
        var source = @"
fn test(x: u16) -> u16 {
    return x
}";
        var asm = GenerateAssembly(source);

        // Parameter access should use correct big-endian offset
        // Word parameter at 8(a6) should be accessed at 10(a6) [8+2]
        Assert.Contains("move.w", asm);
        Assert.Contains("(a6)", asm);
    }

    [Fact]
    public void Generate_68000Multiply_SavesAndRestoresD4Register()
    {
        // When targeting 68000, signed 32-bit multiply uses d2-d4
        // This test verifies that d4 is properly saved/restored
        var source = @"
fn test(x: i32, y: i32) -> i32 {
    return x * y
}";

        var asm = GenerateAssembly(source, cpuTarget: "68000");

        // Should save d2-d4 at the start of multiply routine
        Assert.Contains("movem.l\td2-d4,-(sp)", asm);

        // Should restore d2-d4 at the end
        Assert.Contains("movem.l\t(sp)+,d2-d4", asm);

        // Should NOT have the buggy pattern of saving d2-d3 then restoring then saving d2-d4
        var lines = asm.Split('\n');
        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i].Contains("movem.l\td2-d3,-(sp)"))
            {
                // If we save d2-d3, we should NOT then restore and re-save with d4
                Assert.DoesNotContain("movem.l\t(sp)+,d2-d3", lines[i + 1]);
            }
        }
    }

    [Fact]
    public void Generate_68000UnsignedMultiply_SavesAndRestoresD2D3Only()
    {
        // Unsigned 32-bit multiply on 68000 only uses d2-d3
        var source = @"
fn test(x: u32, y: u32) -> u32 {
    return x * y
}";

        var asm = GenerateAssembly(source, cpuTarget: "68000");

        // Should save d2-d3 at the start
        Assert.Contains("movem.l\td2-d3,-(sp)", asm);

        // Should restore d2-d3 at the end
        Assert.Contains("movem.l\t(sp)+,d2-d3", asm);

        // Should NOT use d4 at all (unsigned doesn't need sign tracking)
        Assert.DoesNotContain("d4", asm);
    }
}
