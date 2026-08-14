namespace Novus.IR.Analysis;

/// <summary>
/// Provides def-use chain analysis for IR functions.
/// Maps each variable to its defining instruction and all instructions that use it.
/// This centralized analysis eliminates the need for repeated use-collection across optimizer passes.
/// </summary>
public class DefUseAnalysis
{
    /// <summary>
    /// Maps variable names to the instruction that defines them.
    /// For IrLocalDecl, IrStore, and result-producing instructions (IrBinaryOp, IrCall, etc.)
    /// </summary>
    private readonly Dictionary<string, IrInstruction> _definitions = new();

    /// <summary>
    /// Maps variable names to the list of instructions that use them.
    /// Includes all instructions that reference the variable in any operand position.
    /// </summary>
    private readonly Dictionary<string, List<IrInstruction>> _uses = new();

    /// <summary>
    /// Private constructor - use Analyze() to create instances
    /// </summary>
    private DefUseAnalysis()
    {
    }

    /// <summary>
    /// Analyzes an IR function and builds def-use chains for all variables.
    /// </summary>
    /// <param name="function">The function to analyze</param>
    /// <returns>A DefUseAnalysis instance containing def-use information</returns>
    public static DefUseAnalysis Analyze(IrFunction function)
    {
        var analysis = new DefUseAnalysis();
        var visitor = new DefUseVisitor(analysis);

        // Analyze all basic blocks
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                visitor.VisitInstruction(instruction, instruction);
            }
        }

        // Analyze deferred blocks
        foreach (var deferredBlock in function.DeferredBlocks)
        {
            foreach (var instruction in deferredBlock.Instructions)
            {
                visitor.VisitInstruction(instruction, instruction);
            }
        }

        return analysis;
    }

    /// <summary>
    /// Gets all instructions that use a given variable.
    /// </summary>
    /// <param name="variableName">The variable name to query</param>
    /// <returns>A list of instructions that use the variable, or an empty list if none</returns>
    public List<IrInstruction> GetUses(string variableName)
    {
        return _uses.TryGetValue(variableName, out var uses) ? uses : new List<IrInstruction>();
    }

    /// <summary>
    /// Gets the instruction that defines a given variable.
    /// </summary>
    /// <param name="variableName">The variable name to query</param>
    /// <returns>The defining instruction, or null if not found</returns>
    public IrInstruction? GetDefinition(string variableName)
    {
        return _definitions.TryGetValue(variableName, out var def) ? def : null;
    }

    /// <summary>
    /// Checks if a variable has any uses.
    /// </summary>
    /// <param name="variableName">The variable name to check</param>
    /// <returns>True if the variable is used at least once, false otherwise</returns>
    public bool IsUsed(string variableName)
    {
        return _uses.TryGetValue(variableName, out var uses) && uses.Count > 0;
    }

    /// <summary>
    /// Replaces all uses of a variable with a new value.
    /// This is useful for copy propagation and constant folding.
    /// </summary>
    /// <param name="oldVariableName">The variable to replace</param>
    /// <param name="newValue">The new value to use instead</param>
    public void ReplaceAllUses(string oldVariableName, IrValue newValue)
    {
        var uses = GetUses(oldVariableName);
        foreach (var instruction in uses)
        {
            ReplaceVariableInInstruction(instruction, oldVariableName, newValue);
        }

        // Clear uses since they've been replaced
        _uses.Remove(oldVariableName);
    }

    /// <summary>
    /// Extracts all variables defined by an instruction.
    /// This includes result variables from operations, local declarations, and stores.
    /// </summary>
    /// <param name="instruction">The instruction to analyze</param>
    /// <returns>A list of variable names defined by this instruction</returns>
    public static List<string> GetDefinedVariables(IrInstruction instruction)
    {
        var defined = new List<string>();

        switch (instruction)
        {
            case IrPhi phi:
                defined.Add(phi.Destination.Name);
                break;

            case IrLocalDecl localDecl:
                defined.Add(localDecl.Name);
                break;

            case IrStore store:
                defined.Add(store.VariableName);
                break;

            case IrBinaryOp binaryOp:
                defined.Add(binaryOp.ResultName);
                break;

            case IrCall call when call.ResultName != null:
                defined.Add(call.ResultName);
                break;

            case IrIndirectCall indirectCall when indirectCall.ResultName != null:
                defined.Add(indirectCall.ResultName);
                break;

            case IrIndexAccess indexAccess:
                defined.Add(indexAccess.ResultName);
                break;

            case IrMemberAccess memberAccess:
                defined.Add(memberAccess.ResultName);
                break;

            case IrExtractTag extractTag:
                defined.Add(extractTag.ResultName);
                break;

            case IrExtractVariantData extractData:
                defined.Add(extractData.ResultName);
                break;

            case IrCreateClosure createClosure:
                defined.Add(createClosure.ResultName);
                break;

            case IrInvokeClosure invokeClosure when invokeClosure.ResultName != null:
                defined.Add(invokeClosure.ResultName);
                break;

            case IrLoadCapture loadCapture:
                defined.Add(loadCapture.ResultName);
                break;

            case IrHardwareRead hardwareRead:
                defined.Add(hardwareRead.ResultName);
                break;

            case IrInlineAsm inlineAsm when inlineAsm.ResultName != null:
                defined.Add(inlineAsm.ResultName);
                break;
        }

        return defined;
    }

    /// <summary>
    /// Records a variable definition
    /// </summary>
    private void AddDefinition(string variableName, IrInstruction instruction)
    {
        _definitions[variableName] = instruction;
    }

    /// <summary>
    /// Records a variable use
    /// </summary>
    private void AddUse(string variableName, IrInstruction instruction)
    {
        if (!_uses.ContainsKey(variableName))
        {
            _uses[variableName] = new List<IrInstruction>();
        }
        _uses[variableName].Add(instruction);
    }

    /// <summary>
    /// Replaces a variable reference in an instruction with a new value
    /// </summary>
    private static void ReplaceVariableInInstruction(IrInstruction instruction, string oldName, IrValue newValue)
    {
        switch (instruction)
        {
            case IrBinaryOp binaryOp:
                binaryOp.Left = ReplaceInValue(binaryOp.Left, oldName, newValue);
                binaryOp.Right = ReplaceInValue(binaryOp.Right, oldName, newValue);
                break;

            case IrCall call:
                for (int i = 0; i < call.Arguments.Count; i++)
                {
                    call.Arguments[i] = ReplaceInValue(call.Arguments[i], oldName, newValue);
                }
                break;

            case IrIndirectCall indirectCall:
                indirectCall.FunctionPointer = ReplaceInValue(indirectCall.FunctionPointer, oldName, newValue);
                for (int i = 0; i < indirectCall.Arguments.Count; i++)
                {
                    indirectCall.Arguments[i] = ReplaceInValue(indirectCall.Arguments[i], oldName, newValue);
                }
                break;

            case IrReturn ret when ret.Value != null:
                ret.Value = ReplaceInValue(ret.Value, oldName, newValue);
                break;

            case IrStore store:
                store.Value = ReplaceInValue(store.Value, oldName, newValue);
                break;

            case IrDereferenceStore derefStore:
                derefStore.Pointer = ReplaceInValue(derefStore.Pointer, oldName, newValue);
                derefStore.Value = ReplaceInValue(derefStore.Value, oldName, newValue);
                break;

            case IrDropInPlace dropInPlace:
                dropInPlace.Pointer = ReplaceInValue(dropInPlace.Pointer, oldName, newValue);
                break;

            case IrConditionalBranch condBranch:
                condBranch.Condition = ReplaceInValue(condBranch.Condition, oldName, newValue);
                break;

            case IrLocalDecl localDecl:
                if (localDecl.InitialValue != null)
                {
                    localDecl.InitialValue = ReplaceInValue(localDecl.InitialValue, oldName, newValue);
                }
                break;

            case IrAssert assert:
                assert.Condition = ReplaceInValue(assert.Condition, oldName, newValue);
                break;

            case IrIndexAccess indexAccess:
                indexAccess.Array = ReplaceInValue(indexAccess.Array, oldName, newValue);
                indexAccess.Index = ReplaceInValue(indexAccess.Index, oldName, newValue);
                if (indexAccess.Length != null)
                    indexAccess.Length = ReplaceInValue(indexAccess.Length, oldName, newValue);
                break;

            case IrSliceBoundsCheck sliceCheck:
                sliceCheck.Start = ReplaceInValue(sliceCheck.Start, oldName, newValue);
                sliceCheck.End = ReplaceInValue(sliceCheck.End, oldName, newValue);
                sliceCheck.Length = ReplaceInValue(sliceCheck.Length, oldName, newValue);
                break;

            case IrMemberAccess memberAccess:
                memberAccess.Struct = ReplaceInValue(memberAccess.Struct, oldName, newValue);
                break;

            case IrMemberStore memberStore:
                memberStore.Struct = ReplaceInValue(memberStore.Struct, oldName, newValue);
                memberStore.Value = ReplaceInValue(memberStore.Value, oldName, newValue);
                break;

            case IrIndexStore indexStore:
                indexStore.Array = ReplaceInValue(indexStore.Array, oldName, newValue);
                indexStore.Index = ReplaceInValue(indexStore.Index, oldName, newValue);
                indexStore.Value = ReplaceInValue(indexStore.Value, oldName, newValue);
                if (indexStore.Length != null)
                    indexStore.Length = ReplaceInValue(indexStore.Length, oldName, newValue);
                break;

            case IrMatch match:
                match.MatchValue = ReplaceInValue(match.MatchValue, oldName, newValue);
                break;

            case IrExtractTag extractTag:
                extractTag.EnumValue = ReplaceInValue(extractTag.EnumValue, oldName, newValue);
                break;

            case IrExtractVariantData extractData:
                extractData.EnumValue = ReplaceInValue(extractData.EnumValue, oldName, newValue);
                break;

            case IrCreateClosure createClosure:
                for (int i = 0; i < createClosure.CapturedValues.Count; i++)
                {
                    var capture = createClosure.CapturedValues[i];
                    createClosure.CapturedValues[i] =
                        (capture.VarName, ReplaceInValue(capture.Value, oldName, newValue), capture.Mode);
                }
                break;

            case IrInvokeClosure invokeClosure:
                invokeClosure.Closure = ReplaceInValue(invokeClosure.Closure, oldName, newValue);
                for (int i = 0; i < invokeClosure.Arguments.Count; i++)
                    invokeClosure.Arguments[i] = ReplaceInValue(invokeClosure.Arguments[i], oldName, newValue);
                break;

            case IrStoreCapture storeCapture:
                storeCapture.Value = ReplaceInValue(storeCapture.Value, oldName, newValue);
                break;

            case IrIndexedFieldStore indexedFieldStore:
                indexedFieldStore.Array = ReplaceInValue(indexedFieldStore.Array, oldName, newValue);
                indexedFieldStore.Index = ReplaceInValue(indexedFieldStore.Index, oldName, newValue);
                indexedFieldStore.Value = ReplaceInValue(indexedFieldStore.Value, oldName, newValue);
                if (indexedFieldStore.Length != null)
                    indexedFieldStore.Length = ReplaceInValue(indexedFieldStore.Length, oldName, newValue);
                break;

            case IrHardwareWrite hardwareWrite:
                hardwareWrite.Value = ReplaceInValue(hardwareWrite.Value, oldName, newValue);
                break;

            case IrInlineAsm inlineAsm:
                foreach (var input in inlineAsm.Inputs)
                    input.Value = ReplaceInValue(input.Value, oldName, newValue);
                break;
        }
    }

    /// <summary>
    /// Replaces a variable in a value if it matches the target name
    /// </summary>
    private static IrValue ReplaceInValue(IrValue value, string oldName, IrValue newValue)
    {
        // Direct variable replacement
        if (value is IrVariable variable && variable.Name == oldName)
        {
            return newValue;
        }

        if (value is IrGlobalVariable globalVar && globalVar.Name == oldName)
        {
            return newValue;
        }

        // Recursively handle composite values
        switch (value)
        {
            case IrDereferenceValue derefValue:
                derefValue.PointerValue = ReplaceInValue(derefValue.PointerValue, oldName, newValue);
                break;

            case IrBorrowValue borrowValue:
                borrowValue.BorrowedValue = ReplaceInValue(borrowValue.BorrowedValue, oldName, newValue);
                break;

            case IrCastValue castValue:
                castValue.Value = ReplaceInValue(castValue.Value, oldName, newValue);
                break;

            case IrPointerOffsetValue pointerOffset:
                pointerOffset.Pointer = ReplaceInValue(pointerOffset.Pointer, oldName, newValue);
                pointerOffset.Index = ReplaceInValue(pointerOffset.Index, oldName, newValue);
                break;

            case IrStructLiteral structLiteral:
                {
                    var updatedFields = new Dictionary<string, IrValue>();
                    foreach (var kvp in structLiteral.FieldValues)
                    {
                        updatedFields[kvp.Key] = ReplaceInValue(kvp.Value, oldName, newValue);
                    }
                    structLiteral.FieldValues = updatedFields;
                }
                break;

            case IrTupleLiteral tupleLiteral:
                for (int i = 0; i < tupleLiteral.Elements.Count; i++)
                {
                    tupleLiteral.Elements[i] = ReplaceInValue(tupleLiteral.Elements[i], oldName, newValue);
                }
                break;

            case IrArrayLiteral arrayLiteral:
                for (int i = 0; i < arrayLiteral.Elements.Count; i++)
                {
                    arrayLiteral.Elements[i] = ReplaceInValue(arrayLiteral.Elements[i], oldName, newValue);
                }
                break;

            case IrEnumValue enumValue:
                for (int i = 0; i < enumValue.AssociatedValues.Count; i++)
                {
                    enumValue.AssociatedValues[i] = ReplaceInValue(enumValue.AssociatedValues[i], oldName, newValue);
                }
                break;

            case IrFieldReference fieldReference:
                fieldReference.Struct = ReplaceInValue(fieldReference.Struct, oldName, newValue);
                break;

            case IrIndexedFieldAccess indexedField:
                indexedField.Array = ReplaceInValue(indexedField.Array, oldName, newValue);
                indexedField.Index = ReplaceInValue(indexedField.Index, oldName, newValue);
                if (indexedField.Length != null)
                    indexedField.Length = ReplaceInValue(indexedField.Length, oldName, newValue);
                break;

            case IrTupleElementAccess tupleElement:
                tupleElement.Tuple = ReplaceInValue(tupleElement.Tuple, oldName, newValue);
                break;

            case IrEnumTagAccess enumTag:
                enumTag.EnumValue = ReplaceInValue(enumTag.EnumValue, oldName, newValue);
                break;

            case IrEnumPayloadAccess enumPayload:
                enumPayload.EnumValue = ReplaceInValue(enumPayload.EnumValue, oldName, newValue);
                break;
        }

        return value;
    }

    /// <summary>
    /// Visitor that builds def-use chains by traversing IR instructions
    /// </summary>
    private class DefUseVisitor : IrVisitor<object?, IrInstruction>
    {
        private readonly DefUseAnalysis _analysis;

        public DefUseVisitor(DefUseAnalysis analysis)
        {
            _analysis = analysis;
        }

        // Override instruction visitors to record definitions

        public override object? VisitLocalDecl(IrLocalDecl localDecl, IrInstruction context)
        {
            _analysis.AddDefinition(localDecl.Name, context);
            base.VisitLocalDecl(localDecl, context);
            return null;
        }

        public override object? VisitStore(IrStore store, IrInstruction context)
        {
            _analysis.AddDefinition(store.VariableName, context);
            base.VisitStore(store, context);
            return null;
        }

        public override object? VisitBinaryOp(IrBinaryOp binaryOp, IrInstruction context)
        {
            _analysis.AddDefinition(binaryOp.ResultName, context);
            base.VisitBinaryOp(binaryOp, context);
            return null;
        }

        public override object? VisitCall(IrCall call, IrInstruction context)
        {
            if (call.ResultName != null)
            {
                _analysis.AddDefinition(call.ResultName, context);
            }
            base.VisitCall(call, context);
            return null;
        }

        public override object? VisitIndirectCall(IrIndirectCall indirectCall, IrInstruction context)
        {
            if (indirectCall.ResultName != null)
            {
                _analysis.AddDefinition(indirectCall.ResultName, context);
            }
            base.VisitIndirectCall(indirectCall, context);
            return null;
        }

        public override object? VisitIndexAccess(IrIndexAccess indexAccess, IrInstruction context)
        {
            _analysis.AddDefinition(indexAccess.ResultName, context);
            base.VisitIndexAccess(indexAccess, context);
            return null;
        }

        public override object? VisitMemberAccess(IrMemberAccess memberAccess, IrInstruction context)
        {
            _analysis.AddDefinition(memberAccess.ResultName, context);
            base.VisitMemberAccess(memberAccess, context);
            return null;
        }

        public override object? VisitExtractTag(IrExtractTag extractTag, IrInstruction context)
        {
            _analysis.AddDefinition(extractTag.ResultName, context);
            base.VisitExtractTag(extractTag, context);
            return null;
        }

        public override object? VisitExtractVariantData(IrExtractVariantData extractData, IrInstruction context)
        {
            _analysis.AddDefinition(extractData.ResultName, context);
            base.VisitExtractVariantData(extractData, context);
            return null;
        }

        // Override value visitors to record uses

        public override object? VisitVariable(IrVariable variable, IrInstruction context)
        {
            _analysis.AddUse(variable.Name, context);
            return null;
        }

        public override object? VisitGlobalVariable(IrGlobalVariable globalVar, IrInstruction context)
        {
            _analysis.AddUse(globalVar.Name, context);
            return null;
        }
    }
}
