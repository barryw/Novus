using Novus.IR;
using Xunit;

namespace Novus.Tests;

public class IrValidatorTests
{
    [Fact]
    public void Validate_ValidModule_ReturnsNoErrors()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance);
        var block = function.CreateBasicBlock("entry");
        block.AddInstruction(new IrReturn());
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_UndeclaredVariable_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);
        var block = function.CreateBasicBlock("entry");

        // Use undeclared variable
        var undeclaredVar = new IrVariable("x", IrIntType.I32);
        block.AddInstruction(new IrReturn(undeclaredVar));
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("undeclared variable: x"));
    }

    [Fact]
    public void Validate_DeclaredVariable_NoError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);

        // Declare parameter
        function.Parameters.Add(new IrParameter("x", IrIntType.I32));

        var block = function.CreateBasicBlock("entry");
        var variable = new IrVariable("x", IrIntType.I32);
        block.AddInstruction(new IrReturn(variable));
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_BranchToUndefinedLabel_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance);
        var block = function.CreateBasicBlock("entry");

        // Branch to non-existent label
        block.AddInstruction(new IrBranch("nonexistent"));
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("undefined label: nonexistent"));
    }

    [Fact]
    public void Validate_BranchToValidLabel_NoError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance);
        var block1 = function.CreateBasicBlock("entry");
        var block2 = function.CreateBasicBlock("exit");

        block1.AddInstruction(new IrBranch("exit"));
        block2.AddInstruction(new IrReturn());
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ConditionalBranchToUndefinedLabel_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance);
        var block = function.CreateBasicBlock("entry");

        var condition = new IrBoolConstant(true);
        block.AddInstruction(new IrConditionalBranch(condition, "undefined1", "undefined2"));
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("undefined label"));
    }

    [Fact]
    public void Validate_DuplicateLabel_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance);
        var block1 = function.CreateBasicBlock("same");
        var block2 = function.CreateBasicBlock("same"); // Duplicate!

        block1.AddInstruction(new IrReturn());
        block2.AddInstruction(new IrReturn());
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicate label: same"));
    }

    [Fact]
    public void Validate_BlockWithoutTerminator_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance);
        var block = function.CreateBasicBlock("entry");

        // Add instruction but no terminator
        var local = new IrLocalVariable("x", IrIntType.I32, false);
        function.LocalVariables.Add(local);
        block.AddInstruction(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(42, IrIntType.I32)));
        // Missing return!

        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("does not end with a terminator"));
    }

    [Fact]
    public void Validate_InstructionAfterTerminator_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance);
        var block = function.CreateBasicBlock("entry");

        block.AddInstruction(new IrReturn());
        // Instruction after return - unreachable!
        block.AddInstruction(new IrLabel("dead_code"));

        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("instructions after terminator"));
    }

    [Fact]
    public void Validate_ReturnTypeMismatch_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance); // Returns void
        var block = function.CreateBasicBlock("entry");

        // But returns a value!
        block.AddInstruction(new IrReturn(new IrConstant(42, IrIntType.I32)));
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("returns void but has return value"));
    }

    [Fact]
    public void Validate_VoidReturnInNonVoidFunction_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32); // Returns i32
        var block = function.CreateBasicBlock("entry");

        // But returns nothing!
        block.AddInstruction(new IrReturn());
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("but return statement has no value"));
    }

    [Fact]
    public void Validate_BinaryOpWithUndeclaredOperand_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);
        var block = function.CreateBasicBlock("entry");

        var left = new IrVariable("x", IrIntType.I32); // Undeclared!
        var right = new IrConstant(10, IrIntType.I32);
        block.AddInstruction(new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add, left, right, IrIntType.I32));
        block.AddInstruction(new IrReturn(new IrVariable("%t0", IrIntType.I32)));
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("undeclared variable: x"));
    }

    [Fact]
    public void Validate_StoreToUndeclaredVariable_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance);
        var block = function.CreateBasicBlock("entry");

        // Store to undeclared variable
        block.AddInstruction(new IrStore("x", new IrConstant(42, IrIntType.I32)));
        block.AddInstruction(new IrReturn());
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Store to undeclared variable: x"));
    }

    [Fact]
    public void Validate_IndexAccessWithNonIntegerIndex_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);

        var arrayType = new IrArrayType(IrIntType.I32, 10);
        function.Parameters.Add(new IrParameter("arr", arrayType));

        var block = function.CreateBasicBlock("entry");
        var arrayVar = new IrVariable("arr", arrayType);
        var floatIndex = new IrFloatConstant(1.5, IrFloatType.F32); // Wrong type!

        block.AddInstruction(new IrIndexAccess("%t0", arrayVar, floatIndex, IrIntType.I32));
        block.AddInstruction(new IrReturn(new IrVariable("%t0", IrIntType.I32)));
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("non-integer type"));
    }

    [Fact]
    public void Validate_ExtractTagFromNonEnum_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);
        function.Parameters.Add(new IrParameter("x", IrIntType.I32));

        var block = function.CreateBasicBlock("entry");
        var intVar = new IrVariable("x", IrIntType.I32); // Not an enum!

        block.AddInstruction(new IrExtractTag("%tag", intVar));
        block.AddInstruction(new IrReturn(new IrVariable("%tag", IrIntType.I32)));
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Extract tag from non-enum type"));
    }

    [Fact]
    public void Validate_DereferenceOfNonPointer_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance);
        function.Parameters.Add(new IrParameter("x", IrIntType.I32));

        var block = function.CreateBasicBlock("entry");
        var intVar = new IrVariable("x", IrIntType.I32); // Not a pointer!
        var value = new IrConstant(42, IrIntType.I32);

        block.AddInstruction(new IrDereferenceStore(intVar, value));
        block.AddInstruction(new IrReturn());
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Dereference store from non-pointer type"));
    }

    [Fact]
    public void Validate_IndirectCallWithNonFunctionPointer_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrVoidType.Instance);
        function.Parameters.Add(new IrParameter("x", IrIntType.I32));

        var block = function.CreateBasicBlock("entry");
        var intVar = new IrVariable("x", IrIntType.I32); // Not a function pointer!

        block.AddInstruction(new IrIndirectCall(intVar, IrVoidType.Instance));
        block.AddInstruction(new IrReturn());
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Indirect call through non-function-pointer type"));
    }

    [Fact]
    public void Validate_MatchWithNoArms_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);

        var enumType = new IrEnumType("Option", new List<IrEnumVariant>
        {
            new IrEnumVariant("Some", 0, new List<IrType> { IrIntType.I32 }),
            new IrEnumVariant("None", 1)
        });

        function.Parameters.Add(new IrParameter("opt", enumType));

        var block = function.CreateBasicBlock("entry");
        var enumVar = new IrVariable("opt", enumType);

        // Match with no arms!
        block.AddInstruction(new IrMatch(enumVar, new List<IrMatchArm>()));
        block.AddInstruction(new IrReturn(new IrConstant(0, IrIntType.I32)));
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Match expression has no arms"));
    }

    [Fact]
    public void Validate_ComplexValidFunction_NoErrors()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);

        // Add parameter
        function.Parameters.Add(new IrParameter("x", IrIntType.I32));

        // Add local variable
        var local = new IrLocalVariable("result", IrIntType.I32, true);
        function.LocalVariables.Add(local);

        // Entry block
        var entry = function.CreateBasicBlock("entry");
        var thenBlock = function.CreateBasicBlock("then");
        var elseBlock = function.CreateBasicBlock("else");
        var exit = function.CreateBasicBlock("exit");

        // entry: if x > 0 goto then else goto else
        var xVar = new IrVariable("x", IrIntType.I32);
        var zero = new IrConstant(0, IrIntType.I32);
        entry.AddInstruction(new IrBinaryOp("%cond", IrBinaryOp.OpKind.Gt, xVar, zero, IrBoolType.Instance));
        entry.AddInstruction(new IrConditionalBranch(new IrVariable("%cond", IrBoolType.Instance), "then", "else"));

        // then: result = x * 2
        thenBlock.AddInstruction(new IrLocalDecl("result", IrIntType.I32, true, new IrConstant(0, IrIntType.I32)));
        var two = new IrConstant(2, IrIntType.I32);
        thenBlock.AddInstruction(new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, xVar, two, IrIntType.I32));
        thenBlock.AddInstruction(new IrStore("result", new IrVariable("%t0", IrIntType.I32)));
        thenBlock.AddInstruction(new IrBranch("exit"));

        // else: result = 0
        elseBlock.AddInstruction(new IrLocalDecl("result", IrIntType.I32, true, new IrConstant(0, IrIntType.I32)));
        elseBlock.AddInstruction(new IrStore("result", zero));
        elseBlock.AddInstruction(new IrBranch("exit"));

        // exit: return result
        exit.AddInstruction(new IrReturn(new IrVariable("result", IrIntType.I32)));

        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.True(result.IsValid, result.ToString());
    }

    [Fact]
    public void Validate_NullType_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", null!); // Null return type!
        var block = function.CreateBasicBlock("entry");
        block.AddInstruction(new IrReturn());
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("null return type"));
    }

    [Fact]
    public void Validate_EmptyVariableName_ReportsError()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);
        var block = function.CreateBasicBlock("entry");

        // Empty variable name
        block.AddInstruction(new IrStore("", new IrConstant(42, IrIntType.I32)));
        block.AddInstruction(new IrReturn(new IrConstant(0, IrIntType.I32)));
        module.AddFunction(function);

        var validator = new IrValidator();
        var result = validator.Validate(module);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }
}
