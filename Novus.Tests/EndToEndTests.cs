using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// End-to-end integration tests that verify the complete compilation pipeline
/// </summary>
public class EndToEndTests
{
    private string CompileToAssembly(string source, string cpuTarget = "68000", int optimizationLevel = 0)
    {
        // Parse
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        if (parser.NumberOfSyntaxErrors > 0)
        {
            throw new Exception($"Parse failed with {parser.NumberOfSyntaxErrors} errors");
        }

        // Build IR
        var builder = new IrBuilder();
        var module = builder.BuildModule(tree);

        // Run optimizer if requested
        if (optimizationLevel > 0)
        {
            var optimizer = Novus.Optimizer.OptimizationPipeline.CreatePipeline(optimizationLevel, verbose: false);
            optimizer.Run(module);
        }

        // Generate code
        var codegen = new M68kCodeGenerator(module, builder.StringLiterals, cpuTarget);
        return codegen.Generate();
    }

    [Fact]
    public void Compile_HelloWorld_Success()
    {
        var source = @"
// Simple Novus program that returns 42
fn main() -> u32 {
    return 42
}";
        var asm = CompileToAssembly(source);

        Assert.NotEmpty(asm);
        Assert.Contains("_main:", asm);
        Assert.Contains("rts", asm);
    }

    [Fact]
    public void Compile_SimpleArithmetic_Success()
    {
        var source = @"
fn main() -> u32 {
    return 10 + 20
}";
        var asm = CompileToAssembly(source);

        Assert.Contains("add.l", asm);
        Assert.Contains("rts", asm);
    }

    [Fact]
    public void Compile_ComplexExpression_Success()
    {
        var source = @"
fn calculate() -> u32 {
    return (10 + 20) * 2
}";
        var asm = CompileToAssembly(source);

        Assert.Contains("add.l", asm);
        Assert.Contains("mul", asm);
    }

    [Fact]
    public void Compile_MultipleFunctions_Success()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}

fn subtract(a: i32, b: i32) -> i32 {
    return a - b
}

