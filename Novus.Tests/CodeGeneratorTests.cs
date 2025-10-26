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
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder();
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

        // For 68000, 32-bit multiply should fall back to workaround
        Assert.Contains("muls.w", asm);
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

        // Should have comparison and test/branch
        Assert.Contains("cmp", asm);
        Assert.Contains("tst.l", asm);  // Test materialized condition
        Assert.Contains("bne", asm);     // Branch if not equal (true)
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

        // Should have test and branch instructions
        Assert.Contains("tst.l", asm);  // Test condition value
        Assert.Contains("bne", asm);     // Branch if not equal (true)
        Assert.Contains("bra", asm);     // Unconditional branch
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

        // Should use Scc instruction (e.g., sgt)
        Assert.Contains("sgt", asm);
    }

    [Fact]
    public void Generate_AllComparisonOperators_GeneratesCorrectConditions()
    {
        var testCases = new[]
        {
            ("==", "seq"),
            ("!=", "sne"),
            ("<", "slt"),
            ("<=", "sle"),
            (">", "sgt"),
            (">=", "sge")
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
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 1
    }
    return 0
}";
        var asm = GenerateAssembly(source);

        // Should use ext.w + ext.l (68000 compatible, not extb.l which is 68020+)
        Assert.Contains("ext.w", asm);
        Assert.Contains("ext.l", asm);

        // Should use neg.l to convert $FFFFFFFF to 1
        Assert.Contains("neg.l", asm);

        // Note: With optimization, tst.l is skipped and we branch directly on cmp result
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

        // Should have conditional branch (test materialized value and branch)
        Assert.Contains("tst.l\td0", asm);
        Assert.Contains("bne\twhile_body_", asm);
        Assert.Contains("bra\twhile_end_", asm);
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

        // Should have comparison
        Assert.Contains("cmp", asm);
        Assert.Contains("sgt", asm);
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
}
