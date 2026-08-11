using System.Text;
using Novus.IR;

namespace Novus.IR;

/// <summary>
/// Validates IR for correctness before code generation.
/// Catches malformed IR that could cause codegen crashes or incorrect output.
/// </summary>
public class IrValidator
{
    private readonly List<string> _errors = new();
    private readonly HashSet<string> _declaredVariables = new();
    private readonly HashSet<string> _labels = new();
    private readonly HashSet<string> _functions = new();
    private readonly HashSet<string> _staticVariables = new();
    private readonly HashSet<string> _externalVariables = new();
    private IrFunction? _currentFunction;

    /// <summary>
    /// Validate an entire IR module
    /// </summary>
    public ValidationResult Validate(IrModule module)
    {
        _errors.Clear();
        _functions.Clear();
        _staticVariables.Clear();
        _externalVariables.Clear();

        // Collect all static variables
        foreach (var staticVar in module.StaticVariables)
        {
            if (string.IsNullOrEmpty(staticVar.Name))
            {
                AddError("Static variable has empty name");
            }
            else if (_staticVariables.Contains(staticVar.Name))
            {
                AddError($"Duplicate static variable name: {staticVar.Name}");
            }
            else
            {
                _staticVariables.Add(staticVar.Name);
            }

            if (staticVar.Type == null)
            {
                AddError($"Static variable '{staticVar.Name}' has null type");
            }
        }

        // Collect all external variables
        foreach (var externVar in module.ExternalVariables)
        {
            if (string.IsNullOrEmpty(externVar.Name))
            {
                AddError("External variable has empty name");
            }
            else if (_externalVariables.Contains(externVar.Name))
            {
                AddError($"Duplicate external variable name: {externVar.Name}");
            }
            else
            {
                _externalVariables.Add(externVar.Name);
            }

            if (externVar.Type == null)
            {
                AddError($"External variable '{externVar.Name}' has null type");
            }
        }

        // First pass: collect all function names
        foreach (var function in module.Functions)
        {
            if (_functions.Contains(function.Name))
            {
                AddError($"Duplicate function name: {function.Name}");
            }
            _functions.Add(function.Name);
        }

        // Second pass: validate each function
        foreach (var function in module.Functions)
        {
            ValidateFunction(function);
        }

        return new ValidationResult(_errors);
    }

