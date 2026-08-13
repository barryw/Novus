namespace Novus.IR;

/// <summary>
/// Abstract base class for transformation passes that modify IR.
/// Provides methods for rewriting instructions and values in-place or creating modified copies.
/// Default implementations return unmodified nodes.
/// </summary>
public abstract class IrRewriter
{
    /// <summary>
    /// Rewrite an entire IR module
    /// </summary>
    public virtual IrModule RewriteModule(IrModule module)
    {
        // Rewrite functions
        for (int i = 0; i < module.Functions.Count; i++)
        {
            module.Functions[i] = RewriteFunction(module.Functions[i]);
        }

        // Rewrite static variables
        for (int i = 0; i < module.StaticVariables.Count; i++)
        {
            module.StaticVariables[i] = RewriteStaticVariable(module.StaticVariables[i]);
        }

        return module;
    }

    /// <summary>
    /// Rewrite a function definition
    /// </summary>
    public virtual IrFunction RewriteFunction(IrFunction function)
    {
        // Rewrite basic blocks
        for (int i = 0; i < function.BasicBlocks.Count; i++)
        {
            function.BasicBlocks[i] = RewriteBasicBlock(function.BasicBlocks[i]);
        }

        // Rewrite deferred blocks
        for (int i = 0; i < function.DeferredBlocks.Count; i++)
        {
            function.DeferredBlocks[i] = RewriteBasicBlock(function.DeferredBlocks[i]);
        }

        return function;
    }

    /// <summary>
    /// Rewrite a basic block
    /// </summary>
    public virtual IrBasicBlock RewriteBasicBlock(IrBasicBlock block)
    {
        // Rewrite phi functions first (they execute at block entry in SSA form)
        for (int i = 0; i < block.PhiFunctions.Count; i++)
        {
            var rewritten = RewritePhi(block.PhiFunctions[i]);
            if (rewritten != null)
            {
                block.PhiFunctions[i] = rewritten;
            }
            else
            {
                // Null means delete the phi function
                block.PhiFunctions.RemoveAt(i);
                i--;
            }
        }

        // Rewrite instructions in-place
        for (int i = 0; i < block.Instructions.Count; i++)
        {
            var rewritten = RewriteInstruction(block.Instructions[i]);
            if (rewritten != null)
            {
                block.Instructions[i] = rewritten;
            }
            else
            {
                // Null means delete the instruction
                block.Instructions.RemoveAt(i);
                i--;
            }
        }

        return block;
    }

    /// <summary>
    /// Rewrite an instruction (dispatcher).
    /// Return null to delete the instruction, return the same instance to keep it unchanged,
    /// or return a new instance to replace it.
    /// </summary>
    public virtual IrInstruction? RewriteInstruction(IrInstruction instruction)
    {
        return instruction switch
        {
            IrPhi phi => RewritePhi(phi),
            IrBinaryOp binaryOp => RewriteBinaryOp(binaryOp),
            IrCall call => RewriteCall(call),
            IrIndirectCall indirectCall => RewriteIndirectCall(indirectCall),
            IrReturn ret => RewriteReturn(ret),
            IrStore store => RewriteStore(store),
            IrDereferenceStore derefStore => RewriteDereferenceStore(derefStore),
            IrLabel label => RewriteLabel(label),
            IrBranch branch => RewriteBranch(branch),
            IrConditionalBranch condBranch => RewriteConditionalBranch(condBranch),
            IrLocalDecl localDecl => RewriteLocalDecl(localDecl),
            IrDefer defer => RewriteDefer(defer),
            IrAssert assert => RewriteAssert(assert),
            IrPanic panic => RewritePanic(panic),
            IrStructuredForLoopHint loopHint => RewriteStructuredForLoopHint(loopHint),
            IrSliceBoundsCheck sliceCheck => RewriteSliceBoundsCheck(sliceCheck),
            IrIndexAccess indexAccess => RewriteIndexAccess(indexAccess),
            IrMemberAccess memberAccess => RewriteMemberAccess(memberAccess),
            IrMemberStore memberStore => RewriteMemberStore(memberStore),
            IrIndexStore indexStore => RewriteIndexStore(indexStore),
            IrMatch match => RewriteMatch(match),
            IrExtractTag extractTag => RewriteExtractTag(extractTag),
            IrExtractVariantData extractData => RewriteExtractVariantData(extractData),
            _ => instruction
        };
    }

