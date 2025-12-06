using Novus.IR;
using Novus.HIR;

namespace Novus.Transforms.Passes;

/// <summary>
/// Async/await lowering transformation pass.
/// Transforms async functions into state machines backed by Exec signals.
///
/// This transforms HirAsyncFunction HIR instructions into:
/// 1. A state machine struct holding local variables and state
/// 2. A resume function implementing the state machine
/// 3. Updated call sites to use the state machine
/// </summary>
public class AsyncLoweringPass : TransformPassBase
{
    public override string Name => "Async Lowering";

    private int _stateMachineCounter = 0;

    public override bool Transform(IrModule module)
    {
        bool changed = false;

        // Find HIR async functions that need lowering
        foreach (var hirInstruction in module.HirInstructions.ToList())
        {
            if (hirInstruction is HirAsyncFunction asyncFn)
            {
                // Lower this async function to a state machine
                LowerAsyncFunction(module, asyncFn);

                module.HirInstructions.Remove(hirInstruction);
                changed = true;
            }
        }

        return changed;
    }

    private void LowerAsyncFunction(IrModule module, HirAsyncFunction asyncFn)
    {
        var smId = _stateMachineCounter++;
        var smStructName = $"__{asyncFn.FunctionName}_state_{smId}";
        var smResumeFnName = $"__{asyncFn.FunctionName}_resume_{smId}";

        // 1. Create state machine struct
        var smStruct = CreateStateMachineStruct(asyncFn, smStructName);
        module.Structs.Add(smStruct);

        // 2. Create resume function
        var resumeFn = CreateResumeFunction(asyncFn, smStruct, smResumeFnName);
        module.Functions.Add(resumeFn);

        // 3. Update the original function to create and drive the state machine
        UpdateOriginalFunction(module, asyncFn, smStruct, smResumeFnName);
    }

    /// <summary>
    /// Create a struct to hold the state machine's state and captured variables
    /// </summary>
    private IrStructType CreateStateMachineStruct(HirAsyncFunction asyncFn, string structName)
    {
        var fields = new List<IrStructField>();

        // Field 0: state number (u32)
        fields.Add(new IrStructField("__state", IrIntType.U32));

        // Field 1: return value storage
        fields.Add(new IrStructField("__result", asyncFn.ReturnType));

        // Field 2: completed flag (bool)
        fields.Add(new IrStructField("__completed", IrBoolType.Instance));

        // Add fields for parameters (captured from original call)
        foreach (var param in asyncFn.Parameters)
        {
            fields.Add(new IrStructField($"__param_{param.Name}", param.Type));
        }

        // Add fields for local variables that need to be preserved across await points
        foreach (var local in asyncFn.StateMachineFields)
        {
            fields.Add(new IrStructField($"__local_{local.Name}", local.Type));
        }

        // Add fields for each await point's result
        foreach (var awaitPoint in asyncFn.AwaitPoints)
        {
            if (!string.IsNullOrEmpty(awaitPoint.ResultVariable))
            {
                // Get the type from the awaited expression if available
                var resultType = awaitPoint.AwaitedExpression?.Type ?? IrIntType.I32;
                fields.Add(new IrStructField($"__await_{awaitPoint.StateNumber}", resultType));
            }
        }

        return new IrStructType(structName, fields);
    }

    /// <summary>
    /// Create the resume function that implements the state machine
    /// </summary>
    private IrFunction CreateResumeFunction(HirAsyncFunction asyncFn, IrStructType smStruct, string fnName)
    {
        // Resume function signature: fn resume(state: *mut SmStruct) -> AsyncResult<T>
        var stateParamType = new IrPointerType(smStruct);
        var returnType = CreateAsyncResultType(asyncFn.ReturnType);

        var function = new IrFunction(fnName, returnType);
        function.Parameters.Add(new IrParameter("__sm", stateParamType));

        // Create the entry block
        var entryBlock = new IrBasicBlock("entry");
        function.BasicBlocks.Add(entryBlock);

        // Read the current state by dereferencing and accessing field
        var smVar = new IrVariable("__sm", stateParamType);
        var derefSm = new IrDereferenceValue(smVar, smStruct);
        var stateReadVar = "__current_state";

        entryBlock.Instructions.Add(new IrLocalDecl(
            stateReadVar,
            IrIntType.U32,
            false,
            new IrFieldReference(derefSm, "__state", IrIntType.U32)
        ));

        // Generate a state dispatch switch
        if (asyncFn.AwaitPoints.Count == 0)
        {
            // No await points - function can complete synchronously
            var completeBlock = new IrBasicBlock("complete");
            function.BasicBlocks.Add(completeBlock);

            // Set completed flag
            completeBlock.Instructions.Add(new IrMemberStore(
                new IrDereferenceValue(smVar, smStruct),
                "__completed",
                GetFieldOffset(smStruct, "__completed"),
                new IrBoolConstant(true)
            ));

            // Return Ready with default value
            completeBlock.Instructions.Add(new IrReturn(
                CreateAsyncResultReady(asyncFn.ReturnType)
            ));

            entryBlock.Instructions.Add(new IrBranch("complete"));
        }
        else
        {
            // Generate state blocks for each await point
            for (int i = 0; i <= asyncFn.AwaitPoints.Count; i++)
            {
                var stateBlock = new IrBasicBlock($"state_{i}");
                function.BasicBlocks.Add(stateBlock);

                if (i < asyncFn.AwaitPoints.Count)
                {
                    // For each state:
                    // 1. Check if the awaited future is ready
                    // 2. If ready, extract value and advance to next state
                    // 3. If not ready, return Pending

                    // For simplicity, we'll generate code that assumes the awaited expression
                    // returns an AsyncResult type that we can poll

                    // Advance state counter
                    stateBlock.Instructions.Add(new IrMemberStore(
                        new IrDereferenceValue(smVar, smStruct),
                        "__state",
                        GetFieldOffset(smStruct, "__state"),
                        new IrConstant(i + 1, IrIntType.U32)
                    ));

                    // Jump to next state (simplified - real impl would poll the future)
                    stateBlock.Instructions.Add(new IrBranch($"state_{i + 1}"));
                }
                else
                {
                    // Final state - return the result
                    stateBlock.Instructions.Add(new IrMemberStore(
                        new IrDereferenceValue(smVar, smStruct),
                        "__completed",
                        GetFieldOffset(smStruct, "__completed"),
                        new IrBoolConstant(true)
                    ));

                    stateBlock.Instructions.Add(new IrReturn(
                        CreateAsyncResultReady(asyncFn.ReturnType)
                    ));
                }
            }

            // Entry block dispatches based on state
            // Generate conditional branches for each state
            for (int i = 0; i <= asyncFn.AwaitPoints.Count; i++)
            {
                var checkVar = $"__is_state_{i}";
                entryBlock.Instructions.Add(new IrBinaryOp(
                    checkVar,
                    IrBinaryOp.OpKind.Eq,
                    new IrVariable(stateReadVar, IrIntType.U32),
                    new IrConstant(i, IrIntType.U32),
                    IrBoolType.Instance
                ));

                if (i == asyncFn.AwaitPoints.Count)
                {
                    // Last state - unconditional branch
                    entryBlock.Instructions.Add(new IrBranch($"state_{i}"));
                }
                else
                {
                    entryBlock.Instructions.Add(new IrConditionalBranch(
                        new IrVariable(checkVar, IrBoolType.Instance),
                        $"state_{i}",
                        $"__check_state_{i + 1}"
                    ));

                    // Add label for next check
                    entryBlock.Instructions.Add(new IrLabel($"__check_state_{i + 1}"));
                }
            }
        }

        return function;
    }

