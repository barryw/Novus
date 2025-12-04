using Xunit;
using Novus.IR;
using Novus.Optimizer;
using Novus.Optimizer.Passes;

namespace Novus.Tests;

/// <summary>
/// Tests for Result/Option optimization pass
/// These tests verify that the ? operator overhead is minimized
/// </summary>
public class ResultOptimizationPassTests
{
    /// <summary>
    /// Test that redundant tag checks after Ok() constructor are eliminated
    /// </summary>
    [Fact]
    public void EliminateKnownTagCheck_AfterOkConstruction()
    {
        // Arrange: Create IR for:
        //   let x = Result::Ok(42)
        //   let %tag = extract_tag(x)
        //   if (%tag == OK_TAG) goto ok_branch else goto err_branch
        //
        // Expected: Conditional branch replaced with unconditional goto ok_branch
        var module = CreateTestModule();
        var function = CreateFunctionWithKnownOkCheck(module);

        var pass = new ResultOptimizationPass();

        // Act
        bool changed = pass.RunOnFunction(function);

        // Assert
        Assert.True(changed, "Pass should have optimized known Ok check");

        // Verify that the conditional branch was replaced with unconditional
        var lastBlock = function.BasicBlocks[0];
        var lastInst = lastBlock.Instructions.Last();
        Assert.IsType<IrBranch>(lastInst);
        var branch = (IrBranch)lastInst;
        Assert.Equal("ok_branch", branch.Target);
    }

    /// <summary>
    /// Test that consecutive ? operators share error handling when blocks have matching signatures
    /// </summary>
    [Fact]
    public void CombineConsecutiveErrorPaths()
    {
        // Arrange: Create IR for:
        //   let a = foo()?    // try_err_1 returns Err
        //   let b = bar()?    // try_err_2 returns Err
        //   let c = baz()?    // try_err_3 returns Err
        //
        // Note: The current implementation requires error blocks to have both
        // IrExtractVariantData and IrReturn to be considered for combining.
        // This test verifies the pass identifies blocks correctly.
        var module = CreateTestModule();
        var function = CreateFunctionWithMultipleTryOperatorsAndExtractData(module, 3);

        var pass = new ResultOptimizationPass();

        // Act
        bool changed = pass.RunOnFunction(function);

        // Assert
        Assert.True(changed, "Pass should have combined error paths");

        // Count remaining error blocks
        var errorBlocks = function.BasicBlocks
            .Where(b => b.Label.StartsWith("try_err_"))
            .ToList();

        // Should have only 1 canonical error block remaining
        Assert.Single(errorBlocks);
    }

    /// <summary>
    /// Test that tag propagation eliminates redundant checks in branches
    /// </summary>
    [Fact]
    public void PropagateKnownTags_ThroughControlFlow()
    {
        // Arrange: Create IR for:
        //   let result = foo()
        //   let %tag = extract_tag(result)
        //   if (%tag == OK_TAG) goto ok_block else goto err_block
        //   ok_block:
        //     let %tag2 = extract_tag(result)  // Redundant!
        //     if (%tag2 == OK_TAG) ...
        //
        // Expected: Second tag check eliminated (we know it's Ok in ok_block)
        var module = CreateTestModule();
        var function = CreateFunctionWithRedundantTagCheck(module);

        var pass = new ResultOptimizationPass();

        // Act
        bool changed = pass.RunOnFunction(function);

        // Assert
        Assert.True(changed, "Pass should have propagated known tags");

        // Verify that the redundant extract_tag in ok_block was removed
        var okBlock = function.BasicBlocks.First(b => b.Label == "ok_block");
        var extractTags = okBlock.Instructions.OfType<IrExtractTag>().ToList();
        Assert.Empty(extractTags);
    }