    /// <summary>
    /// Rewrite a value (dispatcher).
    /// Return the same instance to keep it unchanged, or return a new instance to replace it.
    /// </summary>
    public virtual IrValue RewriteValue(IrValue value)
    {
        return value switch
        {
            IrConstant constant => RewriteConstant(constant),
            IrBoolConstant boolConstant => RewriteBoolConstant(boolConstant),
            IrFloatConstant floatConstant => RewriteFloatConstant(floatConstant),
            IrFixedConstant fixedConstant => RewriteFixedConstant(fixedConstant),
            IrStringLiteral stringLiteral => RewriteStringLiteral(stringLiteral),
            IrVariable variable => RewriteVariable(variable),
            IrGlobalVariable globalVar => RewriteGlobalVariable(globalVar),
            IrStructLiteral structLiteral => RewriteStructLiteral(structLiteral),
            IrArrayLiteral arrayLiteral => RewriteArrayLiteral(arrayLiteral),
            IrDereferenceValue derefValue => RewriteDereferenceValue(derefValue),
            IrBorrowValue borrowValue => RewriteBorrowValue(borrowValue),
            IrFieldReference fieldRef => RewriteFieldReference(fieldRef),
            IrIndexedFieldAccess indexedField => RewriteIndexedFieldAccess(indexedField),
            IrTupleElementAccess tupleElement => RewriteTupleElementAccess(tupleElement),
            IrCastValue castValue => RewriteCastValue(castValue),
            IrPointerOffsetValue pointerOffset => RewritePointerOffsetValue(pointerOffset),
            IrFunctionAddress funcAddr => RewriteFunctionAddress(funcAddr),
            IrEnumValue enumValue => RewriteEnumValue(enumValue),
            IrEnumConstructor enumCtor => RewriteEnumConstructor(enumCtor),
            IrTurboFishType turboFish => RewriteTurboFishType(turboFish),
            IrGenericAssociatedFunction genAssocFunc => RewriteGenericAssociatedFunction(genAssocFunc),
            IrFunctionRef funcRef => RewriteFunctionRef(funcRef),
            _ => value
        };
    }

    // Instruction rewrite methods (default: return unmodified)

    /// <summary>
    /// Rewrite a phi function (SSA form only)
    /// </summary>
    public virtual IrPhi? RewritePhi(IrPhi phi)
    {
        // Rewrite all incoming values
        for (int i = 0; i < phi.IncomingValues.Count; i++)
        {
            phi.IncomingValues[i] = RewriteValue(phi.IncomingValues[i]);
        }
        return phi;
    }

    /// <summary>
    /// Rewrite a binary operation instruction
    /// </summary>
    public virtual IrInstruction? RewriteBinaryOp(IrBinaryOp binaryOp)
    {
        binaryOp.Left = RewriteValue(binaryOp.Left);
        binaryOp.Right = RewriteValue(binaryOp.Right);
        return binaryOp;
    }

    /// <summary>
    /// Rewrite a function call instruction
    /// </summary>
    public virtual IrInstruction? RewriteCall(IrCall call)
    {
        for (int i = 0; i < call.Arguments.Count; i++)
        {
            call.Arguments[i] = RewriteValue(call.Arguments[i]);
        }
        return call;
    }

    /// <summary>
    /// Rewrite an indirect function call instruction
    /// </summary>
    public virtual IrInstruction? RewriteIndirectCall(IrIndirectCall indirectCall)
    {
        indirectCall.FunctionPointer = RewriteValue(indirectCall.FunctionPointer);
        for (int i = 0; i < indirectCall.Arguments.Count; i++)
        {
            indirectCall.Arguments[i] = RewriteValue(indirectCall.Arguments[i]);
        }
        return indirectCall;
    }

    /// <summary>
    /// Rewrite a return instruction
    /// </summary>
    public virtual IrInstruction? RewriteReturn(IrReturn ret)
    {
        if (ret.Value != null)
        {
            ret.Value = RewriteValue(ret.Value);
        }
        return ret;
    }

