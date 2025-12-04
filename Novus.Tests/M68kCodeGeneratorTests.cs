using Novus.Codegen.M68k;
using Novus.IR;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for direct M68k assembly code generation backend
/// </summary>
public class M68kCodeGeneratorTests
{
    [Fact]
    public void TestSimpleFunction()
    {
        // Create a simple function: fn test() -> i32 { return 42; }
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32, Visibility.Public);

        var entryBlock = function.CreateBasicBlock("entry");
        entryBlock.AddInstruction(new IrReturn(new IrConstant(42, IrIntType.I32)));

        module.AddFunction(function);

        // Generate M68k assembly
        var codegen = new M68kCodeGenerator(module, new List<IrStringLiteral>(), "68020");
        var asm = codegen.Generate();

        // Verify output contains expected elements
        Assert.Contains("test:", asm);
        Assert.Contains("link", asm);
        Assert.Contains("unlk", asm);
        Assert.Contains("rts", asm);
        Assert.Contains("moveq", asm); // Loading 42
    }

    [Fact]
    public void TestArithmeticOperations()
    {
        // Create function: fn add(a: i32, b: i32) -> i32 { return a + b; }
        var module = new IrModule();
        var function = new IrFunction("add", IrIntType.I32, Visibility.Public);

        var paramA = new IrParameter("a", IrIntType.I32);
        var paramB = new IrParameter("b", IrIntType.I32);
        function.Parameters.Add(paramA);
        function.Parameters.Add(paramB);

        var entryBlock = function.CreateBasicBlock("entry");

        // result = a + b
        var varA = new IrVariable("a", IrIntType.I32);
        var varB = new IrVariable("b", IrIntType.I32);
        var addOp = new IrBinaryOp("result", IrBinaryOp.OpKind.Add, varA, varB, IrIntType.I32);
        entryBlock.AddInstruction(addOp);

        // return result
        var result = new IrVariable("result", IrIntType.I32);
        entryBlock.AddInstruction(new IrReturn(result));

        module.AddFunction(function);

        // Generate M68k assembly
        var codegen = new M68kCodeGenerator(module, new List<IrStringLiteral>(), "68020");
        var asm = codegen.Generate();

        // Verify output contains addition instruction
        Assert.Contains("add", asm);
        Assert.Contains("d0", asm); // Result register
    }

    [Fact]
    public void TestConditionalBranch()
    {
        // Create function with if-else:
        // fn test(x: i32) -> i32 {
        //     if (x > 0) {
        //         return 1;
        //     } else {
        //         return 0;
        //     }
        // }
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32, Visibility.Public);

        var paramX = new IrParameter("x", IrIntType.I32);
        function.Parameters.Add(paramX);

        var entryBlock = function.CreateBasicBlock("entry");
        var trueBlock = function.CreateBasicBlock("true_branch");
        var falseBlock = function.CreateBasicBlock("false_branch");

        // Compare x > 0
        var varX = new IrVariable("x", IrIntType.I32);
        var zero = new IrConstant(0, IrIntType.I32);
        var cmp = new IrBinaryOp("cond", IrBinaryOp.OpKind.Gt, varX, zero, IrBoolType.Instance);
        entryBlock.AddInstruction(cmp);

        // Conditional branch
        var condVar = new IrVariable("cond", IrBoolType.Instance);
        entryBlock.AddInstruction(new IrConditionalBranch(condVar, "true_branch", "false_branch"));

        // True branch: return 1
        trueBlock.AddInstruction(new IrReturn(new IrConstant(1, IrIntType.I32)));

        // False branch: return 0
        falseBlock.AddInstruction(new IrReturn(new IrConstant(0, IrIntType.I32)));

        module.AddFunction(function);

        // Generate M68k assembly
        var codegen = new M68kCodeGenerator(module, new List<IrStringLiteral>(), "68020");
        var asm = codegen.Generate();

        // Verify output contains comparison and branch instructions
        Assert.Contains("cmp", asm);
        Assert.Contains("bne", asm); // Branch if not equal
        Assert.Contains("true_branch:", asm);
        Assert.Contains("false_branch:", asm);
    }

    [Fact]
    public void TestStringLiteral()
    {
        // Create function that uses a string literal
        var module = new IrModule();
        var function = new IrFunction("get_message", new IrPointerType(IrIntType.U8), Visibility.Public);

        var entryBlock = function.CreateBasicBlock("entry");

        var stringLiterals = new List<IrStringLiteral>();
        var strLit = new IrStringLiteral("Hello, Amiga!", "str_0");
        stringLiterals.Add(strLit);

        entryBlock.AddInstruction(new IrReturn(strLit));

        module.AddFunction(function);

        // Generate M68k assembly
        var codegen = new M68kCodeGenerator(module, stringLiterals, "68020");
        var asm = codegen.Generate();

        // Verify output contains string data section
        Assert.Contains("SECTION data,DATA", asm);
        Assert.Contains("str_0:", asm);
        // Verify string data is emitted as DC.B with individual ASCII characters
        Assert.Contains("DC.B", asm); // String data should have DC.B directive
        Assert.Contains("'H'", asm); // First character
        Assert.Contains("'!'", asm); // Last character before null terminator
        Assert.Contains("lea", asm); // Load effective address
    }

    [Fact]
    public void TestMultipleFunctions()
    {
        // Test generating multiple functions
        var module = new IrModule();

        // Function 1: fn foo() -> i32 { return 1; }
        var foo = new IrFunction("foo", IrIntType.I32, Visibility.Public);
        var fooBlock = foo.CreateBasicBlock("entry");
        fooBlock.AddInstruction(new IrReturn(new IrConstant(1, IrIntType.I32)));
        module.AddFunction(foo);

        // Function 2: fn bar() -> i32 { return 2; }
        var bar = new IrFunction("bar", IrIntType.I32, Visibility.Public);
        var barBlock = bar.CreateBasicBlock("entry");
        barBlock.AddInstruction(new IrReturn(new IrConstant(2, IrIntType.I32)));
        module.AddFunction(bar);

        // Generate M68k assembly
        var codegen = new M68kCodeGenerator(module, new List<IrStringLiteral>(), "68020");
        var asm = codegen.Generate();

        // Verify both functions are present
        Assert.Contains("foo:", asm);
        Assert.Contains("bar:", asm);
        Assert.Contains("XDEF      foo", asm); // Public export
        Assert.Contains("XDEF      bar", asm);
    }

    [Fact]
    public void TestCpuDirective()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance, Visibility.Public);
        var entryBlock = function.CreateBasicBlock("entry");
        entryBlock.AddInstruction(new IrReturn(null));
        module.AddFunction(function);

        // Test different CPU targets
        var targets = new[] { "68000", "68020", "68040", "68060" };

        foreach (var target in targets)
        {
            var codegen = new M68kCodeGenerator(module, new List<IrStringLiteral>(), target);
            var asm = codegen.Generate();

            // Verify CPU directive is present
            Assert.Contains($"CPU {target}", asm);
        }
    }
}
