using Antlr4.Runtime.Misc;
using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;

namespace Novus.Frontend;

/// <summary>
/// IrBuilder partial class containing pattern matching logic.
/// This file contains methods for lowering match expressions into IR control flow.
/// </summary>
public partial class IrBuilder
{
    // Helper struct to represent an expanded match arm (after flattening pipe patterns)
    private class ExpandedMatchArm
    {
        public NovusParser.PatternContext Pattern { get; }
        public NovusParser.MatchArmContext OriginalArm { get; }

        public ExpandedMatchArm(NovusParser.PatternContext pattern, NovusParser.MatchArmContext originalArm)
        {
            Pattern = pattern;
            OriginalArm = originalArm;
        }
    }

    // Recursively flatten pipe patterns into a list of simple patterns
    private List<NovusParser.PatternContext> FlattenPipePattern(NovusParser.PatternContext pattern)
    {
        if (pattern is NovusParser.PipePatternContext pipePattern)
        {
            // Recursively flatten both sides of the pipe
            var leftPatterns = FlattenPipePattern(pipePattern.pattern(0));
            var rightPatterns = FlattenPipePattern(pipePattern.pattern(1));

            // Combine the results
            var result = new List<NovusParser.PatternContext>();
            result.AddRange(leftPatterns);
            result.AddRange(rightPatterns);
            return result;
        }
        else
        {
            // Base case: not a pipe pattern, return as single-element list
            return new List<NovusParser.PatternContext> { pattern };
        }
    }

    // Expand match arms that contain pipe patterns into multiple arms
    private List<ExpandedMatchArm> ExpandMatchArms(NovusParser.MatchArmContext[] arms)
    {
        var expandedArms = new List<ExpandedMatchArm>();

        foreach (var arm in arms)
        {
            var patterns = FlattenPipePattern(arm.pattern());

            // Create an expanded arm for each pattern
            foreach (var pattern in patterns)
            {
                expandedArms.Add(new ExpandedMatchArm(pattern, arm));
            }
        }

        return expandedArms;
    }

    public override object? VisitMatchExpr([NotNull] NovusParser.MatchExprContext context)
    {
        SourceLocation errorLocation;
        var matchValue = (IrValue?)Visit(context.expression());

        if (matchValue == null)
        {
            errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Match expression requires a value",
                errorLocation
            );
            return null;
        }

        // Auto-dereference pointer and reference types for matching
        var actualMatchType = matchValue.Type;
        if (matchValue.Type is IrPointerType ptrType)
        {
            actualMatchType = ptrType.PointeeType;
        }
        else if (matchValue.Type is IrReferenceType refType)
        {
            actualMatchType = refType.PointeeType;
        }
        else if (matchValue.Type is IrMutReferenceType mutRefType)
        {
            actualMatchType = mutRefType.PointeeType;
        }

        bool isEnumMatch = actualMatchType is IrEnumType;
        bool isIntegerMatch = actualMatchType is IrIntType;

        // Handle case where actualMatchType is IrGenericType that refers to an enum
        // This happens when matching on enum types that haven't been fully monomorphized yet
        // or when dereferencing a pointer/reference to an enum yields IrGenericType
        IrEnumType? enumTypeForValidation = null;
        if (isEnumMatch)
        {
            enumTypeForValidation = (IrEnumType)actualMatchType;
        }
        else if (!isIntegerMatch && actualMatchType is IrGenericType genericType)
        {
            if (_symbols.HasEnum(genericType.ParameterName))
            {
                isEnumMatch = true;
                enumTypeForValidation = _symbols.LookupEnum(genericType.ParameterName)!;
            }
        }