    /// <summary>
    /// Rewrite a store instruction
    /// </summary>
    public virtual IrInstruction? RewriteStore(IrStore store)
    {
        store.Value = RewriteValue(store.Value);
        return store;
    }

    /// <summary>
    /// Rewrite a dereference store instruction
    /// </summary>
    public virtual IrInstruction? RewriteDereferenceStore(IrDereferenceStore derefStore)
    {
        derefStore.Pointer = RewriteValue(derefStore.Pointer);
        derefStore.Value = RewriteValue(derefStore.Value);
        return derefStore;
    }

    /// <summary>
    /// Rewrite a label instruction
    /// </summary>
    public virtual IrInstruction? RewriteLabel(IrLabel label)
    {
        return label;
    }

    /// <summary>
    /// Rewrite an unconditional branch instruction
    /// </summary>
    public virtual IrInstruction? RewriteBranch(IrBranch branch)
    {
        return branch;
    }

    /// <summary>
    /// Rewrite a conditional branch instruction
    /// </summary>
    public virtual IrInstruction? RewriteConditionalBranch(IrConditionalBranch condBranch)
    {
        condBranch.Condition = RewriteValue(condBranch.Condition);
        return condBranch;
    }

    /// <summary>
    /// Rewrite a local variable declaration instruction
    /// </summary>
    public virtual IrInstruction? RewriteLocalDecl(IrLocalDecl localDecl)
    {
        if (localDecl.InitialValue != null)
        {
            localDecl.InitialValue = RewriteValue(localDecl.InitialValue);
        }
        return localDecl;
    }

    /// <summary>
    /// Rewrite a defer instruction
    /// </summary>
    public virtual IrInstruction? RewriteDefer(IrDefer defer)
    {
        defer.DeferredBlock = RewriteBasicBlock(defer.DeferredBlock);
        return defer;
    }

    /// <summary>
    /// Rewrite an assert instruction
    /// </summary>
    public virtual IrInstruction? RewriteAssert(IrAssert assert)
    {
        assert.Condition = RewriteValue(assert.Condition);
        return assert;
    }

    /// <summary>
    /// Rewrite a panic instruction
    /// </summary>
    public virtual IrInstruction? RewritePanic(IrPanic panic)
    {
        return panic;
    }

    /// <summary>
    /// Rewrite a structured for-loop hint instruction
    /// </summary>
    public virtual IrInstruction? RewriteStructuredForLoopHint(IrStructuredForLoopHint loopHint)
    {
        return loopHint;
    }

    /// <summary>
    /// Rewrite an array index access instruction
    /// </summary>
    public virtual IrInstruction? RewriteIndexAccess(IrIndexAccess indexAccess)
    {
        indexAccess.Array = RewriteValue(indexAccess.Array);
        indexAccess.Index = RewriteValue(indexAccess.Index);
        if (indexAccess.Length != null)
            indexAccess.Length = RewriteValue(indexAccess.Length);
        return indexAccess;
    }

    public virtual IrInstruction? RewriteSliceBoundsCheck(IrSliceBoundsCheck sliceCheck)
    {
        sliceCheck.Start = RewriteValue(sliceCheck.Start);
        sliceCheck.End = RewriteValue(sliceCheck.End);
        sliceCheck.Length = RewriteValue(sliceCheck.Length);
        return sliceCheck;
    }

    /// <summary>
    /// Rewrite a struct member access instruction
    /// </summary>
    public virtual IrInstruction? RewriteMemberAccess(IrMemberAccess memberAccess)
    {
        memberAccess.Struct = RewriteValue(memberAccess.Struct);
        return memberAccess;
    }

    /// <summary>
    /// Rewrite a struct member store instruction
    /// </summary>
    public virtual IrInstruction? RewriteMemberStore(IrMemberStore memberStore)
    {
        memberStore.Struct = RewriteValue(memberStore.Struct);
        memberStore.Value = RewriteValue(memberStore.Value);
        return memberStore;
    }

    /// <summary>
    /// Rewrite an array index store instruction
    /// </summary>
    public virtual IrInstruction? RewriteIndexStore(IrIndexStore indexStore)
    {
        indexStore.Array = RewriteValue(indexStore.Array);
        indexStore.Index = RewriteValue(indexStore.Index);
        indexStore.Value = RewriteValue(indexStore.Value);
        if (indexStore.Length != null)
            indexStore.Length = RewriteValue(indexStore.Length);
        return indexStore;
    }