    /// <summary>
    /// Validate a single function
    /// </summary>
    private void ValidateFunction(IrFunction function)
    {
        _currentFunction = function;
        _declaredVariables.Clear();
        _labels.Clear();

        // Check return type is valid
        if (function.ReturnType == null)
        {
            AddError($"Function {function.Name} has null return type");
        }

        // Collect all parameters as declared variables
        foreach (var param in function.Parameters)
        {
            if (string.IsNullOrEmpty(param.Name))
            {
                AddError($"Function {function.Name} has parameter with empty name");
            }
            if (param.Type == null)
            {
                AddError($"Function {function.Name} parameter {param.Name} has null type");
            }
            _declaredVariables.Add(param.Name);
        }

        // Collect all local variables as declared
        foreach (var local in function.LocalVariables)
        {
            if (string.IsNullOrEmpty(local.Name))
            {
                AddError($"Function {function.Name} has local variable with empty name");
            }
            if (local.Type == null)
            {
                AddError($"Function {function.Name} local variable {local.Name} has null type");
            }
            _declaredVariables.Add(local.Name);
        }

        // First pass: collect all labels
        foreach (var block in function.BasicBlocks.Concat(function.DeferredBlocks))
        {
            if (string.IsNullOrEmpty(block.Label))
            {
                AddError($"Function {function.Name} has basic block with empty label");
            }
            else if (_labels.Contains(block.Label))
            {
                AddError($"Function {function.Name} has duplicate label: {block.Label}");
            }
            else
            {
                _labels.Add(block.Label);
            }

            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrLabel label)
                {
                    if (_labels.Contains(label.Name))
                    {
                        AddError($"Function {function.Name} has duplicate label: {label.Name}");
                    }
                    _labels.Add(label.Name);
                }
            }
        }

        // Second pass: validate instructions
        foreach (var block in function.BasicBlocks)
        {
            ValidateBasicBlock(block);
        }

        // Validate deferred blocks
        foreach (var block in function.DeferredBlocks)
        {
            ValidateBasicBlock(block);
        }
    }

    /// <summary>
    /// Validate a basic block
    /// </summary>
    private void ValidateBasicBlock(IrBasicBlock block)
    {
        bool hasTerminator = false;

        for (int i = 0; i < block.Instructions.Count; i++)
        {
            var instruction = block.Instructions[i];

            // Check if instruction after terminator
            // EXCEPTION: IrLabel is allowed after a terminator since it marks
            // the start of a new logical code path (e.g., else branch, merge point).
            // This is how the IR builder structures control flow within a single basic block.
            if (hasTerminator && instruction is not IrLabel)
            {
                AddError($"Basic block {block.Label} has non-label instruction after terminator: {instruction.GetType().Name}");
                break;
            }

            // If we see a label after a terminator, we're starting a new logical block
            // Reset the terminator flag
            if (hasTerminator && instruction is IrLabel)
            {
                hasTerminator = false;
            }

            ValidateInstruction(instruction);

            // Check for terminators
            if (instruction is IrReturn or IrBranch or IrConditionalBranch)
            {
                hasTerminator = true;
            }
        }

        // Note: We don't enforce that every basic block ends with a terminator
        // because the IR model uses IrLabel within blocks, and the final "logical block"
        // might fall through to defer cleanup or implicit return.
        // The codegen handles this correctly.
    }

    /// <summary>
    /// Validate a single instruction
    /// </summary>
    private void ValidateInstruction(IrInstruction instruction)
    {
        switch (instruction)
        {
            case IrReturn ret:
                ValidateReturn(ret);
                break;
            case IrBranch branch:
                ValidateBranch(branch);
                break;
            case IrConditionalBranch condBranch:
                ValidateConditionalBranch(condBranch);
                break;
            case IrBinaryOp binOp:
                ValidateBinaryOp(binOp);
                break;
            case IrCall call:
                ValidateCall(call);
                break;
            case IrLocalDecl localDecl:
                ValidateLocalDecl(localDecl);
                break;
            case IrStore store:
                ValidateStore(store);
                break;
            case IrIndexAccess indexAccess:
                ValidateIndexAccess(indexAccess);
                break;
            case IrIndexStore indexStore:
                ValidateIndexStore(indexStore);
                break;
            case IrMemberAccess memberAccess:
                ValidateMemberAccess(memberAccess);
                break;
            case IrMemberStore memberStore:
                ValidateMemberStore(memberStore);
                break;
            case IrExtractTag extractTag:
                ValidateExtractTag(extractTag);
                break;
            case IrExtractVariantData extractData:
                ValidateExtractVariantData(extractData);
                break;
            case IrMatch match:
                ValidateMatch(match);
                break;
            case IrDereferenceStore derefStore:
                ValidateDereferenceStore(derefStore);
                break;
            case IrIndirectCall indirectCall:
                ValidateIndirectCall(indirectCall);
                break;
            case IrLabel:
            case IrDefer:
                // These are always valid
                break;

            // Additional instruction types that are valid
            case IrAssert assertInst:
                ValidateValue(assertInst.Condition);
                break;

            case IrPanic:
                // Panic just has a message string - always valid
                break;

            case IrPhi phi:
                // Phi nodes have a destination variable and incoming values from predecessor blocks
                if (phi.Destination == null)
                {
                    AddError("Phi node has null destination");
                }
                else if (string.IsNullOrEmpty(phi.Destination.Name))
                {
                    AddError("Phi node has empty destination name");
                }
                else
                {
                    _declaredVariables.Add(phi.Destination.Name);
                }

                // Validate incoming values
                foreach (var incomingValue in phi.IncomingValues)
                {
                    ValidateValue(incomingValue);
                }
                break;

            case IrIndexedFieldStore indexedFieldStore:
                ValidateValue(indexedFieldStore.Array);
                ValidateValue(indexedFieldStore.Index);
                ValidateValue(indexedFieldStore.Value);
                break;

            case IrHardwareWrite hwWrite:
                ValidateValue(hwWrite.Value);
                break;

            case IrHardwareRead hwRead:
                if (!string.IsNullOrEmpty(hwRead.ResultName))
                {
                    _declaredVariables.Add(hwRead.ResultName);
                }
                break;

            case IrInlineAsm inlineAsm:
                foreach (var input in inlineAsm.Inputs)
                    ValidateValue(input.Value);
                if (inlineAsm.ResultName != null)
                    _declaredVariables.Add(inlineAsm.ResultName);
                if (inlineAsm.MultiResultNames != null)
                    _declaredVariables.UnionWith(inlineAsm.MultiResultNames);
                break;

            case IrDropInPlace dropInPlace:
                // Validate that the value is a pointer or mutable reference type
                if (dropInPlace.Pointer.Type is not IrPointerType && dropInPlace.Pointer.Type is not IrMutReferenceType)
                {
                    AddError($"@drop_in_place requires a pointer or mutable reference type, got {dropInPlace.Pointer.Type}");
                }
                ValidateValue(dropInPlace.Pointer);
                break;

            case IrStructuredForLoopHint:
                // Loop hints are metadata - always valid
                break;

            default:
                // Instead of error, just warn in debug - there may be new instruction types
                // that are valid but not yet handled by the validator
                #if DEBUG
                Console.WriteLine($"WARNING: IrValidator - unhandled instruction type: {instruction.GetType().Name}");
                #endif
                break;
        }
    }

    private void ValidateReturn(IrReturn ret)
    {
        if (_currentFunction == null) return;

        if (ret.Value != null)
        {
            // Check return value matches function return type
            if (_currentFunction.ReturnType is IrVoidType)
            {
                AddError($"Function {_currentFunction.Name} returns void but has return value");
            }
            ValidateValue(ret.Value);
        }
        // Note: We don't validate that non-void functions have return values here.
        // The IR model allows empty returns in unreachable code paths (e.g., after match
        // expressions where all arms already return). The semantic analyzer handles this
        // check at a higher level where it has control flow information.
    }

    private void ValidateBranch(IrBranch branch)
    {
        if (!_labels.Contains(branch.Target))
        {
            AddError($"Branch to undefined label: {branch.Target}");
        }
    }

    private void ValidateConditionalBranch(IrConditionalBranch condBranch)
    {
        ValidateValue(condBranch.Condition);

        if (!_labels.Contains(condBranch.TrueTarget))
        {
            AddError($"Conditional branch to undefined label: {condBranch.TrueTarget}");
        }
        if (!_labels.Contains(condBranch.FalseTarget))
        {
            AddError($"Conditional branch to undefined label: {condBranch.FalseTarget}");
        }

        // Condition should be boolean or integer
        if (condBranch.Condition.Type is not (IrBoolType or IrIntType))
        {
            AddError($"Conditional branch condition has invalid type: {condBranch.Condition.Type.Name}");
        }
    }

    private void ValidateBinaryOp(IrBinaryOp binOp)
    {
        if (string.IsNullOrEmpty(binOp.ResultName))
        {
            AddError("Binary operation has empty result name");
        }

        ValidateValue(binOp.Left);
        ValidateValue(binOp.Right);

        if (binOp.Type == null)
        {
            AddError($"Binary operation {binOp.ResultName} has null type");
        }

        // Declare the result variable
        _declaredVariables.Add(binOp.ResultName);
    }

    private void ValidateCall(IrCall call)
    {
        if (string.IsNullOrEmpty(call.FunctionName))
        {
            AddError("Function call has empty function name");
        }

        // Check function exists (unless it's extern)
        // We don't validate extern functions here since they might not be in the module

        foreach (var arg in call.Arguments)
        {
            ValidateValue(arg);
        }

        if (call.ReturnType == null)
        {
            AddError($"Function call to {call.FunctionName} has null return type");
        }

        if (call.ResultName != null)
        {
            _declaredVariables.Add(call.ResultName);
        }
    }

    private void ValidateLocalDecl(IrLocalDecl localDecl)
    {
        if (string.IsNullOrEmpty(localDecl.Name))
        {
            AddError("Local declaration has empty name");
        }

        if (localDecl.Type == null)
        {
            AddError($"Local declaration {localDecl.Name} has null type");
        }

        if (localDecl.InitialValue != null)
        {
            ValidateValue(localDecl.InitialValue);
        }

        // Variable should already be declared from function.LocalVariables
        if (!_declaredVariables.Contains(localDecl.Name))
        {
            AddError($"Local declaration {localDecl.Name} not found in function local variables list");
        }
    }

    private void ValidateStore(IrStore store)
    {
        if (string.IsNullOrEmpty(store.VariableName))
        {
            AddError("Store instruction has empty variable name");
        }

        if (!_declaredVariables.Contains(store.VariableName))
        {
            AddError($"Store to undeclared variable: {store.VariableName}");
        }

        ValidateValue(store.Value);
    }

    private void ValidateIndexAccess(IrIndexAccess indexAccess)
    {
        if (string.IsNullOrEmpty(indexAccess.ResultName))
        {
            AddError("Index access has empty result name");
        }

        ValidateValue(indexAccess.Array);
        ValidateValue(indexAccess.Index);

        // Index should be integer type
        if (indexAccess.Index.Type is not IrIntType)
        {
            AddError($"Array index has non-integer type: {indexAccess.Index.Type.Name}");
        }

        if (indexAccess.ElementType == null)
        {
            AddError($"Index access {indexAccess.ResultName} has null element type");
        }

        _declaredVariables.Add(indexAccess.ResultName);
    }

    private void ValidateIndexStore(IrIndexStore indexStore)
    {
        ValidateValue(indexStore.Array);
        ValidateValue(indexStore.Index);
        ValidateValue(indexStore.Value);

        // Index should be integer type
        if (indexStore.Index.Type is not IrIntType)
        {
            AddError($"Array index has non-integer type: {indexStore.Index.Type.Name}");
        }
    }

    private void ValidateMemberAccess(IrMemberAccess memberAccess)
    {
        if (string.IsNullOrEmpty(memberAccess.ResultName))
        {
            AddError("Member access has empty result name");
        }

        ValidateValue(memberAccess.Struct);

        if (string.IsNullOrEmpty(memberAccess.FieldName))
        {
            AddError($"Member access {memberAccess.ResultName} has empty field name");
        }

        if (memberAccess.FieldType == null)
        {
            AddError($"Member access {memberAccess.ResultName} has null field type");
        }

        _declaredVariables.Add(memberAccess.ResultName);
    }

    private void ValidateMemberStore(IrMemberStore memberStore)
    {
        ValidateValue(memberStore.Struct);
        ValidateValue(memberStore.Value);

        if (string.IsNullOrEmpty(memberStore.FieldName))
        {
            AddError("Member store has empty field name");
        }
    }

    private void ValidateExtractTag(IrExtractTag extractTag)
    {
        if (string.IsNullOrEmpty(extractTag.ResultName))
        {
            AddError("Extract tag has empty result name");
        }

        ValidateValue(extractTag.EnumValue);

        // Enum value should be enum type
        if (extractTag.EnumValue.Type is not IrEnumType)
        {
            AddError($"Extract tag from non-enum type: {extractTag.EnumValue.Type.Name}");
        }

        _declaredVariables.Add(extractTag.ResultName);
    }

    private void ValidateExtractVariantData(IrExtractVariantData extractData)
    {
        if (string.IsNullOrEmpty(extractData.ResultName))
        {
            AddError("Extract variant data has empty result name");
        }

        ValidateValue(extractData.EnumValue);

        if (extractData.EnumValue.Type is not IrEnumType)
        {
            AddError($"Extract variant data from non-enum type: {extractData.EnumValue.Type.Name}");
        }

        if (extractData.DataIndex < 0)
        {
            AddError($"Extract variant data has negative index: {extractData.DataIndex}");
        }

        if (extractData.DataType == null)
        {
            AddError($"Extract variant data {extractData.ResultName} has null data type");
        }

        _declaredVariables.Add(extractData.ResultName);
    }

    private void ValidateMatch(IrMatch match)
    {
        ValidateValue(match.MatchValue);

        if (match.Arms is [])
        {
            AddError("Match expression has no arms");
        }

        foreach (var arm in match.Arms)
        {
            if (!_labels.Contains(arm.TargetLabel))
            {
                AddError($"Match arm branches to undefined label: {arm.TargetLabel}");
            }
        }

        if (match.ResultName != null)
        {
            if (match.ResultType == null)
            {
                AddError($"Match expression {match.ResultName} has null result type");
            }
            _declaredVariables.Add(match.ResultName);
        }
    }

    private void ValidateDereferenceStore(IrDereferenceStore derefStore)
    {
        ValidateValue(derefStore.Pointer);
        ValidateValue(derefStore.Value);

        // Pointer should be pointer or reference type
        if (derefStore.Pointer.Type is not (IrPointerType or IrReferenceType or IrMutReferenceType))
        {
            AddError($"Dereference store from non-pointer type: {derefStore.Pointer.Type.Name}");
        }
    }

    private void ValidateIndirectCall(IrIndirectCall indirectCall)
    {
        ValidateValue(indirectCall.FunctionPointer);

        if (indirectCall.FunctionPointer.Type is not IrFunctionPointerType)
        {
            AddError($"Indirect call through non-function-pointer type: {indirectCall.FunctionPointer.Type.Name}");
        }

        foreach (var arg in indirectCall.Arguments)
        {
            ValidateValue(arg);
        }

        if (indirectCall.ReturnType == null)
        {
            AddError("Indirect call has null return type");
        }

        if (indirectCall.ResultName != null)
        {
            _declaredVariables.Add(indirectCall.ResultName);
        }
    }

    /// <summary>
    /// Validate an IR value
    /// </summary>
    private void ValidateValue(IrValue value)
    {
        if (value == null)
        {
            AddError("Null value in IR");
            return;
        }

        if (value.Type == null)
        {
            AddError($"Value has null type: {value.GetType().Name}");
        }

        switch (value)
        {
            case IrVariable variable:
                if (string.IsNullOrEmpty(variable.Name))
                {
                    AddError("Variable has empty name");
                }
                else if (!_declaredVariables.Contains(variable.Name))
                {
                    AddError($"Use of undeclared variable: {variable.Name}");
                }
                break;

            case IrConstant:
            case IrBoolConstant:
            case IrFloatConstant:
            case IrFixedConstant:
            case IrStringLiteral:
            case IrFunctionAddress:
                // Constants are always valid
                break;

            case IrStructLiteral structLit:
                foreach (var fieldValue in structLit.FieldValues.Values)
                {
                    ValidateValue(fieldValue);
                }
                break;

            case IrArrayLiteral arrayLit:
                foreach (var element in arrayLit.Elements)
                {
                    ValidateValue(element);
                }
                break;

            case IrEnumValue enumValue:
                foreach (var assocValue in enumValue.AssociatedValues)
                {
                    ValidateValue(assocValue);
                }
                break;

            case IrEnumPayloadAccess payloadAccess:
                ValidateValue(payloadAccess.EnumValue);
                break;

            case IrBorrowValue borrowValue:
                ValidateValue(borrowValue.BorrowedValue);
                break;

            case IrDereferenceValue derefValue:
                ValidateValue(derefValue.PointerValue);
                if (derefValue.PointerValue.Type is not (IrPointerType or IrReferenceType or IrMutReferenceType))
                {
                    AddError($"Dereference of non-pointer type: {derefValue.PointerValue.Type.Name}");
                }
                break;

            case IrEnumConstructor:
                // Enum constructors are always valid
                break;

            case IrGlobalVariable globalVar:
                if (string.IsNullOrEmpty(globalVar.Name))
                {
                    AddError("Global variable has empty name");
                }
                else if (!_staticVariables.Contains(globalVar.Name) && !_externalVariables.Contains(globalVar.Name))
                {
                    // Global variable references must be to declared statics or externs
                    AddError($"Reference to undeclared global variable: {globalVar.Name}");
                }
                break;

            case IrFunctionRef funcRef:
                if (funcRef.Function == null)
                {
                    AddError("Function reference has null function");
                }
                else if (string.IsNullOrEmpty(funcRef.Function.Name))
                {
                    AddError("Function reference has function with empty name");
                }
                else if (!_functions.Contains(funcRef.Function.Name))
                {
                    AddError($"Function reference to undeclared function: {funcRef.Function.Name}");
                }
                break;

            case IrCastValue castValue:
                if (castValue.Value == null)
                {
                    AddError("Cast has null value");
                }
                else
                {
                    ValidateValue(castValue.Value);
                }
                if (castValue.SourceType == null)
                {
                    AddError("Cast has null source type");
                }
                // Target type is validated via value.Type check above
                break;

            case IrGenericAssociatedFunction genericFunc:
                AddError($"Generic associated function '{genericFunc.TypeName}::{genericFunc.MethodName}' must be monomorphized before validation");
                break;

            case IrTupleLiteral tupleLit:
                // Validate each element of the tuple
                foreach (var element in tupleLit.Elements)
                {
                    ValidateValue(element);
                }
                break;

            case IrFieldReference fieldRef:
                // Field references have a struct value and a field name
                ValidateValue(fieldRef.Struct);
                if (string.IsNullOrEmpty(fieldRef.FieldName))
                {
                    AddError("Field reference has empty field name");
                }
                break;

            case IrIndexedFieldAccess indexedFieldAccess:
                // Indexed field access - array[index].field
                ValidateValue(indexedFieldAccess.Array);
                ValidateValue(indexedFieldAccess.Index);
                if (string.IsNullOrEmpty(indexedFieldAccess.FieldName))
                {
                    AddError("Indexed field access has empty field name");
                }
                break;

            case IrTupleElementAccess tupleElementAccess:
                // Tuple element access - tuple.0, tuple.1, etc.
                ValidateValue(tupleElementAccess.Tuple);
                if (tupleElementAccess.ElementIndex < 0)
                {
                    AddError($"Tuple element access has negative index: {tupleElementAccess.ElementIndex}");
                }
                break;

            case IrSizeOf:
            case IrZeroed:
                // These are compile-time constant expressions - always valid
                break;

            case IrCopperListData:
            case IrBlitterOpData:
                // Hardware DSL data - always valid at IR level
                break;

            default:
                // Instead of error, just warn in debug - there may be new IR node types
                // that are valid but not yet handled by the validator
                #if DEBUG
                Console.WriteLine($"WARNING: IrValidator - unhandled value type: {value.GetType().Name}");
                #endif
                break;
        }
    }

    private void AddError(string message)
    {
        var functionContext = _currentFunction != null ? $"In function {_currentFunction.Name}: " : "";
        _errors.Add($"{functionContext}{message}");
    }
}

/// <summary>
/// Result of IR validation
/// </summary>
public class ValidationResult
{
    public bool IsValid => Errors is [];
    public List<string> Errors { get; }

    public ValidationResult(List<string> errors)
    {
        Errors = new List<string>(errors);
    }

    public override string ToString()
    {
        if (IsValid)
            return "IR validation passed";

        var sb = new StringBuilder();
        sb.AppendLine($"IR validation failed with {Errors.Count} error(s):");
        foreach (var error in Errors)
        {
            sb.AppendLine($"  - {error}");
        }
        return sb.ToString();
    }
}
