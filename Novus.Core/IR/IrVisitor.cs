namespace Novus.IR;

/// <summary>
/// Abstract base class for read-only IR traversal (analysis passes).
/// Implements the visitor pattern for all IR instructions and values.
/// </summary>
/// <typeparam name="TResult">The return type of visit methods</typeparam>
/// <typeparam name="TContext">Context data passed through the traversal</typeparam>
public abstract class IrVisitor<TResult, TContext>
{
    /// <summary>
    /// Visit an entire IR module
    /// </summary>
    public virtual TResult VisitModule(IrModule module, TContext context)
    {
        foreach (var function in module.Functions)
        {
            VisitFunction(function, context);
        }

        foreach (var enumType in module.Enums)
        {
            VisitEnumType(enumType, context);
        }

        foreach (var structType in module.Structs)
        {
            VisitStructType(structType, context);
        }

        foreach (var staticVar in module.StaticVariables)
        {
            VisitStaticVariable(staticVar, context);
        }

        foreach (var externalVar in module.ExternalVariables)
        {
            VisitExternalVariable(externalVar, context);
        }

        return default!;
    }

    /// <summary>
    /// Visit a function definition
    /// </summary>
    public virtual TResult VisitFunction(IrFunction function, TContext context)
    {
        foreach (var block in function.BasicBlocks)
        {
            VisitBasicBlock(block, context);
        }

        foreach (var deferredBlock in function.DeferredBlocks)
        {
            VisitBasicBlock(deferredBlock, context);
        }

        return default!;
    }

    /// <summary>
    /// Visit a basic block
    /// </summary>
    public virtual TResult VisitBasicBlock(IrBasicBlock block, TContext context)
    {
        // Visit phi functions first (they execute at block entry in SSA form)
        foreach (var phi in block.PhiFunctions)
        {
            VisitPhi(phi, context);
        }

        foreach (var instruction in block.Instructions)
        {
            VisitInstruction(instruction, context);
        }

        return default!;
    }

    /// <summary>
    /// Visit an instruction (dispatcher)
    /// </summary>
    public virtual TResult VisitInstruction(IrInstruction instruction, TContext context)
    {
        return instruction switch
        {
            IrPhi phi => VisitPhi(phi, context),
            IrBinaryOp binaryOp => VisitBinaryOp(binaryOp, context),
            IrCall call => VisitCall(call, context),
            IrIndirectCall indirectCall => VisitIndirectCall(indirectCall, context),
            IrReturn ret => VisitReturn(ret, context),
            IrStore store => VisitStore(store, context),
            IrDereferenceStore derefStore => VisitDereferenceStore(derefStore, context),
            IrLabel label => VisitLabel(label, context),
            IrBranch branch => VisitBranch(branch, context),
            IrConditionalBranch condBranch => VisitConditionalBranch(condBranch, context),
            IrLocalDecl localDecl => VisitLocalDecl(localDecl, context),
            IrDefer defer => VisitDefer(defer, context),
            IrAssert assert => VisitAssert(assert, context),
            IrPanic panic => VisitPanic(panic, context),
            IrStructuredForLoopHint loopHint => VisitStructuredForLoopHint(loopHint, context),
            IrIndexAccess indexAccess => VisitIndexAccess(indexAccess, context),
            IrMemberAccess memberAccess => VisitMemberAccess(memberAccess, context),
            IrMemberStore memberStore => VisitMemberStore(memberStore, context),
            IrIndexStore indexStore => VisitIndexStore(indexStore, context),
            IrMatch match => VisitMatch(match, context),
            IrExtractTag extractTag => VisitExtractTag(extractTag, context),
            IrExtractVariantData extractData => VisitExtractVariantData(extractData, context),
            _ => VisitUnknownInstruction(instruction, context)
        };
    }

