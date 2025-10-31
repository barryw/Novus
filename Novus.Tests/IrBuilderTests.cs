using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class IrBuilderTests
{
    private IrModule BuildIr(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder();
        return builder.BuildModule(tree);
    }

    [Fact]
    public void BuildIr_SimpleReturn_CreatesFunction()
    {
        var source = @"
fn main() -> u32 {
    return 42
}";
        var module = BuildIr(source);

        Assert.Single(module.Functions);
        var func = module.Functions[0];
        Assert.Equal("main", func.Name);
        Assert.IsType<IrIntType>(func.ReturnType);
        Assert.Equal("u32", func.ReturnType.Name);
    }

    [Fact]
    public void BuildIr_FunctionWithParameters_CreatesParameters()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a
}";
        var module = BuildIr(source);

        var func = module.Functions[0];
        Assert.Equal(2, func.Parameters.Count);
        Assert.Equal("a", func.Parameters[0].Name);
        Assert.Equal("b", func.Parameters[1].Name);
        Assert.Equal("i32", func.Parameters[0].Type.Name);
        Assert.Equal("i32", func.Parameters[1].Type.Name);
    }

    [Fact]
    public void BuildIr_ReturnConstant_CreatesReturnInstruction()
    {
        var source = @"
fn test() -> u32 {
    return 42
}";
        var module = BuildIr(source);

        var func = module.Functions[0];
        Assert.Single(func.BasicBlocks);
        var block = func.BasicBlocks[0];
        Assert.Single(block.Instructions);

        var returnInst = Assert.IsType<IrReturn>(block.Instructions[0]);
        Assert.NotNull(returnInst.Value);
        var constant = Assert.IsType<IrConstant>(returnInst.Value);
        Assert.Equal(42, constant.Value);
    }

    [Fact]
    public void BuildIr_IntegerLiteralTypes_CreatesCorrectTypes()
    {
        var testCases = new[]
        {
            ("42u8", typeof(IrIntType), 8, false, 42L),
            ("42u16", typeof(IrIntType), 16, false, 42L),
            ("42u32", typeof(IrIntType), 32, false, 42L),
            ("42i8", typeof(IrIntType), 8, true, 42L),
            ("42i16", typeof(IrIntType), 16, true, 42L),
            ("42i32", typeof(IrIntType), 32, true, 42L)
        };

        foreach (var (literal, expectedType, bitWidth, isSigned, value) in testCases)
        {
            var source = $@"
fn test() -> i32 {{
    return {literal}
}}";
            var module = BuildIr(source);
            var func = module.Functions[0];
            var returnInst = (IrReturn)func.BasicBlocks[0].Instructions[0];
            var constant = Assert.IsType<IrConstant>(returnInst.Value);

            Assert.Equal(value, constant.Value);
            var intType = Assert.IsType<IrIntType>(constant.Type);
            Assert.Equal(bitWidth, intType.BitWidth);
            Assert.Equal(isSigned, intType.IsSigned);
        }
    }

    [Fact]
    public void BuildIr_Addition_CreatesBinaryOp()
    {
        var source = @"
fn test() -> u32 {
    return 10 + 20
}";
        var module = BuildIr(source);

        var func = module.Functions[0];
        var block = func.BasicBlocks[0];

        // Should have: BinaryOp (add) and Return
        Assert.Equal(2, block.Instructions.Count);

        var binOp = Assert.IsType<IrBinaryOp>(block.Instructions[0]);
        Assert.Equal(IrBinaryOp.OpKind.Add, binOp.Operation);

        var left = Assert.IsType<IrConstant>(binOp.Left);
        Assert.Equal(10, left.Value);

        var right = Assert.IsType<IrConstant>(binOp.Right);
        Assert.Equal(20, right.Value);
    }

    [Fact]
    public void BuildIr_Subtraction_CreatesBinaryOp()
    {
        var source = @"
fn test() -> u32 {
    return 30 - 10
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var binOp = Assert.IsType<IrBinaryOp>(block.Instructions[0]);
        Assert.Equal(IrBinaryOp.OpKind.Sub, binOp.Operation);
    }

    [Fact]
    public void BuildIr_Multiplication_CreatesBinaryOp()
    {
        var source = @"
fn test() -> u32 {
    return 5 * 6
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var binOp = Assert.IsType<IrBinaryOp>(block.Instructions[0]);
        Assert.Equal(IrBinaryOp.OpKind.Mul, binOp.Operation);
    }

    [Fact]
    public void BuildIr_Division_CreatesBinaryOp()
    {
        var source = @"
fn test() -> u32 {
    return 20 / 4
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var binOp = Assert.IsType<IrBinaryOp>(block.Instructions[0]);
        Assert.Equal(IrBinaryOp.OpKind.Div, binOp.Operation);
    }

    [Fact]
    public void BuildIr_Modulo_CreatesBinaryOp()
    {
        var source = @"
fn test() -> u32 {
    return 20 % 3
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var binOp = Assert.IsType<IrBinaryOp>(block.Instructions[0]);
        Assert.Equal(IrBinaryOp.OpKind.Mod, binOp.Operation);
    }

    [Fact]
    public void BuildIr_ChainedAddition_CreatesMultipleBinaryOps()
    {
        var source = @"
fn test() -> u32 {
    return (10 + 20) * 2
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];

        // Should have: Add, Mul, Return
        Assert.Equal(3, block.Instructions.Count);

        var add = Assert.IsType<IrBinaryOp>(block.Instructions[0]);
        Assert.Equal(IrBinaryOp.OpKind.Add, add.Operation);

        var mul = Assert.IsType<IrBinaryOp>(block.Instructions[1]);
        Assert.Equal(IrBinaryOp.OpKind.Mul, mul.Operation);

        // The multiply should reference the result of the add
        var mulLeft = Assert.IsType<IrVariable>(mul.Left);
        Assert.StartsWith("%t", mulLeft.Name); // Temporary variable
    }

    [Fact]
    public void BuildIr_VoidReturnType_CreatesVoidType()
    {
        var source = @"
fn test() {
    return 42
}";
        var module = BuildIr(source);

        var func = module.Functions[0];
        Assert.IsType<IrVoidType>(func.ReturnType);
    }

    [Fact]
    public void BuildIr_MultipleFunctions_CreatesAllFunctions()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}

fn multiply(a: i32, b: i32) -> i32 {
    return a * b
}

fn main() -> u32 {
    return 42
}";
        var module = BuildIr(source);

        Assert.Equal(3, module.Functions.Count);
        Assert.Equal("add", module.Functions[0].Name);
        Assert.Equal("multiply", module.Functions[1].Name);
        Assert.Equal("main", module.Functions[2].Name);
    }

    [Fact]
    public void BuildIr_TemporaryVariables_HaveUniqueNames()
    {
        var source = @"
fn test() -> u32 {
    return (10 + 20) + (30 + 40)
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var temps = block.Instructions
            .OfType<IrBinaryOp>()
            .Select(op => op.ResultName)
            .ToList();

        // Should have unique temporary names
        Assert.Equal(temps.Count, temps.Distinct().Count());
    }

    [Fact]
    public void BuildIr_UnderscoredLiteral_ParsesCorrectValue()
    {
        var source = @"
fn test() -> u32 {
    return 1_000_000
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(1000000, constant.Value);
    }

    [Fact]
    public void BuildIr_UnderscoredLiteralWithType_ParsesCorrectValueAndType()
    {
        var source = @"
fn test() -> u32 {
    return 1_234_567u32
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(1234567, constant.Value);
        Assert.Equal(IrIntType.U32, constant.Type);
    }

    [Fact]
    public void BuildIr_MultipleUnderscoresInLiteral_ParsesCorrectly()
    {
        var source = @"
fn test() -> i32 {
    return 1_2_3_4_5
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(12345, constant.Value);
    }

    [Fact]
    public void BuildIr_BinaryLiteral_ParsesCorrectValue()
    {
        var source = @"
fn test() -> u8 {
    return %11111111
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(255, constant.Value);
    }

    [Fact]
    public void BuildIr_HexLiteral_ParsesCorrectValue()
    {
        var source = @"
fn test() -> u8 {
    return $FF
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(255, constant.Value);
    }

    [Fact]
    public void BuildIr_BinaryLiteralWithType_ParsesCorrectValueAndType()
    {
        var source = @"
fn test() -> u16 {
    return %1111_1111_0000_0000u16
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(0xFF00, constant.Value);
        Assert.Equal(IrIntType.U16, constant.Type);
    }

    [Fact]
    public void BuildIr_HexLiteralWithType_ParsesCorrectValueAndType()
    {
        var source = @"
fn test() -> u32 {
    return $DEAD_BEEFu32
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(0xDEADBEEF, constant.Value);
        Assert.Equal(IrIntType.U32, constant.Type);
    }

    [Fact]
    public void BuildIr_NegativeBinaryLiteral_ParsesCorrectValue()
    {
        var source = @"
fn test() -> i32 {
    return -%1010
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(-10, constant.Value);
    }

    [Fact]
    public void BuildIr_NegativeHexLiteral_ParsesCorrectValue()
    {
        var source = @"
fn test() -> i32 {
    return -$FF
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(-255, constant.Value);
    }

    [Fact]
    public void BuildIr_CastExpression_ChangesType()
    {
        var source = @"
fn test() -> u16 {
    return (u16)$DEAD_BEEF
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(0xDEADBEEF, constant.Value);
        Assert.Equal(IrIntType.U16, constant.Type);
    }

    [Fact]
    public void BuildIr_CastDecimal_ChangesType()
    {
        var source = @"
fn test() -> u8 {
    return (u8)255
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(255, constant.Value);
        Assert.Equal(IrIntType.U8, constant.Type);
    }

    [Fact]
    public void BuildIr_NestedCast_AppliesInnerCast()
    {
        var source = @"
fn test() -> u8 {
    return (u8)((u16)1000)
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(1000, constant.Value);
        Assert.Equal(IrIntType.U8, constant.Type);
    }

    [Fact]
    public void BuildIr_EntryBlock_HasCorrectLabel()
    {
        var source = @"
fn test() -> u32 {
    return 42
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        Assert.Equal("entry", block.Label);
    }

    // Control Flow IR Tests

    [Fact]
    public void BuildIr_IfStatement_CreatesConditionalBranch()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 100
    }
    return 0
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var instructions = block.Instructions;

        // Should have: comparison, conditional branch, labels
        Assert.Contains(instructions, i => i is IrBinaryOp binOp && binOp.Operation == IrBinaryOp.OpKind.Gt);
        Assert.Contains(instructions, i => i is IrConditionalBranch);
        Assert.Contains(instructions, i => i is IrLabel label && label.Name.StartsWith("if_then_"));
        Assert.Contains(instructions, i => i is IrLabel label && label.Name.StartsWith("if_end_"));
    }

    [Fact]
    public void BuildIr_IfElseStatement_CreatesLabelsAndBranches()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 100
    } else {
        return 200
    }
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var instructions = block.Instructions;

        // Should have: comparison, conditional branch, then label, else label, end label
        Assert.Contains(instructions, i => i is IrConditionalBranch);
        Assert.Contains(instructions, i => i is IrLabel label && label.Name.StartsWith("if_then_"));
        Assert.Contains(instructions, i => i is IrLabel label && label.Name.StartsWith("if_else_"));
        Assert.Contains(instructions, i => i is IrLabel label && label.Name.StartsWith("if_end_"));

        // Note: No branch from then to end because both blocks return directly (dead code elimination)
    }

    [Fact]
    public void BuildIr_ComparisonExpression_CreatesBinaryOp()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x == 10 {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var comparison = block.Instructions.OfType<IrBinaryOp>().FirstOrDefault();

        Assert.NotNull(comparison);
        Assert.Equal(IrBinaryOp.OpKind.Eq, comparison.Operation);
    }

    [Fact]
    public void BuildIr_AllComparisonOperators_CreatesCorrectOps()
    {
        var testCases = new[]
        {
            ("==", IrBinaryOp.OpKind.Eq),
            ("!=", IrBinaryOp.OpKind.Ne),
            ("<", IrBinaryOp.OpKind.Lt),
            ("<=", IrBinaryOp.OpKind.Le),
            (">", IrBinaryOp.OpKind.Gt),
            (">=", IrBinaryOp.OpKind.Ge)
        };

        foreach (var (op, expectedKind) in testCases)
        {
            var source = $@"
fn test(x: i32) -> i32 {{
    if x {op} 10 {{
        return 1
    }}
    return 0
}}";
            var module = BuildIr(source);
            var block = module.Functions[0].BasicBlocks[0];
            var comparison = block.Instructions.OfType<IrBinaryOp>().FirstOrDefault();

            Assert.NotNull(comparison);
            Assert.Equal(expectedKind, comparison.Operation);
        }
    }

    [Fact]
    public void BuildIr_WhileStatement_CreatesLoopStructure()
    {
        var source = @"
fn test() -> i32 {
    while 1 == 1 {
        return 42
    }
    return 0
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var instructions = block.Instructions;

        // Should have: cond label, condition check, conditional branch, body label, branch back to cond, end label
        Assert.Contains(instructions, i => i is IrBranch branch && branch.Target.StartsWith("while_cond_"));
        Assert.Contains(instructions, i => i is IrLabel label && label.Name.StartsWith("while_cond_"));
        Assert.Contains(instructions, i => i is IrLabel label && label.Name.StartsWith("while_body_"));
        Assert.Contains(instructions, i => i is IrLabel label && label.Name.StartsWith("while_end_"));
        Assert.Contains(instructions, i => i is IrConditionalBranch);
    }

    [Fact]
    public void BuildIr_ForeverStatement_CreatesInfiniteLoop()
    {
        var source = @"
fn test() -> i32 {
    forever {
        break
    }
    return 42
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var instructions = block.Instructions;

        // Should have: body label, loop body, branch back to body, end label
        Assert.Contains(instructions, i => i is IrLabel label && label.Name.StartsWith("forever_body_"));
        Assert.Contains(instructions, i => i is IrLabel label && label.Name.StartsWith("forever_end_"));

        // Should have at least one branch instruction for the loop
        Assert.Contains(instructions, i => i is IrBranch);
    }

    [Fact]
    public void BuildIr_BreakStatement_CreatesBranchToEnd()
    {
        var source = @"
fn test() -> i32 {
    forever {
        break
    }
    return 42
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var instructions = block.Instructions;

        // Break should create a branch to the loop end label
        var breakBranch = instructions.OfType<IrBranch>()
            .FirstOrDefault(b => b.Target.StartsWith("forever_end_"));
        Assert.NotNull(breakBranch);
    }

    [Fact]
    public void BuildIr_NestedIf_CreatesUniqueLabels()
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
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var labels = block.Instructions.OfType<IrLabel>().ToList();

        // Should have unique labels for both if statements
        var labelNames = labels.Select(l => l.Name).ToList();
        Assert.Equal(labelNames.Count, labelNames.Distinct().Count());

        // Should have labels for outer and inner if statements
        Assert.True(labels.Count >= 4); // At least 4 labels for nested ifs
    }

    [Fact]
    public void BuildIr_NestedLoops_CreatesUniqueLabels()
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
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var labels = block.Instructions.OfType<IrLabel>().ToList();

        // Should have unique labels
        var labelNames = labels.Select(l => l.Name).ToList();
        Assert.Equal(labelNames.Count, labelNames.Distinct().Count());

        // Should have labels for both loops
        Assert.Contains(labels, l => l.Name.StartsWith("while_"));
        Assert.Contains(labels, l => l.Name.StartsWith("forever_"));
    }

    [Fact]
    public void BuildIr_ConditionalBranch_HasCorrectTargets()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 100
    } else {
        return 200
    }
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];
        var condBranch = block.Instructions.OfType<IrConditionalBranch>().FirstOrDefault();

        Assert.NotNull(condBranch);
        Assert.NotNull(condBranch.Condition);
        Assert.NotNull(condBranch.TrueTarget);
        Assert.NotNull(condBranch.FalseTarget);
        Assert.StartsWith("if_then_", condBranch.TrueTarget);
        Assert.StartsWith("if_else_", condBranch.FalseTarget);
    }

    [Fact]
    public void BuildIr_WhileCondition_EvaluatedCorrectly()
    {
        var source = @"
fn test() -> i32 {
    while 0 == 1 {
        return 999
    }
    return 42
}";
        var module = BuildIr(source);

        var block = module.Functions[0].BasicBlocks[0];

        // Should have comparison for condition
        var comparison = block.Instructions.OfType<IrBinaryOp>()
            .FirstOrDefault(b => b.Operation == IrBinaryOp.OpKind.Eq);
        Assert.NotNull(comparison);

        // Should compare 0 and 1
        var left = Assert.IsType<IrConstant>(comparison.Left);
        var right = Assert.IsType<IrConstant>(comparison.Right);
        Assert.Equal(0, left.Value);
        Assert.Equal(1, right.Value);
    }

    // Visibility Tests

    [Fact]
    public void BuildIr_PublicFunction_MarkedAsPublic()
    {
        var source = @"
pub fn exported() -> u32 {
    return 42
}";
        var module = BuildIr(source);

        Assert.Single(module.Functions);
        var func = module.Functions[0];
        Assert.Equal("exported", func.Name);
        Assert.True(func.IsPublic);
    }

    [Fact]
    public void BuildIr_PrivateFunction_MarkedAsPrivate()
    {
        var source = @"
fn internal() -> u32 {
    return 42
}";
        var module = BuildIr(source);

        Assert.Single(module.Functions);
        var func = module.Functions[0];
        Assert.Equal("internal", func.Name);
        Assert.False(func.IsPublic);
    }

    [Fact]
    public void BuildIr_MixedVisibility_CorrectlyMarked()
    {
        var source = @"
pub fn public_api() -> u32 {
    return 42
}

fn helper() -> u32 {
    return 10
}";
        var module = BuildIr(source);

        Assert.Equal(2, module.Functions.Count);
        Assert.True(module.Functions[0].IsPublic);
        Assert.Equal("public_api", module.Functions[0].Name);
        Assert.False(module.Functions[1].IsPublic);
        Assert.Equal("helper", module.Functions[1].Name);
    }

    [Fact]
    public void BuildIr_FunctionCallNoArguments_CreatesCallInstruction()
    {
        var source = @"
pub fn main() -> u32 {
    return helper()
}

pub fn helper() -> u32 {
    return 42
}";
        var module = BuildIr(source);

        Assert.Equal(2, module.Functions.Count);
        var mainFunc = module.Functions[0];
        var instructions = mainFunc.BasicBlocks[0].Instructions;

        var callInst = instructions.OfType<IrCall>().FirstOrDefault();
        Assert.NotNull(callInst);
        Assert.Equal("helper", callInst.FunctionName);
        Assert.Empty(callInst.Arguments);
        Assert.Equal("u32", callInst.ReturnType.Name);
        Assert.NotNull(callInst.ResultName);
    }

    [Fact]
    public void BuildIr_FunctionCallWithOneArgument_CreatesCallWithArgument()
    {
        var source = @"
pub fn main() -> u32 {
    return double(21)
}

pub fn double(x: u32) -> u32 {
    return x + x
}";
        var module = BuildIr(source);

        var mainFunc = module.Functions[0];
        var instructions = mainFunc.BasicBlocks[0].Instructions;

        var callInst = instructions.OfType<IrCall>().FirstOrDefault();
        Assert.NotNull(callInst);
        Assert.Equal("double", callInst.FunctionName);
        Assert.Single(callInst.Arguments);
        Assert.IsType<IrConstant>(callInst.Arguments[0]);
        Assert.Equal(21, ((IrConstant)callInst.Arguments[0]).Value);
    }

    [Fact]
    public void BuildIr_FunctionCallWithMultipleArguments_CreatesCallWithAllArguments()
    {
        var source = @"
pub fn main() -> u32 {
    return add(10, 32)
}

pub fn add(a: u32, b: u32) -> u32 {
    return a + b
}";
        var module = BuildIr(source);

        var mainFunc = module.Functions[0];
        var instructions = mainFunc.BasicBlocks[0].Instructions;

        var callInst = instructions.OfType<IrCall>().FirstOrDefault();
        Assert.NotNull(callInst);
        Assert.Equal("add", callInst.FunctionName);
        Assert.Equal(2, callInst.Arguments.Count);
        Assert.Equal(10, ((IrConstant)callInst.Arguments[0]).Value);
        Assert.Equal(32, ((IrConstant)callInst.Arguments[1]).Value);
    }

    [Fact]
    public void BuildIr_NestedFunctionCalls_CreatesMultipleCallInstructions()
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
        var module = BuildIr(source);

        var mainFunc = module.Functions[0];
        var instructions = mainFunc.BasicBlocks[0].Instructions;

        var callInstructions = instructions.OfType<IrCall>().ToList();
        Assert.Equal(2, callInstructions.Count);

        // First call should be double(5)
        Assert.Equal("double", callInstructions[0].FunctionName);
        Assert.Single(callInstructions[0].Arguments);

        // Second call should be add(result, 10)
        Assert.Equal("add", callInstructions[1].FunctionName);
        Assert.Equal(2, callInstructions[1].Arguments.Count);
    }

    [Fact]
    public void BuildIr_FunctionCallWrongArgumentCount_ThrowsException()
    {
        var source = @"
pub fn main() -> u32 {
    return add(10)
}

pub fn add(a: u32, b: u32) -> u32 {
    return a + b
}";
        var exception = Assert.Throws<Exception>(() => BuildIr(source));
        Assert.Contains("expects 2 arguments, got 1", exception.Message);
    }

    [Fact]
    public void BuildIr_FunctionCallUnknownFunction_ThrowsException()
    {
        var source = @"
pub fn main() -> u32 {
    return unknown()
}";
        var exception = Assert.Throws<Exception>(() => BuildIr(source));
        Assert.Contains("Unknown function: unknown", exception.Message);
    }
}