    /// <summary>
    /// Rewrite a match expression instruction
    /// </summary>
    public virtual IrInstruction? RewriteMatch(IrMatch match)
    {
        match.MatchValue = RewriteValue(match.MatchValue);
        for (int i = 0; i < match.Arms.Count; i++)
        {
            match.Arms[i] = RewriteMatchArm(match.Arms[i]);
        }
        return match;
    }

    /// <summary>
    /// Rewrite a match arm
    /// </summary>
    public virtual IrMatchArm RewriteMatchArm(IrMatchArm arm)
    {
        arm.Pattern = RewritePattern(arm.Pattern);
        return arm;
    }

    /// <summary>
    /// Rewrite a pattern
    /// </summary>
    public virtual IrPattern RewritePattern(IrPattern pattern)
    {
        return pattern switch
        {
            IrWildcardPattern wildcard => RewriteWildcardPattern(wildcard),
            IrVariantPattern variant => RewriteVariantPattern(variant),
            IrLiteralPattern literal => RewriteLiteralPattern(literal),
            _ => pattern
        };
    }

    /// <summary>
    /// Rewrite a wildcard pattern
    /// </summary>
    public virtual IrPattern RewriteWildcardPattern(IrWildcardPattern wildcard)
    {
        return wildcard;
    }

    /// <summary>
    /// Rewrite a variant pattern
    /// </summary>
    public virtual IrPattern RewriteVariantPattern(IrVariantPattern variant)
    {
        return variant;
    }

    /// <summary>
    /// Rewrite a literal pattern
    /// </summary>
    public virtual IrPattern RewriteLiteralPattern(IrLiteralPattern literal)
    {
        literal.LiteralValue = RewriteValue(literal.LiteralValue);
        return literal;
    }

    /// <summary>
    /// Rewrite an extract tag instruction
    /// </summary>
    public virtual IrInstruction? RewriteExtractTag(IrExtractTag extractTag)
    {
        extractTag.EnumValue = RewriteValue(extractTag.EnumValue);
        return extractTag;
    }

    /// <summary>
    /// Rewrite an extract variant data instruction
    /// </summary>
    public virtual IrInstruction? RewriteExtractVariantData(IrExtractVariantData extractData)
    {
        extractData.EnumValue = RewriteValue(extractData.EnumValue);
        return extractData;
    }

    // Value rewrite methods (default: return unmodified)

    /// <summary>
    /// Rewrite an integer constant value
    /// </summary>
    public virtual IrValue RewriteConstant(IrConstant constant)
    {
        return constant;
    }

    /// <summary>
    /// Rewrite a boolean constant value
    /// </summary>
    public virtual IrValue RewriteBoolConstant(IrBoolConstant boolConstant)
    {
        return boolConstant;
    }

    /// <summary>
    /// Rewrite a floating-point constant value
    /// </summary>
    public virtual IrValue RewriteFloatConstant(IrFloatConstant floatConstant)
    {
        return floatConstant;
    }

    /// <summary>
    /// Rewrite a fixed-point constant value
    /// </summary>
    public virtual IrValue RewriteFixedConstant(IrFixedConstant fixedConstant)
    {
        return fixedConstant;
    }

    /// <summary>
    /// Rewrite a string literal value
    /// </summary>
    public virtual IrValue RewriteStringLiteral(IrStringLiteral stringLiteral)
    {
        return stringLiteral;
    }

    /// <summary>
    /// Rewrite a variable reference
    /// </summary>
    public virtual IrValue RewriteVariable(IrVariable variable)
    {
        return variable;
    }

    /// <summary>
    /// Rewrite a global variable reference
    /// </summary>
    public virtual IrValue RewriteGlobalVariable(IrGlobalVariable globalVar)
    {
        return globalVar;
    }

    /// <summary>
    /// Rewrite a struct literal value
    /// </summary>
    public virtual IrValue RewriteStructLiteral(IrStructLiteral structLiteral)
    {
        var rewrittenFields = new Dictionary<string, IrValue>();
        foreach (var kvp in structLiteral.FieldValues)
        {
            rewrittenFields[kvp.Key] = RewriteValue(kvp.Value);
        }
        structLiteral.FieldValues = rewrittenFields;
        return structLiteral;
    }