fn main() -> u32 {
    return 42
}";
        var asm = CompileToAssembly(source);

        Assert.Contains("_add:", asm);
        Assert.Contains("_subtract:", asm);
        Assert.Contains("_main:", asm);
    }

    [Fact]
    public void Compile_DifferentCPUTargets_GeneratesDifferentCode()
    {
        var source = @"
fn test() -> u32 {
    return 100 * 200
}";

        var asm68000 = CompileToAssembly(source, "68000");
        var asm68020 = CompileToAssembly(source, "68020");

        // 68000 should use different multiply instruction than 68020
        Assert.NotEqual(asm68000, asm68020);
        Assert.Contains("68000", asm68000);
        Assert.Contains("68020", asm68020);
    }

    [Fact]
    public void Compile_SignedTypes_GeneratesCorrectInstructions()
    {
        var source = @"
fn signed_multiply() -> i32 {
    return 5 * 6
}";
        var asm = CompileToAssembly(source, "68020");

        Assert.Contains("muls", asm);
    }

    [Fact]
    public void Compile_UnsignedTypes_GeneratesCorrectInstructions()
    {
        var source = @"
fn unsigned_multiply() -> u32 {
    return 5u32 * 6u32
}";
        var asm = CompileToAssembly(source, "68020");

        Assert.Contains("mulu", asm);
    }

    [Fact]
    public void Compile_AllArithmeticOperators_Success()
    {
        var testCases = new[]
        {
            ("+", "add"),
            ("-", "sub"),
            ("*", "mul")
        };

        foreach (var (op, expectedInst) in testCases)
        {
            var source = $@"
fn test() -> u32 {{
    return 10 {op} 5
}}";
            var asm = CompileToAssembly(source, "68020");

            Assert.Contains(expectedInst, asm.ToLower());
        }
    }

    [Fact]
    public void Compile_WithComments_IgnoresComments()
    {
        var source = @"
// This is a line comment
/* This is a block comment */
fn main() -> u32 {
    // Return value
    return 42  // Inline comment
}";
        var asm = CompileToAssembly(source);

        // Should compile successfully, ignoring comments
        Assert.Contains("_main:", asm);
    }

    [Fact]
    public void Compile_NestedExpressions_CorrectOperatorPrecedence()
    {
        var source = @"
fn test() -> u32 {
    return 2 + 3 * 4
}";
        var asm = CompileToAssembly(source);

        // Should multiply first, then add
        var lines = asm.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        var mulIndex = Array.FindIndex(lines, l => l.Contains("\tmul"));
        var addIndex = Array.FindIndex(lines, l => l.Contains("\tadd"));

        Assert.True(mulIndex < addIndex, "Multiplication should come before addition");
    }

    [Fact]
    public void Compile_ValidVasmSyntax_NoSyntaxErrors()
    {
        var source = @"
pub fn main() -> u32 {
    return 42
}";
        var asm = CompileToAssembly(source);

        // Check for proper vasm syntax elements
        Assert.Contains("section\t", asm);  // Tab-separated
        Assert.Contains("xdef\t", asm);     // Tab-separated
        Assert.DoesNotContain(".global", asm); // Should use xdef, not .global
        Assert.DoesNotContain(".text", asm);   // Should use section, not .text
    }

    [Fact]
    public void Compile_AmigaABICompliance_ReturnInD0()
    {
        var source = @"
fn main() -> u32 {
    return 42
}";
        var asm = CompileToAssembly(source);

        // Verify d0 is used for return value (Amiga ABI)
        var lines = asm.Split('\n');
        var hasD0Move = lines.Any(l =>
            l.Contains(",d0") &&
            !l.Trim().StartsWith(";") &&
            l.Contains("move"));

        Assert.True(hasD0Move, "Should move return value to d0");
    }

    [Fact]
    public void Compile_MoveqOptimization_AppliedForSmallConstants()
    {
        var testCases = new[] { -128, -1, 0, 1, 10, 42, 127 };

        foreach (var value in testCases)
        {
            var source = $@"
fn test() -> i32 {{
    return {value}
}}";
            var asm = CompileToAssembly(source);

            Assert.Contains($"moveq\t#{value},d0", asm);
        }
    }

    [Fact]
    public void Compile_MoveqOptimization_NotAppliedForLargeConstants()
    {
        var testCases = new[] { -129, 128, 200, 1000 };

        foreach (var value in testCases)
        {
            var source = $@"
fn test() -> i32 {{
    return {value}
}}";
            var asm = CompileToAssembly(source);

            Assert.DoesNotContain("moveq", asm);
            Assert.Contains("move.l", asm);
        }
    }

    [Fact]
    public void Compile_TypeSizes_GenerateCorrectSuffixes()
    {
        // Test different type sizes
        var source = @"
fn test8() -> u8 {
    return 42u8
}

fn test16() -> u16 {
    return 42u16
}

fn test32() -> u32 {
    return 42u32
}";
        var asm = CompileToAssembly(source);

        // Should successfully compile all type sizes
        Assert.Contains("_test8:", asm);
        Assert.Contains("_test16:", asm);
        Assert.Contains("_test32:", asm);
    }

    [Fact]
    public void Compile_FunctionParameters_InSymbolTable()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a
}";
        var asm = CompileToAssembly(source);

        // Should compile successfully with parameters
        Assert.Contains("_add:", asm);
    }

    [Fact]
    public void Compile_LetStatement_Success()
    {
        var source = @"
fn test() -> u32 {
    let x = 42
    return x
}";
        var asm = CompileToAssembly(source);

        // Should compile (implementation details may vary)
        Assert.Contains("_test:", asm);
    }

    [Theory]
    [InlineData("68000")]
    [InlineData("68010")]
    [InlineData("68020")]
    [InlineData("68030")]
    [InlineData("68040")]
    [InlineData("68060")]
    public void Compile_AllCPUTargets_Success(string cpuTarget)
    {
        var source = @"
fn main() -> u32 {
    return 42
}";
        var asm = CompileToAssembly(source, cpuTarget);

        Assert.NotEmpty(asm);
        Assert.Contains(cpuTarget.ToUpper(), asm);
    }

    [Fact]
    public void Compile_InvalidSyntax_ThrowsException()
    {
        var source = @"
fn test() -> u32 {
    return
}";

        Assert.Throws<Exception>(() => CompileToAssembly(source));
    }

    [Fact]
    public void Compile_EmptyProgram_Success()
    {
        var source = "";

        // Empty program should compile (with no functions)
        var asm = CompileToAssembly(source);

        Assert.NotEmpty(asm);
        Assert.Contains("section\ttext,code", asm);
    }

    [Fact]
    public void Compile_LargeExpression_Success()
    {
        var source = @"
fn complex() -> u32 {
    return ((10 + 5) * (20 - 3)) / 2
}";
        var asm = CompileToAssembly(source);

        // Should handle complex nested expressions
        Assert.Contains("add", asm);
        Assert.Contains("sub", asm);
        Assert.Contains("mul", asm);
    }

    [Fact]
    public void Compile_SimpleFunctionCall_Success()
    {
        var source = @"
pub fn main() -> u32 {
    return helper()
}

pub fn helper() -> u32 {
    return 42
}";
        var asm = CompileToAssembly(source);

        // Should generate both functions
        Assert.Contains("_main:", asm);
        Assert.Contains("_helper:", asm);
        // Should call helper from main
        Assert.Contains("jsr\t_helper", asm);
    }

    [Fact]
    public void Compile_FunctionCallWithArguments_Success()
    {
        var source = @"
pub fn main() -> u32 {
    return add(10, 32)
}

pub fn add(a: u32, b: u32) -> u32 {
    return a + b
}";
        var asm = CompileToAssembly(source);

        // Should generate call with stack arguments
        Assert.Contains("jsr\t_add", asm);
        Assert.Contains("lea\t8(sp),sp", asm); // Clean up 2 arguments
        // add should load from stack
        Assert.Contains("8(a6)", asm);
        Assert.Contains("12(a6)", asm);
    }

    [Fact]
    public void Compile_RecursiveFunctionStructure_Success()
    {
        var source = @"
pub fn main() -> u32 {
    return fibonacci(5)
}

pub fn fibonacci(n: u32) -> u32 {
    if n == 0 {
        return 0
    }
    if n == 1 {
        return 1
    }
    return fibonacci(n - 1) + fibonacci(n - 2)
}";
        var asm = CompileToAssembly(source);

        // Should generate recursive calls
        Assert.Contains("jsr\t_fibonacci", asm);
        // Should have branching logic (test and branch)
        Assert.Contains("tst.l", asm);
        Assert.Contains("bne", asm);
    }

    [Fact]
    public void Compile_MultipleHelperFunctions_Success()
    {
        var source = @"
pub fn main() -> u32 {
    return compute(10)
}

pub fn compute(x: u32) -> u32 {
    return add(double(x), triple(x))
}

pub fn double(x: u32) -> u32 {
    return x + x
}

pub fn triple(x: u32) -> u32 {
    return x + x + x
}

pub fn add(a: u32, b: u32) -> u32 {
    return a + b
}";
        var asm = CompileToAssembly(source);

        // Should generate all functions
        Assert.Contains("_main:", asm);
        Assert.Contains("_compute:", asm);
        Assert.Contains("_double:", asm);
        Assert.Contains("_triple:", asm);
        Assert.Contains("_add:", asm);
        // Should have multiple JSR calls
        var jsrCount = asm.Split("jsr").Length - 1;
        Assert.True(jsrCount >= 3); // At least 3 calls from compute
    }

    [Fact]
    public void Optimize_ConstantFolding_ReducesInstructions()
    {
        var source = @"
pub fn main() -> u32 {
    return (10 + 20) + (30 + 40)
}";
        var noOpt = CompileToAssembly(source, optimizationLevel: 0);
        var withOpt = CompileToAssembly(source, optimizationLevel: 2);

        // Without optimization: should have multiple add instructions
        Assert.Contains("add.l", noOpt);

        // With optimization: should be constant folded to 100
        Assert.Contains("moveq\t#100,d0", withOpt);
        Assert.DoesNotContain("add.l", withOpt);

        // Optimized version should be significantly shorter
        var noOptLines = noOpt.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith(";")).Count();
        var withOptLines = withOpt.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith(";")).Count();
        Assert.True(withOptLines < noOptLines, $"Optimized version ({withOptLines} lines) should be shorter than unoptimized ({noOptLines} lines)");
    }

    [Fact]
    public void Optimize_Level0_NoOptimization()
    {
        var source = @"
pub fn main() -> u32 {
    return 5 + 5
}";
        var asm = CompileToAssembly(source, optimizationLevel: 0);

        // Should have the add instruction (not folded)
        Assert.Contains("add.l", asm);
    }

    [Fact]
    public void Optimize_Level1_BasicOptimizations()
    {
        var source = @"
pub fn main() -> u32 {
    return 5 + 5
}";
        var asm = CompileToAssembly(source, optimizationLevel: 1);

        // Should be constant folded
        Assert.Contains("moveq\t#10,d0", asm);
        Assert.DoesNotContain("add.l", asm);
    }

    [Fact]
    public void Optimize_Level2_AggressiveOptimizations()
    {
        var source = @"
pub fn main() -> u32 {
    return ((1 + 2) * 3) + ((4 + 5) * 6)
}";
        var asm = CompileToAssembly(source, optimizationLevel: 2);

        // Should be constant folded to: ((3) * 3) + ((9) * 6) = 9 + 54 = 63
        Assert.Contains("moveq\t#63,d0", asm);
        Assert.DoesNotContain("\tadd.l", asm);
        Assert.DoesNotContain("\tmul", asm);
    }

    [Fact]
    public void Optimize_ComparesOptimizationLevels()
    {
        var source = @"
pub fn test() -> u32 {
    return (2 + 3) + (4 + 5) + (6 + 7) + (8 + 9)
}";
        var opt0 = CompileToAssembly(source, optimizationLevel: 0);
        var opt1 = CompileToAssembly(source, optimizationLevel: 1);
        var opt2 = CompileToAssembly(source, optimizationLevel: 2);

        // Count non-comment, non-empty lines
        int CountInstructions(string asm) => asm.Split('\n')
            .Count(l => !string.IsNullOrWhiteSpace(l) &&
                        !l.TrimStart().StartsWith(";") &&
                        !l.Contains("section") &&
                        !l.Contains("xdef") &&
                        !l.Contains(":"));

        var count0 = CountInstructions(opt0);
        var count1 = CountInstructions(opt1);
        var count2 = CountInstructions(opt2);

        // Higher optimization levels should produce fewer instructions
        Assert.True(count1 <= count0, $"O1 ({count1}) should have <= instructions than O0 ({count0})");
        Assert.True(count2 <= count1, $"O2 ({count2}) should have <= instructions than O1 ({count1})");

        // O2 should be significantly better than O0
        Assert.True(count2 < count0 / 2, $"O2 ({count2}) should be at least 50% smaller than O0 ({count0})");
    }

    [Fact]
    public void Optimize_WithFunctionCalls_PreservesCorrectness()
    {
        var source = @"
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}