    /// <summary>
    /// Visit a value (dispatcher)
    /// </summary>
    public virtual TResult VisitValue(IrValue value, TContext context)
    {
        return value switch
        {
            IrConstant constant => VisitConstant(constant, context),
            IrBoolConstant boolConstant => VisitBoolConstant(boolConstant, context),
            IrFloatConstant floatConstant => VisitFloatConstant(floatConstant, context),
            IrFixedConstant fixedConstant => VisitFixedConstant(fixedConstant, context),
            IrStringLiteral stringLiteral => VisitStringLiteral(stringLiteral, context),
            IrVariable variable => VisitVariable(variable, context),
            IrGlobalVariable globalVar => VisitGlobalVariable(globalVar, context),
            IrStructLiteral structLiteral => VisitStructLiteral(structLiteral, context),
            IrArrayLiteral arrayLiteral => VisitArrayLiteral(arrayLiteral, context),
            IrDereferenceValue derefValue => VisitDereferenceValue(derefValue, context),
            IrBorrowValue borrowValue => VisitBorrowValue(borrowValue, context),
            IrFieldReference fieldRef => VisitFieldReference(fieldRef, context),
            IrIndexedFieldAccess indexedField => VisitIndexedFieldAccess(indexedField, context),
            IrCastValue castValue => VisitCastValue(castValue, context),
            IrFunctionAddress funcAddr => VisitFunctionAddress(funcAddr, context),
            IrEnumValue enumValue => VisitEnumValue(enumValue, context),
            IrEnumConstructor enumCtor => VisitEnumConstructor(enumCtor, context),
            IrTurboFishType turboFish => VisitTurboFishType(turboFish, context),
            IrGenericAssociatedFunction genAssocFunc => VisitGenericAssociatedFunction(genAssocFunc, context),
            IrFunctionRef funcRef => VisitFunctionRef(funcRef, context),
            _ => VisitUnknownValue(value, context)
        };
    }

    // Instruction visit methods

    /// <summary>
    /// Visit a binary operation instruction
    /// </summary>
    public virtual TResult VisitBinaryOp(IrBinaryOp binaryOp, TContext context)
    {
        VisitValue(binaryOp.Left, context);
        VisitValue(binaryOp.Right, context);
        return default!;
    }

    /// <summary>
    /// Visit a function call instruction
    /// </summary>
    public virtual TResult VisitCall(IrCall call, TContext context)
    {
        foreach (var arg in call.Arguments)
        {
            VisitValue(arg, context);
        }
        return default!;
    }

    /// <summary>
    /// Visit an indirect function call instruction
    /// </summary>
    public virtual TResult VisitIndirectCall(IrIndirectCall indirectCall, TContext context)
    {
        VisitValue(indirectCall.FunctionPointer, context);
        foreach (var arg in indirectCall.Arguments)
        {
            VisitValue(arg, context);
        }
        return default!;
    }

    /// <summary>
    /// Visit a return instruction
    /// </summary>
    public virtual TResult VisitReturn(IrReturn ret, TContext context)
    {
        if (ret.Value != null)
        {
            VisitValue(ret.Value, context);
        }
        return default!;
    }

    /// <summary>
    /// Visit a store instruction
    /// </summary>
    public virtual TResult VisitStore(IrStore store, TContext context)
    {
        VisitValue(store.Value, context);
        return default!;
    }

    /// <summary>
    /// Visit a dereference store instruction
    /// </summary>
    public virtual TResult VisitDereferenceStore(IrDereferenceStore derefStore, TContext context)
    {
        VisitValue(derefStore.Pointer, context);
        VisitValue(derefStore.Value, context);
        return default!;
    }