    /// <summary>
    /// Rewrite an array literal value
    /// </summary>
    public virtual IrValue RewriteArrayLiteral(IrArrayLiteral arrayLiteral)
    {
        for (int i = 0; i < arrayLiteral.Elements.Count; i++)
        {
            arrayLiteral.Elements[i] = RewriteValue(arrayLiteral.Elements[i]);
        }
        return arrayLiteral;
    }

    /// <summary>
    /// Rewrite a dereference value
    /// </summary>
    public virtual IrValue RewriteDereferenceValue(IrDereferenceValue derefValue)
    {
        derefValue.PointerValue = RewriteValue(derefValue.PointerValue);
        return derefValue;
    }

    /// <summary>
    /// Rewrite a borrow value
    /// </summary>
    public virtual IrValue RewriteBorrowValue(IrBorrowValue borrowValue)
    {
        borrowValue.BorrowedValue = RewriteValue(borrowValue.BorrowedValue);
        return borrowValue;
    }

    /// <summary>
    /// Rewrite a field reference
    /// </summary>
    public virtual IrValue RewriteFieldReference(IrFieldReference fieldRef)
    {
        fieldRef.Struct = RewriteValue(fieldRef.Struct);
        return fieldRef;
    }

    public virtual IrValue RewriteIndexedFieldAccess(IrIndexedFieldAccess indexedField)
    {
        indexedField.Array = RewriteValue(indexedField.Array);
        indexedField.Index = RewriteValue(indexedField.Index);
        if (indexedField.Length != null)
            indexedField.Length = RewriteValue(indexedField.Length);
        return indexedField;
    }

    public virtual IrValue RewriteTupleElementAccess(IrTupleElementAccess tupleElement)
    {
        tupleElement.Tuple = RewriteValue(tupleElement.Tuple);
        return tupleElement;
    }

    /// <summary>
    /// Rewrite a cast value
    /// </summary>
    public virtual IrValue RewriteCastValue(IrCastValue castValue)
    {
        castValue.Value = RewriteValue(castValue.Value);
        return castValue;
    }

    public virtual IrValue RewritePointerOffsetValue(IrPointerOffsetValue pointerOffset)
    {
        pointerOffset.Pointer = RewriteValue(pointerOffset.Pointer);
        pointerOffset.Index = RewriteValue(pointerOffset.Index);
        return pointerOffset;
    }

    /// <summary>
    /// Rewrite a function address value
    /// </summary>
    public virtual IrValue RewriteFunctionAddress(IrFunctionAddress funcAddr)
    {
        return funcAddr;
    }

    /// <summary>
    /// Rewrite an enum value
    /// </summary>
    public virtual IrValue RewriteEnumValue(IrEnumValue enumValue)
    {
        for (int i = 0; i < enumValue.AssociatedValues.Count; i++)
        {
            enumValue.AssociatedValues[i] = RewriteValue(enumValue.AssociatedValues[i]);
        }
        return enumValue;
    }

    /// <summary>
    /// Rewrite an enum constructor
    /// </summary>
    public virtual IrValue RewriteEnumConstructor(IrEnumConstructor enumCtor)
    {
        return enumCtor;
    }

    /// <summary>
    /// Rewrite a turbo-fish type
    /// </summary>
    public virtual IrValue RewriteTurboFishType(IrTurboFishType turboFish)
    {
        return turboFish;
    }

    /// <summary>
    /// Rewrite a generic associated function
    /// </summary>
    public virtual IrValue RewriteGenericAssociatedFunction(IrGenericAssociatedFunction genAssocFunc)
    {
        return genAssocFunc;
    }

    /// <summary>
    /// Rewrite a function reference
    /// </summary>
    public virtual IrValue RewriteFunctionRef(IrFunctionRef funcRef)
    {
        return funcRef;
    }

    /// <summary>
    /// Rewrite a static variable
    /// </summary>
    public virtual IrStaticVariable RewriteStaticVariable(IrStaticVariable staticVar)
    {
        staticVar.InitialValue = RewriteValue(staticVar.InitialValue);
        return staticVar;
    }
}
