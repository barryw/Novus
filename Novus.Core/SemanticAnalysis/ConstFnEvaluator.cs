using Novus.IR;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Result of evaluating a const function at compile time.
/// Contains either a successful value or an error message.
/// </summary>
public class ConstFnResult
{
    public bool Success { get; }
    public object? Value { get; }
    public IrType? ValueType { get; }
    public string? Error { get; }

    private ConstFnResult(bool success, object? value, IrType? valueType, string? error)
    {
        Success = success;
        Value = value;
        ValueType = valueType;
        Error = error;
    }

    public static ConstFnResult Ok(object? value, IrType valueType) => new(true, value, valueType, null);
    public static ConstFnResult Err(string error) => new(false, null, null, error);
}

/// <summary>
/// Evaluates const functions at compile time by interpreting their IR.
///
/// Const functions have the following restrictions:
/// - No side effects (no I/O, no mutable statics)
/// - No heap allocations
/// - No unsafe blocks
/// - Can only call other const functions
/// - No loops (for now - could support bounded loops later)
///
/// The evaluator interprets the IR instructions directly, tracking variable
/// values and control flow to compute the return value.
/// </summary>
public class ConstFnEvaluator
{
    private readonly IrModule _module;
    private readonly Dictionary<string, object?> _variables = new();
    private readonly Dictionary<string, IrFunction> _constFunctions = new();
    private readonly int _maxSteps;
    private int _stepCount;

    /// <summary>
    /// Maximum recursion depth for const function calls
    /// </summary>
    private const int MaxRecursionDepth = 100;
    private int _recursionDepth;

    public ConstFnEvaluator(IrModule module, int maxSteps = 10000)
    {
        _module = module;
        _maxSteps = maxSteps;

        // Cache all const functions for quick lookup
        foreach (var func in module.Functions)
        {
            if (func.IsConstFn)
            {
                _constFunctions[func.Name] = func;
            }
        }
    }