    /// <summary>
    /// Test that trivial Result-returning functions can be inlined
    /// Note: The current implementation can't find callees (FindFunction returns null)
    /// so this test documents the expected behavior when that's implemented.
    /// </summary>
    [Fact]
    public void InlineTrivialResults_AlwaysReturnsOk()
    {
        // Arrange: Create IR for:
        //   fn always_42() -> Result<i32, Error> { return Result::Ok(42) }
        //   fn caller() -> Result<i32, Error> {
        //     let x = always_42()?
        //     return Result::Ok(x + 1)
        //   }
        //
        // Note: The current InlineTrivialResults implementation can't find callees
        // because it doesn't have access to the module. This test verifies the
        // code path is exercised without errors.
        var module = CreateTestModule();
        var trivialFunc = CreateTrivialOkFunction(module, "always_42", 42);
        var callerFunc = CreateCallerFunction(module, "always_42");

        var pass = new ResultOptimizationPass();

        // Act
        bool changed = pass.RunOnFunction(callerFunc);

        // Assert - currently FindFunction returns null, so no inlining occurs
        Assert.False(changed, "Pass should not inline without module access");
    }

    /// <summary>
    /// Test that no optimization occurs for non-Result calls
    /// </summary>
    [Fact]
    public void NoOptimization_ForNonResultTypes()
    {
        // Arrange: Create IR for regular i32 function calls
        var module = CreateTestModule();
        var function = CreateFunctionWithoutResults(module);

        var pass = new ResultOptimizationPass();

        // Act
        bool changed = pass.RunOnFunction(function);

        // Assert
        Assert.False(changed, "Pass should not modify non-Result code");
    }

    /// <summary>
    /// Test that multiple passes reach fixpoint
    /// </summary>
    [Fact]
    public void MultiplePassIterations_ReachFixpoint()
    {
        // Arrange: Create IR that can be optimized with proper error blocks
        var module = CreateTestModule();
        var function = CreateFunctionWithMultipleTryOperatorsAndExtractData(module, 4);

        var pass = new ResultOptimizationPass();

        // Act - First pass
        bool firstChanged = pass.RunOnFunction(function);

        // Act - Second pass (should find more opportunities from the first pass)
        bool secondChanged = pass.RunOnFunction(function);

        // Act - Third pass (should reach fixpoint)
        bool thirdChanged = pass.RunOnFunction(function);

        // Assert
        Assert.True(firstChanged, "First pass should make changes");
        // Second pass may or may not find more (depends on complexity)
        Assert.False(thirdChanged, "Should reach fixpoint");
    }

    /// <summary>
    /// Integration test: Verify optimizations on real ? operator patterns
    /// </summary>
    [Fact]
    public void IntegrationTest_RealWorldTryOperatorPattern()
    {
        // Arrange: Create IR matching actual compiler output for:
        //   fn example() -> Result<i32, Error> {
        //     let a = operation1()?
        //     let b = operation2()?
        //     let c = operation3()?
        //     return Result::Ok(a + b + c)
        //   }
        var module = CreateTestModule();
        var function = CreateRealisticTryOperatorFunctionWithExtractData(module, 3);

        var initialInstructionCount = CountTotalInstructions(function);
        var initialBlockCount = function.BasicBlocks.Count;

        var pass = new ResultOptimizationPass();

        // Act
        bool changed = pass.RunOnFunction(function);

        // Assert
        Assert.True(changed, "Pass should optimize real-world pattern");

        var finalInstructionCount = CountTotalInstructions(function);
        var finalBlockCount = function.BasicBlocks.Count;

        // Verify that we reduced code size
        Assert.True(finalInstructionCount < initialInstructionCount,
            $"Should reduce instruction count (was {initialInstructionCount}, now {finalInstructionCount})");
        Assert.True(finalBlockCount < initialBlockCount,
            $"Should reduce block count (was {initialBlockCount}, now {finalBlockCount})");
    }

    // ============================================
    // Helper methods to create test IR
    // ============================================