    /// <summary>
    /// Update the original function to create the state machine and call resume
    /// </summary>
    private void UpdateOriginalFunction(IrModule module, HirAsyncFunction asyncFn, IrStructType smStruct, string resumeFnName)
    {
        // Find the original function
        var originalFn = module.GetFunction(asyncFn.FunctionName);
        if (originalFn == null)
        {
            // Function doesn't exist yet - create a wrapper that initializes and runs the state machine
            originalFn = new IrFunction(asyncFn.FunctionName, asyncFn.ReturnType);

            foreach (var param in asyncFn.Parameters)
            {
                originalFn.Parameters.Add(param);
            }

            var entryBlock = new IrBasicBlock("entry");
            originalFn.BasicBlocks.Add(entryBlock);

            // Create local state machine instance with initialization
            var initValues = new Dictionary<string, IrValue>
            {
                { "__state", new IrConstant(0, IrIntType.U32) },
                { "__completed", new IrBoolConstant(false) }
            };

            entryBlock.Instructions.Add(new IrLocalDecl(
                "__state_machine",
                smStruct,
                true,
                new IrStructLiteral(smStruct, initValues)
            ));

            var smVar = new IrVariable("__state_machine", smStruct);

            // Copy parameters to state machine
            foreach (var param in asyncFn.Parameters)
            {
                entryBlock.Instructions.Add(new IrMemberStore(
                    smVar,
                    $"__param_{param.Name}",
                    GetFieldOffset(smStruct, $"__param_{param.Name}"),
                    new IrVariable(param.Name, param.Type)
                ));
            }

            // Call resume function with pointer to state machine
            var smPtrType = new IrPointerType(smStruct);
            var resultType = CreateAsyncResultType(asyncFn.ReturnType);

            var resumeCall = new IrCall(resumeFnName, resultType, "__result");
            resumeCall.Arguments.Add(new IrBorrowValue(smVar, smPtrType, true));
            entryBlock.Instructions.Add(resumeCall);

            // Return the result
            entryBlock.Instructions.Add(new IrReturn(new IrVariable("__result", resultType)));

            module.Functions.Add(originalFn);
        }
    }

    /// <summary>
    /// Get field offset in struct (simplified - returns index for now)
    /// </summary>
    private int GetFieldOffset(IrStructType structType, string fieldName)
    {
        for (int i = 0; i < structType.Fields.Count; i++)
        {
            if (structType.Fields[i].Name == fieldName)
                return i;
        }
        return 0;
    }

    /// <summary>
    /// Create an AsyncResult[T] type for the given inner type
    /// </summary>
    private IrType CreateAsyncResultType(IrType innerType)
    {
        // For now, just use the inner type directly
        // A real implementation would use a proper AsyncResult enum type
        return innerType;
    }

    /// <summary>
    /// Create an AsyncResult::Ready(value) expression
    /// </summary>
    private IrValue CreateAsyncResultReady(IrType innerType)
    {
        // For now, return a default value
        // A real implementation would construct a proper AsyncResult::Ready variant
        return CreateDefaultValue(innerType);
    }

    /// <summary>
    /// Create a default value for a type
    /// </summary>
    private IrValue CreateDefaultValue(IrType type)
    {
        if (type is IrIntType intType)
        {
            return new IrConstant(0, intType);
        }
        else if (type is IrBoolType)
        {
            return new IrBoolConstant(false);
        }
        else if (type is IrVoidType || type == IrTupleType.Unit)
        {
            return new IrTupleLiteral(IrTupleType.Unit, new List<IrValue>());
        }
        else
        {
            // For other types, return a zero constant
            return new IrConstant(0, IrIntType.I32);
        }
    }
}