        if (!isEnumMatch && !isIntegerMatch)
        {
            errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Match can only be used with enum or integer types, got '{matchValue.Type.Name}'",
                errorLocation
            );
            return null;
        }

        IrEnumType? enumType = enumTypeForValidation;
        if (enumType != null)
        {
            foreach (var v in enumType.Variants)
            {
            }
        }

        // Expand match arms - flatten pipe patterns into separate arms
        var expandedArms = ExpandMatchArms(context.matchArm());

        // Generate labels for match arms and end
        var matchEndLabel = $"match_end_{_labelCounter}";
        var armLabels = new List<string>();
        var checkLabels = new List<string>();

        for (int i = 0; i < expandedArms.Count; i++)
        {
            armLabels.Add($"match_arm_{_labelCounter}_{i}");
            checkLabels.Add($"match_check_{_labelCounter}_{i}");
        }
        var matchId = _labelCounter;
        _labelCounter++;

        // Determine if arms produce values and their type
        IrType? matchResultType = null;
        // Arms produce values if they have expression(s) that aren't guards
        // With guards: expression()[0] is guard, expression()[1] is value (if present)
        // Without guards: expression()[0] is value (if present)
        bool armsProduceValues = expandedArms.Any(arm =>
        {
            var exprs = arm.OriginalArm.expression();
            if (exprs == null || exprs.Length == 0) return false;
            // If there's a guard (KW_IF present), we need at least 2 expressions (guard + value)
            if (arm.OriginalArm.KW_IF() != null) return exprs.Length >= 2;
            // Otherwise, first expression is the value
            return true;
        });
        string? matchResultVarName = null;

        // Extract tag from enum value (before declaring match result, so it appears first)
        // Only needed for enum matches
        IrVariable? tagVar = null;
        IrValue? enumValueForExtract = null;  // For extracting variant data later
        if (isEnumMatch)
        {
            // If matchValue is a pointer/reference to an enum, we need to dereference it first
            enumValueForExtract = matchValue;
            if (matchValue.Type is IrPointerType || matchValue.Type is IrReferenceType || matchValue.Type is IrMutReferenceType)
            {
                // Create a dereference value - use the resolved enum type
                enumValueForExtract = new IrDereferenceValue(matchValue, enumTypeForValidation!);
            }

            var tagName = $"%t{_tempCounter++}";
            _currentBlock!.AddInstruction(new IrExtractTag(tagName, enumValueForExtract));
            tagVar = new IrVariable(tagName, IrIntType.I32);
        }

        // Declare match result variable if arms produce values
        if (armsProduceValues)
        {
            // Use expected type if available (e.g., from function return type)
            matchResultType = _expectedType ?? _currentFunction?.ReturnType;

            if (matchResultType != null && matchResultType is not IrVoidType)
            {
                matchResultVarName = $"%match_{matchId}_result";

                // Declare the match result variable with an uninitialized value
                var matchResultVar = new IrLocalVariable(matchResultVarName, matchResultType, true);
                _currentFunction!.LocalVariables.Add(matchResultVar);
                _localVariables[matchResultVarName] = matchResultVar;

                // Emit the declaration instruction (C needs this to actually declare the variable)
                // We use a default value as initializer (will be overwritten by match arms)
                IrValue defaultValue;
                if (matchResultType is IrIntType intType)
                {
                    defaultValue = new IrConstant(0, intType);
                }
                else if (matchResultType is IrBoolType)
                {
                    defaultValue = new IrBoolConstant(false);
                }
                else
                {
                    // For complex types, we'll initialize later in the first arm
                    // For now, create a zero constant
                    defaultValue = new IrConstant(0, matchResultType);
                }

                _currentBlock!.AddInstruction(new IrLocalDecl(matchResultVarName, matchResultType, true, defaultValue));
            }
        }

        // Track whether any arm can reach match_end (doesn't terminate)
        bool anyArmReachesEnd = false;

        // Generate comparisons and branches for each arm
        for (int i = 0; i < expandedArms.Count; i++)
        {
            var expandedArm = expandedArms[i];
            var pattern = expandedArm.Pattern;

            // Add label for this check (skip first one - execution falls through to it)
            if (i > 0)
            {
                _currentBlock!.AddInstruction(new IrLabel(checkLabels[i]));
            }

            // Check if this is a wildcard pattern
            if (pattern is NovusParser.WildcardPatternContext)
            {
                // Wildcard always matches, jump directly
                _currentBlock!.AddInstruction(new IrBranch(armLabels[i]));
                break;
            }

            // Handle patterns based on match type
            if (isEnumMatch)
            {
                // Handle variant patterns
                string? variantName = null;
                if (pattern is NovusParser.VariantPatternContext variantPattern)
                {
                    // Extract the last identifier from the qualified name (e.g., SimpleResult::Ok -> Ok)
                    var identifiers = variantPattern.variantName().IDENTIFIER();
                    variantName = identifiers[identifiers.Length - 1].GetText();
                }
                else if (pattern is NovusParser.SimpleVariantPatternContext simpleVariantPattern)
                {
                    // SimpleVariantPattern is IDENTIFIER '::' IDENTIFIER ('::' IDENTIFIER)*
                    // Extract the last identifier from the qualified name (e.g., SimpleResult::Ok -> Ok)
                    var identifiers = simpleVariantPattern.IDENTIFIER();
                    variantName = identifiers[identifiers.Length - 1].GetText();
                }
                else if (pattern is NovusParser.IdentifierPatternContext identPattern)
                {
                    variantName = identPattern.IDENTIFIER().GetText();
                }

                if (variantName != null)
                {
                    var variant = enumType!.GetVariant(variantName);
                    if (variant == null)
                    {
                        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.InvalidExpressionType,
                            $"Enum '{enumType.EnumName}' has no variant '{variantName}'",
                            errorLocation
                        );
                        return null;
                    }

                    // Compare tag with variant tag
                    var cmpName = $"%t{_tempCounter++}";
                    var tagConst = new IrConstant(variant.Tag, IrIntType.I32);
                    _currentBlock!.AddInstruction(new IrBinaryOp(cmpName, IrBinaryOp.OpKind.Eq, tagVar!, tagConst, IrBoolType.Instance));
                    var cmpVar = new IrVariable(cmpName, IrBoolType.Instance);

                    // Branch: if match, go to arm, otherwise continue to next check
                    var nextLabel = i < checkLabels.Count - 1 ? checkLabels[i + 1] : matchEndLabel;
                    _currentBlock!.AddInstruction(new IrConditionalBranch(cmpVar, armLabels[i], nextLabel));
                }
            }
            else if (isIntegerMatch)
            {
                // Handle integer literal patterns (decimal, hex, or binary)
                if (pattern is NovusParser.LiteralPatternContext literalPattern)
                {
                    long value;
                    bool parsed = false;

                    // Try decimal integer literal
                    if (literalPattern.INTEGER_LITERAL() != null)
                    {
                        var literalText = literalPattern.INTEGER_LITERAL().GetText();
                        (value, _) = ParseIntegerLiteral(literalText);
                        parsed = true;
                    }
                    // Try hex literal ($FF, $DEADBEEF, etc.)
                    else if (literalPattern.HEX_LITERAL() != null)
                    {
                        var literalText = literalPattern.HEX_LITERAL().GetText();
                        (value, _) = ParseHexLiteral(literalText);
                        parsed = true;
                    }
                    // Try binary literal (%1010, %11110000, etc.)
                    else if (literalPattern.BINARY_LITERAL() != null)
                    {
                        var literalText = literalPattern.BINARY_LITERAL().GetText();
                        (value, _) = ParseBinaryLiteral(literalText);
                        parsed = true;
                    }
                    else
                    {
                        value = 0;
                    }

                    if (parsed)
                    {
                        // Compare match value with literal
                        var cmpName = $"%t{_tempCounter++}";
                        var literalConst = new IrConstant(value, matchValue.Type);
                        _currentBlock!.AddInstruction(new IrBinaryOp(cmpName, IrBinaryOp.OpKind.Eq, matchValue, literalConst, IrBoolType.Instance));
                        var cmpVar = new IrVariable(cmpName, IrBoolType.Instance);

                        // Branch: if match, go to arm, otherwise continue to next check
                        var nextLabel = i < checkLabels.Count - 1 ? checkLabels[i + 1] : matchEndLabel;
                        _currentBlock!.AddInstruction(new IrConditionalBranch(cmpVar, armLabels[i], nextLabel));
                    }
                }
                // Handle identifier patterns that refer to constants
                else if (pattern is NovusParser.IdentifierPatternContext identPattern)
                {
                    var identName = identPattern.IDENTIFIER().GetText();
                    var constantSymbol = _symbols.LookupConstant(identName);

                    if (constantSymbol != null && constantSymbol.Type is IrIntType)
                    {
                        // Extract integer value from constant
                        long value;
                        if (constantSymbol.Value is int intVal)
                            value = intVal;
                        else if (constantSymbol.Value is uint uintVal)
                            value = uintVal;
                        else if (constantSymbol.Value is long longVal)
                            value = longVal;
                        else if (constantSymbol.Value is ulong ulongVal)
                            value = (long)ulongVal;
                        else if (constantSymbol.Value is short shortVal)
                            value = shortVal;
                        else if (constantSymbol.Value is ushort ushortVal)
                            value = ushortVal;
                        else if (constantSymbol.Value is byte byteVal)
                            value = byteVal;
                        else if (constantSymbol.Value is sbyte sbyteVal)
                            value = sbyteVal;
                        else
                            value = 0; // Fallback (should never happen if semantic analysis passed)

                        // Compare match value with constant value
                        var cmpName = $"%t{_tempCounter++}";
                        var constValue = new IrConstant(value, matchValue.Type);
                        _currentBlock!.AddInstruction(new IrBinaryOp(cmpName, IrBinaryOp.OpKind.Eq, matchValue, constValue, IrBoolType.Instance));
                        var cmpVar = new IrVariable(cmpName, IrBoolType.Instance);

                        // Branch: if match, go to arm, otherwise continue to next check
                        var nextLabel = i < checkLabels.Count - 1 ? checkLabels[i + 1] : matchEndLabel;
                        _currentBlock!.AddInstruction(new IrConditionalBranch(cmpVar, armLabels[i], nextLabel));
                    }
                }
                else
                {
                    // Unknown pattern type for integer match - this shouldn't happen if semantic analysis passed
                    // Leave the else clause empty so we fall through to the next check or default case
                }
            }
        }

        // Generate code for each arm
        for (int i = 0; i < expandedArms.Count; i++)
        {
            var expandedArm = expandedArms[i];
            var armCtx = expandedArm.OriginalArm;
            var pattern = expandedArm.Pattern;

            _currentBlock!.AddInstruction(new IrLabel(armLabels[i]));

            // Push a new defer scope for this match arm
            // Variables declared in this arm will have their cleanup emitted before jumping to match_end
            PushDeferScope();

            // Extract associated data for variant patterns (enum matches only)
            if (isEnumMatch && pattern is NovusParser.VariantPatternContext variantPattern)
            {
                // Extract the last identifier from the qualified name (e.g., SimpleResult::Ok -> Ok)
                var identifiers = variantPattern.variantName().IDENTIFIER();
                var variantName = identifiers[identifiers.Length - 1].GetText();
                var variant = enumType!.GetVariant(variantName);

                // Extract associated data and bind to pattern variables
                if (variantPattern.patternList() != null)
                {
                    var bindingPatterns = variantPattern.patternList().pattern();
                    for (int dataIdx = 0; dataIdx < bindingPatterns.Length; dataIdx++)
                    {
                        var bindingPattern = bindingPatterns[dataIdx];

                        // Only handle identifier bindings for now
                        if (bindingPattern is NovusParser.IdentifierPatternContext idPattern)
                        {
                            var bindingName = idPattern.IDENTIFIER().GetText();
                            var dataType = variant!.AssociatedData[dataIdx];

                            // Extract the data - use enumValueForExtract which has the proper enum type
                            // (dereferenced if matchValue was a pointer/reference)
                            var extractName = $"%t{_tempCounter++}";
                            _currentBlock!.AddInstruction(new IrExtractVariantData(extractName, enumValueForExtract!, variantName, dataIdx, dataType));

                            // Store in a local variable
                            var localVar = new IrLocalVariable(bindingName, dataType, false);
                            _currentFunction!.LocalVariables.Add(localVar);
                            _localVariables[bindingName] = localVar;

                            var extractedValue = new IrVariable(extractName, dataType);
                            _currentBlock!.AddInstruction(new IrLocalDecl(bindingName, dataType, false, extractedValue));

                            // Automatic defer for types with drop() method (RAII-style cleanup)
                            // This ensures pattern-bound variables in match arms are properly cleaned up
                            if (EnsureDropMethodInstantiated(dataType))
                            {
                                InjectAutomaticDrop(bindingName, dataType);
                            }
                        }
                    }
                }
            }
            // Integer matches don't have associated data to extract

            // Visit guard if present and generate conditional branch
            var expressions = armCtx.expression();
            int valueExprIndex = 0;
            if (armCtx.KW_IF() != null && expressions != null && expressions.Length > 0)
            {
                // Evaluate guard condition
                var guardValue = (IrValue?)Visit(expressions[0]);
                if (guardValue != null)
                {
                    // If guard is true, execute this arm. If false, jump to next case
                    var executeLabel = $"%match_{matchId}_arm_{i}_execute";
                    var skipLabel = $"%match_{matchId}_arm_{i}_skip";
                    _currentBlock!.AddInstruction(new IrConditionalBranch(guardValue, executeLabel, skipLabel));

                    // Create the execute block for this arm
                    var executeBlock = _currentFunction!.CreateBasicBlock(executeLabel);
                    _currentBlock = executeBlock;
                }
                valueExprIndex = 1;
            }

            // Visit the arm body and capture result if it's an expression
            IrValue? armResult = null;
            if (expressions != null && expressions.Length > valueExprIndex)
            {
                // Set expected type so enum constructors get the correct monomorphized type
                // We set this once before visiting all arms and keep it set
                if (matchResultType != null)
                {
                    _expectedType = matchResultType;
                }

                armResult = (IrValue?)Visit(expressions[valueExprIndex]);

                // Infer match result type from first arm if we didn't have an expected type
                if (i == 0 && armResult != null && matchResultType == null)
                {
                    matchResultType = armResult.Type;
                    matchResultVarName = $"%match_{matchId}_result";

                    // Declare the variable now that we know the type
                    var matchResultVar = new IrLocalVariable(matchResultVarName, matchResultType, true);
                    _currentFunction!.LocalVariables.Add(matchResultVar);
                    _localVariables[matchResultVarName] = matchResultVar;

                    // Now that we know the type, set it as expected type for subsequent arms
                    _expectedType = matchResultType;
                }
            }
            else if (armCtx.block() != null)
            {
                // Set expected type so enum constructors get the correct monomorphized type
                // We set this once before visiting all arms and keep it set
                if (matchResultType != null)
                {
                    _expectedType = matchResultType;
                }

                armResult = (IrValue?)Visit(armCtx.block());

                // Infer match result type from first arm if we didn't have an expected type
                if (i == 0 && armResult != null && matchResultType == null)
                {
                    matchResultType = armResult.Type;
                    matchResultVarName = $"%match_{matchId}_result";

                    // Declare the variable now that we know the type
                    var matchResultVar = new IrLocalVariable(matchResultVarName, matchResultType, true);
                    _currentFunction!.LocalVariables.Add(matchResultVar);
                    _localVariables[matchResultVarName] = matchResultVar;

                    // Now that we know the type, set it as expected type for subsequent arms
                    _expectedType = matchResultType;
                }
            }
            else if (armCtx.returnStatement() != null)
            {
                // Handle return statement in match arm
                // IMPORTANT: Must emit scope cleanup BEFORE the return statement
                // Inline emit the defer block instructions, but DON'T remove them from function defer list
                // (EmitReturn in C code gen will handle function-level defers)
                if (_scopeDeferStack.Count > 0)
                {
                    var scopeDefers = _scopeDeferStack.Peek(); // Peek, don't pop yet

                    // Emit defers in LIFO order (last registered, first executed)
                    for (int deferIdx = scopeDefers.Count - 1; deferIdx >= 0; deferIdx--)
                    {
                        var deferBlock = scopeDefers[deferIdx];

                        // Emit all instructions in the defer block inline
                        foreach (var instruction in deferBlock.Instructions)
                        {
                            _currentBlock!.AddInstruction(instruction);
                        }
                    }

                    // Now pop the scope (without removing from function defer list)
                    _scopeDeferStack.Pop();
                }

                Visit(armCtx.returnStatement());
                // Return statements terminate the block, so no result to store
            }

            // If we have a result value, result type, and variable name, store it
            if (armResult != null && matchResultType != null && matchResultVarName != null && !CurrentBlockHasTerminator())
            {
                _currentBlock!.AddInstruction(new IrStore(matchResultVarName, armResult));
            }

            // Pop defer scope and emit cleanup BEFORE jumping to match_end
            // This ensures variables declared in this match arm are cleaned up before leaving the scope
            // Note: Skip this if we already popped the scope (e.g., for return statements)
            if (!CurrentBlockHasTerminator())
            {
                PopDeferScope();
            }

            // Jump to end (if not already terminated)
            if (!CurrentBlockHasTerminator())
            {
                _currentBlock!.AddInstruction(new IrBranch(matchEndLabel));
                anyArmReachesEnd = true;  // This arm can reach match_end
            }
        }

        // Always emit the match_end label (needed for fall-through checks even if all arms terminate)
        _currentBlock!.AddInstruction(new IrLabel(matchEndLabel));

        // If all arms terminated, add a return after the label to avoid falling off the end
        // This handles the case where an invalid enum tag is encountered
        if (!anyArmReachesEnd)
        {
            // All arms terminated - this code is unreachable in correct programs
            // But we still emit a return to satisfy C compiler
            if (_currentFunction?.ReturnType is not null and not IrVoidType)
            {
                // Non-void function: return zero as unreachable fallback
                var returnType = _currentFunction.ReturnType;
                IrValue? defaultValue = null;

                if (returnType is IrIntType intType)
                {
                    defaultValue = new IrConstant(0, intType);
                }
                else if (returnType is IrBoolType)
                {
                    defaultValue = new IrBoolConstant(false);
                }
                // For struct/enum types, we can't create a valid constant
                // Since this code is unreachable, just emit a bare return
                // The C code generator will handle this via output parameter

                _currentBlock!.AddInstruction(new IrReturn(defaultValue));
            }
            else
            {
                // Void function: bare return is fine
                _currentBlock!.AddInstruction(new IrReturn(null));
            }
        }

        // Return match result if we computed one
        if (matchResultType != null && matchResultVarName != null)
        {
            return new IrVariable(matchResultVarName, matchResultType);
        }

        return null;
    }
}