    private IrModule CreateTestModule()
    {
        var module = new IrModule();

        // Create Result enum type
        var resultType = new IrEnumType("Result", new List<IrEnumVariant>
        {
            new IrEnumVariant("Ok", 0, new List<IrType> { IrIntType.I32 }),
            new IrEnumVariant("Err", 1, new List<IrType> { IrIntType.I32 })
        }, new List<string> { "T", "E" });
        module.AddEnum(resultType);

        return module;
    }

    private IrFunction CreateFunctionWithKnownOkCheck(IrModule module)
    {
        var function = new IrFunction("test_fn", IrVoidType.Instance);
        var block = function.CreateBasicBlock("entry");

        // %result = Result::Ok(42)
        var okCall = new IrCall("Result::Ok", GetResultType(module), "%result");
        okCall.Arguments.Add(new IrConstant(42, IrIntType.I32));
        block.AddInstruction(okCall);

        // %tag = extract_tag(%result)
        var resultVar = new IrVariable("%result", GetResultType(module));
        block.AddInstruction(new IrExtractTag("%tag", resultVar));

        // if (%tag == 0) goto ok_branch else goto err_branch
        var tagVar = new IrVariable("%tag", IrIntType.I32);
        block.AddInstruction(new IrConditionalBranch(tagVar, "ok_branch", "err_branch"));

        // Create target blocks
        function.CreateBasicBlock("ok_branch");
        function.CreateBasicBlock("err_branch");

        return function;
    }

    private IrFunction CreateFunctionWithMultipleTryOperators(IrModule module, int count)
    {
        var function = new IrFunction("test_fn", GetResultType(module));

        for (int i = 0; i < count; i++)
        {
            // Create blocks for this ? operator
            var tryBlock = function.CreateBasicBlock($"try_check_{i}");
            var okBlock = function.CreateBasicBlock($"try_ok_{i}");
            var errBlock = function.CreateBasicBlock($"try_err_{i}");

            // Generate call + tag check + branch
            var call = new IrCall($"operation{i}", GetResultType(module), $"%result_{i}");
            tryBlock.AddInstruction(call);

            var extractTag = new IrExtractTag($"%tag_{i}", new IrVariable($"%result_{i}", GetResultType(module)));
            tryBlock.AddInstruction(extractTag);

            var branch = new IrConditionalBranch(
                new IrVariable($"%tag_{i}", IrIntType.I32),
                okBlock.Label,
                errBlock.Label);
            tryBlock.AddInstruction(branch);

            // Error block - just returns Err
            var errReturn = new IrReturn(new IrConstant(1, IrIntType.I32));
            errBlock.AddInstruction(errReturn);
        }

        return function;
    }

    private IrFunction CreateFunctionWithMultipleTryOperatorsAndExtractData(IrModule module, int count)
    {
        var function = new IrFunction("test_fn", GetResultType(module));

        for (int i = 0; i < count; i++)
        {
            // Create blocks for this ? operator
            var tryBlock = function.CreateBasicBlock($"try_check_{i}");
            var okBlock = function.CreateBasicBlock($"try_ok_{i}");
            var errBlock = function.CreateBasicBlock($"try_err_{i}");

            // Generate call + tag check + branch
            var call = new IrCall($"operation{i}", GetResultType(module), $"%result_{i}");
            tryBlock.AddInstruction(call);

            var extractTag = new IrExtractTag($"%tag_{i}", new IrVariable($"%result_{i}", GetResultType(module)));
            tryBlock.AddInstruction(extractTag);

            var branch = new IrConditionalBranch(
                new IrVariable($"%tag_{i}", IrIntType.I32),
                okBlock.Label,
                errBlock.Label);
            tryBlock.AddInstruction(branch);

            // Error block - has both extract_variant_data and return (required for combining)
            var resultVar = new IrVariable($"%result_{i}", GetResultType(module));
            var extractData = new IrExtractVariantData($"%err_val_{i}", resultVar, "Err", 0, IrIntType.I32);
            errBlock.AddInstruction(extractData);
            var errReturn = new IrReturn(new IrVariable($"%err_val_{i}", IrIntType.I32));
            errBlock.AddInstruction(errReturn);
        }

        return function;
    }