pub fn main() -> u32 {
    return (i32)add(5, 10)
}";
        var noOpt = CompileToAssembly(source, optimizationLevel: 0);
        var withOpt = CompileToAssembly(source, optimizationLevel: 2);

        // Both should call the add function (shouldn't inline or remove it)
        Assert.Contains("jsr\t_add", noOpt);
        Assert.Contains("jsr\t_add", withOpt);

        // Both should have the add function
        Assert.Contains("_add:", noOpt);
        Assert.Contains("_add:", withOpt);
    }

    [Fact]
    public void Optimize_ConstantPropagation_WorksAcrossOperations()
    {
        var source = @"
pub fn main() -> u32 {
    return (7 * 3) + (12 - 5)
}";
        var noOpt = CompileToAssembly(source, optimizationLevel: 0);
        var withOpt = CompileToAssembly(source, optimizationLevel: 2);

        // Without optimization: should have mul, add, and sub instructions
        Assert.Contains("\tmul", noOpt);
        Assert.Contains("\tsub.l", noOpt);
        Assert.Contains("\tadd.l", noOpt);

        // With optimization: should be constant folded to 28
        Assert.Contains("moveq\t#28,d0", withOpt);
        Assert.DoesNotContain("\tmul", withOpt);
        Assert.DoesNotContain("\tadd.l", withOpt);
        Assert.DoesNotContain("\tsub.l", withOpt);
    }
}
