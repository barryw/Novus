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

    private bool PatternNeedsRuntimeCheck(NovusParser.PatternContext pattern)
    {
        if (pattern is NovusParser.ReferencePatternContext reference)
            return PatternNeedsRuntimeCheck(reference.pattern());

        if (pattern is NovusParser.IdentifierPatternContext identifier)
            return _symbols.LookupConstant(identifier.IDENTIFIER().GetText()) is
                { Type: IrIntType or IrBoolType };

        return pattern is NovusParser.VariantPatternContext or
               NovusParser.SimpleVariantPatternContext or
               NovusParser.LiteralPatternContext or
               NovusParser.BoolLiteralPatternContext or
               NovusParser.NullLiteralPatternContext;
    }

    /// <summary>
    /// Emit the checks for a pattern nested inside an enum payload. Bindings and
    /// wildcards need no comparison; enum patterns recurse through their payloads.
    /// </summary>
    private bool EmitNestedPatternCheck(
        NovusParser.PatternContext pattern,
        IrValue value,
        IrType valueType,
        string matchedLabel,
        string failedLabel,
        string labelPrefix)
    {
        if (pattern is NovusParser.ReferencePatternContext reference)
            return EmitNestedPatternCheck(reference.pattern(), value, valueType, matchedLabel, failedLabel, labelPrefix);

        if (pattern is NovusParser.IdentifierPatternContext identifierPattern)
        {
            var constant = _symbols.LookupConstant(identifierPattern.IDENTIFIER().GetText());
            if (constant?.Type is not (IrIntType or IrBoolType))
            {
                _currentBlock!.AddInstruction(new IrBranch(matchedLabel));
                return true;
            }

            var cmpName = $"%t{_tempCounter++}";
            IrValue constantValue = constant.Type is IrBoolType && constant.Value is bool boolValue
                ? new IrBoolConstant(boolValue)
                : new IrConstant(Convert.ToInt64(constant.Value), valueType);
            _currentBlock!.AddInstruction(new IrBinaryOp(
                cmpName, IrBinaryOp.OpKind.Eq, value,
                constantValue, IrBoolType.Instance));
            _currentBlock.AddInstruction(new IrConditionalBranch(
                new IrVariable(cmpName, IrBoolType.Instance), matchedLabel, failedLabel));
            return true;
        }

        if (pattern is NovusParser.LiteralPatternContext literalPattern && valueType is IrIntType)
        {
            long literalValue;
            if (literalPattern.INTEGER_LITERAL() != null)
                (literalValue, _) = ParseIntegerLiteral(literalPattern.INTEGER_LITERAL().GetText());
            else if (literalPattern.HEX_LITERAL() != null)
                (literalValue, _) = ParseHexLiteral(literalPattern.HEX_LITERAL().GetText());
            else if (literalPattern.BINARY_LITERAL() != null)
                (literalValue, _) = ParseBinaryLiteral(literalPattern.BINARY_LITERAL().GetText());
            else if (literalPattern.CHAR_LITERAL() != null)
                literalValue = ParseCharLiteralValue(literalPattern.CHAR_LITERAL().GetText());
            else
                return false;

            var cmpName = $"%t{_tempCounter++}";
            _currentBlock!.AddInstruction(new IrBinaryOp(
                cmpName, IrBinaryOp.OpKind.Eq, value,
                new IrConstant(literalValue, valueType), IrBoolType.Instance));
            _currentBlock.AddInstruction(new IrConditionalBranch(
                new IrVariable(cmpName, IrBoolType.Instance), matchedLabel, failedLabel));
            return true;
        }

        if (pattern is NovusParser.BoolLiteralPatternContext boolPattern && valueType is IrBoolType)
        {
            var cmpName = $"%t{_tempCounter++}";
            _currentBlock!.AddInstruction(new IrBinaryOp(
                cmpName, IrBinaryOp.OpKind.Eq, value,
                new IrBoolConstant(boolPattern.KW_TRUE() != null), IrBoolType.Instance));
            _currentBlock.AddInstruction(new IrConditionalBranch(
                new IrVariable(cmpName, IrBoolType.Instance), matchedLabel, failedLabel));
            return true;
        }

        if (pattern is NovusParser.NullLiteralPatternContext && valueType is IrPointerType)
        {
            var cmpName = $"%t{_tempCounter++}";
            _currentBlock!.AddInstruction(new IrBinaryOp(
                cmpName, IrBinaryOp.OpKind.Eq, value,
                new IrConstant(0, valueType), IrBoolType.Instance));
            _currentBlock.AddInstruction(new IrConditionalBranch(
                new IrVariable(cmpName, IrBoolType.Instance), matchedLabel, failedLabel));
            return true;
        }

        if (valueType is IrEnumType nestedEnum &&
            pattern is NovusParser.VariantPatternContext or NovusParser.SimpleVariantPatternContext)
        {
            string variantName;
            NovusParser.PatternContext[] payloadPatterns;
            if (pattern is NovusParser.VariantPatternContext variantPattern)
            {
                var identifiers = variantPattern.variantName().IDENTIFIER();
                variantName = identifiers[^1].GetText();
                payloadPatterns = variantPattern.patternList()?.pattern() ?? [];
            }
            else
            {
                var simple = (NovusParser.SimpleVariantPatternContext)pattern;
                var identifiers = simple.IDENTIFIER();
                variantName = identifiers[^1].GetText();
                payloadPatterns = [];
            }

            var variant = nestedEnum.GetVariant(variantName);
            if (variant == null)
                return false;

            var tagName = $"%t{_tempCounter++}";
            var cmpName = $"%t{_tempCounter++}";
            _currentBlock!.AddInstruction(new IrExtractTag(tagName, value));
            _currentBlock.AddInstruction(new IrBinaryOp(
                cmpName, IrBinaryOp.OpKind.Eq,
                new IrVariable(tagName, IrIntType.I32),
                new IrConstant(variant.Tag, IrIntType.I32), IrBoolType.Instance));

            var checkedPayloads = payloadPatterns
                .Select((payload, index) => (payload, index))
                .Where(item => PatternNeedsRuntimeCheck(item.payload))
                .ToList();
            if (checkedPayloads.Count == 0)
            {
                _currentBlock.AddInstruction(new IrConditionalBranch(
                    new IrVariable(cmpName, IrBoolType.Instance), matchedLabel, failedLabel));
                return true;
            }

            var firstPayloadLabel = $"{labelPrefix}_0";
            _currentBlock.AddInstruction(new IrConditionalBranch(
                new IrVariable(cmpName, IrBoolType.Instance), firstPayloadLabel, failedLabel));
            for (var i = 0; i < checkedPayloads.Count; i++)
            {
                var (payload, dataIndex) = checkedPayloads[i];
                if (dataIndex >= variant.AssociatedData.Count)
                    return false;

                var payloadType = variant.AssociatedData[dataIndex];
                var extractName = $"%t{_tempCounter++}";
                var nextMatchedLabel = i == checkedPayloads.Count - 1
                    ? matchedLabel
                    : $"{labelPrefix}_{i + 1}";
                _currentBlock.AddInstruction(new IrLabel($"{labelPrefix}_{i}"));
                _currentBlock.AddInstruction(new IrExtractVariantData(
                    extractName, value, variantName, dataIndex, payloadType));
                if (!EmitNestedPatternCheck(
                        payload, new IrVariable(extractName, payloadType), payloadType,
                        nextMatchedLabel, failedLabel, $"{labelPrefix}_{i}_nested"))
                    return false;
            }
            return true;
        }

        _currentBlock!.AddInstruction(new IrBranch(matchedLabel));
        return true;
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
        bool isByteSequenceMatch = actualMatchType is IrArrayType { ElementType: IrIntType { BitWidth: 8 } } ||
                                   actualMatchType is IrStructType { StructName: "Slice" or "MutSlice" } slice &&
                                   slice.GetField("ptr")?.Type switch
                                   {
                                       IrPointerType { PointeeType: IrIntType { BitWidth: 8 } } => true,
                                       IrReferenceType { PointeeType: IrIntType { BitWidth: 8 } } => true,
                                       IrMutReferenceType { PointeeType: IrIntType { BitWidth: 8 } } => true,
                                       _ => false
                                   };

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
                enumTypeForValidation = RequireEnum(genericType.ParameterName);
            }
        }

        if (!isEnumMatch && !isIntegerMatch && !isByteSequenceMatch)
        {
            errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Match can only be used with enum, integer, or byte-sequence types, got '{matchValue.Type.Name}'",
                errorLocation
            );
            return null;
        }

        IrEnumType? enumType = enumTypeForValidation;
        var matchesBorrowedEnum = matchValue.Type is IrPointerType or IrReferenceType or IrMutReferenceType;
        var ownedEnumNeedsDrop = isEnumMatch && !matchesBorrowedEnum && _module.EnumNeedsDrop(enumType!);

        if (matchesBorrowedEnum && context.matchArm().Any(arm =>
                PatternMovesOwnedPayload(arm.pattern(), enumType!)))
        {
            errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "cannot move owned payload out of a borrowed enum; bind it with '&value' instead",
                errorLocation
            );
            return null;
        }

        // An owned enum match moves its payload. Materialize temporary values so
        // successful arms can invalidate the source tag after taking ownership.
        if (ownedEnumNeedsDrop && matchValue is not IrVariable)
        {
            var valueName = $"__match_value_{_tempCounter++}";
            var localVar = new IrLocalVariable(valueName, actualMatchType, false);
            _currentFunction!.LocalVariables.Add(localVar);
            _localVariables[valueName] = localVar;
            _currentBlock!.AddInstruction(new IrLocalDecl(valueName, actualMatchType, false, matchValue));
            EnsureDropMethodInstantiated(actualMatchType);
            InjectAutomaticDrop(valueName, actualMatchType);
            matchValue = new IrVariable(valueName, actualMatchType);
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
            if (exprs == null || exprs is []) return false;
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
            // Use expected type if available (e.g., from type annotation on let binding)
            // Do NOT fall back to function return type - that would cause nested matches
            // in non-return contexts to incorrectly use the function's return type.
            // If no expected type, we'll infer the type from the first arm (lines 500-512, 526-537).
            matchResultType = _expectedType;

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
                if (expandedArm.OriginalArm.KW_IF() == null)
                {
                    break;
                }
                continue;
            }

            // Handle patterns based on match type
            if (isByteSequenceMatch)
            {
                var nextLabel = i < checkLabels.Count - 1 ? checkLabels[i + 1] : matchEndLabel;
                if (pattern is not NovusParser.LiteralPatternContext bytePattern ||
                    bytePattern.BYTE_STRING_LITERAL() == null)
                {
                    _diagnostics.ReportError(ErrorCodes.InvalidExpressionType,
                        "byte-sequence matches require byte-string patterns or '_'", GetLocation(pattern));
                    return null;
                }
                EmitByteSequencePatternCheck(matchValue, actualMatchType,
                    ByteLiteralParser.Parse(bytePattern.BYTE_STRING_LITERAL().GetText(), 1),
                    armLabels[i], nextLabel, $"match_{matchId}_bytes_{i}");
            }
            else if (isEnumMatch)
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

                    var nextLabel = i < checkLabels.Count - 1 ? checkLabels[i + 1] : matchEndLabel;
                    var checkedPayloads = pattern is NovusParser.VariantPatternContext payloadPattern &&
                                          payloadPattern.patternList() != null
                        ? payloadPattern.patternList().pattern()
                            .Select((payload, index) => (payload, index))
                            .Where(item => PatternNeedsRuntimeCheck(item.payload))
                            .ToList()
                        : [];

                    if (checkedPayloads.Count == 0)
                    {
                        _currentBlock!.AddInstruction(new IrConditionalBranch(cmpVar, armLabels[i], nextLabel));
                    }
                    else
                    {
                        var payloadCheckLabel = $"match_{matchId}_arm_{i}_payload_0";
                        _currentBlock!.AddInstruction(new IrConditionalBranch(cmpVar, payloadCheckLabel, nextLabel));

                        for (var payloadIndex = 0; payloadIndex < checkedPayloads.Count; payloadIndex++)
                        {
                            var (payload, dataIndex) = checkedPayloads[payloadIndex];
                            var dataType = variant.AssociatedData[dataIndex];
                            var extractName = $"%t{_tempCounter++}";

                            _currentBlock!.AddInstruction(new IrLabel($"match_{matchId}_arm_{i}_payload_{payloadIndex}"));
                            _currentBlock.AddInstruction(new IrExtractVariantData(
                                extractName, enumValueForExtract!, variantName, dataIndex, dataType));

                            var matchedLabel = payloadIndex == checkedPayloads.Count - 1
                                ? armLabels[i]
                                : $"match_{matchId}_arm_{i}_payload_{payloadIndex + 1}";
                            if (!EmitNestedPatternCheck(
                                    payload, new IrVariable(extractName, dataType), dataType,
                                    matchedLabel, nextLabel,
                                    $"match_{matchId}_arm_{i}_payload_{payloadIndex}_nested"))
                            {
                                errorLocation = GetLocation(payload);
                                _diagnostics.ReportError(
                                    ErrorCodes.InvalidExpressionType,
                                    $"Unsupported nested pattern for '{dataType.Name}'",
                                    errorLocation);
                                return null;
                            }
                        }
                    }
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
                    else if (literalPattern.CHAR_LITERAL() != null)
                    {
                        value = ParseCharLiteralValue(literalPattern.CHAR_LITERAL().GetText());
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
                    else
                    {
                        // An identifier that is not a constant binds the matched value.
                        _currentBlock!.AddInstruction(new IrBranch(armLabels[i]));
                        if (expandedArm.OriginalArm.KW_IF() == null)
                        {
                            break;
                        }
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
            var armDeferScopePopped = false;

            // Track pattern-bound variable names with Drop types for this arm
            // We need this to detect when a variable is moved (used as match result) vs. dropped
            var patternBoundDropVars = new HashSet<string>();
            var unboundDropPayloads = new List<(string Variant, int Index, IrType Type)>();

            // Integer identifier patterns bind the matched value for guards and arm bodies.
            if (isIntegerMatch && pattern is NovusParser.IdentifierPatternContext integerBindingPattern)
            {
                var bindingName = integerBindingPattern.IDENTIFIER().GetText();
                if (_symbols.LookupConstant(bindingName) == null)
                {
                    var uniqueBindingName = UniqueLocalName(bindingName);
                    var localVar = new IrLocalVariable(uniqueBindingName, matchValue.Type, false);
                    _currentFunction!.LocalVariables.Add(localVar);
                    _localVariables[uniqueBindingName] = localVar;
                    _localVariables[bindingName] = localVar;
                    _currentBlock.AddInstruction(new IrLocalDecl(uniqueBindingName, matchValue.Type, false, matchValue));
                }
            }

            // Extract associated data for variant patterns (enum matches only)
            if (isEnumMatch && pattern is NovusParser.VariantPatternContext variantPattern)
            {
                // Extract the last identifier from the qualified name (e.g., SimpleResult::Ok -> Ok)
                var identifiers = variantPattern.variantName().IDENTIFIER();
                var variantName = identifiers[identifiers.Length - 1].GetText();
                var variant = enumType!.GetVariant(variantName);

                // Extract associated data and bind to pattern variables. Payloads
                // ignored with `_` still need an owner so they are dropped once.
                var bindingPatterns = variantPattern.patternList()?.pattern() ?? [];
                for (int dataIdx = 0; dataIdx < variant!.AssociatedData.Count; dataIdx++)
                {
                    var bindingPattern = dataIdx < bindingPatterns.Length ? bindingPatterns[dataIdx] : null;

                    // Extract binding name and mutability from pattern
                    string? bindingName = null;
                    bool isMutable = false;

                    if (bindingPattern is NovusParser.IdentifierPatternContext idPattern)
                    {
                        bindingName = idPattern.IDENTIFIER().GetText();
                        isMutable = false;

                        // Constants constrain associated data; they are not bindings.
                        if (_symbols.LookupConstant(bindingName) != null)
                        {
                            bindingName = null;
                        }
                    }
                    else if (bindingPattern is NovusParser.VarIdentifierPatternContext mutIdPattern)
                    {
                        bindingName = mutIdPattern.IDENTIFIER().GetText();
                        isMutable = true;
                    }

                    var referencePattern = bindingPattern as NovusParser.ReferencePatternContext;
                    var referenceInner = referencePattern?.pattern();
                    var bindsReference = referenceInner is NovusParser.IdentifierPatternContext or
                        NovusParser.VarIdentifierPatternContext;
                    var bindsMutableReference = referenceInner is NovusParser.VarIdentifierPatternContext;
                    if (bindsReference)
                    {
                        bindingName = referenceInner switch
                        {
                            NovusParser.IdentifierPatternContext identifier => identifier.IDENTIFIER().GetText(),
                            NovusParser.VarIdentifierPatternContext identifier => identifier.IDENTIFIER().GetText(),
                            _ => null
                        };
                        isMutable = false;
                    }

                    var dataType = variant.AssociatedData[dataIdx];
                    if (bindingName != null)
                    {
                            IrType bindingType = dataType;
                            IrValue extractedValue;
                            if (bindsReference)
                            {
                                bindingType = bindsMutableReference
                                    ? _typeInterner.GetMutReferenceType(dataType)
                                    : _typeInterner.GetReferenceType(dataType);
                                var payload = new IrEnumPayloadAccess(
                                    enumValueForExtract!, enumType, variantName, dataIdx, dataType);
                                extractedValue = new IrBorrowValue(payload, bindingType, bindsMutableReference);
                            }
                            else
                            {
                                // Extract the data - use enumValueForExtract which has the proper enum type
                                // (dereferenced if matchValue was a pointer/reference)
                                var extractName = $"%t{_tempCounter++}";
                                _currentBlock!.AddInstruction(new IrExtractVariantData(
                                    extractName, enumValueForExtract!, variantName, dataIdx, dataType));
                                extractedValue = new IrVariable(extractName, dataType);
                            }

                            // C locals are function-scoped in generated code, so every
                            // later pattern binding needs its own IR/C name. Reusing the
                            // source name can alias an earlier binding even when the
                            // current symbol-table entry already has a suffixed name.
                            var uniqueBindingName = UniqueLocalName(bindingName);

                            // Store in a local variable
                            var localVar = new IrLocalVariable(uniqueBindingName, bindingType, isMutable);
                            _currentFunction!.LocalVariables.Add(localVar);
                            // Map BOTH the unique name and original name to this variable.
                            // The unique name is needed for C code generation (avoids type conflicts).
                            // The original name is needed so subsequent references (e.g., *val) find this variable.
                            _localVariables[uniqueBindingName] = localVar;
                            _localVariables[bindingName] = localVar;  // Overwrite so *val finds val_901's variable

                            _currentBlock!.AddInstruction(new IrLocalDecl(
                                uniqueBindingName, bindingType, isMutable, extractedValue));

                        if (!bindsReference && EnsureDropMethodInstantiated(dataType))
                        {
                            // Activate cleanup only after a guard succeeds. Before
                            // then this binding is a non-owning view of the payload.
                            patternBoundDropVars.Add(uniqueBindingName);
                        }
                    }
                    else if (!matchesBorrowedEnum && EnsureDropMethodInstantiated(dataType))
                    {
                        unboundDropPayloads.Add((variantName, dataIdx, dataType));
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
                    var executeLabel = $"match_{matchId}_arm_{i}_execute";
                    var nextLabel = i < checkLabels.Count - 1 ? checkLabels[i + 1] : matchEndLabel;
                    _currentBlock!.AddInstruction(new IrConditionalBranch(guardValue, executeLabel, nextLabel));

                    // Create the execute block for this arm
                    var executeBlock = _currentFunction!.CreateBasicBlock(executeLabel);
                    _currentBlock = executeBlock;
                }
                valueExprIndex = 1;
            }

            // The arm is now committed (including a successful guard). Transfer
            // every owned payload out of the source enum, then invalidate its tag
            // so the source defer cannot drop the same payload a second time.
            foreach (var dropVar in patternBoundDropVars)
            {
                InjectAutomaticDrop(dropVar, _localVariables[dropVar].Type);
            }

            foreach (var (variantName, dataIndex, dataType) in unboundDropPayloads)
            {
                var extractName = $"%t{_tempCounter++}";
                _currentBlock!.AddInstruction(new IrExtractVariantData(
                    extractName, enumValueForExtract!, variantName, dataIndex, dataType));
                var ownerName = $"__match_drop_{_tempCounter++}";
                var owner = new IrLocalVariable(ownerName, dataType, false);
                _currentFunction!.LocalVariables.Add(owner);
                _localVariables[ownerName] = owner;
                _currentBlock.AddInstruction(new IrLocalDecl(
                    ownerName, dataType, false, new IrVariable(extractName, dataType)));
                InjectAutomaticDrop(ownerName, dataType);
            }

            var transfersVariantPayload = pattern is NovusParser.VariantPatternContext &&
                                          (patternBoundDropVars.Count != 0 || unboundDropPayloads.Count != 0);
            var transfersWildcardValue = pattern is NovusParser.WildcardPatternContext && ownedEnumNeedsDrop;
            if (transfersWildcardValue)
            {
                var ownerName = $"__match_drop_{_tempCounter++}";
                var owner = new IrLocalVariable(ownerName, actualMatchType, false);
                _currentFunction!.LocalVariables.Add(owner);
                _localVariables[ownerName] = owner;
                _currentBlock!.AddInstruction(new IrLocalDecl(ownerName, actualMatchType, false, enumValueForExtract!));
                EnsureDropMethodInstantiated(actualMatchType);
                InjectAutomaticDrop(ownerName, actualMatchType);
            }

            if (ownedEnumNeedsDrop && (transfersVariantPayload || transfersWildcardValue))
            {
                _currentBlock!.AddInstruction(new IrMemberStore(
                    enumValueForExtract!, "tag", 0, new IrConstant(-1, IrIntType.I32)));
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

                    // CRITICAL: Emit an IrLocalDecl instruction so LivenessAnalysis sees the correct type.
                    // Without this, the slot type is inferred from the first IrStore, which could have
                    // a different type if the value is wrapped or converted.
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
                        defaultValue = new IrConstant(0, matchResultType);
                    }
                    _currentBlock!.AddInstruction(new IrLocalDecl(matchResultVarName, matchResultType, true, defaultValue));

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

                    // CRITICAL: Emit an IrLocalDecl instruction so LivenessAnalysis sees the correct type.
                    // Without this, the slot type is inferred from the first IrStore, which could have
                    // a different type if the value is wrapped or converted.
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
                        defaultValue = new IrConstant(0, matchResultType);
                    }
                    _currentBlock!.AddInstruction(new IrLocalDecl(matchResultVarName, matchResultType, true, defaultValue));

                    // Now that we know the type, set it as expected type for subsequent arms
                    _expectedType = matchResultType;
                }
            }
            else if (armCtx.returnStatement() != null)
            {
                // Handle return statement in match arm
                // DO NOT emit scope cleanup here - let the C code generator's EmitReturn handle it.
                // EmitReturn has DeactivateDefersForMovedVariables logic that correctly detects
                // when a pattern-bound variable is being returned (ownership transfer) vs. dropped.
                //
                // If we inline the defer block instructions here, we'd Drop variables BEFORE the
                // return value is captured, causing use-after-free bugs when returning Drop types
                // from match arms (e.g., `Some(val) => return val` would Drop val before returning it).
                //
                // The defer blocks are already registered at the function level via DeferredBlocks,
                // so EmitReturn will process them correctly.
                // A postfix return can fall through, so its arm scope must stay
                // active until the normal arm cleanup below. Treating it as an
                // unconditional return pops the enclosing scope a second time.
                if (armCtx.returnStatement().postfixCondition() == null && _scopeDeferStack.Count > 0)
                {
                    // Just pop the scope without emitting the defer blocks inline
                    // The defer blocks remain in _currentFunction.DeferredBlocks for EmitReturn to handle
                    _scopeDeferStack.Pop();
                    armDeferScopePopped = true;
                }

                Visit(armCtx.returnStatement());
                // Return statements terminate the block, so no result to store
            }

            // If we have a result value, result type, and variable name, store it
            if (armResult != null && matchResultType != null && matchResultVarName != null && !CurrentBlockHasTerminator())
            {
                _currentBlock!.AddInstruction(new IrStore(matchResultVarName, armResult));
            }

            // Check if the arm result is a pattern-bound variable with Drop type being moved.
            // If so, we should NOT drop it - ownership is being transferred to the match result.
            // We need to remove its defer block from the scope before PopDeferScope() emits them.
            if (armResult is IrVariable movedVar && patternBoundDropVars.Contains(movedVar.Name))
            {
                // Find and remove the defer block for this variable from the current scope
                // The defer block label starts with "autoclean_{varName}_"
                if (_scopeDeferStack.Count > 0)
                {
                    var currentScopeDefers = _scopeDeferStack.Peek();
                    var deferToRemove = currentScopeDefers.FirstOrDefault(
                        d => d.Label.StartsWith($"autoclean_{movedVar.Name}_"));
                    if (deferToRemove != null)
                    {
                        currentScopeDefers.Remove(deferToRemove);
                        // Also remove from function-level defers to prevent it being emitted elsewhere
                        _currentFunction!.DeferredBlocks.Remove(deferToRemove);
                        // Mark as emitted to prevent any other code path from emitting it
                        _emittedDeferBlocks.Add(deferToRemove);
                    }
                }
            }

            // Pop defer scope and emit cleanup BEFORE jumping to match_end
            // This ensures variables declared in this match arm are cleaned up before leaving the scope
            // Note: Skip this if we already popped the scope (e.g., for return statements)
            if (!armDeferScopePopped && !CurrentBlockHasTerminator())
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

    private bool PatternMovesOwnedPayload(NovusParser.PatternContext pattern, IrEnumType enumType)
    {
        if (pattern is NovusParser.PipePatternContext pipe)
            return PatternMovesOwnedPayload(pipe.pattern(0), enumType) ||
                   PatternMovesOwnedPayload(pipe.pattern(1), enumType);
        if (pattern is not NovusParser.VariantPatternContext variantPattern)
            return false;

        var variantName = variantPattern.variantName().IDENTIFIER().Last().GetText();
        var variant = enumType.GetVariant(variantName);
        var bindings = variantPattern.patternList()?.pattern() ?? [];
        if (variant == null)
            return false;

        for (var index = 0; index < Math.Min(bindings.Length, variant.AssociatedData.Count); index++)
        {
            if (_module.TypeImplementsDrop(variant.AssociatedData[index]) &&
                bindings[index] is not (NovusParser.ReferencePatternContext or NovusParser.WildcardPatternContext))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Compile matches!(expr, pattern) to IR - evaluates to bool
    /// Lowers to: match expr { pattern => true, _ => false }
    /// </summary>
    public override object? VisitMatchesExpr([NotNull] NovusParser.MatchesExprContext context)
    {
        // Evaluate the expression being matched
        var exprValue = Visit(context.expression());
        if (exprValue == null)
        {
            return new IrBoolConstant(false);  // Error case
        }

        var exprIr = exprValue as IrValue;
        if (exprIr == null)
        {
            return new IrBoolConstant(false);  // Error case
        }

        // Get the pattern
        var pattern = context.pattern();

        // Check for wildcard pattern "_"
        if (pattern is NovusParser.WildcardPatternContext)
        {
            return new IrBoolConstant(true);
        }

        // For simple identifier patterns (variable binding) - always matches
        if (pattern is NovusParser.IdentifierPatternContext)
        {
            return new IrBoolConstant(true);
        }

        // Handle enum variant patterns like Some(_), Ok(_), Status::Active
        if (pattern is NovusParser.VariantPatternContext variantPattern)
        {
            return CompileEnumVariantMatch(exprIr, variantPattern.variantName().GetText());
        }

        // Handle simple variant patterns like Status::Active (no parentheses)
        if (pattern is NovusParser.SimpleVariantPatternContext simpleVariantPattern)
        {
            // Build the full name from identifiers
            var identifiers = simpleVariantPattern.IDENTIFIER();
            var fullName = string.Join("::", identifiers.Select(i => i.GetText()));
            return CompileEnumVariantMatch(exprIr, fullName);
        }

        // For literal patterns (integers, booleans, etc.)
        if (pattern is NovusParser.LiteralPatternContext literalPattern)
        {
            var literalText = literalPattern.GetText();
            long? parsed = literalPattern.CHAR_LITERAL() != null
                ? ParseCharLiteralValue(literalText)
                : exprIr.Type is IrIntType integerType
                    ? IntegerLiteralParser.Parse(literalText, integerType).ToBitPattern()
                    : null;
            if (parsed is { } literalValue)
            {
                var literalConstant = new IrConstant(literalValue, exprIr.Type);
                var compareResultName = $"%matches_cmp_{_tempCounter++}";
                Emit(new IrBinaryOp(compareResultName, IrBinaryOp.OpKind.Eq, exprIr, literalConstant, IrBoolType.Instance));
                return new IrVariable(compareResultName, IrBoolType.Instance);
            }
        }

        if (pattern is NovusParser.BoolLiteralPatternContext boolPattern)
        {
            var boolValue = boolPattern.GetText() == "true";
            var literalConstant = new IrBoolConstant(boolValue);
            var compareResultName = $"%matches_cmp_{_tempCounter++}";
            Emit(new IrBinaryOp(compareResultName, IrBinaryOp.OpKind.Eq, exprIr, literalConstant, IrBoolType.Instance));
            return new IrVariable(compareResultName, IrBoolType.Instance);
        }

        // Fallback for unsupported patterns
        return new IrBoolConstant(false);
    }

    private void EmitByteSequencePatternCheck(IrValue value, IrType type, byte[] expected,
        string matchedLabel, string failedLabel, string prefix)
    {
        IrValue pointer;
        IrValue length;
        if (type is IrArrayType array)
        {
            pointer = value;
            length = new IrConstant(array.Length, IrIntType.U32);
        }
        else
        {
            var slice = (IrStructType)type;
            var pointerField = slice.GetField("ptr")!;
            var lengthField = slice.GetField("len")!;
            var pointerName = $"%t{_tempCounter++}";
            var lengthName = $"%t{_tempCounter++}";
            Emit(new IrMemberAccess(pointerName, value, "ptr", pointerField.Type, pointerField.Offset));
            Emit(new IrMemberAccess(lengthName, value, "len", lengthField.Type, lengthField.Offset));
            pointer = new IrVariable(pointerName, pointerField.Type);
            length = new IrVariable(lengthName, lengthField.Type);
        }

        var lengthMatch = $"%t{_tempCounter++}";
        Emit(new IrBinaryOp(lengthMatch, IrBinaryOp.OpKind.Eq, length,
            new IrConstant(expected.Length, IrIntType.U32), IrBoolType.Instance));
        if (expected.Length == 0)
        {
            Emit(new IrConditionalBranch(new IrVariable(lengthMatch, IrBoolType.Instance), matchedLabel, failedLabel));
            return;
        }

        Emit(new IrConditionalBranch(new IrVariable(lengthMatch, IrBoolType.Instance), $"{prefix}_0", failedLabel));
        for (var index = 0; index < expected.Length; index++)
        {
            Emit(new IrLabel($"{prefix}_{index}"));
            var elementName = $"%t{_tempCounter++}";
            Emit(new IrIndexAccess(elementName, pointer, new IrConstant(index, IrIntType.U32), IrIntType.U8,
                IrBoundsCheckMode.Proven, length));
            var equalName = $"%t{_tempCounter++}";
            Emit(new IrBinaryOp(equalName, IrBinaryOp.OpKind.Eq,
                new IrVariable(elementName, IrIntType.U8), new IrConstant(expected[index], IrIntType.U8),
                IrBoolType.Instance));
            Emit(new IrConditionalBranch(new IrVariable(equalName, IrBoolType.Instance),
                index == expected.Length - 1 ? matchedLabel : $"{prefix}_{index + 1}", failedLabel));
        }
    }

    /// <summary>
    /// Helper to compile enum variant matching for matches!() macro.
    /// Extracts the tag from the enum and compares it to the variant's tag.
    /// </summary>
    private IrValue CompileEnumVariantMatch(IrValue exprIr, string variantName)
    {
        // Get just the variant name (without enum type prefix)
        var variantNameOnly = variantName.Contains("::") ? variantName.Split("::").Last() : variantName;

        // For simple C-style enums (no associated data), compare directly
        if (exprIr.Type is IrEnumType enumType)
        {
            // Find the variant
            var variant = enumType.Variants.FirstOrDefault(v => v.Name == variantNameOnly);
            if (variant != null)
            {
                // Extract the tag and compare
                var tagResultName = $"%matches_tag_{_tempCounter++}";
                Emit(new IrExtractTag(tagResultName, exprIr));
                var tagValue = new IrVariable(tagResultName, IrIntType.I32);

                var tagConstant = new IrConstant(variant.Tag, IrIntType.I32);
                var compareResultName = $"%matches_cmp_{_tempCounter++}";
                Emit(new IrBinaryOp(compareResultName, IrBinaryOp.OpKind.Eq, tagValue, tagConstant, IrBoolType.Instance));
                return new IrVariable(compareResultName, IrBoolType.Instance);
            }
        }

        // For simple C-style enums that are represented as ints
        // Check if there's an enum in scope with this variant
        var enumName = variantName.Contains("::") ? variantName.Substring(0, variantName.LastIndexOf("::")) : null;
        if (enumName != null && _symbols.HasEnum(enumName))
        {
            var enumInfo = _symbols.LookupEnum(enumName);
            if (enumInfo != null)
            {
                var variant = enumInfo.Variants.FirstOrDefault(v => v.Name == variantNameOnly);
                if (variant != null)
                {
                    // For simple enums, compare the value directly against the tag constant
                    var tagConstant = new IrConstant(variant.Tag, exprIr.Type);
                    var compareResultName = $"%matches_cmp_{_tempCounter++}";
                    Emit(new IrBinaryOp(compareResultName, IrBinaryOp.OpKind.Eq, exprIr, tagConstant, IrBoolType.Instance));
                    return new IrVariable(compareResultName, IrBoolType.Instance);
                }
            }
        }

        // Fallback - couldn't resolve variant
        return new IrBoolConstant(false);
    }
}