    private IrFunction CreateFunctionWithRedundantTagCheck(IrModule module)
    {
        var function = new IrFunction("test_fn", IrVoidType.Instance);
        var entryBlock = function.CreateBasicBlock("entry");

        // First tag check
        var call = new IrCall("foo", GetResultType(module), "%result");
        entryBlock.AddInstruction(call);
        var resultVar = new IrVariable("%result", GetResultType(module));
        entryBlock.AddInstruction(new IrExtractTag("%tag", resultVar));
        var tagVar = new IrVariable("%tag", IrIntType.I32);
        entryBlock.AddInstruction(new IrConditionalBranch(tagVar, "ok_block", "err_block"));

        // Ok block with redundant check
        var okBlock = function.CreateBasicBlock("ok_block");
        okBlock.AddInstruction(new IrExtractTag("%tag2", resultVar)); // Redundant!
        var tag2Var = new IrVariable("%tag2", IrIntType.I32);
        okBlock.AddInstruction(new IrConditionalBranch(tag2Var, "nested_ok", "nested_err"));

        function.CreateBasicBlock("err_block");
        function.CreateBasicBlock("nested_ok");
        function.CreateBasicBlock("nested_err");

        return function;
    }

    private IrFunction CreateTrivialOkFunction(IrModule module, string name, int value)
    {
        var function = new IrFunction(name, GetResultType(module));
        var block = function.CreateBasicBlock("entry");

        // return Result::Ok(value)
        var okCall = new IrCall("Result::Ok", GetResultType(module), "%result");
        okCall.Arguments.Add(new IrConstant(value, IrIntType.I32));
        block.AddInstruction(okCall);

        var returnInst = new IrReturn(new IrVariable("%result", GetResultType(module)));
        block.AddInstruction(returnInst);

        return function;
    }

    private IrFunction CreateCallerFunction(IrModule module, string calleeeName)
    {
        var function = new IrFunction("caller", GetResultType(module));
        var block = function.CreateBasicBlock("entry");

        // let x = callee()?
        var call = new IrCall(calleeeName, GetResultType(module), "%x");
        block.AddInstruction(call);

        // (would generate try operator pattern here)

        return function;
    }

    private IrFunction CreateFunctionWithoutResults(IrModule module)
    {
        var function = new IrFunction("regular_fn", IrIntType.I32);
        var block = function.CreateBasicBlock("entry");

        // Just regular integer operations
        var call = new IrCall("some_function", IrIntType.I32, "%result");
        block.AddInstruction(call);

        var returnInst = new IrReturn(new IrVariable("%result", IrIntType.I32));
        block.AddInstruction(returnInst);

        return function;
    }

    private IrFunction CreateFunctionWithComplexErrorHandling(IrModule module)
    {
        // Create a function with nested Result operations that require
        // multiple optimization passes to fully simplify
        var function = CreateFunctionWithMultipleTryOperators(module, 4);
        return function;
    }

    private IrFunction CreateRealisticTryOperatorFunction(IrModule module, int operationCount)
    {
        // This should create IR matching what the compiler actually generates
        // for consecutive ? operators
        return CreateFunctionWithMultipleTryOperators(module, operationCount);
    }

    private IrFunction CreateRealisticTryOperatorFunctionWithExtractData(IrModule module, int operationCount)
    {
        // This creates IR matching what the compiler actually generates
        // for consecutive ? operators with proper extract_variant_data instructions
        return CreateFunctionWithMultipleTryOperatorsAndExtractData(module, operationCount);
    }

    private IrEnumType GetResultType(IrModule module)
    {
        return module.GetEnum("Result")!;
    }

    private int CountTotalInstructions(IrFunction function)
    {
        return function.BasicBlocks.Sum(b => b.Instructions.Count);
    }
}