    /// <summary>
    /// Visit a label instruction
    /// </summary>
    public virtual TResult VisitLabel(IrLabel label, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit an unconditional branch instruction
    /// </summary>
    public virtual TResult VisitBranch(IrBranch branch, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a conditional branch instruction
    /// </summary>
    public virtual TResult VisitConditionalBranch(IrConditionalBranch condBranch, TContext context)
    {
        VisitValue(condBranch.Condition, context);
        return default!;
    }

    /// <summary>
    /// Visit a phi function (SSA form only)
    /// </summary>
    public virtual TResult VisitPhi(IrPhi phi, TContext context)
    {
        // Visit all incoming values
        foreach (var value in phi.IncomingValues)
        {
            VisitValue(value, context);
        }
        return default!;
    }

    /// <summary>
    /// Visit a local variable declaration instruction
    /// </summary>
    public virtual TResult VisitLocalDecl(IrLocalDecl localDecl, TContext context)
    {
        if (localDecl.InitialValue != null)
        {
            VisitValue(localDecl.InitialValue, context);
        }
        return default!;
    }

    /// <summary>
    /// Visit a defer instruction
    /// </summary>
    public virtual TResult VisitDefer(IrDefer defer, TContext context)
    {
        VisitBasicBlock(defer.DeferredBlock, context);
        return default!;
    }

    /// <summary>
    /// Visit an assert instruction
    /// </summary>
    public virtual TResult VisitAssert(IrAssert assert, TContext context)
    {
        VisitValue(assert.Condition, context);
        return default!;
    }

    /// <summary>
    /// Visit a panic instruction
    /// </summary>
    public virtual TResult VisitPanic(IrPanic panic, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a structured for-loop hint instruction
    /// </summary>
    public virtual TResult VisitStructuredForLoopHint(IrStructuredForLoopHint loopHint, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit an array index access instruction
    /// </summary>
    public virtual TResult VisitIndexAccess(IrIndexAccess indexAccess, TContext context)
    {
        VisitValue(indexAccess.Array, context);
        VisitValue(indexAccess.Index, context);
        return default!;
    }

    /// <summary>
    /// Visit a struct member access instruction
    /// </summary>
    public virtual TResult VisitMemberAccess(IrMemberAccess memberAccess, TContext context)
    {
        VisitValue(memberAccess.Struct, context);
        return default!;
    }

    /// <summary>
    /// Visit a struct member store instruction
    /// </summary>
    public virtual TResult VisitMemberStore(IrMemberStore memberStore, TContext context)
    {
        VisitValue(memberStore.Struct, context);
        VisitValue(memberStore.Value, context);
        return default!;
    }

    /// <summary>
    /// Visit an array index store instruction
    /// </summary>
    public virtual TResult VisitIndexStore(IrIndexStore indexStore, TContext context)
    {
        VisitValue(indexStore.Array, context);
        VisitValue(indexStore.Index, context);
        VisitValue(indexStore.Value, context);
        return default!;
    }

    /// <summary>
    /// Visit a match expression instruction
    /// </summary>
    public virtual TResult VisitMatch(IrMatch match, TContext context)
    {
        VisitValue(match.MatchValue, context);
        foreach (var arm in match.Arms)
        {
            VisitMatchArm(arm, context);
        }
        return default!;
    }

    /// <summary>
    /// Visit a match arm
    /// </summary>
    public virtual TResult VisitMatchArm(IrMatchArm arm, TContext context)
    {
        VisitPattern(arm.Pattern, context);
        return default!;
    }

    /// <summary>
    /// Visit a pattern
    /// </summary>
    public virtual TResult VisitPattern(IrPattern pattern, TContext context)
    {
        return pattern switch
        {
            IrWildcardPattern wildcard => VisitWildcardPattern(wildcard, context),
            IrVariantPattern variant => VisitVariantPattern(variant, context),
            IrLiteralPattern literal => VisitLiteralPattern(literal, context),
            _ => default!
        };
    }

    /// <summary>
    /// Visit a wildcard pattern
    /// </summary>
    public virtual TResult VisitWildcardPattern(IrWildcardPattern wildcard, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a variant pattern
    /// </summary>
    public virtual TResult VisitVariantPattern(IrVariantPattern variant, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a literal pattern
    /// </summary>
    public virtual TResult VisitLiteralPattern(IrLiteralPattern literal, TContext context)
    {
        VisitValue(literal.LiteralValue, context);
        return default!;
    }

    /// <summary>
    /// Visit an extract tag instruction
    /// </summary>
    public virtual TResult VisitExtractTag(IrExtractTag extractTag, TContext context)
    {
        VisitValue(extractTag.EnumValue, context);
        return default!;
    }

    /// <summary>
    /// Visit an extract variant data instruction
    /// </summary>
    public virtual TResult VisitExtractVariantData(IrExtractVariantData extractData, TContext context)
    {
        VisitValue(extractData.EnumValue, context);
        return default!;
    }

    /// <summary>
    /// Visit an unknown instruction (fallback)
    /// </summary>
    public virtual TResult VisitUnknownInstruction(IrInstruction instruction, TContext context)
    {
        return default!;
    }

    // Value visit methods

    /// <summary>
    /// Visit an integer constant value
    /// </summary>
    public virtual TResult VisitConstant(IrConstant constant, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a boolean constant value
    /// </summary>
    public virtual TResult VisitBoolConstant(IrBoolConstant boolConstant, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a floating-point constant value
    /// </summary>
    public virtual TResult VisitFloatConstant(IrFloatConstant floatConstant, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a fixed-point constant value
    /// </summary>
    public virtual TResult VisitFixedConstant(IrFixedConstant fixedConstant, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a string literal value
    /// </summary>
    public virtual TResult VisitStringLiteral(IrStringLiteral stringLiteral, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a variable reference
    /// </summary>
    public virtual TResult VisitVariable(IrVariable variable, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a global variable reference
    /// </summary>
    public virtual TResult VisitGlobalVariable(IrGlobalVariable globalVar, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a struct literal value
    /// </summary>
    public virtual TResult VisitStructLiteral(IrStructLiteral structLiteral, TContext context)
    {
        foreach (var fieldValue in structLiteral.FieldValues.Values)
        {
            VisitValue(fieldValue, context);
        }
        return default!;
    }

    /// <summary>
    /// Visit an array literal value
    /// </summary>
    public virtual TResult VisitArrayLiteral(IrArrayLiteral arrayLiteral, TContext context)
    {
        foreach (var element in arrayLiteral.Elements)
        {
            VisitValue(element, context);
        }
        return default!;
    }

    /// <summary>
    /// Visit a dereference value
    /// </summary>
    public virtual TResult VisitDereferenceValue(IrDereferenceValue derefValue, TContext context)
    {
        VisitValue(derefValue.PointerValue, context);
        return default!;
    }

    /// <summary>
    /// Visit a borrow value
    /// </summary>
    public virtual TResult VisitBorrowValue(IrBorrowValue borrowValue, TContext context)
    {
        VisitValue(borrowValue.BorrowedValue, context);
        return default!;
    }

    /// <summary>
    /// Visit a field reference
    /// </summary>
    public virtual TResult VisitFieldReference(IrFieldReference fieldRef, TContext context)
    {
        VisitValue(fieldRef.Struct, context);
        return default!;
    }

    /// <summary>
    /// Visit an indexed field access (array[index].field optimization)
    /// </summary>
    public virtual TResult VisitIndexedFieldAccess(IrIndexedFieldAccess indexedField, TContext context)
    {
        VisitValue(indexedField.Array, context);
        VisitValue(indexedField.Index, context);
        return default!;
    }

    /// <summary>
    /// Visit a cast value
    /// </summary>
    public virtual TResult VisitCastValue(IrCastValue castValue, TContext context)
    {
        VisitValue(castValue.Value, context);
        return default!;
    }

    /// <summary>
    /// Visit a function address value
    /// </summary>
    public virtual TResult VisitFunctionAddress(IrFunctionAddress funcAddr, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit an enum value
    /// </summary>
    public virtual TResult VisitEnumValue(IrEnumValue enumValue, TContext context)
    {
        foreach (var associatedValue in enumValue.AssociatedValues)
        {
            VisitValue(associatedValue, context);
        }
        return default!;
    }

    /// <summary>
    /// Visit an enum constructor
    /// </summary>
    public virtual TResult VisitEnumConstructor(IrEnumConstructor enumCtor, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a turbo-fish type
    /// </summary>
    public virtual TResult VisitTurboFishType(IrTurboFishType turboFish, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a generic associated function
    /// </summary>
    public virtual TResult VisitGenericAssociatedFunction(IrGenericAssociatedFunction genAssocFunc, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a function reference
    /// </summary>
    public virtual TResult VisitFunctionRef(IrFunctionRef funcRef, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit an unknown value (fallback)
    /// </summary>
    public virtual TResult VisitUnknownValue(IrValue value, TContext context)
    {
        return default!;
    }

    // Type visit methods

    /// <summary>
    /// Visit an enum type definition
    /// </summary>
    public virtual TResult VisitEnumType(IrEnumType enumType, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a struct type definition
    /// </summary>
    public virtual TResult VisitStructType(IrStructType structType, TContext context)
    {
        return default!;
    }

    /// <summary>
    /// Visit a static variable
    /// </summary>
    public virtual TResult VisitStaticVariable(IrStaticVariable staticVar, TContext context)
    {
        VisitValue(staticVar.InitialValue, context);
        return default!;
    }

    /// <summary>
    /// Visit an external variable
    /// </summary>
    public virtual TResult VisitExternalVariable(IrExternalVariable externalVar, TContext context)
    {
        return default!;
    }
}