    /// <summary>
    /// Evaluate a const function call with the given arguments.
    /// </summary>
    public ConstFnResult Evaluate(string functionName, List<object?> arguments)
    {
        if (!_constFunctions.TryGetValue(functionName, out var function))
        {
            return ConstFnResult.Err($"Function '{functionName}' is not a const fn");
        }

        if (arguments.Count != function.Parameters.Count)
        {
            return ConstFnResult.Err($"Expected {function.Parameters.Count} arguments, got {arguments.Count}");
        }

        _recursionDepth++;
        if (_recursionDepth > MaxRecursionDepth)
        {
            _recursionDepth--;
            return ConstFnResult.Err($"Maximum recursion depth ({MaxRecursionDepth}) exceeded in const fn evaluation");
        }

        // Save current variables for nested calls
        var savedVariables = new Dictionary<string, object?>(_variables);

        try
        {
            // Bind arguments to parameters
            _variables.Clear();
            for (int i = 0; i < arguments.Count; i++)
            {
                _variables[function.Parameters[i].Name] = arguments[i];
            }

            // Execute the function
            return ExecuteFunction(function);
        }
        finally
        {
            _recursionDepth--;
            // Restore previous variables
            _variables.Clear();
            foreach (var kvp in savedVariables)
            {
                _variables[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Evaluate a const function with typed IrValue arguments (from constant expressions).
    /// </summary>
    public ConstFnResult Evaluate(IrFunction function, List<IrValue> arguments)
    {
        if (!function.IsConstFn)
        {
            return ConstFnResult.Err($"Function '{function.Name}' is not a const fn");
        }

        if (arguments.Count != function.Parameters.Count)
        {
            return ConstFnResult.Err($"Expected {function.Parameters.Count} arguments, got {arguments.Count}");
        }

        _recursionDepth++;
        if (_recursionDepth > MaxRecursionDepth)
        {
            _recursionDepth--;
            return ConstFnResult.Err($"Maximum recursion depth ({MaxRecursionDepth}) exceeded in const fn evaluation");
        }

        // Save current variables for nested calls
        var savedVariables = new Dictionary<string, object?>(_variables);

        try
        {
            // Bind arguments to parameters
            _variables.Clear();
            for (int i = 0; i < arguments.Count; i++)
            {
                var argValue = EvaluateValue(arguments[i]);
                // Allow null for pointer arguments (e.g., null literal)
                if (argValue == null && arguments[i] is not IrConstant { Value: 0 })
                {
                    return ConstFnResult.Err($"Could not evaluate argument {i} at compile time");
                }
                _variables[function.Parameters[i].Name] = argValue;
            }

            // Execute the function
            return ExecuteFunction(function);
        }
        finally
        {
            _recursionDepth--;
            // Restore previous variables
            _variables.Clear();
            foreach (var kvp in savedVariables)
            {
                _variables[kvp.Key] = kvp.Value;
            }
        }
    }

    private ConstFnResult ExecuteFunction(IrFunction function)
    {
        if (function.BasicBlocks.Count == 0)
        {
            return ConstFnResult.Err($"Const function '{function.Name}' has no body");
        }

        // Start from entry block at instruction index 0
        var currentBlock = function.BasicBlocks[0];
        int currentInstrIndex = 0;
        _stepCount = 0;

        while (currentBlock != null)
        {
            while (currentInstrIndex < currentBlock.Instructions.Count)
            {
                var instruction = currentBlock.Instructions[currentInstrIndex];
                currentInstrIndex++;
                _stepCount++;

                if (_stepCount > _maxSteps)
                {
                    return ConstFnResult.Err($"Const evaluation exceeded maximum steps ({_maxSteps}). Possible infinite loop?");
                }

                // Check if this instruction is a terminator or label
                if (instruction is IrLabel)
                {
                    // Just skip labels - they're targets for branches
                    continue;
                }

                if (instruction is IrReturn ret)
                {
                    if (ret.Value == null)
                    {
                        return ConstFnResult.Ok(null, IrVoidType.Instance);
                    }
                    var returnValue = EvaluateValue(ret.Value);
                    return ConstFnResult.Ok(returnValue, ret.Value.Type);
                }

                if (instruction is IrBranch branch)
                {
                    // Try to find a separate block first
                    var targetBlock = FindBlock(function, branch.Target);
                    if (targetBlock != null)
                    {
                        currentBlock = targetBlock;
                        currentInstrIndex = 0;
                    }
                    else
                    {
                        // Look for label within current block
                        var labelIdx = FindLabelIndex(currentBlock, branch.Target);
                        if (labelIdx >= 0)
                        {
                            currentInstrIndex = labelIdx + 1; // Start after the label
                        }
                        else
                        {
                            return ConstFnResult.Err($"Branch target '{branch.Target}' not found");
                        }
                    }
                    continue;
                }

                if (instruction is IrConditionalBranch condBranch)
                {
                    var condition = EvaluateValue(condBranch.Condition);
                    bool condBool;
                    if (condition is bool b)
                    {
                        condBool = b;
                    }
                    else if (condition is long l)
                    {
                        condBool = l != 0;
                    }
                    else if (condition is int i)
                    {
                        condBool = i != 0;
                    }
                    else
                    {
                        var condVar = condBranch.Condition as IrVariable;
                        var varName = condVar?.Name ?? "unknown";
                        var availableVars = string.Join(", ", _variables.Keys);
                        return ConstFnResult.Err($"Conditional branch condition is not a boolean: got {condition?.GetType().Name ?? "null"}, var name: '{varName}', available: [{availableVars}]");
                    }

                    var targetLabel = condBool ? condBranch.TrueTarget : condBranch.FalseTarget;

                    // Try to find a separate block first
                    var targetBlock = FindBlock(function, targetLabel);
                    if (targetBlock != null)
                    {
                        currentBlock = targetBlock;
                        currentInstrIndex = 0;
                    }
                    else
                    {
                        // Look for label within current block
                        var labelIdx = FindLabelIndex(currentBlock, targetLabel);
                        if (labelIdx >= 0)
                        {
                            currentInstrIndex = labelIdx + 1; // Start after the label
                        }
                        else
                        {
                            return ConstFnResult.Err($"Branch target '{targetLabel}' not found");
                        }
                    }
                    continue;
                }

                var result = ExecuteInstruction(instruction, function);
                if (result != null)
                {
                    // Error occurred
                    return result;
                }
            }

            // Reached end of current block - try to fall through to next block
            var idx = function.BasicBlocks.IndexOf(currentBlock);
            if (idx + 1 < function.BasicBlocks.Count)
            {
                currentBlock = function.BasicBlocks[idx + 1];
                currentInstrIndex = 0;
            }
            else
            {
                currentBlock = null;
            }
        }

        // Reached end without return
        return ConstFnResult.Ok(null, IrVoidType.Instance);
    }

    private IrBasicBlock? FindBlock(IrFunction function, string label)
    {
        return function.BasicBlocks.FirstOrDefault(b => b.Label == label);
    }

    /// <summary>
    /// Find the index of an IrLabel instruction within a block.
    /// Returns -1 if not found.
    /// </summary>
    private int FindLabelIndex(IrBasicBlock block, string label)
    {
        for (int i = 0; i < block.Instructions.Count; i++)
        {
            if (block.Instructions[i] is IrLabel lbl && lbl.Name == label)
            {
                return i;
            }
        }
        return -1;
    }

    private ConstFnResult? ExecuteInstruction(IrInstruction instruction, IrFunction function)
    {
        switch (instruction)
        {
            case IrLocalDecl localDecl:
                _variables[localDecl.Name] = localDecl.InitialValue != null
                    ? EvaluateValue(localDecl.InitialValue)
                    : GetDefaultValue(localDecl.Type);
                return null;

            case IrStore store:
                _variables[store.VariableName] = EvaluateValue(store.Value);
                return null;

            case IrBinaryOp binOp:
                var left = EvaluateValue(binOp.Left);
                var right = EvaluateValue(binOp.Right);
                if (left == null || right == null)
                {
                    return ConstFnResult.Err($"Binary operation operands cannot be evaluated: left={left?.GetType().Name ?? "null"}, right={right?.GetType().Name ?? "null"}, leftVal={binOp.Left?.GetType().Name}, rightVal={binOp.Right?.GetType().Name}");
                }
                var result = EvaluateBinaryOp(binOp.Operation, left, right, binOp.Type);
                if (result == null)
                {
                    return ConstFnResult.Err($"Binary operation '{binOp.Operation}' returned null for {left.GetType().Name} and {right.GetType().Name}");
                }
                _variables[binOp.ResultName] = result;
                return null;

            case IrCall call:
                // Check if calling another const function
                if (_constFunctions.TryGetValue(call.FunctionName, out var callee))
                {
                    var args = call.Arguments.Select(a => EvaluateValue(a)).ToList();
                    var callResult = Evaluate(call.FunctionName, args);
                    if (!callResult.Success)
                    {
                        return callResult;
                    }
                    if (call.ResultName != null)
                    {
                        _variables[call.ResultName] = callResult.Value;
                    }
                    return null;
                }
                return ConstFnResult.Err($"Cannot call non-const function '{call.FunctionName}' from const context");

            case IrReturn ret:
                // Handled by terminator processing
                return null;

            case IrBranch:
            case IrConditionalBranch:
                // Handled by terminator processing
                return null;

            case IrPhi phi:
                // PHI nodes in const evaluation - we need to track which block we came from
                // For now, skip - proper PHI handling would require tracking predecessor
                return null;

            case IrDefer:
                return ConstFnResult.Err("defer is not allowed in const fn");

            case IrPanic:
                return ConstFnResult.Err("panic! is not allowed in const fn");

            case IrAssert assert:
                // Evaluate assert at compile time
                var cond = EvaluateValue(assert.Condition);
                if (cond is bool b && !b)
                {
                    return ConstFnResult.Err($"Compile-time assertion failed: {assert.Message ?? "assertion failed"}");
                }
                return null;

            case IrDereferenceStore:
                return ConstFnResult.Err("Pointer dereference store is not allowed in const fn");

            default:
                return ConstFnResult.Err($"Unsupported instruction type in const fn: {instruction.GetType().Name}");
        }
    }

    private object? EvaluateValue(IrValue value)
    {
        switch (value)
        {
            case IrConstant c: return c.Value;
            case IrBoolConstant b: return b.Value;
            case IrFloatConstant f: return f.Value;
            case IrFixedConstant fx: return fx.Value;
            case IrStringLiteral or IrGlobalVariable or IrFunctionAddress:
                return value;
            case IrVariable v:
                if (_variables.TryGetValue(v.Name, out var val))
                    return val;
                // Variable not found - this can happen if comparison result wasn't stored
                // Return null and let caller handle the error
                return null;
            case IrSizeOf sz: return sz.TargetType.SizeInBytes;
            case IrCastValue cast:
            {
                var inner = MaterializeValue(EvaluateValue(cast.Value), cast.SourceType);
                return inner == null ? null : new IrCastValue(inner, cast.SourceType, cast.Type);
            }
            case IrStructLiteral structure:
            {
                var fields = new Dictionary<string, IrValue>();
                foreach (var (name, field) in structure.FieldValues)
                {
                    var evaluated = MaterializeValue(EvaluateValue(field), field.Type);
                    if (evaluated == null) return null;
                    fields[name] = evaluated;
                }
                return new IrStructLiteral((IrStructType)structure.Type, fields);
            }
            case IrTupleLiteral tuple:
            {
                var elements = new List<IrValue>();
                foreach (var element in tuple.Elements)
                {
                    var evaluated = MaterializeValue(EvaluateValue(element), element.Type);
                    if (evaluated == null) return null;
                    elements.Add(evaluated);
                }
                return new IrTupleLiteral((IrTupleType)tuple.Type, elements);
            }
            case IrArrayLiteral array:
            {
                var result = new IrArrayLiteral((IrArrayType)array.Type);
                foreach (var element in array.Elements)
                {
                    var evaluated = MaterializeValue(EvaluateValue(element), element.Type);
                    if (evaluated == null) return null;
                    result.Elements.Add(evaluated);
                }
                return result;
            }
            case IrEnumValue enumValue:
            {
                var values = new List<IrValue>();
                foreach (var associated in enumValue.AssociatedValues)
                {
                    var evaluated = MaterializeValue(EvaluateValue(associated), associated.Type);
                    if (evaluated == null) return null;
                    values.Add(evaluated);
                }
                return new IrEnumValue((IrEnumType)enumValue.Type, enumValue.VariantName,
                    enumValue.VariantTag, values);
            }
            // null pointer is represented as IrConstant(0) with pointer type
            default: return null;
        }
    }

    private static IrValue? MaterializeValue(object? value, IrType type)
    {
        if (value is IrValue irValue)
            return irValue;

        return type switch
        {
            IrBoolType when value is bool boolean => new IrBoolConstant(boolean),
            IrFloatType floatType when value is IConvertible =>
                new IrFloatConstant(Convert.ToDouble(value), floatType),
            IrFixedType fixedType when value is IConvertible =>
                new IrFixedConstant(Convert.ToDouble(value), fixedType),
            IrIntType when value is IConvertible => new IrConstant(Convert.ToInt64(value), type),
            IrPointerType when value is IConvertible => new IrConstant(Convert.ToInt64(value), type),
            _ => null
        };
    }

    private object? EvaluateBinaryOp(IrBinaryOp.OpKind op, object? left, object? right, IrType resultType)
    {
        // Handle integer operations
        if (left is long lLong && right is long rLong)
        {
            return op switch
            {
                IrBinaryOp.OpKind.Add => lLong + rLong,
                IrBinaryOp.OpKind.Sub => lLong - rLong,
                IrBinaryOp.OpKind.Mul => lLong * rLong,
                IrBinaryOp.OpKind.Div => rLong != 0 ? lLong / rLong : 0L,
                IrBinaryOp.OpKind.Mod => rLong != 0 ? lLong % rLong : 0L,
                IrBinaryOp.OpKind.And => lLong & rLong,
                IrBinaryOp.OpKind.Or => lLong | rLong,
                IrBinaryOp.OpKind.Xor => lLong ^ rLong,
                IrBinaryOp.OpKind.Shl => lLong << (int)rLong,
                IrBinaryOp.OpKind.Shr => lLong >> (int)rLong,
                IrBinaryOp.OpKind.Eq => lLong == rLong,
                IrBinaryOp.OpKind.Ne => lLong != rLong,
                IrBinaryOp.OpKind.Lt => lLong < rLong,
                IrBinaryOp.OpKind.Le => lLong <= rLong,
                IrBinaryOp.OpKind.Gt => lLong > rLong,
                IrBinaryOp.OpKind.Ge => lLong >= rLong,
                _ => null
            };
        }

        // Handle float operations
        if (left is double lDouble && right is double rDouble)
        {
            return op switch
            {
                IrBinaryOp.OpKind.Add => lDouble + rDouble,
                IrBinaryOp.OpKind.Sub => lDouble - rDouble,
                IrBinaryOp.OpKind.Mul => lDouble * rDouble,
                IrBinaryOp.OpKind.Div => rDouble != 0 ? lDouble / rDouble : 0.0,
                IrBinaryOp.OpKind.Eq => lDouble == rDouble,
                IrBinaryOp.OpKind.Ne => lDouble != rDouble,
                IrBinaryOp.OpKind.Lt => lDouble < rDouble,
                IrBinaryOp.OpKind.Le => lDouble <= rDouble,
                IrBinaryOp.OpKind.Gt => lDouble > rDouble,
                IrBinaryOp.OpKind.Ge => lDouble >= rDouble,
                _ => null
            };
        }

        // Handle boolean operations
        if (left is bool lBool && right is bool rBool)
        {
            return op switch
            {
                IrBinaryOp.OpKind.And => lBool && rBool,
                IrBinaryOp.OpKind.Or => lBool || rBool,
                IrBinaryOp.OpKind.Eq => lBool == rBool,
                IrBinaryOp.OpKind.Ne => lBool != rBool,
                _ => null
            };
        }

        // Mixed numeric types - convert to long
        if (IsNumeric(left) && IsNumeric(right))
        {
            var l = ToLong(left);
            var r = ToLong(right);
            return EvaluateBinaryOp(op, l, r, resultType);
        }

        return null;
    }

    private static bool IsNumeric(object? value) =>
        value is long or int or short or sbyte or ulong or uint or ushort or byte or double or float;

    private static long ToLong(object? value) => value switch
    {
        long l => l,
        int i => i,
        short s => s,
        sbyte sb => sb,
        ulong ul => (long)ul,
        uint ui => ui,
        ushort us => us,
        byte b => b,
        double d => (long)d,
        float f => (long)f,
        _ => 0L
    };

    private static object? GetDefaultValue(IrType type) => type switch
    {
        IrIntType => 0L,
        IrBoolType => false,
        IrFloatType => 0.0,
        IrFixedType => 0.0,
        _ => null
    };

    /// <summary>
    /// Validate that a function can be a const fn (has no disallowed operations).
    /// This performs comprehensive purity checking to ensure the function has no side effects.
    /// </summary>
    /// <param name="function">The function to validate</param>
    /// <param name="module">The module containing all functions (needed for checking callee constness)</param>
    /// <returns>List of validation errors (empty if function is valid)</returns>
    public static List<string> ValidateConstFn(IrFunction function, IrModule? module = null)
    {
        var errors = new List<string>();

        if (function.BasicBlocks.Count == 0)
        {
            errors.Add("const fn must have a body");
            return errors;
        }

        // Track which values come from global variables (for transitive checking)
        var globalVarReferences = new HashSet<string>();

        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                // Check instruction for purity violations
                ValidateInstruction(instruction, function, module, errors, globalVarReferences);

                // Also check any IrValue operands within the instruction
                ValidateInstructionOperands(instruction, function, errors, globalVarReferences);
            }
        }

        return errors;
    }

    /// <summary>
    /// Validate a single instruction for const fn purity.
    /// </summary>
    private static void ValidateInstruction(
        IrInstruction instruction,
        IrFunction function,
        IrModule? module,
        List<string> errors,
        HashSet<string> globalVarReferences)
    {
        switch (instruction)
        {
            case IrDefer:
                errors.Add($"const fn '{function.Name}': defer is not allowed in const fn");
                break;

            case IrPanic:
                errors.Add($"const fn '{function.Name}': panic! is not allowed in const fn");
                break;

            case IrDereferenceStore derefStore:
                errors.Add($"const fn '{function.Name}': pointer writes are not allowed in const fn");
                break;

            case IrHardwareWrite:
                errors.Add($"const fn '{function.Name}': hardware register writes are not allowed in const fn");
                break;

            case IrHardwareRead:
                errors.Add($"const fn '{function.Name}': hardware register reads are not allowed in const fn");
                break;

            case IrInlineAsm:
                errors.Add($"const fn '{function.Name}': inline assembly is not allowed in const fn");
                break;

            case IrCreateClosure:
                errors.Add($"const fn '{function.Name}': closures are not allowed in const fn");
                break;

            case IrInvokeClosure:
                errors.Add($"const fn '{function.Name}': closure invocation is not allowed in const fn");
                break;

            case IrIndirectCall:
                errors.Add($"const fn '{function.Name}': indirect function calls (function pointers) are not allowed in const fn");
                break;

            case IrCall call:
                // Check if callee is a const fn
                if (module != null)
                {
                    var callee = module.GetFunction(call.FunctionName);
                    if (callee != null && !callee.IsConstFn)
                    {
                        errors.Add($"const fn '{function.Name}': cannot call non-const function '{call.FunctionName}'");
                    }
                }
                break;

            case IrStore store:
                // Check if storing to a static/global variable (by name)
                if (module != null && IsStaticVariableName(store.VariableName, module))
                {
                    errors.Add($"const fn '{function.Name}': cannot write to global variable '{store.VariableName}'");
                }
                break;

            case IrMemberStore memberStore:
                // Check if the struct being modified is a global
                if (IsGlobalVariableAccess(memberStore.Struct, globalVarReferences))
                {
                    errors.Add($"const fn '{function.Name}': cannot modify field '{memberStore.FieldName}' of global variable");
                }
                break;

            case IrIndexStore indexStore:
                // Check if the array being modified is a global
                if (IsGlobalVariableAccess(indexStore.Array, globalVarReferences))
                {
                    errors.Add($"const fn '{function.Name}': cannot modify element of global array");
                }
                break;
        }
    }

    /// <summary>
    /// Validate operands within an instruction for global variable reads.
    /// </summary>
    private static void ValidateInstructionOperands(
        IrInstruction instruction,
        IrFunction function,
        List<string> errors,
        HashSet<string> globalVarReferences)
    {
        // Extract all IrValue operands from the instruction
        var operands = GetInstructionOperands(instruction);

        foreach (var operand in operands)
        {
            ValidateValue(operand, function, errors, globalVarReferences);
        }
    }

    /// <summary>
    /// Validate a value for const fn purity (checks for global variable reads).
    /// </summary>
    private static void ValidateValue(
        IrValue? value,
        IrFunction function,
        List<string> errors,
        HashSet<string> globalVarReferences)
    {
        if (value == null) return;

        switch (value)
        {
            case IrGlobalVariable globalVar:
                errors.Add($"const fn '{function.Name}': cannot read global variable '{globalVar.Name}'");
                globalVarReferences.Add(globalVar.Name);
                break;

            case IrCopperListData:
                errors.Add($"const fn '{function.Name}': copper list data is not allowed in const fn");
                break;

            case IrBlitterOpData:
                errors.Add($"const fn '{function.Name}': blitter operations are not allowed in const fn");
                break;

            case IrFunctionAddress funcAddr:
                // Function addresses are okay for const fn - they're compile-time constants
                // But we should verify the referenced function exists
                break;

            case IrCastValue castValue:
                ValidateValue(castValue.Value, function, errors, globalVarReferences);
                break;

            case IrBorrowValue borrowValue:
                ValidateValue(borrowValue.BorrowedValue, function, errors, globalVarReferences);
                break;

            case IrDereferenceValue derefValue:
                ValidateValue(derefValue.PointerValue, function, errors, globalVarReferences);
                break;

            case IrStructLiteral structLit:
                foreach (var fieldValue in structLit.FieldValues.Values)
                {
                    ValidateValue(fieldValue, function, errors, globalVarReferences);
                }
                break;

            case IrTupleLiteral tupleLit:
                foreach (var elem in tupleLit.Elements)
                {
                    ValidateValue(elem, function, errors, globalVarReferences);
                }
                break;

            case IrArrayLiteral arrayLit:
                foreach (var elem in arrayLit.Elements)
                {
                    ValidateValue(elem, function, errors, globalVarReferences);
                }
                break;
        }
    }

    /// <summary>
    /// Check if a value represents access to a global variable (directly or indirectly).
    /// </summary>
    private static bool IsGlobalVariableAccess(IrValue? value, HashSet<string> globalVarReferences)
    {
        if (value == null) return false;

        return value switch
        {
            IrGlobalVariable => true,
            IrVariable varRef => globalVarReferences.Contains(varRef.Name),
            IrCastValue castValue => IsGlobalVariableAccess(castValue.Value, globalVarReferences),
            IrBorrowValue borrowValue => IsGlobalVariableAccess(borrowValue.BorrowedValue, globalVarReferences),
            IrDereferenceValue derefValue => IsGlobalVariableAccess(derefValue.PointerValue, globalVarReferences),
            _ => false
        };
    }

    /// <summary>
    /// Check if a variable name refers to a static/global variable in the module.
    /// </summary>
    private static bool IsStaticVariableName(string varName, IrModule module)
    {
        return module.StaticVariables.Any(sv => sv.Name == varName);
    }

    /// <summary>
    /// Extract all IrValue operands from an instruction.
    /// </summary>
    private static IEnumerable<IrValue?> GetInstructionOperands(IrInstruction instruction)
    {
        return instruction switch
        {
            IrReturn ret => new IrValue?[] { ret.Value },
            IrBinaryOp binOp => new IrValue?[] { binOp.Left, binOp.Right },
            IrCall call => call.Arguments.Cast<IrValue?>(),
            IrStore store => new IrValue?[] { store.Value },
            IrLocalDecl localDecl => new IrValue?[] { localDecl.InitialValue },
            IrConditionalBranch condBranch => new IrValue?[] { condBranch.Condition },
            IrDereferenceStore derefStore => new IrValue?[] { derefStore.Pointer, derefStore.Value },
            IrMemberAccess memberAccess => new IrValue?[] { memberAccess.Struct },
            IrMemberStore memberStore => new IrValue?[] { memberStore.Struct, memberStore.Value },
            IrIndexAccess indexAccess => new IrValue?[] { indexAccess.Array, indexAccess.Index },
            IrIndexStore indexStore => new IrValue?[] { indexStore.Array, indexStore.Index, indexStore.Value },
            IrAssert assertInst => new IrValue?[] { assertInst.Condition },
            IrIndirectCall indirectCall => indirectCall.Arguments.Cast<IrValue?>().Prepend(indirectCall.FunctionPointer),
            _ => Array.Empty<IrValue?>()
        };
    }
}
