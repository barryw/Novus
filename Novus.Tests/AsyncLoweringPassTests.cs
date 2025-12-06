using Novus.HIR;
using Novus.IR;
using Novus.Transforms.Passes;

namespace Novus.Tests;

/// <summary>
/// Tests for AsyncLoweringPass which transforms HirAsyncFunction to state machines.
/// </summary>
public class AsyncLoweringPassTests
{
    #region Transform Detection Tests

    [Fact]
    public void Transform_NoAsyncFunctions_ReturnsFalse()
    {
        var module = new IrModule();
        var pass = new AsyncLoweringPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
        Assert.Empty(module.Structs);
    }

    [Fact]
    public void Transform_WithAsyncFunction_ReturnsTrue()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_async",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        var changed = pass.Transform(module);

        Assert.True(changed);
    }

    [Fact]
    public void Transform_RemovesHirAsyncFunctionFromModule()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_async",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        Assert.DoesNotContain(module.HirInstructions, h => h is HirAsyncFunction);
    }

    #endregion

    #region State Machine Struct Generation Tests

    [Fact]
    public void Transform_CreatesStateMachineStruct()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "my_func",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        Assert.Contains(module.Structs, s => s.Name.StartsWith("__my_func_state_"));
    }

    [Fact]
    public void Transform_StateMachineStructHasStateField()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var smStruct = module.Structs.First(s => s.Name.Contains("test_func"));
        Assert.Contains(smStruct.Fields, f => f.Name == "__state" && f.Type == IrIntType.U32);
    }

    [Fact]
    public void Transform_StateMachineStructHasResultField()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I64
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var smStruct = module.Structs.First(s => s.Name.Contains("test_func"));
        Assert.Contains(smStruct.Fields, f => f.Name == "__result" && f.Type == IrIntType.I64);
    }

    [Fact]
    public void Transform_StateMachineStructHasCompletedField()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var smStruct = module.Structs.First(s => s.Name.Contains("test_func"));
        Assert.Contains(smStruct.Fields, f => f.Name == "__completed" && f.Type is IrBoolType);
    }

    [Fact]
    public void Transform_StateMachineStructCapturesParameters()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        asyncFn.Parameters.Add(new IrParameter("x", IrIntType.I32));
        asyncFn.Parameters.Add(new IrParameter("y", IrIntType.I64));
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var smStruct = module.Structs.First(s => s.Name.Contains("test_func"));
        Assert.Contains(smStruct.Fields, f => f.Name == "__param_x");
        Assert.Contains(smStruct.Fields, f => f.Name == "__param_y");
    }

    [Fact]
    public void Transform_StateMachineStructCapturesLocals()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        asyncFn.StateMachineFields.Add(new IrLocalVariable("temp", IrIntType.I32, true));
        asyncFn.StateMachineFields.Add(new IrLocalVariable("counter", IrIntType.U64, false));
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var smStruct = module.Structs.First(s => s.Name.Contains("test_func"));
        Assert.Contains(smStruct.Fields, f => f.Name == "__local_temp");
        Assert.Contains(smStruct.Fields, f => f.Name == "__local_counter");
    }

    [Fact]
    public void Transform_StateMachineStructCapturesAwaitResults()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        asyncFn.AwaitPoints.Add(new AwaitPoint
        {
            StateNumber = 0,
            ResultVariable = "result1",
            AwaitedExpression = new IrConstant(0, IrIntType.I32)
        });
        asyncFn.AwaitPoints.Add(new AwaitPoint
        {
            StateNumber = 1,
            ResultVariable = "result2",
            AwaitedExpression = new IrConstant(0, IrIntType.I64)
        });
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var smStruct = module.Structs.First(s => s.Name.Contains("test_func"));
        Assert.Contains(smStruct.Fields, f => f.Name == "__await_0");
        Assert.Contains(smStruct.Fields, f => f.Name == "__await_1");
    }

    #endregion

    #region Resume Function Generation Tests

    [Fact]
    public void Transform_CreatesResumeFunction()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "my_func",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        Assert.Contains(module.Functions, f => f.Name.StartsWith("__my_func_resume_"));
    }

    [Fact]
    public void Transform_ResumeFunctionTakesStateMachinePointer()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var resumeFn = module.Functions.First(f => f.Name.Contains("resume"));
        Assert.Single(resumeFn.Parameters);
        Assert.Equal("__sm", resumeFn.Parameters[0].Name);
        Assert.IsType<IrPointerType>(resumeFn.Parameters[0].Type);
    }

    [Fact]
    public void Transform_ResumeFunctionHasEntryBlock()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var resumeFn = module.Functions.First(f => f.Name.Contains("resume"));
        Assert.Contains(resumeFn.BasicBlocks, b => b.Label == "entry");
    }

    [Fact]
    public void Transform_NoAwaitPoints_GeneratesCompleteBlock()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        // No await points - function can complete synchronously
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var resumeFn = module.Functions.First(f => f.Name.Contains("resume"));
        Assert.Contains(resumeFn.BasicBlocks, b => b.Label == "complete");
    }

    [Fact]
    public void Transform_WithAwaitPoints_GeneratesStateBlocks()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        asyncFn.AwaitPoints.Add(new AwaitPoint
        {
            StateNumber = 0,
            ResultVariable = "res",
            AwaitedExpression = new IrConstant(0, IrIntType.I32)
        });
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var resumeFn = module.Functions.First(f => f.Name.Contains("resume"));
        Assert.Contains(resumeFn.BasicBlocks, b => b.Label == "state_0");
        Assert.Contains(resumeFn.BasicBlocks, b => b.Label == "state_1"); // Final state
    }

    [Fact]
    public void Transform_MultipleAwaitPoints_GeneratesAllStateBlocks()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        for (int i = 0; i < 3; i++)
        {
            asyncFn.AwaitPoints.Add(new AwaitPoint
            {
                StateNumber = i,
                ResultVariable = $"res{i}",
                AwaitedExpression = new IrConstant(0, IrIntType.I32)
            });
        }
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var resumeFn = module.Functions.First(f => f.Name.Contains("resume"));
        Assert.Contains(resumeFn.BasicBlocks, b => b.Label == "state_0");
        Assert.Contains(resumeFn.BasicBlocks, b => b.Label == "state_1");
        Assert.Contains(resumeFn.BasicBlocks, b => b.Label == "state_2");
        Assert.Contains(resumeFn.BasicBlocks, b => b.Label == "state_3"); // Final state
    }

    [Fact]
    public void Transform_FinalStateSetCompletedFlag()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        asyncFn.AwaitPoints.Add(new AwaitPoint
        {
            StateNumber = 0,
            ResultVariable = "res",
            AwaitedExpression = new IrConstant(0, IrIntType.I32)
        });
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var resumeFn = module.Functions.First(f => f.Name.Contains("resume"));
        var finalBlock = resumeFn.BasicBlocks.First(b => b.Label == "state_1");

        // Final state should set __completed to true
        Assert.Contains(finalBlock.Instructions, i => i is IrMemberStore ms && ms.FieldName == "__completed");
    }

    #endregion

    #region Original Function Generation Tests

    [Fact]
    public void Transform_CreatesOriginalFunction()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "my_async_func",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        Assert.Contains(module.Functions, f => f.Name == "my_async_func");
    }

    [Fact]
    public void Transform_OriginalFunctionHasSameParameters()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "my_func",
            ReturnType = IrIntType.I32
        };
        asyncFn.Parameters.Add(new IrParameter("a", IrIntType.I32));
        asyncFn.Parameters.Add(new IrParameter("b", IrBoolType.Instance));
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var origFn = module.Functions.First(f => f.Name == "my_func");
        Assert.Equal(2, origFn.Parameters.Count);
        Assert.Equal("a", origFn.Parameters[0].Name);
        Assert.Equal("b", origFn.Parameters[1].Name);
    }

    [Fact]
    public void Transform_OriginalFunctionInitializesStateMachine()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "my_func",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var origFn = module.Functions.First(f => f.Name == "my_func");
        var entryBlock = origFn.BasicBlocks.First(b => b.Label == "entry");

        // Should have a local decl for __state_machine
        Assert.Contains(entryBlock.Instructions, i => i is IrLocalDecl ld && ld.Name == "__state_machine");
    }

    [Fact]
    public void Transform_OriginalFunctionCallsResume()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "my_func",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var origFn = module.Functions.First(f => f.Name == "my_func");
        var entryBlock = origFn.BasicBlocks.First(b => b.Label == "entry");

        // Should have a call to the resume function
        var hasResumeCall = entryBlock.Instructions
            .OfType<IrCall>()
            .Any(c => c.FunctionName.Contains("resume"));
        Assert.True(hasResumeCall);
    }

    [Fact]
    public void Transform_OriginalFunctionCopiesParametersToStateMachine()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "my_func",
            ReturnType = IrIntType.I32
        };
        asyncFn.Parameters.Add(new IrParameter("x", IrIntType.I32));
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var origFn = module.Functions.First(f => f.Name == "my_func");
        var entryBlock = origFn.BasicBlocks.First(b => b.Label == "entry");

        // Should have a member store for __param_x
        Assert.Contains(entryBlock.Instructions, i => i is IrMemberStore ms && ms.FieldName == "__param_x");
    }

    [Fact]
    public void Transform_OriginalFunctionReturnsResult()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "my_func",
            ReturnType = IrIntType.I32
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var origFn = module.Functions.First(f => f.Name == "my_func");
        var entryBlock = origFn.BasicBlocks.First(b => b.Label == "entry");

        // Should end with a return
        Assert.IsType<IrReturn>(entryBlock.Instructions.Last());
    }

    #endregion

    #region Multiple Async Functions Tests

    [Fact]
    public void Transform_MultipleAsyncFunctions_LowersAll()
    {
        var module = new IrModule();

        var asyncFn1 = new HirAsyncFunction
        {
            FunctionName = "func1",
            ReturnType = IrIntType.I32
        };
        var asyncFn2 = new HirAsyncFunction
        {
            FunctionName = "func2",
            ReturnType = IrBoolType.Instance
        };

        module.HirInstructions.Add(asyncFn1);
        module.HirInstructions.Add(asyncFn2);
        var pass = new AsyncLoweringPass();

        var changed = pass.Transform(module);

        Assert.True(changed);
        Assert.Equal(2, module.Structs.Count);
        Assert.Equal(4, module.Functions.Count); // 2 originals + 2 resumes
    }

    [Fact]
    public void Transform_MultipleAsyncFunctions_UniqueStateMachineNames()
    {
        var module = new IrModule();

        var asyncFn1 = new HirAsyncFunction
        {
            FunctionName = "func1",
            ReturnType = IrIntType.I32
        };
        var asyncFn2 = new HirAsyncFunction
        {
            FunctionName = "func2",
            ReturnType = IrBoolType.Instance
        };

        module.HirInstructions.Add(asyncFn1);
        module.HirInstructions.Add(asyncFn2);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var structNames = module.Structs.Select(s => s.Name).ToList();
        Assert.Equal(structNames.Count, structNames.Distinct().Count()); // All unique
    }

    #endregion

    #region Return Type Tests

    [Fact]
    public void Transform_VoidReturnType_HandlesCorrectly()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "void_func",
            ReturnType = IrVoidType.Instance
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var smStruct = module.Structs.First();
        Assert.Contains(smStruct.Fields, f => f.Name == "__result" && f.Type is IrVoidType);
    }

    [Fact]
    public void Transform_BoolReturnType_HandlesCorrectly()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "bool_func",
            ReturnType = IrBoolType.Instance
        };
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var smStruct = module.Structs.First();
        Assert.Contains(smStruct.Fields, f => f.Name == "__result" && f.Type is IrBoolType);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Transform_AwaitWithNoResultVariable_SkipsFieldCreation()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        asyncFn.AwaitPoints.Add(new AwaitPoint
        {
            StateNumber = 0,
            ResultVariable = "", // No result variable
            AwaitedExpression = new IrConstant(0, IrIntType.I32)
        });
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var smStruct = module.Structs.First();
        // Should NOT have __await_0 field since ResultVariable is empty
        Assert.DoesNotContain(smStruct.Fields, f => f.Name == "__await_0");
    }

    [Fact]
    public void Transform_AwaitWithNullExpression_UsesDefaultType()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        asyncFn.AwaitPoints.Add(new AwaitPoint
        {
            StateNumber = 0,
            ResultVariable = "result",
            AwaitedExpression = null! // Null expression
        });
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var smStruct = module.Structs.First();
        var awaitField = smStruct.Fields.FirstOrDefault(f => f.Name == "__await_0");
        Assert.NotNull(awaitField);
        Assert.IsType<IrIntType>(awaitField.Type); // Defaults to I32
    }

    [Fact]
    public void Transform_MixedHirInstructions_OnlyLowersAsync()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "test_func",
            ReturnType = IrIntType.I32
        };
        var copperList = new HirCopperList(new List<HirCopperInstruction>());

        module.HirInstructions.Add(asyncFn);
        module.HirInstructions.Add(copperList);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        // Only async should be removed
        Assert.Single(module.HirInstructions);
        Assert.IsType<HirCopperList>(module.HirInstructions[0]);
    }

    [Fact]
    public void Transform_EmptyParametersAndLocals_StructHasMinimalFields()
    {
        var module = new IrModule();
        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "minimal_func",
            ReturnType = IrIntType.I32
        };
        // No parameters, no locals, no await points
        module.HirInstructions.Add(asyncFn);
        var pass = new AsyncLoweringPass();

        pass.Transform(module);

        var smStruct = module.Structs.First();
        // Should have exactly 3 fields: __state, __result, __completed
        Assert.Equal(3, smStruct.Fields.Count);
    }

    #endregion
}
