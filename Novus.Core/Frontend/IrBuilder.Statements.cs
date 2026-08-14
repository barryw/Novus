using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// IrBuilder partial class containing statement visitor methods.
/// This file contains methods for processing statements: variable declarations,
/// assignments, control flow (if/while/for), returns, defers, asserts, and panics.
/// </summary>
public partial class IrBuilder
{
    public override object? VisitFunctionDeclaration([NotNull] NovusParser.FunctionDeclarationContext context)
    {
        var baseName = context.IDENTIFIER().GetText();
        var returnType = ParseReturnType(context.type());

        // Parse visibility, extern flag, and other modifiers
        var (visibility, isExtern, _, _) = AstModifierHelper.ParseModifiers(context, 5);

        // Parse parameters first so we can compute the mangled name if needed
        var parameters = new List<IrParameter>();
        bool hasVariadic = false;
        if (context.parameterList() != null)
        {
            var paramList = context.parameterList();
            ParseRegularParameters(paramList, parameters);

            // Check for variadic parameter (handled separately below)
            if (paramList.variadicParameter() != null)
            {
                hasVariadic = true;
            }
        }

        // Compute function name - use mangled name if this function is overloaded
        var paramTypes = parameters.Select(p => p.Type).ToList();
        var functionName = GetMangledFunctionName(baseName, paramTypes);

        var function = new IrFunction(functionName, returnType, visibility, isExtern);
        ApplyFunctionAbi(function, context);
        // Store the original unmangled name for display/debugging purposes
        if (functionName != baseName)
        {
            function.OriginalName = baseName;
        }
        _module.AddFunction(function);
        _currentFunction = function;

        // Clear emitted defer blocks tracking for this new function
        _emittedDeferBlocks.Clear();

        // Parse and store function attributes (for @library, @test, @export, etc.)
        // This is CRITICAL for FFI functions that use @library("bsdsocket.library") etc.
        // Also filters out module-level attributes (stack_size, cpu) and applies them to the module
        var attributes = ProcessAndFilterModuleAttributes(context.attribute());
        function.Attributes = attributes;
        if (attributes.Has("export"))
        {
            function.IsExported = true;
        }

        // Parse generic parameters
        function.GenericParameters.AddRange(ParseGenericParameters(context.genericParams()));

        // Parse where clause
        function.WhereClause = ParseWhereClause(context.whereClause());

        // Add pre-parsed parameters to function
        function.Parameters.AddRange(parameters);

        // Add variadic parameter if present
        if (hasVariadic && context.parameterList() != null)
        {
            ParseVariadicParameter(context.parameterList(), function);
        }

        // Skip body processing for extern functions
        if (isExtern)
        {
            return null;
        }

        // Create entry block
        _currentBlock = function.CreateBasicBlock("entry");
        InjectParameterDrops();

        // Set expected type for implicit returns (so match expressions know their result type)
        var savedExpectedType = _expectedType;
        if (returnType is not IrVoidType)
        {
            _expectedType = returnType;
        }

        // Visit function body
        var blockResult = Visit(context.block());

        // Restore previous expected type
        _expectedType = savedExpectedType;

        // If function has non-void return type and block produced a value, add implicit return
        if (returnType is not IrVoidType && blockResult is IrValue lastValue)
        {
            // Only add implicit return if block doesn't already have a terminator
            if (!CurrentBlockHasTerminator())
            {
                _currentBlock!.AddInstruction(new IrReturn(lastValue));
            }
        }

        return null;
    }

    public override object? VisitBlock([NotNull] NovusParser.BlockContext context)
    {
        // Check if this block is being used as a statement (nested block)
        // vs being the function body. Nested blocks need their own scope for drops.
        bool isNestedBlock = context.Parent is NovusParser.StatementContext;

        if (isNestedBlock)
        {
            PushDeferScope();
        }

        IrValue? lastValue = null;
        var statements = context.statement();
        var blockProducesValue = _preserveExpressionResult ||
            context.Parent is NovusParser.MatchArmContext or NovusParser.IfExprContext or
                NovusParser.IfElseChainContext ||
            context.Parent is NovusParser.FunctionDeclarationContext &&
                _currentFunction?.ReturnType is not IrVoidType;

        for (var index = 0; index < statements.Length; index++)
        {
            var savedPreserveExpressionResult = _preserveExpressionResult;
            _preserveExpressionResult = blockProducesValue && index == statements.Length - 1;
            var result = Visit(statements[index]);
            _preserveExpressionResult = savedPreserveExpressionResult;
            // Track the last expression value for implicit returns
            if (result is IrValue value)
            {
                lastValue = value;
            }
            else
            {
                // Non-expression statements clear the last value
                lastValue = null;
            }
        }

        if (isNestedBlock)
        {
            // Pop scope and emit cleanup (drops) for variables declared in this block
            PopDeferScope();
        }

        return lastValue;
    }

    public override object? VisitReturnStatement([NotNull] NovusParser.ReturnStatementContext context)
    {
        // Set current statement location for debug symbols
        _currentStatementLocation = GetLocation(context);

        // Handle postfix conditional (if/unless)
        var postfixCondition = context.postfixCondition();

        HandlePostfixCondition(postfixCondition, () =>
        {
            // Check if there's an expression (bare return for void functions)
            var exprContext = context.expression();

            IrValue? value = null;
            if (exprContext != null)
            {
                // Set expected type for bidirectional type checking
                var savedExpectedType = _expectedType;
                _expectedType = _currentFunction?.ReturnType;

                value = (IrValue?)Visit(exprContext);

                // Restore previous expected type
                _expectedType = savedExpectedType;

                // If returning a variable with Drop, deactivate its defer to prevent double-free
                if (value != null)
                {
                    DeactivateVariableDeferIfMove(value);
                }
            }

            Emit(new IrReturn(value));
        });

        return null;
    }

    public override object? VisitVariableDeclaration([NotNull] NovusParser.VariableDeclarationContext context)
    {
        // Set current statement location for debug symbols
        _currentStatementLocation = GetLocation(context);

        var isMutable = context.KW_VAR() != null;

        // Check if this is a tuple destructuring pattern
        var tuplePatternNode = context.tuplePattern();
        if (tuplePatternNode != null)
        {
            return HandleTupleDestructuring(tuplePatternNode, context.expression(), context.type(), isMutable, context);
        }

        // Check if this is a throwaway binding (_)
        var identifierNode = context.IDENTIFIER();
        var name = identifierNode?.GetText() ?? "_";
        var isThrowaway = name == "_";

        // Parse type annotation if present (before evaluating the expression)
        IrType? annotatedType = null;
        if (context.type() != null)
        {
            annotatedType = ParseType(context.type());
        }

        // Set expected type for bidirectional type checking
        var savedExpectedType = _expectedType;
        _expectedType = annotatedType;

        var value = (IrValue?)Visit(context.expression());

        // Restore previous expected type
        _expectedType = savedExpectedType;

        if (value == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.MissingInitializer,
                $"Variable must have an initial value",
                errorLocation
            );
            return null;
        }

        // For throwaway bindings, just evaluate the expression for side effects
        // and discard the result - don't create a variable
        if (isThrowaway)
        {
            if (_module.TypeImplementsDrop(value.Type))
                InjectDropForTemporary(value);
            return null;
        }

        // Use annotated type if specified, otherwise infer from value
        IrType type = annotatedType ?? value.Type;

        // FORBIDDEN: Specifying array size when initializing with elements
        // The compiler MUST infer the size. Use [T] not [T; N] when providing elements.
        if (annotatedType is IrArrayType annotatedArrayType &&
            annotatedArrayType.Length >= 0 &&
            value is IrArrayLiteral)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.RedundantArraySize,
                $"Do not specify array size when initializing with elements. Use '[{annotatedArrayType.ElementType.Name}]' instead of '[{annotatedArrayType.ElementType.Name}; {annotatedArrayType.Length}]' - the compiler will infer the size.",
                errorLocation
            );
            return null;
        }

        // When annotated type is an unsized array ([T] with Length=-1) and value is an array literal,
        // use the value's type which has the correct inferred size
        if (annotatedType is IrArrayType unsizedArrayType &&
            unsizedArrayType.Length < 0 &&
            value is IrArrayLiteral)
        {
            type = value.Type;
        }

        // C locals are function-scoped. Every shadowing Novus declaration needs
        // distinct storage even when its type matches an earlier binding.
        var uniqueName = UniqueLocalName(name);

        // Create local variable
        var localVar = new IrLocalVariable(uniqueName, type, isMutable);
        _currentFunction!.LocalVariables.Add(localVar);
        // Map BOTH the unique name and original name to this variable.
        // The unique name is needed for C code generation (avoids type conflicts).
        // The original name is needed so subsequent references find this variable.
        _localVariables[uniqueName] = localVar;
        _localVariables[name] = localVar;  // Overwrite so references find the new variable

        // A try expression's Ok payload is a compiler temporary used only to initialize
        // this binding. Extract directly into the binding instead of moving the owned
        // value through another whole-struct copy.
        var coalescedTryValue = false;
        if (value is IrVariable tryValue && tryValue.Name.StartsWith("%try_ok_val_", StringComparison.Ordinal))
        {
            var extract = _currentFunction.BasicBlocks
                .SelectMany(block => block.Instructions)
                .OfType<IrExtractVariantData>()
                .FirstOrDefault(instruction => instruction.ResultName == tryValue.Name);
            if (extract != null)
            {
                extract.ResultName = uniqueName;
                _currentFunction.LocalVariables.RemoveAll(variable => variable.Name == tryValue.Name);
                _localVariables.Remove(tryValue.Name);
                Emit(new IrLocalDecl(uniqueName, type, isMutable, null));
                coalescedTryValue = true;
            }
        }

        if (!coalescedTryValue)
        {
            // If initializing from a variable with Drop, deactivate its defer to prevent double-free
            DeactivateVariableDeferIfMove(value);
            Emit(new IrLocalDecl(uniqueName, type, isMutable, value));
        }

        // Automatic defer for types with drop() method (RAII-style cleanup)
        // For generic types, eagerly instantiate the drop() method if it exists as a template
        if (EnsureDropMethodInstantiated(type))
        {
            InjectAutomaticDrop(uniqueName, type);
        }

        return null;
    }

    private object? HandleTupleDestructuring(NovusParser.TuplePatternContext tuplePattern,
        NovusParser.ExpressionContext exprContext, NovusParser.TypeContext? typeContext,
        bool isMutable, ParserRuleContext fullContext)
    {
        // Parse type annotation if present
        IrType? annotatedType = null;
        if (typeContext != null)
        {
            annotatedType = ParseType(typeContext);
            if (annotatedType is not IrTupleType)
            {
                var errorLocation = GetLocation(fullContext);
                _diagnostics.ReportError(
                    ErrorCodes.TypeMismatch,
                    $"Type annotation for tuple destructuring must be a tuple type",
                    errorLocation
                );
                return null;
            }
        }

        // Set expected type for bidirectional type checking
        var savedExpectedType = _expectedType;
        _expectedType = annotatedType;

        var value = (IrValue?)Visit(exprContext);

        // Restore previous expected type
        _expectedType = savedExpectedType;

        if (value == null)
        {
            var errorLocation = GetLocation(fullContext);
            _diagnostics.ReportError(
                ErrorCodes.MissingInitializer,
                $"Tuple destructuring must have an initial value",
                errorLocation
            );
            return null;
        }

        // Verify the value is a tuple type
        if (value.Type is not IrTupleType tupleType)
        {
            var errorLocation = GetLocation(fullContext);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Cannot destructure non-tuple type '{value.Type.Name}'",
                errorLocation
            );
            return null;
        }

        // Get pattern identifiers (IDENTIFIER or '_')
        var patternElements = new List<string>();
        for (int i = 0; i < tuplePattern.ChildCount; i++)
        {
            var child = tuplePattern.GetChild(i);
            if (child is ITerminalNode terminal && terminal.Symbol.Type == NovusLexer.IDENTIFIER)
            {
                patternElements.Add(terminal.GetText());
            }
            else if (child.GetText() == "_")
            {
                patternElements.Add("_");
            }
        }

        // Verify element count matches
        if (patternElements.Count != tupleType.ElementTypes.Count)
        {
            var errorLocation = GetLocation(fullContext);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Tuple destructuring pattern has {patternElements.Count} elements but value has {tupleType.ElementTypes.Count} elements",
                errorLocation
            );
            return null;
        }

        // Create a temporary variable to hold the tuple value
        var tempName = $"__tuple_tmp_{_tempCounter++}";
        var tempVar = new IrLocalVariable(tempName, tupleType, false);
        _currentFunction!.LocalVariables.Add(tempVar);
        _localVariables[tempName] = tempVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(tempName, tupleType, false, value));

        // Extract each element and create local variables
        for (int i = 0; i < patternElements.Count; i++)
        {
            var elementName = patternElements[i];

            // Skip throwaway bindings
            if (elementName == "_")
                continue;

            var elementType = tupleType.ElementTypes[i];

            // Create local variable for this element
            var elementVar = new IrLocalVariable(elementName, elementType, isMutable);
            _currentFunction.LocalVariables.Add(elementVar);
            _localVariables[elementName] = elementVar;

            // Generate IR to extract this element from the tuple
            // Create a reference to the temporary tuple variable
            var tempVarRef = new IrVariable(tempName, tupleType);
            var extractedValue = new IrTupleElementAccess(tempVarRef, i, elementType);
            _currentBlock.AddInstruction(new IrLocalDecl(elementName, elementType, isMutable, extractedValue));

            // Automatic defer for types with drop() method
            if (EnsureDropMethodInstantiated(elementType))
            {
                InjectAutomaticDrop(elementName, elementType);
            }
        }

        // CRITICAL FIX: If the tuple value is a variable (not a temporary expression result),
        // we need to deactivate its Drop defer block because ownership has been transferred
        // to the destructured element variables. Without this, both the original tuple
        // and the destructured variables would be dropped, causing a double-free.
        if (value is IrVariable sourceVar)
        {
            // Find and remove the defer block for the source variable
            // The defer block label starts with "autoclean_{varName}_"
            if (_scopeDeferStack.Count > 0)
            {
                var currentScopeDefers = _scopeDeferStack.Peek();
                var deferToRemove = currentScopeDefers.FirstOrDefault(
                    d => d.Label.StartsWith($"autoclean_{sourceVar.Name}_"));
                if (deferToRemove != null)
                {
                    currentScopeDefers.Remove(deferToRemove);
                    // Also remove from function-level defers to prevent it being emitted elsewhere
                    _currentFunction!.DeferredBlocks.Remove(deferToRemove);
                    // Mark as emitted to prevent any other code path from emitting it
                    _emittedDeferBlocks.Add(deferToRemove);
                }
            }

            // Also check function-level defers if not in a scope or not found in current scope
            if (_currentFunction != null)
            {
                var functionLevelDefer = _currentFunction.DeferredBlocks.FirstOrDefault(
                    d => d.Label.StartsWith($"autoclean_{sourceVar.Name}_"));
                if (functionLevelDefer != null)
                {
                    _currentFunction.DeferredBlocks.Remove(functionLevelDefer);
                    _emittedDeferBlocks.Add(functionLevelDefer);

                    // CRITICAL: Also remove the IrDefer instruction from ALL blocks
                    // (including the current block that's still being built)
                    // This instruction tells the C code generator to activate the defer flag
                    // We need to remove it so the defer doesn't get activated in the generated code

                    // First check all completed basic blocks
                    foreach (var block in _currentFunction.BasicBlocks)
                    {
                        var deferInstructionsToRemove = block.Instructions
                            .OfType<IrDefer>()
                            .Where(defer => defer.DeferredBlock == functionLevelDefer)
                            .ToList();

                        foreach (var deferInst in deferInstructionsToRemove)
                        {
                            block.Instructions.Remove(deferInst);
                        }
                    }

                    // Also check the current block being built (if different from BasicBlocks)
                    if (_currentBlock != null && !_currentFunction.BasicBlocks.Contains(_currentBlock))
                    {
                        var currentBlockDefers = _currentBlock.Instructions
                            .OfType<IrDefer>()
                            .Where(defer => defer.DeferredBlock == functionLevelDefer)
                            .ToList();

                        foreach (var deferInst in currentBlockDefers)
                        {
                            _currentBlock.Instructions.Remove(deferInst);
                        }
                    }
                }
            }
        }

        return null;
    }

    public override object? VisitAssignmentStatement([NotNull] NovusParser.AssignmentStatementContext context)
    {
        // Set current statement location for debug symbols
        _currentStatementLocation = GetLocation(context);

        // Handle postfix conditional (if/unless)
        var postfixCondition = context.postfixCondition();

        if (postfixCondition != null)
        {
            // Wrap the entire assignment in a postfix conditional
            HandlePostfixCondition(postfixCondition, () => ProcessAssignmentStatement(context));
            return null;
        }

        // No postfix condition - process normally
        return ProcessAssignmentStatement(context);
    }

    private IrValue LowerCompoundAssignment(string? op, IrValue current, IrValue value, IrType type)
    {
        if (op == "=") return value;

        var opKind = op switch
        {
            "+=" => IrBinaryOp.OpKind.Add,
            "-=" => IrBinaryOp.OpKind.Sub,
            "*=" => IrBinaryOp.OpKind.Mul,
            "/=" => IrBinaryOp.OpKind.Div,
            "%=" => IrBinaryOp.OpKind.Mod,
            "&=" => IrBinaryOp.OpKind.And,
            "|=" => IrBinaryOp.OpKind.Or,
            "^=" => IrBinaryOp.OpKind.Xor,
            "<<=" => IrBinaryOp.OpKind.Shl,
            ">>=" => IrBinaryOp.OpKind.Shr,
            _ => throw new InvalidOperationException($"Unknown compound assignment operator: {op}")
        };
        var result = $"%t{_tempCounter++}";
        Emit(new IrBinaryOp(result, opKind, current, value, type));
        return new IrVariable(result, type);
    }

    private IrType? GetAssignmentTargetType(
        string name,
        int dereferenceCount,
        NovusParser.LvalueSuffixContext[] suffixes,
        int suffixesBeforeDereference)
    {
        IrType? type = _localVariables.TryGetValue(name, out var local)
            ? local.Type
            : _currentFunction?.Parameters.FirstOrDefault(parameter => parameter.Name == name)?.Type
              ?? _module.StaticVariables.FirstOrDefault(variable => variable.Name == name)?.Type
              ?? _module.ExternalVariables.FirstOrDefault(variable => variable.Name == name)?.Type;

        for (var suffixIndex = 0; suffixIndex <= suffixes.Length; suffixIndex++)
        {
            if (suffixIndex == suffixesBeforeDereference)
            {
                for (var index = 0; index < dereferenceCount && type != null; index++)
                {
                    type = type switch
                    {
                        IrPointerType pointer => pointer.PointeeType,
                        IrReferenceType reference => reference.PointeeType,
                        IrMutReferenceType reference => reference.PointeeType,
                        _ => null
                    };
                }
            }

            if (suffixIndex == suffixes.Length)
                break;

            var suffix = suffixes[suffixIndex];
            if (type == null)
                return null;

            if (suffix.GetChild(0).GetText() == ".")
            {
                type = type switch
                {
                    IrPointerType memberPointer => memberPointer.PointeeType,
                    IrReferenceType memberReference => memberReference.PointeeType,
                    IrMutReferenceType mutableMemberReference => mutableMemberReference.PointeeType,
                    _ => type
                };
                if (type is not IrStructType structure)
                    return null;
                type = structure.Fields.FirstOrDefault(field =>
                    field.Name == suffix.IDENTIFIER().GetText())?.Type;
                continue;
            }

            type = type switch
            {
                IrReferenceType indexedReference => indexedReference.PointeeType,
                IrMutReferenceType mutableIndexedReference => mutableIndexedReference.PointeeType,
                _ => type
            };
            type = type switch
            {
                IrArrayType array => array.ElementType,
                IrPointerType indexedPointer => indexedPointer.PointeeType,
                _ => GetIndexedAssignmentType(type)
            };
        }

        return type;
    }

    private IrType? GetIndexedAssignmentType(IrType type)
    {
        var typeName = GetTypeNameForOperator(type);
        var methodName = _module.FindTraitMethod(typeName, "index_set");
        var method = methodName == null ? null : _module.GetFunction(methodName);
        return method?.Parameters.Count >= 3 ? method.Parameters[2].Type : null;
    }

    private object? ProcessAssignmentStatement(NovusParser.AssignmentStatementContext context)
    {
        // Declare errorLocation once at method start to avoid CS0136 errors
        SourceLocation errorLocation;

        // Get the lvalue expression (the left side of the assignment)
        var lvalueExpr = context.lvalueExpression();

        // Check for null lvalue expression (shouldn't happen with valid grammar, but handle defensively)
        if (lvalueExpr == null)
        {
            errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InternalCompilerError,
                "Assignment statement has no lvalue expression (internal parser error)",
                errorLocation
            );
            return null;
        }

        // Get the identifier or 'self' keyword from the lvalue expression
        // Note: parenDeref is handled by extracting the identifier from within the parens
        var identifier = lvalueExpr.IDENTIFIER();
        var selfKeyword = lvalueExpr.KW_SELF();
        var parenDeref = lvalueExpr.parenDeref();

        string name;
        bool hasParenDeref = parenDeref != null;

        if (identifier != null)
        {
            name = identifier.GetText();
        }
        else if (selfKeyword != null)
        {
            name = "self";
        }
        else if (hasParenDeref)
        {
            // Handle (*ptr).field style access - extract identifier from parenDeref
            var parenIdentifier = parenDeref!.IDENTIFIER();
            var parenSelf = parenDeref!.KW_SELF();
            if (parenIdentifier != null)
            {
                name = parenIdentifier.GetText();
            }
            else if (parenSelf != null)
            {
                name = "self";
            }
            else
            {
                errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.CannotAssignToExpression,
                    "Parenthesized dereference must contain IDENTIFIER or KW_SELF",
                    errorLocation
                );
                return null;
            }
        }
        else
        {
            errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.CannotAssignToExpression,
                "Assignment statement must have either IDENTIFIER or KW_SELF",
                errorLocation
            );
            return null;
        }
        var storageName = _localVariables.TryGetValue(name, out var scopedLocal)
            ? scopedLocal.Name
            : name;

        // Detect which kind of assignment this is
        string? op = null;
        bool isPostIncDec = false;

        // Check for compound operators and increment/decrement
        for (int i = 0; i < context.ChildCount; i++)
        {
            var childText = context.GetChild(i).GetText();
            if (childText == "+=" || childText == "-=" || childText == "*=" || childText == "/=" ||
                childText == "%=" || childText == "&=" || childText == "|=" || childText == "^=" ||
                childText == "<<=" || childText == ">>=")
            {
                op = childText;
                break;
            }
            else if (childText == "=" && context.expression() != null)
            {
                op = "=";
                break;
            }
            else if (childText == "++" || childText == "--")
            {
                // Post-increment/decrement (after identifier)
                isPostIncDec = true;
                op = childText;
                break;
            }
        }

        // Count dereference operators before the identifier
        int derefCount = 0;
        if (hasParenDeref)
        {
            // For (*ptr).field, count stars inside the parens
            for (int i = 0; i < parenDeref!.ChildCount; i++)
            {
                if (parenDeref!.GetChild(i).GetText() == "*")
                    derefCount++;
                else if (parenDeref!.GetChild(i) is ITerminalNode terminal && terminal.Symbol.Type == NovusLexer.IDENTIFIER)
                    break;
            }
        }
        else
        {
            // For *ptr, count stars at the lvalue expression level
            for (int i = 0; i < lvalueExpr.ChildCount; i++)
            {
                if (lvalueExpr.GetChild(i).GetText() == "*")
                    derefCount++;
                else if (lvalueExpr.GetChild(i) is ITerminalNode terminal && terminal.Symbol.Type == NovusLexer.IDENTIFIER)
                    break;
            }
        }

        // Get lvalue suffixes - combine from both lvalueExpr and parenDeref if present
        var lvalueSuffixes = lvalueExpr.lvalueSuffix();
        NovusParser.LvalueSuffixContext[] parenDerefSuffixes = hasParenDeref ? parenDeref!.lvalueSuffix() : [];

        // If there are suffixes inside the parens, they come first
        if (parenDerefSuffixes.Length > 0)
        {
            lvalueSuffixes = parenDerefSuffixes.Concat(lvalueSuffixes).ToArray();
        }

        var suffixesBeforeDereference = hasParenDeref
            ? parenDerefSuffixes.Length
            : lvalueSuffixes.Length;
        var assignmentTargetType = GetAssignmentTargetType(
            name, derefCount, lvalueSuffixes, suffixesBeforeDereference);

        // Handle post-increment/decrement statements (no expression)
        if (isPostIncDec)
        {
            // Check if there are lvalue suffixes (member/index access)
            if (lvalueSuffixes.Length > 0)
            {
                // Complex lvalue: self.field++ or arr[i]++
                // Build the full lvalue expression and use HandlePostIncrementDecrement

                // Get the variable
                IrValue? baseVar = null;
                if (_localVariables.ContainsKey(name))
                {
                    var localVar = _localVariables[name];
                    baseVar = new IrVariable(localVar.Name, localVar.Type);
                }
                else if (_currentFunction != null)
                {
                    var param = _currentFunction.Parameters.FirstOrDefault(p => p.Name == name);
                    if (param != null)
                    {
                        baseVar = new IrVariable(name, param.Type);
                    }
                }

                // Check for static variables
                if (baseVar == null)
                {
                    var staticVar = _module.StaticVariables.FirstOrDefault(sv => sv.Name == name);
                    if (staticVar != null)
                    {
                        baseVar = new IrGlobalVariable(name, staticVar.Type);
                    }
                }

                // Check for external variables
                if (baseVar == null)
                {
                    var externVar = _module.ExternalVariables.FirstOrDefault(ev => ev.Name == name);
                    if (externVar != null)
                    {
                        baseVar = new IrGlobalVariable(name, externVar.Type);
                    }
                }

                if (baseVar == null)
                {
                    errorLocation = GetLocation(context);
                    _diagnostics.ReportError(
                        ErrorCodes.VariableNotFound,
                        $"Variable {name} not found",
                        errorLocation
                    );
                    return null;
                }

                // Process lvalue suffix chain for increment/decrement
                // We need to build the chain of intermediate loads, then do load-increment-store on the final element
                IrValue currentLValue = baseVar;

                // Process all but the last suffix to build intermediate loads
                for (int i = 0; i < lvalueSuffixes.Length - 1; i++)
                {
                    var suffix = lvalueSuffixes[i];

                    if (suffix.GetChild(0).GetText() == ".")
                    {
                        // Field access
                        var memberName = suffix.IDENTIFIER().GetText();

                        // Auto-dereference pointers and references to structs
                        IrValue actualBase = currentLValue;
                        var structType = currentLValue.Type;
                        if (structType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
                        {
                            actualBase = new IrDereferenceValue(currentLValue, ptrType.PointeeType);
                            structType = ptrType.PointeeType;
                        }
                        else if (structType is IrReferenceType refType && refType.PointeeType is IrStructType)
                        {
                            actualBase = new IrDereferenceValue(currentLValue, refType.PointeeType);
                            structType = refType.PointeeType;
                        }
                        else if (structType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType)
                        {
                            actualBase = new IrDereferenceValue(currentLValue, mutRefType.PointeeType);
                            structType = mutRefType.PointeeType;
                        }

                        if (structType is not IrStructType irStructType)
                        {
                            errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.CannotAccessMember,
                                $"Cannot access member '{memberName}' on non-struct type",
                                errorLocation
                            );
                            return null;
                        }

                        var field = irStructType.Fields.FirstOrDefault(f => f.Name == memberName);
                        if (field == null)
                        {
                            errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.FieldNotFound,
                                $"Field '{memberName}' not found in struct '{irStructType.Name}'",
                                errorLocation
                            );
                            return null;
                        }

                        // Load the intermediate field value
                        var tempName = $"_field_{memberName}_{_tempCounter++}";
                        var loadMember = new IrMemberAccess(tempName, actualBase, memberName, field.Type, field.Offset);
                        Emit(loadMember);
                        currentLValue = new IrVariable(tempName, field.Type);
                    }
                    else if (suffix.GetChild(0).GetText() == "[")
                    {
                        // Index access
                        var indexExpr = (IrValue?)Visit(suffix.expression());
                        if (indexExpr == null)
                        {
                            errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.MissingExpression,
                                "Index expression is required",
                                errorLocation
                            );
                            return null;
                        }

                        // Determine element type
                        IrType elementType;
                        if (currentLValue.Type is IrPointerType pt)
                        {
                            elementType = pt.PointeeType;
                        }
                        else if (currentLValue.Type is IrArrayType at)
                        {
                            elementType = at.ElementType;
                        }
                        else
                        {
                            errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.CannotIndexType,
                                $"Cannot index type '{currentLValue.Type}' - must be pointer or array",
                                errorLocation
                            );
                            return null;
                        }

                        // Load the intermediate indexed value
                        var tempName = $"_indexed_{_tempCounter++}";
                        var loadIndex = new IrIndexAccess(
                            tempName, currentLValue, indexExpr, elementType, IndexBoundsMode(currentLValue));
                        _currentBlock!.AddInstruction(loadIndex);
                        currentLValue = new IrVariable(tempName, elementType);
                    }
                    else
                    {
                        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.InvalidExpressionType,
                            $"Unexpected lvalue suffix: {suffix.GetText()}",
                            errorLocation
                        );
                        return null;
                    }
                }

                // Now handle the last suffix - this is where we do the load-increment-store
                var lastSuffix = lvalueSuffixes[lvalueSuffixes.Length - 1];

                if (lastSuffix.GetChild(0).GetText() == ".")
                {
                    // Final field access: load, increment, store
                    var memberName = lastSuffix.IDENTIFIER().GetText();

                    // Auto-dereference pointers and references to structs
                    IrValue actualBase = currentLValue;
                    var structType = currentLValue.Type;
                    if (structType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
                    {
                        actualBase = new IrDereferenceValue(currentLValue, ptrType.PointeeType);
                        structType = ptrType.PointeeType;
                    }
                    else if (structType is IrReferenceType refType && refType.PointeeType is IrStructType)
                    {
                        actualBase = new IrDereferenceValue(currentLValue, refType.PointeeType);
                        structType = refType.PointeeType;
                    }
                    else if (structType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType)
                    {
                        actualBase = new IrDereferenceValue(currentLValue, mutRefType.PointeeType);
                        structType = mutRefType.PointeeType;
                    }

                    if (structType is not IrStructType irStructType)
                    {
                        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.CannotAccessMember,
                            $"Cannot access member '{memberName}' on non-struct type",
                            errorLocation
                        );
                        return null;
                    }

                    var field = irStructType.Fields.FirstOrDefault(f => f.Name == memberName);
                    if (field == null)
                    {
                        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.FieldNotFound,
                            $"Field '{memberName}' not found in struct '{irStructType.Name}'",
                            errorLocation
                        );
                        return null;
                    }

                    // Load current value
                    var loadTemp = $"%member_load_{_tempCounter++}";
                    Emit(new IrMemberAccess(loadTemp, actualBase, memberName, field.Type, field.Offset));
                    var currentValue = new IrVariable(loadTemp, field.Type);

                    // Increment/decrement
                    var newValueTemp = $"%t{_tempCounter++}";
                    var opKind = (op == "++" ? IrBinaryOp.OpKind.Add : IrBinaryOp.OpKind.Sub);
                    var binOp = new IrBinaryOp(newValueTemp, opKind, currentValue, new IrConstant(1, field.Type), field.Type);
                    Emit(binOp);

                    // Store back
                    var newValue = new IrVariable(newValueTemp, field.Type);
                    Emit(new IrMemberStore(actualBase, memberName, field.Offset, newValue));

                    return null;
                }
                else if (lastSuffix.GetChild(0).GetText() == "[")
                {
                    // Final index access: load, increment, store
                    var indexExpr = (IrValue?)Visit(lastSuffix.expression());
                    if (indexExpr == null)
                    {
                        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.MissingExpression,
                            "Index expression is required",
                            errorLocation
                        );
                        return null;
                    }

                    // Determine element type
                    IrType elementType;
                    if (currentLValue.Type is IrPointerType pt)
                    {
                        elementType = pt.PointeeType;
                    }
                    else if (currentLValue.Type is IrArrayType at)
                    {
                        elementType = at.ElementType;
                    }
                    else
                    {
                        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.CannotIndexType,
                            $"Cannot index type '{currentLValue.Type}' - must be pointer or array",
                            errorLocation
                        );
                        return null;
                    }

                    // Load current value
                    var loadTemp = $"%index_load_{_tempCounter++}";
                    _currentBlock!.AddInstruction(new IrIndexAccess(
                        loadTemp, currentLValue, indexExpr, elementType, IndexBoundsMode(currentLValue)));
                    var currentValue = new IrVariable(loadTemp, elementType);

                    // Increment/decrement
                    var newValueTemp = $"%t{_tempCounter++}";
                    var opKind = (op == "++" ? IrBinaryOp.OpKind.Add : IrBinaryOp.OpKind.Sub);
                    var binOp = new IrBinaryOp(newValueTemp, opKind, currentValue, new IrConstant(1, elementType), elementType);
                    _currentBlock.AddInstruction(binOp);

                    // Store back
                    var newValue = new IrVariable(newValueTemp, elementType);
                    _currentBlock.AddInstruction(new IrIndexStore(
                        currentLValue, indexExpr, newValue, IndexBoundsMode(currentLValue)));

                    return null;
                }
                else
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        $"Unexpected lvalue suffix: {lastSuffix.GetText()}",
                        errorLocation
                    );
                    return null;
                }
            }
            else
            {
                // Simple variable increment/decrement: var++
                IrValue? variable = null;
                IrType? varType = null;

                if (_localVariables.ContainsKey(name))
                {
                    var localVar = _localVariables[name];
                    variable = new IrVariable(localVar.Name, localVar.Type);
                    varType = localVar.Type;
                }
                else if (_currentFunction != null)
                {
                    var param = _currentFunction.Parameters.FirstOrDefault(p => p.Name == name);
                    if (param != null)
                    {
                        variable = new IrVariable(name, param.Type);
                        varType = param.Type;
                    }
                }

                // Check for static variables
                if (variable == null)
                {
                    var staticVar = _module.StaticVariables.FirstOrDefault(sv => sv.Name == name);
                    if (staticVar != null)
                    {
                        variable = new IrGlobalVariable(name, staticVar.Type);
                        varType = staticVar.Type;
                    }
                }

                // Check for external variables
                if (variable == null)
                {
                    var externVar = _module.ExternalVariables.FirstOrDefault(ev => ev.Name == name);
                    if (externVar != null)
                    {
                        variable = new IrGlobalVariable(name, externVar.Type);
                        varType = externVar.Type;
                    }
                }

                if (variable == null || varType == null)
                {
                    errorLocation = GetLocation(context);
                    _diagnostics.ReportError(
                        ErrorCodes.VariableNotFound,
                        $"Variable {name} not found",
                        errorLocation
                    );
                    return null;
                }

                // Increment or decrement: var = var +/- 1
                var resultTemp = $"%t{_tempCounter++}";
                var opKind = (op == "++" ? IrBinaryOp.OpKind.Add : IrBinaryOp.OpKind.Sub);
                var binOp = new IrBinaryOp(resultTemp, opKind, variable, new IrConstant(1, varType), varType);
                _currentBlock!.AddInstruction(binOp);

                // Store back to the variable
                _currentBlock.AddInstruction(new IrStore(storageName, new IrVariable(resultTemp, varType)));

                return null;
            }
        }

        // Check if this is a member or index assignment (has lvalueSuffix elements)
        if (lvalueSuffixes.Length > 0)
        {
            // Handle member/index assignments: obj.field = value, arr[index] = value
            var value = (IrValue?)VisitWithExpectedType(context.expression(), assignmentTargetType);
            if (value == null)
            {
                errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.MissingAssignmentValue,
                    $"Assignment requires a value",
                    errorLocation
                );
                return null;
            }

            // Start with the base variable
            IrValue baseVar;
            if (_localVariables.ContainsKey(name))
            {
                baseVar = new IrVariable(_localVariables[name].Name, _localVariables[name].Type);
            }
            else if (_currentFunction != null && _currentFunction.Parameters.Any(p => p.Name == name))
            {
                var param = _currentFunction.Parameters.First(p => p.Name == name);
                baseVar = new IrVariable(name, param.Type);
            }
            else
            {
                // Check for static variables
                var staticVar = _module.StaticVariables.FirstOrDefault(sv => sv.Name == name);
                if (staticVar != null)
                {
                    baseVar = new IrGlobalVariable(name, staticVar.Type);
                }
                else
                {
                    // Check for external variables
                    var externVar = _module.ExternalVariables.FirstOrDefault(ev => ev.Name == name);
                    if (externVar != null)
                    {
                        baseVar = new IrGlobalVariable(name, externVar.Type);
                    }
                    else
                    {
                        errorLocation = GetLocation(context);
                        _diagnostics.ReportError(
                            ErrorCodes.UndefinedVariable,
                            $"Undefined variable: {name}",
                            errorLocation
                        );
                        return null;
                    }
                }
            }

            // For single member access (e.g., self.value = expr)
            if (derefCount == 0 && lvalueSuffixes.Length == 1 && lvalueSuffixes[0].GetChild(0).GetText() == ".")
            {
                var memberName = lvalueSuffixes[0].IDENTIFIER().GetText();

                // Auto-dereference pointers and references to structs (like in VisitMemberAccessExpr)
                IrValue actualBase = baseVar;
                var structType = baseVar.Type;
                if (structType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
                {
                    // Wrap in IrDereferenceValue for auto-dereference
                    actualBase = new IrDereferenceValue(baseVar, ptrType.PointeeType);
                    structType = ptrType.PointeeType;
                }
                else if (structType is IrReferenceType refType && refType.PointeeType is IrStructType)
                {
                    // Wrap in IrDereferenceValue for auto-dereference
                    actualBase = new IrDereferenceValue(baseVar, refType.PointeeType);
                    structType = refType.PointeeType;
                }
                else if (structType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType)
                {
                    // Wrap in IrDereferenceValue for auto-dereference
                    actualBase = new IrDereferenceValue(baseVar, mutRefType.PointeeType);
                    structType = mutRefType.PointeeType;
                }

                if (structType is not IrStructType irStructType)
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.CannotAccessMember,
                        $"Cannot access member '{memberName}' on non-struct type",
                        errorLocation
                    );
                    return null;
                }

                var field = irStructType.Fields.FirstOrDefault(candidate => candidate.Name == memberName);
                if (field == null)
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.FieldNotFound,
                        $"Field '{memberName}' not found in struct '{irStructType.Name}'",
                        errorLocation
                    );
                    return null;
                }

                // Generate store to struct member (using actualBase which may be dereferenced)

                // If value is a variable with Drop, deactivate its defer to prevent double-free
                DeactivateVariableDeferIfMove(value);

                if (op != "=")
                {
                    var loaded = $"%member_load_{_tempCounter++}";
                    Emit(new IrMemberAccess(loaded, actualBase, memberName, field.Type, field.Offset));
                    value = LowerCompoundAssignment(op, new IrVariable(loaded, field.Type), value, field.Type);
                }
                var storeMember = new IrMemberStore(actualBase, memberName, field.Offset, value);
                Emit(storeMember);

                return null;
            }

            // For single index access (e.g., ptr[0] = value, arr[i] = value)
            if (derefCount == 0 && lvalueSuffixes.Length == 1 && lvalueSuffixes[0].GetChild(0).GetText() == "[")
            {
                // Parse the index expression
                var indexExpr = (IrValue?)Visit(lvalueSuffixes[0].expression());
                if (indexExpr == null)
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.MissingExpression,
                        "Index expression is required",
                        errorLocation
                    );
                    return null;
                }

                // Check if this is a pointer or array (built-in indexing)
                if (baseVar.Type is IrPointerType || baseVar.Type is IrArrayType)
                {
                    // If value is a variable with Drop, deactivate its defer to prevent double-free
                    DeactivateVariableDeferIfMove(value);

                    if (op != "=")
                    {
                        var elementType = baseVar.Type is IrPointerType pointer
                            ? pointer.PointeeType
                            : ((IrArrayType)baseVar.Type).ElementType;
                        var loaded = $"%index_load_{_tempCounter++}";
                        Emit(new IrIndexAccess(
                            loaded, baseVar, indexExpr, elementType, IndexBoundsMode(baseVar)));
                        value = LowerCompoundAssignment(op, new IrVariable(loaded, elementType), value, elementType);
                    }
                    var indexStore = new IrIndexStore(
                        baseVar, indexExpr, value, IndexBoundsMode(baseVar));
                    _currentBlock!.AddInstruction(indexStore);
                    return null;
                }

                // Try IndexMut trait for custom types
                if (EmitIndexMutTraitCall(baseVar, indexExpr, value, context))
                {
                    return null;
                }

                // No built-in indexing and no IndexMut impl
                errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.CannotIndexType,
                    $"Cannot index into type '{baseVar.Type.Name}' for assignment - type does not implement IndexMut",
                    errorLocation
                );
                return null;
            }

            // OPTIMIZATION: Detect patterns ending with [index].field = value
            // This includes: arr[i].field, base.ptr[i].field, etc.
            // We need to emit IrIndexedFieldStore to write directly to the array element,
            // not create a copy of the struct and then write to it.
            if (derefCount == 0 && lvalueSuffixes.Length >= 2 &&
                lvalueSuffixes[lvalueSuffixes.Length - 2].GetChild(0).GetText() == "[" &&
                lvalueSuffixes[lvalueSuffixes.Length - 1].GetChild(0).GetText() == ".")
            {
                // First, process all leading suffixes to get to the array/pointer
                IrValue arrayBase = baseVar;
                for (int i = 0; i < lvalueSuffixes.Length - 2; i++)
                {
                    var suffix = lvalueSuffixes[i];
                    if (suffix.GetChild(0).GetText() == ".")
                    {
                        // Field access - load the field to get to the array
                        var fieldName = suffix.IDENTIFIER().GetText();

                        // Auto-dereference pointers and references
                        IrValue actualBase = arrayBase;
                        var structType = arrayBase.Type;
                        if (structType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
                        {
                            actualBase = new IrDereferenceValue(arrayBase, ptrType.PointeeType);
                            structType = ptrType.PointeeType;
                        }
                        else if (structType is IrReferenceType refType && refType.PointeeType is IrStructType)
                        {
                            actualBase = new IrDereferenceValue(arrayBase, refType.PointeeType);
                            structType = refType.PointeeType;
                        }
                        else if (structType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType)
                        {
                            actualBase = new IrDereferenceValue(arrayBase, mutRefType.PointeeType);
                            structType = mutRefType.PointeeType;
                        }

                        if (structType is IrStructType irStruct)
                        {
                            var leadingField = irStruct.Fields.FirstOrDefault(f => f.Name == fieldName);
                            if (leadingField != null)
                            {
                                var tempName = $"_field_{fieldName}_{_tempCounter++}";
                                var loadMember = new IrMemberAccess(tempName, actualBase, fieldName, leadingField.Type, leadingField.Offset);
                                Emit(loadMember);
                                arrayBase = new IrVariable(tempName, leadingField.Type);
                            }
                        }
                    }
                    else if (suffix.GetChild(0).GetText() == "[")
                    {
                        // Index access - load the element
                        var idxExpr = (IrValue?)Visit(suffix.expression());
                        if (idxExpr != null)
                        {
                            IrType elemType;
                            if (arrayBase.Type is IrPointerType leadingPt)
                                elemType = leadingPt.PointeeType;
                            else if (arrayBase.Type is IrArrayType leadingAt)
                                elemType = leadingAt.ElementType;
                            else
                                break; // Error case - will be caught below

                            var tempName = $"_indexed_{_tempCounter++}";
                            var loadIndex = new IrIndexAccess(
                                tempName, arrayBase, idxExpr, elemType, IndexBoundsMode(arrayBase));
                            _currentBlock!.AddInstruction(loadIndex);
                            arrayBase = new IrVariable(tempName, elemType);
                        }
                    }
                }

                // Parse the final index expression
                var indexExpr = (IrValue?)Visit(lvalueSuffixes[lvalueSuffixes.Length - 2].expression());
                if (indexExpr == null)
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.MissingExpression,
                        "Index expression is required",
                        errorLocation
                    );
                    return null;
                }

                // Get the field name
                var memberName = lvalueSuffixes[lvalueSuffixes.Length - 1].IDENTIFIER().GetText();

                // Determine the element type of the array
                IrType elementType;
                if (arrayBase.Type is IrPointerType pt)
                {
                    elementType = pt.PointeeType;
                }
                else if (arrayBase.Type is IrArrayType at)
                {
                    elementType = at.ElementType;
                }
                else
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.CannotIndexType,
                        $"Cannot index type '{arrayBase.Type}' - must be pointer or array",
                        errorLocation
                    );
                    return null;
                }

                // Verify element is a struct
                if (elementType is not IrStructType irStructType)
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.CannotAccessMember,
                        $"Cannot access member '{memberName}' on non-struct type",
                        errorLocation
                    );
                    return null;
                }

                // Find the field
                var field = irStructType.Fields.FirstOrDefault(f => f.Name == memberName);
                if (field == null)
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.FieldNotFound,
                        $"Field '{memberName}' not found in struct '{irStructType.Name}'",
                        errorLocation
                    );
                    return null;
                }

                // If value is a variable with Drop, deactivate its defer to prevent double-free
                DeactivateVariableDeferIfMove(value);

                if (op != "=")
                {
                    var element = $"%indexed_{_tempCounter++}";
                    Emit(new IrIndexAccess(
                        element, arrayBase, indexExpr, elementType, IndexBoundsMode(arrayBase)));
                    var loaded = $"%member_load_{_tempCounter++}";
                    Emit(new IrMemberAccess(loaded, new IrVariable(element, elementType), memberName, field.Type, field.Offset));
                    value = LowerCompoundAssignment(op, new IrVariable(loaded, field.Type), value, field.Type);
                }
                var indexedFieldStore = new IrIndexedFieldStore(
                    arrayBase, indexExpr, memberName, field.Offset, value, IndexBoundsMode(arrayBase));
                _currentBlock!.AddInstruction(indexedFieldStore);
                return null;
            }

            // Handle complex lvalue chains (e.g., self.ptr[index] = value, self.field1.field2 = value)
            // Build up the lvalue by processing each suffix in order
            IrValue currentLValue = baseVar;
            var storeThroughDereference = derefCount > 0 &&
                suffixesBeforeDereference == lvalueSuffixes.Length;

            for (int i = 0; i < lvalueSuffixes.Length; i++)
            {
                if (derefCount > 0 && i == suffixesBeforeDereference)
                {
                    for (var dereference = 0; dereference < derefCount; dereference++)
                    {
                        var pointeeType = currentLValue.Type switch
                        {
                            IrPointerType pointer => pointer.PointeeType,
                            IrReferenceType reference => reference.PointeeType,
                            IrMutReferenceType reference => reference.PointeeType,
                            _ => null
                        };
                        if (pointeeType == null)
                        {
                            _diagnostics.ReportError(ErrorCodes.CannotDereferenceType,
                                $"Cannot dereference non-pointer type '{currentLValue.Type}'", GetLocation(context));
                            return null;
                        }
                        currentLValue = new IrDereferenceValue(currentLValue, pointeeType);
                    }
                }

                var suffix = lvalueSuffixes[i];
                bool isLastSuffix = (i == lvalueSuffixes.Length - 1);

                if (suffix.GetChild(0).GetText() == ".")
                {
                    // Field access
                    var memberName = suffix.IDENTIFIER().GetText();

                    // Auto-dereference pointers and references to structs
                    IrValue actualBase = currentLValue;
                    var structType = currentLValue.Type;
                    if (structType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
                    {
                        actualBase = new IrDereferenceValue(currentLValue, ptrType.PointeeType);
                        structType = ptrType.PointeeType;
                    }
                    else if (structType is IrReferenceType refType && refType.PointeeType is IrStructType)
                    {
                        actualBase = new IrDereferenceValue(currentLValue, refType.PointeeType);
                        structType = refType.PointeeType;
                    }
                    else if (structType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType)
                    {
                        actualBase = new IrDereferenceValue(currentLValue, mutRefType.PointeeType);
                        structType = mutRefType.PointeeType;
                    }

                    if (structType is not IrStructType irStructType)
                    {
                        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.CannotAccessMember,
                            $"Cannot access member '{memberName}' on non-struct type",
                            errorLocation
                        );
                        return null;
                    }

                    // Find the field
                    var field = irStructType.Fields.FirstOrDefault(f => f.Name == memberName);
                    if (field == null)
                    {
                        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.FieldNotFound,
                            $"Field '{memberName}' not found in struct '{irStructType.Name}'",
                            errorLocation
                        );
                        return null;
                    }

                    if (isLastSuffix && !storeThroughDereference)
                    {
                        // This is the final field - emit a store

                        // If value is a variable with Drop, deactivate its defer to prevent double-free
                        DeactivateVariableDeferIfMove(value);

                        if (op != "=")
                        {
                            var loaded = $"%member_load_{_tempCounter++}";
                            Emit(new IrMemberAccess(loaded, actualBase, memberName, field.Type, field.Offset));
                            value = LowerCompoundAssignment(op, new IrVariable(loaded, field.Type), value, field.Type);
                        }
                        var storeMember = new IrMemberStore(actualBase, memberName, field.Offset, value);
                        Emit(storeMember);
                        return null;
                    }
                    else
                    {
                        // This is an intermediate field - load it for the next suffix
                        var tempName = $"_field_{memberName}_{_tempCounter++}";
                        var loadMember = new IrMemberAccess(tempName, actualBase, memberName, field.Type, field.Offset);
                        Emit(loadMember);
                        currentLValue = new IrVariable(tempName, field.Type);
                    }
                }
                else if (suffix.GetChild(0).GetText() == "[")
                {
                    // Index access
                    var indexExpr = (IrValue?)Visit(suffix.expression());
                    if (indexExpr == null)
                    {
                        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.MissingExpression,
                            "Index expression is required",
                            errorLocation
                        );
                        return null;
                    }

                    if (isLastSuffix && !storeThroughDereference)
                    {
                        // This is the final index - emit an index store

                        // If value is a variable with Drop, deactivate its defer to prevent double-free
                        DeactivateVariableDeferIfMove(value);

                        if (op != "=")
                        {
                            var elementType = currentLValue.Type is IrPointerType pointer
                                ? pointer.PointeeType
                                : ((IrArrayType)currentLValue.Type).ElementType;
                            var loaded = $"%index_load_{_tempCounter++}";
                            Emit(new IrIndexAccess(
                                loaded, currentLValue, indexExpr, elementType, IndexBoundsMode(currentLValue)));
                            value = LowerCompoundAssignment(op, new IrVariable(loaded, elementType), value, elementType);
                        }
                        var indexStore = new IrIndexStore(
                            currentLValue, indexExpr, value, IndexBoundsMode(currentLValue));
                        _currentBlock!.AddInstruction(indexStore);
                        return null;
                    }
                    else
                    {
                        // This is an intermediate index - load it for the next suffix
                        IrType elementType;
                        if (currentLValue.Type is IrPointerType pt)
                        {
                            elementType = pt.PointeeType;
                        }
                        else if (currentLValue.Type is IrArrayType at)
                        {
                            elementType = at.ElementType;
                        }
                        else
                        {
                            errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.CannotIndexType,
                                $"Cannot index type '{currentLValue.Type}' - must be pointer or array",
                                errorLocation
                            );
                            return null;
                        }

                        var tempName = $"_indexed_{_tempCounter++}";
                        var loadIndex = new IrIndexAccess(
                            tempName, currentLValue, indexExpr, elementType, IndexBoundsMode(currentLValue));
                        _currentBlock!.AddInstruction(loadIndex);
                        currentLValue = new IrVariable(tempName, elementType);
                    }
                }
                else
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        $"Unexpected lvalue suffix: {suffix.GetText()}",
                        errorLocation
                    );
                    return null;
                }
            }

            if (storeThroughDereference)
            {
                IrValue pointer = currentLValue;
                var pointerType = currentLValue.Type;
                for (var dereference = 0; dereference < derefCount - 1; dereference++)
                {
                    var pointeeType = pointerType switch
                    {
                        IrPointerType rawPointer => rawPointer.PointeeType,
                        IrReferenceType reference => reference.PointeeType,
                        IrMutReferenceType reference => reference.PointeeType,
                        _ => null
                    };
                    if (pointeeType == null)
                    {
                        _diagnostics.ReportError(ErrorCodes.CannotDereferenceType,
                            $"Cannot dereference non-pointer type '{pointerType}'", GetLocation(context));
                        return null;
                    }
                    pointer = new IrDereferenceValue(pointer, pointeeType);
                    pointerType = pointeeType;
                }
                var targetType = pointerType switch
                {
                    IrPointerType rawPointer => rawPointer.PointeeType,
                    IrReferenceType reference => reference.PointeeType,
                    IrMutReferenceType reference => reference.PointeeType,
                    _ => pointerType
                };
                if (op != "=")
                    value = LowerCompoundAssignment(op, new IrDereferenceValue(pointer, targetType), value, targetType);
                DeactivateVariableDeferIfMove(value);
                Emit(new IrDereferenceStore(pointer, value));
                return null;
            }

            // If we get here, something went wrong
            errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Failed to process lvalue chain",
                errorLocation
            );
            return null;
        }

        if (derefCount > 0)
        {
            // Dereference assignment: *ptr = value or **ptr = value, etc.
            var value = (IrValue?)VisitWithExpectedType(context.expression(), assignmentTargetType);

            if (value == null)
            {
                errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Assignment to dereferenced variable requires a value",
                    errorLocation
                );
                return null;
            }

            // Get the variable
            IrValue? variable = null;
            IrType? varType = null;

            if (_localVariables.ContainsKey(name))
            {
                var localVar = _localVariables[name];
                variable = new IrVariable(localVar.Name, localVar.Type);
                varType = localVar.Type;
            }
            else if (_currentFunction != null)
            {
                var param = _currentFunction.Parameters.FirstOrDefault(p => p.Name == name);
                if (param != null)
                {
                    variable = new IrVariable(name, param.Type);
                    varType = param.Type;
                }
            }

            // Check for static variables
            if (variable == null)
            {
                var staticVar = _module.StaticVariables.FirstOrDefault(sv => sv.Name == name);
                if (staticVar != null)
                {
                    variable = new IrGlobalVariable(name, staticVar.Type);
                    varType = staticVar.Type;
                }
            }

            // Check for external variables
            if (variable == null)
            {
                var externVar = _module.ExternalVariables.FirstOrDefault(ev => ev.Name == name);
                if (externVar != null)
                {
                    variable = new IrGlobalVariable(name, externVar.Type);
                    varType = externVar.Type;
                }
            }

            if (variable == null || varType == null)
            {
                errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.VariableNotFound,
                    $"Variable {name} not found",
                    errorLocation
                );
                return null;
            }

            // Apply dereferences to get the pointer/reference
            IrValue pointer = variable;
            for (int i = 0; i < derefCount - 1; i++)
            {
                // For multiple dereferences, each dereference gives us another pointer
                IrType pointeeType;
                if (varType is IrPointerType ptrType)
                    pointeeType = ptrType.PointeeType;
                else if (varType is IrReferenceType refType)
                    pointeeType = refType.PointeeType;
                else if (varType is IrMutReferenceType mutRefType)
                    pointeeType = mutRefType.PointeeType;
                else
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.CannotDereferenceType,
                        $"Cannot dereference non-pointer type",
                        errorLocation
                    );
                    return null;
                }

                pointer = new IrDereferenceValue(pointer, pointeeType);
                varType = pointeeType;
            }

            if (op != "=")
            {
                var targetType = varType switch
                {
                    IrPointerType pointerType => pointerType.PointeeType,
                    IrReferenceType referenceType => referenceType.PointeeType,
                    IrMutReferenceType referenceType => referenceType.PointeeType,
                    _ => varType
                };
                value = LowerCompoundAssignment(op, new IrDereferenceValue(pointer, targetType), value, targetType);
            }
            Emit(new IrDereferenceStore(pointer, value));
        }
        else
        {
            // Simple variable assignment or compound operator: x = value or x += value
            var value = (IrValue?)VisitWithExpectedType(context.expression(), assignmentTargetType);

            if (value == null)
            {
                errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Assignment to {name} requires a value",
                    errorLocation
                );
                return null;
            }

            // Handle compound operators by desugaring them to binary ops
            if (op != "=")
            {
                // Get the variable
                IrValue? variable = null;
                IrType? varType = null;

                if (_localVariables.ContainsKey(name))
                {
                    var localVar = _localVariables[name];
                    variable = new IrVariable(localVar.Name, localVar.Type);
                    varType = localVar.Type;
                }
                else if (_currentFunction != null)
                {
                    var param = _currentFunction.Parameters.FirstOrDefault(p => p.Name == name);
                    if (param != null)
                    {
                        variable = new IrVariable(name, param.Type);
                        varType = param.Type;
                    }
                }

                // Check for static variables
                if (variable == null)
                {
                    var staticVar = _module.StaticVariables.FirstOrDefault(sv => sv.Name == name);
                    if (staticVar != null)
                    {
                        variable = new IrGlobalVariable(name, staticVar.Type);
                        varType = staticVar.Type;
                    }
                }

                // Check for external variables
                if (variable == null)
                {
                    var externVar = _module.ExternalVariables.FirstOrDefault(ev => ev.Name == name);
                    if (externVar != null)
                    {
                        variable = new IrGlobalVariable(name, externVar.Type);
                        varType = externVar.Type;
                    }
                }

                if (variable == null || varType == null)
                {
                    errorLocation = GetLocation(context);
                    _diagnostics.ReportError(
                        ErrorCodes.VariableNotFound,
                        $"Variable {name} not found",
                        errorLocation
                    );
                    return null;
                }

                value = LowerCompoundAssignment(op, variable, value, varType);
                Emit(new IrStore(storageName, value));
            }
            else
            {
                // Simple assignment: x = value
                // The semantic analyzer will check if the variable is mutable
                // Here we just generate the IR

                // If value is a variable with Drop, deactivate its defer to prevent double-free
                DeactivateVariableDeferIfMove(value);

                Emit(new IrStore(storageName, value));
            }
        }

        return null;
    }

    public override object? VisitExpressionStatement([NotNull] NovusParser.ExpressionStatementContext context)
    {
        // Set current statement location for debug symbols
        _currentStatementLocation = GetLocation(context);

        // Handle postfix conditional (if/unless)
        var postfixCondition = context.postfixCondition();

        object? result = null;
        HandlePostfixCondition(postfixCondition, () =>
        {
            // Visit the expression and return its value (for implicit returns)
            result = Visit(context.expression());
        });

        // CRITICAL: If the expression result is a value with a Drop type and it's not being
        // assigned to anything (i.e., the result is discarded), we must inject cleanup code.
        // This prevents memory leaks when ignoring return values like Option<T> where T has Drop.
        if (!_preserveExpressionResult && result is IrValue irValue && _module.TypeImplementsDrop(irValue.Type))
        {
            InjectDropForTemporary(irValue);
        }

        if (result is IrValue resultValue &&
            ResultUsagePolicy.IsResult(resultValue.Type) &&
            !ResultUsagePolicy.IsConsumed(context, _currentFunction?.ReturnType))
        {
            _diagnostics.ReportError(
                ErrorCodes.UnusedResult,
                "unused Result must be handled",
                GetLocation(context),
                helpTexts: new List<string>
                {
                    "propagate the error with `?`",
                    "handle both variants with `match`",
                    "or explicitly discard it with `let _ = expression`"
                }
            );
        }

        return result;
    }

    public override object? VisitIfStatement([NotNull] NovusParser.IfStatementContext context)
    {
        // Set current statement location for debug symbols
        _currentStatementLocation = GetLocation(context);

        var thenLabel = $"if_then_{_labelCounter}";
        var elseLabel = $"if_else_{_labelCounter}";
        var endLabel = $"if_end_{_labelCounter}";
        _labelCounter++;

        var hasElse = context.GetChild(4) != null; // Check if 'else' keyword exists
        var falseTarget = hasElse ? elseLabel : endLabel;
        var parentStatement = context.Parent as NovusParser.StatementContext;
        var parentBlock = parentStatement?.Parent as NovusParser.BlockContext;
        var captureTailValue = hasElse && parentStatement != null &&
                               ReferenceEquals(parentBlock?.statement().LastOrDefault(), parentStatement) &&
                               _expectedType is not IrVoidType;
        string? resultTemp = null;
        IrType? resultType = null;

        // Visit condition - this will emit the condition check and conditional branch
        // Pass labels to the condition visitor via a tuple
        _ifLabels = (thenLabel, falseTarget);
        Visit(context.ifCondition());
        _ifLabels = null;

        // Then block
        _currentBlock!.AddInstruction(new IrLabel(thenLabel));

        // If we have a pending if let/var variable, declare it now
        if (_pendingIfLetVariable != null)
        {
            var (varName, tempName, type, isMutable) = _pendingIfLetVariable.Value;

            // Declare the variable and initialize it with the temp
            var tempVar = new IrVariable(tempName, type);
            var localVar = new IrLocalVariable(varName, type, isMutable);
            _currentFunction!.LocalVariables.Add(localVar);
            _localVariables[varName] = localVar;
            _currentBlock!.AddInstruction(new IrLocalDecl(varName, type, isMutable, tempVar));

            _pendingIfLetVariable = null;
        }

        var thenValue = Visit(context.block(0)) as IrValue;

        // Track whether the then block terminates
        bool thenBranchTerminates = CurrentBlockHasTerminator();

        if (captureTailValue && !thenBranchTerminates && thenValue != null && thenValue.Type is not IrVoidType)
        {
            resultTemp = $"%t{_tempCounter++}";
            resultType = thenValue!.Type;
            _currentFunction!.LocalVariables.Add(new IrLocalVariable(resultTemp, resultType, false));
            _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, resultType, false, thenValue));
        }

        // Jump to end if there's an else clause (but only if block doesn't already end with return/branch)
        if (hasElse)
        {
            if (!thenBranchTerminates)
            {
                _currentBlock!.AddInstruction(new IrBranch(endLabel));
            }

            // Else block
            _currentBlock!.AddInstruction(new IrLabel(elseLabel));

            // Check if it's 'else if' or 'else' block
            IrValue? elseValue = null;
            if (context.ifStatement() != null)
            {
                elseValue = Visit(context.ifStatement()) as IrValue;
            }
            else if (context.block().Length > 1)
            {
                elseValue = Visit(context.block(1)) as IrValue;
            }

            // Track whether the else block terminates
            bool elseBranchTerminates = CurrentBlockHasTerminator();

            if (resultTemp != null && resultType != null && !elseBranchTerminates && elseValue != null)
            {
                _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, resultType, false, elseValue));
            }

            // Only emit the end label if at least one branch can reach it
            if (!thenBranchTerminates || !elseBranchTerminates)
            {
                _currentBlock!.AddInstruction(new IrLabel(endLabel));
            }

            if (resultTemp != null && resultType != null && elseValue != null)
            {
                return new IrVariable(resultTemp, resultType);
            }
        }
        else
        {
            // No else clause - always need the end label for the false path
            _currentBlock!.AddInstruction(new IrLabel(endLabel));
        }

        return null;
    }

    // Helper to pass labels to condition visitors
    private (string thenLabel, string falseTarget)? _ifLabels;

    public override object? VisitIfConditionExpression([NotNull] NovusParser.IfConditionExpressionContext context)
    {
        var condition = Visit(context.expression()) as IrValue;

        // If condition had an error, bail out
        if (condition == null)
        {
            return null;
        }

        // Check that _ifLabels has been set by VisitIfStatement
        // This can be null if an if-expression is used in an unexpected context
        if (!_ifLabels.HasValue)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InternalCompilerError,
                "Internal compiler error: if condition visited without labels set. " +
                "This may happen if an if-expression is used in an unsupported context.",
                errorLocation
            );
            return null;
        }

        var (thenLabel, falseTarget) = _ifLabels.Value;

        condition = CoerceConditionToBool(condition);

        // Branch based on condition
        _currentBlock!.AddInstruction(new IrConditionalBranch(condition, thenLabel, falseTarget));
        return null;
    }

    public override object? VisitIfConditionLet([NotNull] NovusParser.IfConditionLetContext context)
    {
        // if let variable = expression { ... } else { ... }
        // Translates to:
        //   temp = expression
        //   if temp != 0 goto then_label
        //   goto else_label
        // then_label:
        //   variable = temp
        //   ... (then block)

        var (thenLabel, falseTarget) = _ifLabels!.Value;
        var varName = context.IDENTIFIER().GetText();
        var expression = (IrValue?)Visit(context.expression());

        if (expression == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"if let expression returned null",
                errorLocation
            );
            return null;
        }

        // Store expression in a temp
        var tempName = $"%if_let_{_labelCounter++}";
        var tempLocal = new IrLocalVariable(tempName, expression.Type, true);
        _currentFunction!.LocalVariables.Add(tempLocal);
        _localVariables[tempName] = tempLocal;
        _currentBlock!.AddInstruction(new IrLocalDecl(tempName, expression.Type, true, expression));

        // Check if non-zero
        IrValue zeroValue;
        if (expression.Type is IrPointerType)
        {
            zeroValue = new IrConstant(0, IrIntType.U32);
        }
        else if (expression.Type is IrIntType intType)
        {
            zeroValue = new IrConstant(0, intType);
        }
        else
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"if let only works with pointers or integers, got {expression.Type.Name}",
                errorLocation
            );
            return null;
        }

        // Create comparison: temp != 0
        var resultTemp = $"%t{_tempCounter++}";
        var tempVar = new IrVariable(tempName, expression.Type);
        var comparison = new IrBinaryOp(resultTemp, IrBinaryOp.OpKind.Ne, tempVar, zeroValue, IrBoolType.Instance);
        _currentBlock!.AddInstruction(comparison);

        // Branch: if (comparison) goto then, else goto false
        var comparisonResult = new IrVariable(resultTemp, IrBoolType.Instance);
        _currentBlock!.AddInstruction(new IrConditionalBranch(comparisonResult, thenLabel, falseTarget));

        // In the then block, we need to declare the variable with the non-null value
        // But we can't do it here because we haven't emitted the then label yet
        // Store it for later
        _pendingIfLetVariable = (varName, tempName, expression.Type, false); // false = immutable

        return null;
    }

    public override object? VisitIfConditionVar([NotNull] NovusParser.IfConditionVarContext context)
    {
        // if var variable = expression { ... } else { ... }
        // Same as if let, but variable is mutable

        var (thenLabel, falseTarget) = _ifLabels!.Value;
        var varName = context.IDENTIFIER().GetText();
        var expression = (IrValue?)Visit(context.expression());

        if (expression == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"if var expression returned null",
                errorLocation
            );
            return null;
        }

        // Store expression in a temp
        var tempName = $"%if_var_{_labelCounter++}";
        var tempLocal = new IrLocalVariable(tempName, expression.Type, true);
        _currentFunction!.LocalVariables.Add(tempLocal);
        _localVariables[tempName] = tempLocal;
        _currentBlock!.AddInstruction(new IrLocalDecl(tempName, expression.Type, true, expression));

        // Check if non-zero
        IrValue zeroValue;
        if (expression.Type is IrPointerType)
        {
            zeroValue = new IrConstant(0, IrIntType.U32);
        }
        else if (expression.Type is IrIntType intType)
        {
            zeroValue = new IrConstant(0, intType);
        }
        else
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"if var only works with pointers or integers, got {expression.Type.Name}",
                errorLocation
            );
            return null;
        }

        // Create comparison: temp != 0
        var resultTemp = $"%t{_tempCounter++}";
        var tempVar = new IrVariable(tempName, expression.Type);
        var comparison = new IrBinaryOp(resultTemp, IrBinaryOp.OpKind.Ne, tempVar, zeroValue, IrBoolType.Instance);
        _currentBlock!.AddInstruction(comparison);

        // Branch: if (comparison) goto then, else goto false
        var comparisonResult = new IrVariable(resultTemp, IrBoolType.Instance);
        _currentBlock!.AddInstruction(new IrConditionalBranch(comparisonResult, thenLabel, falseTarget));

        // Store for declaring in then block
        _pendingIfLetVariable = (varName, tempName, expression.Type, true); // true = mutable

        return null;
    }

    // Helper to declare the if let/var variable in the then block
    private (string varName, string tempName, IrType type, bool isMutable)? _pendingIfLetVariable;

    public override object? VisitLabeledLoop([NotNull] NovusParser.LabeledLoopContext context)
    {
        // Get the label name
        var labelName = context.IDENTIFIER().GetText();

        // Check for duplicate label name
        if (_labeledLoops.ContainsKey(labelName))
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"duplicate loop label '{labelName}'",
                errorLocation
            );
            return null;
        }

        // Determine which loop type we have and visit it
        // The loop will push its exit/continue labels to the stacks
        // We need to capture those labels to register with the label name

        // Generate labels for this loop
        var exitLabel = $"labeled_{labelName}_end_{_labelCounter}";
        var continueLabel = $"labeled_{labelName}_cont_{_labelCounter}";
        _labelCounter++;

        // Pre-register the labels (the actual loop will use these via peek after we push them)
        _loopExitLabels.Push(exitLabel);
        _loopContinueLabels.Push(continueLabel);

        // Register the labeled loop
        _labeledLoops[labelName] = (exitLabel, continueLabel);

        // Visit the inner loop - we need to handle this specially since we've already pushed the labels
        if (context.whileStatement() != null)
        {
            var whileCtx = context.whileStatement();

            // Handle the two while statement alternatives
            if (whileCtx is NovusParser.WhileExprContext whileExprCtx)
            {
                // Standard while expression: while condition { ... }
                // Jump to condition check
                _currentBlock!.AddInstruction(new IrBranch(continueLabel));

                // Continue label (condition check)
                _currentBlock!.AddInstruction(new IrLabel(continueLabel));
                var condition = (IrValue?)Visit(whileExprCtx.expression());
                if (condition == null)
                {
                    var errorLocation = GetLocation(whileExprCtx);
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        "While condition expression is null or invalid",
                        errorLocation
                    );
                    return null;
                }

                condition = CoerceConditionToBool(condition);

                var bodyLabel = $"labeled_{labelName}_body_{_labelCounter - 1}";
                _currentBlock!.AddInstruction(new IrConditionalBranch(condition, bodyLabel, exitLabel));

                // Body
                _currentBlock!.AddInstruction(new IrLabel(bodyLabel));
                Visit(whileExprCtx.block());

                // Jump back to condition
                if (!CurrentBlockHasTerminator())
                {
                    _currentBlock!.AddInstruction(new IrBranch(continueLabel));
                }
            }
            else if (whileCtx is NovusParser.WhileVarContext whileVarCtx)
            {
                // Inline variable while: while var i < expr { ... }
                var varName = whileVarCtx.IDENTIFIER().GetText();

                // Evaluate RHS to determine type
                var rhsValue = (IrValue?)Visit(whileVarCtx.expression());
                if (rhsValue == null)
                {
                    var errorLocation = GetLocation(whileVarCtx);
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        "While condition RHS expression is null or invalid",
                        errorLocation
                    );
                    return null;
                }

                // Determine variable type
                IrType varType = whileVarCtx.type() != null
                    ? _typeParser.ParseType(whileVarCtx.type())
                    : rhsValue.Type;

                // Create loop variable initialized to zero
                var zeroValue = CreateZeroValue(varType);
                if (zeroValue == null)
                {
                    var errorLocation = GetLocation(whileVarCtx);
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        $"Cannot create zero value for type '{varType.Name}'",
                        errorLocation
                    );
                    return null;
                }

                // Create local variable (scoped to while block)
                var localVar = new IrLocalVariable(varName, varType, true);
                _currentFunction!.LocalVariables.Add(localVar);
                _localVariables[varName] = localVar;
                Emit(new IrLocalDecl(varName, varType, true, zeroValue));

                // Jump to condition check
                _currentBlock!.AddInstruction(new IrBranch(continueLabel));

                // Continue label (condition check)
                _currentBlock!.AddInstruction(new IrLabel(continueLabel));

                // Build comparison
                var varValue = new IrVariable(varName, varType);
                var compOp = GetComparisonOp(whileVarCtx.comparisonOp());
                var conditionTemp = $"%t{_tempCounter++}";
                var comparison = new IrBinaryOp(conditionTemp, compOp, varValue, rhsValue, IrBoolType.Instance);
                _currentBlock!.AddInstruction(comparison);
                var condition = new IrVariable(conditionTemp, IrBoolType.Instance);

                var bodyLabel = $"labeled_{labelName}_body_{_labelCounter - 1}";
                _currentBlock!.AddInstruction(new IrConditionalBranch(condition, bodyLabel, exitLabel));

                // Body
                _currentBlock!.AddInstruction(new IrLabel(bodyLabel));
                Visit(whileVarCtx.block());

                // Jump back to condition
                if (!CurrentBlockHasTerminator())
                {
                    _currentBlock!.AddInstruction(new IrBranch(continueLabel));
                }
            }
        }
        else if (context.forStatement() != null)
        {
            // For loops are more complex - visit the child and it will use our pushed labels
            // But we need to prevent it from pushing its own labels
            // For now, let's pop our labels and let the for statement handle it normally
            _loopExitLabels.Pop();
            _loopContinueLabels.Pop();

            // Update the registered labels to match what the for loop will generate
            var forCtx = context.forStatement();
            if (forCtx is NovusParser.ForCStyleContext)
            {
                var incrLabel = $"for_incr_{_labelCounter}";
                var endLabel = $"for_end_{_labelCounter}";
                _labeledLoops[labelName] = (endLabel, incrLabel);
            }
            else // ForInLoop
            {
                // Check if this is a range expression (uses range_* labels) or iterable (uses for_* labels)
                var forInCtx = forCtx as NovusParser.ForInLoopContext;
                var exprCtx = forInCtx?.expression();
                bool isRangeExpr = exprCtx is NovusParser.RangeExprContext or NovusParser.RangeInclusiveExprContext;

                if (isRangeExpr)
                {
                    var incrLabel = $"range_incr_{_labelCounter}";
                    var endLabel = $"range_end_{_labelCounter}";
                    _labeledLoops[labelName] = (endLabel, incrLabel);
                }
                else
                {
                    var incrLabel = $"for_incr_{_labelCounter}";
                    var endLabel = $"for_end_{_labelCounter}";
                    _labeledLoops[labelName] = (endLabel, incrLabel);
                }
            }

            Visit(forCtx);
        }
        else if (context.foreverStatement() != null)
        {
            // Forever loop - continue goes to body
            var foreverCtx = context.foreverStatement();

            // Body label
            _currentBlock!.AddInstruction(new IrLabel(continueLabel)); // Continue jumps here
            Visit(foreverCtx.block());

            // Jump back to start
            if (!CurrentBlockHasTerminator())
            {
                _currentBlock!.AddInstruction(new IrBranch(continueLabel));
            }
        }

        // Exit label
        _currentBlock!.AddInstruction(new IrLabel(exitLabel));

        // Clean up
        if (context.forStatement() == null)
        {
            _loopExitLabels.Pop();
            _loopContinueLabels.Pop();
        }
        _labeledLoops.Remove(labelName);

        return null;
    }

    public override object? VisitWhileExpr([NotNull] NovusParser.WhileExprContext context)
    {
        // Set current statement location for debug symbols
        _currentStatementLocation = GetLocation(context);

        var condLabel = $"while_cond_{_labelCounter}";
        var bodyLabel = $"while_body_{_labelCounter}";
        var endLabel = $"while_end_{_labelCounter}";
        _labelCounter++;

        // Push exit and continue labels for break/continue statements
        _loopExitLabels.Push(endLabel);
        _loopContinueLabels.Push(condLabel); // continue jumps to condition check

        // Jump to condition check
        _currentBlock!.AddInstruction(new IrBranch(condLabel));

        // Condition label
        _currentBlock!.AddInstruction(new IrLabel(condLabel));
        var condition = (IrValue?)Visit(context.expression());
        if (condition == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "While condition expression is null or invalid",
                errorLocation
            );
            return null;
        }

        condition = CoerceConditionToBool(condition);

        _currentBlock!.AddInstruction(new IrConditionalBranch(condition, bodyLabel, endLabel));

        // Body label
        _currentBlock!.AddInstruction(new IrLabel(bodyLabel));
        Visit(context.block());

        // Jump back to condition (only if block doesn't end with return/break)
        if (!CurrentBlockHasTerminator())
        {
            _currentBlock!.AddInstruction(new IrBranch(condLabel));
        }

        // End label
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        // Pop exit and continue labels
        _loopExitLabels.Pop();
        _loopContinueLabels.Pop();
        return null;
    }

    /// <summary>
    /// Handle while loops with inline variable declaration: while var i < expr { ... }
    /// The variable is initialized to zero, type-inferred from the RHS, and scoped to the loop.
    /// </summary>
    public override object? VisitWhileVar([NotNull] NovusParser.WhileVarContext context)
    {
        // Set current statement location for debug symbols
        _currentStatementLocation = GetLocation(context);

        var condLabel = $"while_cond_{_labelCounter}";
        var bodyLabel = $"while_body_{_labelCounter}";
        var endLabel = $"while_end_{_labelCounter}";
        _labelCounter++;

        // Push exit and continue labels for break/continue statements
        _loopExitLabels.Push(endLabel);
        _loopContinueLabels.Push(condLabel); // continue jumps to condition check

        // Get variable name
        var varName = context.IDENTIFIER().GetText();

        // Evaluate the RHS expression to determine type
        var rhsValue = (IrValue?)Visit(context.expression());
        if (rhsValue == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "While condition RHS expression is null or invalid",
                errorLocation
            );
            return null;
        }

        // Determine variable type: explicit type annotation or inferred from RHS
        IrType varType;
        if (context.type() != null)
        {
            varType = _typeParser.ParseType(context.type());
        }
        else
        {
            varType = rhsValue.Type;
        }

        // Create the loop variable initialized to zero
        var zeroValue = CreateZeroValue(varType);
        if (zeroValue == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Cannot create zero value for type '{varType.Name}'",
                errorLocation
            );
            return null;
        }

        // Create local variable (scoped to while block)
        var localVar = new IrLocalVariable(varName, varType, true);
        _currentFunction!.LocalVariables.Add(localVar);
        _localVariables[varName] = localVar;
        Emit(new IrLocalDecl(varName, varType, true, zeroValue));

        // Jump to condition check
        _currentBlock!.AddInstruction(new IrBranch(condLabel));

        // Condition label
        _currentBlock!.AddInstruction(new IrLabel(condLabel));

        // Build the comparison: var <op> expr
        var varValue = new IrVariable(varName, varType);
        var compOp = GetComparisonOp(context.comparisonOp());
        var conditionTemp = $"%t{_tempCounter++}";
        var comparison = new IrBinaryOp(conditionTemp, compOp, varValue, rhsValue, IrBoolType.Instance);
        _currentBlock!.AddInstruction(comparison);
        var condition = new IrVariable(conditionTemp, IrBoolType.Instance);

        _currentBlock!.AddInstruction(new IrConditionalBranch(condition, bodyLabel, endLabel));

        // Body label
        _currentBlock!.AddInstruction(new IrLabel(bodyLabel));
        Visit(context.block());

        // Jump back to condition (only if block doesn't end with return/break)
        if (!CurrentBlockHasTerminator())
        {
            _currentBlock!.AddInstruction(new IrBranch(condLabel));
        }

        // End label
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        // Pop exit and continue labels
        _loopExitLabels.Pop();
        _loopContinueLabels.Pop();

        // Remove the variable from scope (it was scoped to the while block)
        // Note: The symbol table's scope mechanism handles this automatically when
        // the block is exited, but we should ensure the variable isn't accessible after.

        return null;
    }

    /// <summary>
    /// Convert a comparisonOp context to IrBinaryOp.OpKind
    /// </summary>
    private IrBinaryOp.OpKind GetComparisonOp(NovusParser.ComparisonOpContext context)
    {
        if (context.LESS() != null) return IrBinaryOp.OpKind.Lt;
        if (context.GREATER() != null) return IrBinaryOp.OpKind.Gt;
        if (context.LE() != null) return IrBinaryOp.OpKind.Le;
        if (context.GE() != null) return IrBinaryOp.OpKind.Ge;
        if (context.EQEQ() != null) return IrBinaryOp.OpKind.Eq;
        if (context.NE() != null) return IrBinaryOp.OpKind.Ne;

        throw new InvalidOperationException($"Unknown comparison operator: {context.GetText()}");
    }

    /// <summary>
    /// Create a zero value for the given type (for initializing loop variables)
    /// </summary>
    private IrValue? CreateZeroValue(IrType type)
    {
        return type switch
        {
            IrIntType intType => new IrConstant(0, intType),
            IrFloatType floatType => new IrFloatConstant(0.0, floatType),
            IrBoolType => new IrBoolConstant(false),
            IrPointerType ptrType => new IrConstant(0, ptrType), // null pointer
            _ => null // Unsupported type for zero initialization
        };
    }

    public override object? VisitForCStyle([NotNull] NovusParser.ForCStyleContext context)
    {
        // Set current statement location for debug symbols
        _currentStatementLocation = GetLocation(context);

        var condLabel = $"for_cond_{_labelCounter}";
        var bodyLabel = $"for_body_{_labelCounter}";
        var incrLabel = $"for_incr_{_labelCounter}";
        var endLabel = $"for_end_{_labelCounter}";
        _labelCounter++;

        // Push exit and continue labels for break/continue statements
        _loopExitLabels.Push(endLabel);
        _loopContinueLabels.Push(incrLabel); // continue jumps to increment section

        // Initialization (optional)
        // Grammar: KW_FOR (variableDeclaration | assignmentStatement) ';' expression ';' assignmentStatement block
        // Child indices: 0=for, 1=init, 2=';', 3=expr, 4=';', 5=incr, 6=block
        if (context.GetChild(1) is NovusParser.VariableDeclarationContext varDecl)
        {
            Visit(varDecl);
        }
        else if (context.GetChild(1) is NovusParser.AssignmentStatementContext assignment)
        {
            Visit(assignment);
        }

        // Jump to condition check
        _currentBlock!.AddInstruction(new IrBranch(condLabel));

        // Condition label
        _currentBlock!.AddInstruction(new IrLabel(condLabel));

        // Condition (optional - if missing, loop forever)
        if (context.expression() != null)
        {
            var condition = (IrValue?)Visit(context.expression());
            _currentBlock!.AddInstruction(new IrConditionalBranch(condition!, bodyLabel, endLabel));
        }
        else
        {
            // No condition means infinite loop
            _currentBlock!.AddInstruction(new IrBranch(bodyLabel));
        }

        // Body label
        _currentBlock!.AddInstruction(new IrLabel(bodyLabel));
        Visit(context.block());

        // Jump to increment (only if block doesn't end with return/break)
        if (!CurrentBlockHasTerminator())
        {
            _currentBlock!.AddInstruction(new IrBranch(incrLabel));
        }

        // Increment label (continue jumps here)
        _currentBlock!.AddInstruction(new IrLabel(incrLabel));

        // Increment statement (optional)
        // Grammar: KW_FOR (variableDeclaration | assignmentStatement) ';' expression ';' assignmentStatement block
        // Child indices: 0=for, 1=init, 2=';', 3=expr, 4=';', 5=incr, 6=block
        if (context.GetChild(5) is NovusParser.AssignmentStatementContext incrAssignment)
        {
            Visit(incrAssignment);
        }

        // Jump back to condition
        _currentBlock!.AddInstruction(new IrBranch(condLabel));

        // End label
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        // Pop exit and continue labels
        _loopExitLabels.Pop();
        _loopContinueLabels.Pop();

        return null;
    }
    public override object? VisitForInLoop([NotNull] NovusParser.ForInLoopContext context)
    {
        // Set current statement location for debug symbols
        _currentStatementLocation = GetLocation(context);

        var exprCtx = context.expression();
        var isEnumerated = TryGetEnumeratedSource(exprCtx, out var enumeratedSource);
        if (isEnumerated)
            exprCtx = enumeratedSource!;

        var (indexName, itemName) = GetForBindings(context);
        if ((isEnumerated && indexName == null) || (!isEnumerated && indexName != null))
        {
            _diagnostics.ReportError(ErrorCodes.InvalidExpressionType,
                isEnumerated
                    ? "enumerate() requires a two-item loop binding"
                    : "a two-item loop binding requires enumerate()",
                GetLocation(context));
            return null;
        }

        // Check if this is a range expression - handle it specially for efficiency
        if (exprCtx is NovusParser.RangeExprContext rangeExpr)
        {
            return VisitForInRangeLoop(context, rangeExpr, false, indexName, itemName);
        }
        if (exprCtx is NovusParser.RangeInclusiveExprContext rangeInclusiveExpr)
        {
            return VisitForInRangeLoop(context, rangeInclusiveExpr, true, indexName, itemName);
        }

        // Desugar: for item in collection { body }
        // Into:    let _coll = collection
        //          let _idx = 0
        //          let _len = _coll.len()
        //          while _idx < _len {
        //              let item = _coll.get_unchecked(_idx)
        //              { body }
        //              _idx = _idx + 1
        //          }

        var collVarName = $"_for_coll_{_labelCounter}";
        var idxVarName = $"_for_idx_{_labelCounter}";
        var lenVarName = $"_for_len_{_labelCounter}";
        var condLabel = $"for_cond_{_labelCounter}";
        var bodyLabel = $"for_body_{_labelCounter}";
        var incrLabel = $"for_incr_{_labelCounter}";
        var endLabel = $"for_end_{_labelCounter}";
        var matchSomeLabel = $"for_some_{_labelCounter}";
        var matchNoneLabel = $"for_none_{_labelCounter}";
        _labelCounter++;

        // Evaluate the collection expression
        var collection = (IrValue)Visit(exprCtx)!;
        var collectionType = collection.Type;

        // Store the collection in a local variable
        // Iterator<T>::next needs mutable state. Keeping the hidden loop value
        // mutable is harmless for indexable Iterable<T> collections.
        var collVar = new IrLocalVariable(collVarName, collectionType, true);
        _currentFunction!.LocalVariables.Add(collVar);
        _localVariables[collVarName] = collVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(collVarName, collectionType, true, collection));

        // Get the type name for method lookup
        var collectionValueType = collectionType switch
        {
            IrReferenceType reference => reference.PointeeType,
            IrMutReferenceType reference => reference.PointeeType,
            IrPointerType pointer => pointer.PointeeType,
            _ => collectionType
        };
        var typeName = collectionValueType is IrStructType st ? st.StructName : collectionValueType.Name;
        var arrayType = collectionValueType as IrArrayType;
        var collVarRef = new IrVariable(collVarName, collectionType);

        // Stateful Iterator<T> values advance with next(). Prefer Iterable<T>
        // when a type deliberately supports both protocols.
        var iterableLenName = arrayType == null
            ? _module.FindTraitMethod(typeName, "Iterable", "len")
            : null;
        var iteratorNextName = arrayType == null
            ? _module.FindTraitMethod(typeName, "Iterator", "next")
            : null;
        if (arrayType == null && iterableLenName == null && iteratorNextName != null)
        {
            var nextMethod = FindOrInstantiateTraitMethod(
                collVarRef, "Iterator", "next", []);
            if (nextMethod == null)
            {
                _diagnostics.ReportError(
                    ErrorCodes.MethodNotFound,
                    $"Iterator implementation for '{typeName}' is missing next()",
                    GetLocation(context));
                return null;
            }

            return VisitForInIteratorLoop(
                context, collVarName, collectionType, nextMethod, nextMethod.Name,
                condLabel, bodyLabel, endLabel, matchSomeLabel, matchNoneLabel, indexName, itemName);
        }

        // For-in loops require the Iterable trait (with get_unchecked() and len() methods).
        var lenMethod = arrayType == null
            ? FindOrInstantiateTraitMethod(collVarRef, "Iterable", "len", [])
            : null;

        if (arrayType == null && lenMethod == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Type '{typeName}' does not implement Iterable trait (missing len() method). For-in loops require types to implement Iterable<T>.",
                errorLocation
            );
            return null;
        }

        IrType lenType = IrIntType.U32;
        IrValue lenResult;
        if (arrayType != null)
        {
            lenResult = new IrConstant(arrayType.Length, IrIntType.U32);
        }
        else
        {
            lenType = lenMethod!.ReturnType;
            IrValue lenReceiverArg = collectionType is IrReferenceType or IrMutReferenceType or IrPointerType
                ? collVarRef
                : new IrBorrowValue(collVarRef, lenMethod.Parameters[0].Type, false);
            var lenResultName = $"%t{_tempCounter++}";
            var lenCall = new IrCall(lenMethod.Name, lenType, lenResultName);
            lenCall.Location = GetLocation(context);
            lenCall.Arguments.Add(lenReceiverArg);
            _currentBlock!.AddInstruction(lenCall);
            lenResult = new IrVariable(lenResultName, lenType);
        }

        // Store length in local variable
        var lenVar = new IrLocalVariable(lenVarName, lenType, false);
        _currentFunction!.LocalVariables.Add(lenVar);
        _localVariables[lenVarName] = lenVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(lenVarName, lenType, false, lenResult));

        // Initialize index to 0
        var idxVar = new IrLocalVariable(idxVarName, IrIntType.U32, true);
        _currentFunction!.LocalVariables.Add(idxVar);
        _localVariables[idxVarName] = idxVar;
        var zeroLiteral = new IrConstant(0, IrIntType.U32);
        _currentBlock!.AddInstruction(new IrLocalDecl(idxVarName, IrIntType.U32, true, zeroLiteral));

        // Push exit and continue labels for break/continue statements
        _loopExitLabels.Push(endLabel);
        _loopContinueLabels.Push(incrLabel); // continue jumps to increment section

        // Jump to condition check
        _currentBlock!.AddInstruction(new IrBranch(condLabel));

        // Condition label
        _currentBlock!.AddInstruction(new IrLabel(condLabel));

        // Check: _idx < _len
        var idxVarRef = new IrVariable(idxVarName, IrIntType.U32);
        var lenVarRef = new IrVariable(lenVarName, lenType);
        var condResultName = $"%t{_tempCounter++}";
        var condCheck = new IrBinaryOp(condResultName, IrBinaryOp.OpKind.Lt, idxVarRef, lenVarRef, IrBoolType.Instance);
        _currentBlock!.AddInstruction(condCheck);
        var condResult = new IrVariable(condResultName, IrBoolType.Instance);
        _currentBlock!.AddInstruction(new IrConditionalBranch(condResult, bodyLabel, endLabel));

        // Body label
        _currentBlock!.AddInstruction(new IrLabel(bodyLabel));

        // The loop condition proves _idx < _len, so call Iterable's primitive
        // directly instead of constructing and inspecting an Option each iteration.
        var getMethod = arrayType == null
            ? FindOrInstantiateTraitMethod(collVarRef, "Iterable", "get_unchecked", [idxVarRef])
            : null;

        if (arrayType == null && getMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Type '{typeName}' does not implement Iterable trait (missing get_unchecked() method).",
                errorLocation
            );
            return null;
        }

        IrType bindingType;
        IrValue bindingValue;
        if (arrayType != null)
        {
            IrValue arrayBase = collectionType is IrReferenceType or IrMutReferenceType
                ? new IrDereferenceValue(collVarRef, arrayType)
                : collVarRef;
            var elementName = $"%t{_tempCounter++}";
            Emit(new IrIndexAccess(
                elementName, arrayBase, idxVarRef, arrayType.ElementType,
                IrBoundsCheckMode.Proven));
            bindingType = arrayType.ElementType;
            bindingValue = new IrVariable(elementName, bindingType);
            if (!IsCopyIterationType(bindingType))
            {
                bindingType = _typeInterner.GetReferenceType(bindingType);
                bindingValue = new IrBorrowValue(bindingValue, bindingType, false);
            }
        }
        else
        {
            // Call get_unchecked(index); the loop condition above proves this index valid.
            IrValue getReceiverArg = collectionType is IrReferenceType or IrMutReferenceType or IrPointerType
                ? collVarRef
                : new IrBorrowValue(collVarRef, getMethod!.Parameters[0].Type, false);
            var getResultName = $"%t{_tempCounter++}";
            var getCall = new IrCall(getMethod!.Name, getMethod.ReturnType, getResultName);
            getCall.Location = GetLocation(context);
            getCall.Arguments.Add(getReceiverArg);
            getCall.Arguments.Add(idxVarRef);
            _currentBlock!.AddInstruction(getCall);
            bindingType = getMethod.ReturnType;
            bindingValue = new IrVariable(getResultName, bindingType);
        }

        // Copy small Copy values for ergonomic loops; keep move-only values borrowed.
        if (bindingType is IrReferenceType itemReference && IsCopyIterationType(itemReference.PointeeType))
        {
            bindingType = itemReference.PointeeType;
            bindingValue = new IrDereferenceValue(bindingValue, bindingType);
        }

        // Bind to item variable
        if (indexName != null && indexName != "_")
        {
            var indexVar = new IrLocalVariable(indexName, IrIntType.U32, false);
            _currentFunction!.LocalVariables.Add(indexVar);
            _localVariables[indexName] = indexVar;
            _currentBlock!.AddInstruction(new IrLocalDecl(indexName, IrIntType.U32, false, idxVarRef));
        }

        // Bind to item variable
        if (itemName == "_")
        {
            Visit(context.block());
        }
        else
        {
        var itemVar = new IrLocalVariable(itemName, bindingType, context.KW_VAR() != null);
        _currentFunction!.LocalVariables.Add(itemVar);
        _localVariables[itemName] = itemVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(itemName, bindingType, context.KW_VAR() != null, bindingValue));

        // Visit the loop body
        Visit(context.block());
        }

        // Jump to increment (only if block doesn't end with return/break)
        if (!CurrentBlockHasTerminator())
        {
            _currentBlock!.AddInstruction(new IrBranch(incrLabel));
        }

        // Increment label (continue jumps here)
        _currentBlock!.AddInstruction(new IrLabel(incrLabel));

        // Increment index: _idx = _idx + 1
        var incResultName = $"%t{_tempCounter++}";
        var oneLiteral = new IrConstant(1, IrIntType.U32);
        var incOp = new IrBinaryOp(incResultName, IrBinaryOp.OpKind.Add, idxVarRef, oneLiteral, IrIntType.U32);
        _currentBlock!.AddInstruction(incOp);
        var incResult = new IrVariable(incResultName, IrIntType.U32);
        _currentBlock!.AddInstruction(new IrStore(idxVarName, incResult));

        // Jump back to condition
        _currentBlock!.AddInstruction(new IrBranch(condLabel));

        // End label
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        // Pop exit and continue labels
        _loopExitLabels.Pop();
        _loopContinueLabels.Pop();

        return null;
    }

    private object? VisitForInIteratorLoop(
        NovusParser.ForInLoopContext context,
        string collVarName,
        IrType collectionType,
        IrFunction nextMethod,
        string nextMethodName,
        string nextLabel,
        string bodyLabel,
        string endLabel,
        string someLabel,
        string noneLabel,
        string? enumerateIndexName,
        string itemName)
    {
        if (nextMethod.ReturnType is not IrEnumType optionType || optionType.EnumName != "Option")
        {
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Iterator::next must return Option<T>, but returned {nextMethod.ReturnType.Name}",
                GetLocation(context));
            return null;
        }

        var someVariant = optionType.Variants.FirstOrDefault(v => v.Name == "Some");
        var noneVariant = optionType.Variants.FirstOrDefault(v => v.Name == "None");
        if (someVariant?.AssociatedData is not [var itemType] || noneVariant == null)
        {
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Iterator::next must return the standard Option<T> type",
                GetLocation(context));
            return null;
        }

        var incrementLabel = enumerateIndexName == null ? nextLabel : $"{nextLabel}_enumerate";
        _loopExitLabels.Push(endLabel);
        _loopContinueLabels.Push(incrementLabel);
        var enumerateCounterName = $"_for_enumerate_{_labelCounter++}";
        if (enumerateIndexName != null)
        {
            var counter = new IrLocalVariable(enumerateCounterName, IrIntType.U32, true);
            _currentFunction!.LocalVariables.Add(counter);
            _localVariables[enumerateCounterName] = counter;
            _currentBlock!.AddInstruction(new IrLocalDecl(
                enumerateCounterName, IrIntType.U32, true, new IrConstant(0, IrIntType.U32)));
        }
        _currentBlock!.AddInstruction(new IrBranch(nextLabel));
        _currentBlock.AddInstruction(new IrLabel(nextLabel));

        var collRef = new IrVariable(collVarName, collectionType);
        IrValue receiver = collectionType is IrMutReferenceType or IrPointerType
            ? collRef
            : new IrBorrowValue(collRef, nextMethod.Parameters[0].Type, true);
        var nextResultName = $"%t{_tempCounter++}";
        var nextCall = new IrCall(nextMethodName, nextMethod.ReturnType, nextResultName)
        {
            Location = GetLocation(context)
        };
        nextCall.Arguments.Add(receiver);
        _currentBlock.AddInstruction(nextCall);

        var nextResult = new IrVariable(nextResultName, nextMethod.ReturnType);
        var tagName = $"%t{_tempCounter++}";
        _currentBlock.AddInstruction(new IrExtractTag(tagName, nextResult));
        var isNoneName = $"%t{_tempCounter++}";
        _currentBlock.AddInstruction(new IrBinaryOp(
            isNoneName,
            IrBinaryOp.OpKind.Eq,
            new IrVariable(tagName, IrIntType.I32),
            new IrConstant(noneVariant.Tag, IrIntType.I32),
            IrBoolType.Instance));
        _currentBlock.AddInstruction(new IrConditionalBranch(
            new IrVariable(isNoneName, IrBoolType.Instance), noneLabel, someLabel));

        _currentBlock.AddInstruction(new IrLabel(noneLabel));
        _currentBlock.AddInstruction(new IrBranch(endLabel));
        _currentBlock.AddInstruction(new IrLabel(someLabel));

        var itemValueName = $"%t{_tempCounter++}";
        _currentBlock.AddInstruction(new IrExtractVariantData(
            itemValueName, nextResult, "Some", 0, itemType));
        if (enumerateIndexName != null && enumerateIndexName != "_")
        {
            var indexVar = new IrLocalVariable(enumerateIndexName, IrIntType.U32, false);
            _currentFunction!.LocalVariables.Add(indexVar);
            _localVariables[enumerateIndexName] = indexVar;
            _currentBlock.AddInstruction(new IrLocalDecl(enumerateIndexName, IrIntType.U32, false,
                new IrVariable(enumerateCounterName, IrIntType.U32)));
        }

        var isMutable = context.KW_VAR() != null;
        if (itemName != "_")
        {
            var itemVar = new IrLocalVariable(itemName, itemType, isMutable);
            _currentFunction!.LocalVariables.Add(itemVar);
            _localVariables[itemName] = itemVar;
            _currentBlock.AddInstruction(new IrLocalDecl(
                itemName, itemType, isMutable, new IrVariable(itemValueName, itemType)));
        }

        Visit(context.block());
        if (!CurrentBlockHasTerminator())
            _currentBlock!.AddInstruction(new IrBranch(incrementLabel));
        if (enumerateIndexName != null)
        {
            _currentBlock.AddInstruction(new IrLabel(incrementLabel));
            var nextIndexName = $"%t{_tempCounter++}";
            _currentBlock.AddInstruction(new IrBinaryOp(nextIndexName, IrBinaryOp.OpKind.Add,
                new IrVariable(enumerateCounterName, IrIntType.U32),
                new IrConstant(1, IrIntType.U32), IrIntType.U32));
            _currentBlock.AddInstruction(new IrStore(enumerateCounterName,
                new IrVariable(nextIndexName, IrIntType.U32)));
            _currentBlock.AddInstruction(new IrBranch(nextLabel));
        }

        _currentBlock!.AddInstruction(new IrLabel(endLabel));
        _loopExitLabels.Pop();
        _loopContinueLabels.Pop();
        return null;
    }

    private bool IsCopyIterationType(IrType type)
    {
        if (type is IrIntType or IrBoolType or IrFloatType or IrFixedType or
            IrPointerType or IrReferenceType or IrMutReferenceType or IrFunctionPointerType)
            return true;

        var typeName = type switch
        {
            IrStructType structType => structType.StructName,
            IrEnumType enumType => enumType.EnumName,
            _ => type.Name
        };
        return _module.GetTraitImpl("Copy", typeName) != null;
    }

    /// <summary>
    /// Handles for-in loops over range expressions (e.g., 0..10 or 1..=5)
    /// Generates efficient counter-based code without needing Iterable trait.
    /// </summary>
    private object? VisitForInRangeLoop(NovusParser.ForInLoopContext context, NovusParser.ExpressionContext rangeExpr,
        bool inclusive, string? enumerateIndexName, string itemName)
    {
        // for i in start..end { body }
        // Desugars to:
        //   let i = start
        //   while i < end {  (or i <= end for inclusive)
        //       body
        //       i = i + 1
        //   }

        var isMutable = context.KW_VAR() != null;
        var condLabel = $"range_cond_{_labelCounter}";
        var bodyLabel = $"range_body_{_labelCounter}";
        var incrLabel = $"range_incr_{_labelCounter}";
        var endLabel = $"range_end_{_labelCounter}";
        _labelCounter++;

        // Infer unsuffixed endpoints from one another before materializing the loop.
        IrValue? startValue = null, endValue = null;
        string endExpressionText;
        if (rangeExpr is NovusParser.RangeExprContext exclusiveRange)
        {
            (startValue, endValue) = VisitBinaryOperands(
                exclusiveRange.expression(0), exclusiveRange.expression(1));
            endExpressionText = exclusiveRange.expression(1).GetText();
        }
        else if (rangeExpr is NovusParser.RangeInclusiveExprContext inclusiveRange)
        {
            (startValue, endValue) = VisitBinaryOperands(
                inclusiveRange.expression(0), inclusiveRange.expression(1));
            endExpressionText = inclusiveRange.expression(1).GetText();
        }
        else
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Invalid range expression",
                errorLocation
            );
            return null;
        }

        // If either operand had an error, bail out
        if (startValue == null || endValue == null)
        {
            return null;
        }

        // Contextual inference guarantees matching endpoint types unless the source
        // explicitly forced incompatible suffixes; semantic analysis diagnoses that.
        var rangeType = startValue.Type;

        // Declare the loop variable with the start value
        var loopVar = new IrLocalVariable(itemName, rangeType, true); // Always mutable for incrementing
        _currentFunction!.LocalVariables.Add(loopVar);
        _localVariables[itemName] = loopVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(itemName, rangeType, true, startValue));

        string? enumerateCounterName = null;
        if (enumerateIndexName != null)
        {
            enumerateCounterName = $"_range_enumerate_{_labelCounter - 1}";
            var counter = new IrLocalVariable(enumerateCounterName, IrIntType.U32, true);
            _currentFunction.LocalVariables.Add(counter);
            _localVariables[enumerateCounterName] = counter;
            _currentBlock.AddInstruction(new IrLocalDecl(enumerateCounterName, IrIntType.U32, true,
                new IrConstant(0, IrIntType.U32)));
        }

        // Store the end value in a temp variable to avoid re-evaluating
        var endVarName = $"_range_end_{_labelCounter - 1}";
        var endVar = new IrLocalVariable(endVarName, rangeType, false);
        _currentFunction!.LocalVariables.Add(endVar);
        _localVariables[endVarName] = endVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(endVarName, rangeType, false, endValue));
        if (!inclusive)
            _currentBlock.AddInstruction(new IrStructuredForLoopHint(
                itemName, endVarName, bodyLabel, condLabel, endLabel));

        // Push exit and continue labels for break/continue statements
        _loopExitLabels.Push(endLabel);
        _loopContinueLabels.Push(incrLabel); // continue jumps to increment

        // Jump to condition
        _currentBlock!.AddInstruction(new IrBranch(condLabel));

        // Condition label
        _currentBlock!.AddInstruction(new IrLabel(condLabel));

        // Check: i < end (or i <= end for inclusive)
        var loopVarRef = new IrVariable(itemName, rangeType);
        var endVarRef = new IrVariable(endVarName, rangeType);
        var condResultName = $"%t{_tempCounter++}";
        var compareOp = inclusive ? IrBinaryOp.OpKind.Le : IrBinaryOp.OpKind.Lt;
        var condCheck = new IrBinaryOp(condResultName, compareOp, loopVarRef, endVarRef, IrBoolType.Instance);
        _currentBlock!.AddInstruction(condCheck);
        var condResult = new IrVariable(condResultName, IrBoolType.Instance);
        _currentBlock!.AddInstruction(new IrConditionalBranch(condResult, bodyLabel, endLabel));

        // Body label
        _currentBlock!.AddInstruction(new IrLabel(bodyLabel));

        if (enumerateIndexName != null && enumerateIndexName != "_")
        {
            var indexVar = new IrLocalVariable(enumerateIndexName, IrIntType.U32, false);
            _currentFunction.LocalVariables.Add(indexVar);
            _localVariables[enumerateIndexName] = indexVar;
            _currentBlock.AddInstruction(new IrLocalDecl(enumerateIndexName, IrIntType.U32, false,
                new IrVariable(enumerateCounterName!, IrIntType.U32)));
        }

        // Visit the loop body while retaining the source-level bound relationship.
        _rangeLoopBounds.Push((itemName, endVarName, endExpressionText, inclusive));
        try
        {
            Visit(context.block());
        }
        finally
        {
            _rangeLoopBounds.Pop();
        }

        // Increment label (continue jumps here)
        _currentBlock!.AddInstruction(new IrLabel(incrLabel));

        // Increment: i = i + 1
        var incResultName = $"%t{_tempCounter++}";
        var oneLiteral = new IrConstant(1, rangeType);
        var incOp = new IrBinaryOp(incResultName, IrBinaryOp.OpKind.Add, loopVarRef, oneLiteral, rangeType);
        _currentBlock!.AddInstruction(incOp);
        var incResult = new IrVariable(incResultName, rangeType);
        _currentBlock!.AddInstruction(new IrStore(itemName, incResult));
        if (enumerateCounterName != null)
        {
            var nextIndexName = $"%t{_tempCounter++}";
            _currentBlock.AddInstruction(new IrBinaryOp(nextIndexName, IrBinaryOp.OpKind.Add,
                new IrVariable(enumerateCounterName, IrIntType.U32),
                new IrConstant(1, IrIntType.U32), IrIntType.U32));
            _currentBlock.AddInstruction(new IrStore(enumerateCounterName,
                new IrVariable(nextIndexName, IrIntType.U32)));
        }

        // Jump back to condition
        _currentBlock!.AddInstruction(new IrBranch(condLabel));

        // End label
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        // Pop exit and continue labels
        _loopExitLabels.Pop();
        _loopContinueLabels.Pop();

        // Remove the loop variable from scope (it's local to the loop)
        _localVariables.Remove(itemName);
        _localVariables.Remove(endVarName);

        return null;
    }

    private static bool TryGetEnumeratedSource(NovusParser.ExpressionContext expression,
        out NovusParser.ExpressionContext? source)
    {
        source = null;
        if (expression is not NovusParser.CallExprContext call || call.argumentList() != null ||
            call.expression() is not NovusParser.MemberAccessExprContext member ||
            member.IDENTIFIER().GetText() != "enumerate")
            return false;
        source = member.expression();
        return true;
    }

    private static (string? Index, string Item) GetForBindings(NovusParser.ForInLoopContext context)
    {
        if (context.tuplePattern() == null)
            return (null, context.IDENTIFIER().GetText());
        var bindings = context.tuplePattern().GetText()[1..^1].Split(',');
        return (bindings[0], bindings[1]);
    }

    /// <summary>
    /// Returns the larger of two integer types for range type coercion.
    /// </summary>
    private IrIntType GetLargerIntType(IrIntType a, IrIntType b)
    {
        // Simple heuristic: prefer the type with more bits
        int sizeA = a.BitWidth;
        int sizeB = b.BitWidth;
        return sizeA >= sizeB ? a : b;
    }

    public override object? VisitForeverStatement([NotNull] NovusParser.ForeverStatementContext context)
    {
        // Set current statement location for debug symbols
        _currentStatementLocation = GetLocation(context);

        var bodyLabel = $"forever_body_{_labelCounter}";
        var endLabel = $"forever_end_{_labelCounter}";
        _labelCounter++;

        // Push exit and continue labels for break/continue statements
        _loopExitLabels.Push(endLabel);
        _loopContinueLabels.Push(bodyLabel); // continue jumps back to body start

        // Body label
        _currentBlock!.AddInstruction(new IrLabel(bodyLabel));
        Visit(context.block());

        // Jump back to start (infinite loop) - only if block doesn't end with return/break
        if (!CurrentBlockHasTerminator())
        {
            _currentBlock!.AddInstruction(new IrBranch(bodyLabel));
        }

        // End label (only reachable via break)
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        // Pop exit and continue labels
        _loopExitLabels.Pop();
        _loopContinueLabels.Pop();
        return null;
    }

    public override object? VisitBreakStatement([NotNull] NovusParser.BreakStatementContext context)
    {
        // Check for optional label
        var labelIdentifier = context.IDENTIFIER();

        if (labelIdentifier != null)
        {
            // Labeled break: break label_name
            var labelName = labelIdentifier.GetText();
            if (!_labeledLoops.TryGetValue(labelName, out var loopLabels))
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"unknown loop label '{labelName}'",
                    errorLocation
                );
                return null;
            }

            // Handle postfix conditional (if/unless)
            var postfixCondition = context.postfixCondition();
            HandlePostfixCondition(postfixCondition, () =>
            {
                _currentBlock!.AddInstruction(new IrBranch(loopLabels.ExitLabel));
            });
        }
        else
        {
            // Regular break (innermost loop)
            if (_loopExitLabels.Count == 0)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    "break statement outside of loop",
                    errorLocation
                );
                return null;
            }

            // Handle postfix conditional (if/unless)
            var postfixCondition = context.postfixCondition();
            HandlePostfixCondition(postfixCondition, () =>
            {
                var exitLabel = _loopExitLabels.Peek();
                _currentBlock!.AddInstruction(new IrBranch(exitLabel));
            });
        }

        return null;
    }

    public override object? VisitContinueStatement([NotNull] NovusParser.ContinueStatementContext context)
    {
        // Check for optional label
        var labelIdentifier = context.IDENTIFIER();

        if (labelIdentifier != null)
        {
            // Labeled continue: continue label_name
            var labelName = labelIdentifier.GetText();
            if (!_labeledLoops.TryGetValue(labelName, out var loopLabels))
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"unknown loop label '{labelName}'",
                    errorLocation
                );
                return null;
            }

            // Handle postfix conditional (if/unless)
            var postfixCondition = context.postfixCondition();
            HandlePostfixCondition(postfixCondition, () =>
            {
                _currentBlock!.AddInstruction(new IrBranch(loopLabels.ContinueLabel));
            });
        }
        else
        {
            // Regular continue (innermost loop)
            if (_loopContinueLabels.Count == 0)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    "continue statement outside of loop",
                    errorLocation
                );
                return null;
            }

            // Handle postfix conditional (if/unless)
            var postfixCondition = context.postfixCondition();
            HandlePostfixCondition(postfixCondition, () =>
            {
                var continueLabel = _loopContinueLabels.Peek();
                _currentBlock!.AddInstruction(new IrBranch(continueLabel));
            });
        }

        return null;
    }

    // Handle: defer { statements }
    public override object? VisitDeferBlock([NotNull] NovusParser.DeferBlockContext context)
    {
        // Create a new basic block for the deferred code (don't add to function's basic blocks)
        var deferLabel = $"defer_{_labelCounter++}";
        var deferBlock = new IrBasicBlock(deferLabel);

        // Save current block and switch to defer block
        var savedBlock = _currentBlock;
        _currentBlock = deferBlock;

        // Visit the deferred block's statements
        foreach (var statement in context.block().statement())
        {
            Visit(statement);
        }

        // Restore current block
        _currentBlock = savedBlock;

        // Add the defer block to the function's deferred blocks list (LIFO)
        _currentFunction!.DeferredBlocks.Add(deferBlock);

        // Add defer instruction to current block (marker only)
        _currentBlock!.AddInstruction(new IrDefer(deferBlock));

        return null;
    }

    // Handle: defer => expression
    public override object? VisitDeferExpression([NotNull] NovusParser.DeferExpressionContext context)
    {
        // Create a new basic block for the deferred code
        var deferLabel = $"defer_{_labelCounter++}";
        var deferBlock = new IrBasicBlock(deferLabel);

        // Save current block and switch to defer block
        var savedBlock = _currentBlock;
        _currentBlock = deferBlock;

        // Visit the expression (typically a function call like FreeMem(mem))
        Visit(context.expression());

        // Restore current block
        _currentBlock = savedBlock;

        // Add the defer block to the function's deferred blocks list (LIFO)
        _currentFunction!.DeferredBlocks.Add(deferBlock);

        // Add defer instruction to current block (marker only)
        _currentBlock!.AddInstruction(new IrDefer(deferBlock));

        return null;
    }

    // Handle: using expression { statements }
    public override object? VisitUsingStatement([NotNull] NovusParser.UsingStatementContext context)
    {
        PushDeferScope();

        // Evaluate the expression to get the resource
        var resourceValue = (IrValue)Visit(context.expression())!;
        var resourceType = resourceValue.Type;

        // Store the resource in a temporary variable so we can reference it in the deferred drop call
        var tempVarName = $"__using_resource_{_labelCounter++}";
        var tempVar = new IrLocalVariable(tempVarName, resourceType, true); // Mutable because drop() takes &var self

        _currentFunction!.LocalVariables.Add(tempVar);
        _localVariables[tempVarName] = tempVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(tempVarName, resourceType, true, resourceValue));

        // Ensure the type has a drop() method and inject automatic cleanup
        // This leverages the existing RAII infrastructure
        if (EnsureDropMethodInstantiated(resourceType))
        {
            InjectAutomaticDrop(tempVarName, resourceType);
        }

        // Now visit the body block
        foreach (var statement in context.block().statement())
        {
            Visit(statement);
        }

        PopDeferScope();

        return null;
    }

    // Handle: unsafe { statements }
    public override object? VisitUnsafeBlock([NotNull] NovusParser.UnsafeBlockContext context)
    {
        // Enter unsafe context
        _unsafeDepth++;

        try
        {
            // Visit the block with unsafe operations allowed
            return Visit(context.block());
        }
        finally
        {
            // Exit unsafe context
            _unsafeDepth--;
        }
    }

    // Handle: assert!(condition) or assert!(condition, "message")
    public override object? VisitAssertStatement([NotNull] NovusParser.AssertStatementContext context)
    {
        // Evaluate the condition expression
        var condition = (IrValue)Visit(context.expression())!;

        // Get optional message
        string? message = null;
        if (context.STRING_LITERAL() != null)
        {
            var messageText = context.STRING_LITERAL().GetText();
            // Strip quotes from string literal
            message = messageText.Substring(1, messageText.Length - 2);
        }

        // Get source location for error reporting
        var errorLocation = new SourceLocation(
            _inputFilePath ?? "unknown",
            context.Start.Line,
            context.Start.Column,
            context.GetText().Length,
            context.Start.InputStream.ToString() ?? ""
        );

        // Add assert instruction to current block
        _currentBlock!.AddInstruction(new IrAssert(condition, message, errorLocation));

        return null;
    }

    public override object? VisitPanicStatement([NotNull] NovusParser.PanicStatementContext context)
    {
        // Get the panic message
        var messageText = context.STRING_LITERAL().GetText();
        // Strip quotes from string literal
        var message = messageText.Substring(1, messageText.Length - 2);

        // Get source location for error reporting
        var errorLocation = new SourceLocation(
            _inputFilePath ?? "unknown",
            context.Start.Line,
            context.Start.Column,
            context.GetText().Length,
            context.Start.InputStream.ToString() ?? ""
        );

        // Add panic instruction to current block
        _currentBlock!.AddInstruction(new IrPanic(message, errorLocation));

        return null;
    }

    /// <summary>
    /// Visit inline assembly statement.
    /// Parses the asm block syntax and emits an IrInlineAsm instruction.
    /// </summary>
    public override object? VisitAsmStatement([NotNull] NovusParser.AsmStatementContext context)
    {
        // Set current statement location for debug symbols
        _currentStatementLocation = GetLocation(context);

        return ProcessInlineAssembly(
            context.asmInputList(),
            context.asmReturnSpec(),
            context.asmUseClause(),
            context.asmVolatile(),
            context.asmClobbers(),
            context.asmBlock(),
            GetLocation(context)
        );
    }

    /// <summary>
    /// Shared helper for processing inline assembly (used by both statement and expression forms).
    /// </summary>
    private object? ProcessInlineAssembly(
        NovusParser.AsmInputListContext? inputList,
        NovusParser.AsmReturnSpecContext? returnSpec,
        NovusParser.AsmUseClauseContext? useClause,
        NovusParser.AsmVolatileContext? volatileSpec,
        NovusParser.AsmClobbersContext? clobbersSpec,
        NovusParser.AsmBlockContext asmBlock,
        SourceLocation location)
    {
        var asmInst = new IrInlineAsm
        {
            Location = location
        };

        // Parse input parameters
        if (inputList != null)
        {
            int dataRegIndex = 0;  // d0, d1
            int addrRegIndex = 0;  // a0, a1

            foreach (var input in inputList.asmInput())
            {
                var paramName = input.IDENTIFIER().GetText();

                // Check for address-of prefix (&)
                bool isAddressOf = input.AMPERSAND() != null;

                // Check for mutable binding (var)
                bool isMutable = input.KW_VAR() != null;

                // Get the expression value (either the identifier itself or an assigned expression)
                IrValue inputValue;
                var expr = input.expression();
                if (expr != null)
                {
                    // Expression provided: name = expression
                    inputValue = (IrValue?)Visit(expr) ?? new IrConstant(0, IrIntType.I32);
                }
                else
                {
                    // Just the identifier - look up the variable
                    inputValue = LookupVariable(paramName) ?? new IrConstant(0, IrIntType.I32);
                }

                var inputType = inputValue.Type ?? IrIntType.I32;

                // For address-of, we need to treat pointers correctly
                // The type we pass should be a pointer type (address registers)
                bool useAddressRegister = isAddressOf ||
                    inputType is IrPointerType ||
                    inputType is IrReferenceType ||
                    inputType is IrMutReferenceType;

                // Parse register binding if explicit
                M68kRegister reg = M68kRegister.None;
                var regBinding = input.asmRegisterBinding();
                if (regBinding != null)
                {
                    var regName = regBinding.asmRegister().GetText();
                    reg = IrInlineAsm.ParseRegister(regName);
                }
                else
                {
                    // Infer register from calling convention and type
                    if (useAddressRegister)
                    {
                        reg = addrRegIndex switch
                        {
                            0 => M68kRegister.A0,
                            1 => M68kRegister.A1,
                            _ => M68kRegister.None // Too many, will need explicit binding
                        };
                        addrRegIndex++;
                    }
                    else
                    {
                        reg = dataRegIndex switch
                        {
                            0 => M68kRegister.D0,
                            1 => M68kRegister.D1,
                            _ => M68kRegister.None // Too many, will need explicit binding
                        };
                        dataRegIndex++;
                    }
                }

                // Parse output binding for writeback (out reg)
                M68kRegister? outputReg = null;
                var outputBinding = input.asmOutputBinding();
                if (outputBinding != null)
                {
                    var outRegName = outputBinding.asmRegister().GetText();
                    outputReg = IrInlineAsm.ParseRegister(outRegName);
                }

                var asmInput = new IrAsmInput(paramName, inputValue, inputType, reg)
                {
                    IsAddressOf = isAddressOf,
                    IsMutable = isMutable,
                    OutputRegister = outputReg,
                    WritebackVariable = isMutable ? paramName : null
                };

                asmInst.Inputs.Add(asmInput);
            }
        }

        // Parse return specification
        if (returnSpec != null)
        {
            var multiReturn = returnSpec.asmMultiReturn();
            if (multiReturn != null)
            {
                // Multi-value return: -> (type in reg, type in reg)
                var types = multiReturn.type();
                var registers = multiReturn.asmRegister();
                for (int i = 0; i < types.Length && i < registers.Length; i++)
                {
                    var returnType = ParseType(types[i]);
                    var regName = registers[i].GetText();
                    var reg = IrInlineAsm.ParseRegister(regName);
                    asmInst.Outputs.Add(new IrAsmOutput(returnType, reg));
                }
            }
            else
            {
                // Single return: -> type [in reg]
                var returnType = ParseType(returnSpec.type());
                M68kRegister reg = M68kRegister.D0; // Default return register

                var regBinding = returnSpec.asmRegisterBinding();
                if (regBinding != null)
                {
                    var regName = regBinding.asmRegister().GetText();
                    reg = IrInlineAsm.ParseRegister(regName);
                }

                // Check for void type - no output in that case
                if (returnType is not IrVoidType)
                {
                    asmInst.Outputs.Add(new IrAsmOutput(returnType, reg));
                }
            }
        }

        // Parse volatile flag
        if (volatileSpec != null)
        {
            asmInst.IsVolatile = true;
        }

        // Parse clobbers
        if (clobbersSpec != null)
        {
            foreach (var clobber in clobbersSpec.asmClobberList().asmClobberItem())
            {
                var clobberName = clobber.IDENTIFIER().GetText();
                if (clobberName == "memory")
                {
                    asmInst.ClobbersMemory = true;
                }
                else
                {
                    var reg = IrInlineAsm.ParseRegister(clobberName);
                    if (reg != M68kRegister.None)
                    {
                        asmInst.Clobbers.Add(reg);
                    }
                }
            }
        }

        // Parse use clause for compile-time constants
        if (useClause != null)
        {
            foreach (var useItem in useClause.asmUseItem())
            {
                var irUseItem = ParseAsmUseItem(useItem, location);
                if (irUseItem != null)
                {
                    asmInst.UseItems.Add(irUseItem);
                }
            }
        }

        // Parse assembly instructions - supports both raw and legacy quoted syntax
        foreach (var instruction in asmBlock.asmInstruction())
        {
            // Handle legacy quoted string assembly instructions
            if (instruction.STRING_LITERAL() != null)
            {
                var instructionText = instruction.STRING_LITERAL().GetText();
                // Remove surrounding quotes
                instructionText = instructionText.Substring(1, instructionText.Length - 2);
                asmInst.Instructions.Add(instructionText);
            }
            // Handle raw assembly syntax
            else if (instruction.asmMnemonic() != null || instruction.asmLabel() != null)
            {
                var instructionText = ReconstructAsmInstruction(instruction);
                if (!string.IsNullOrWhiteSpace(instructionText))
                {
                    asmInst.Instructions.Add(instructionText);
                }
            }
            // Skip empty lines (NEWLINEs)
        }

        // Generate result variable name if needed
        if (asmInst.Outputs.Count == 1)
        {
            asmInst.ResultName = $"%t{_tempCounter++}";
        }
        else if (asmInst.Outputs.Count > 1)
        {
            asmInst.MultiResultNames = new List<string>();
            for (int i = 0; i < asmInst.Outputs.Count; i++)
            {
                asmInst.MultiResultNames.Add($"%t{_tempCounter++}");
            }
        }

        // Emit the instruction
        Emit(asmInst);

        // Return the result value if any
        if (asmInst.Outputs.Count == 1 && asmInst.ResultName != null)
        {
            return new IrVariable(asmInst.ResultName, asmInst.Outputs[0].Type);
        }
        else if (asmInst.Outputs.Count > 1 && asmInst.MultiResultNames != null)
        {
            var tupleElements = asmInst.Outputs.Zip(asmInst.MultiResultNames,
                (output, name) => new IrVariable(name, output.Type) as IrValue).ToList();
            var tupleElementTypes = asmInst.Outputs.Select(o => o.Type).ToList();
            var tupleType = new IrTupleType(tupleElementTypes);
            return new IrTupleLiteral(tupleType, tupleElements);
        }

        return null;
    }

    /// <summary>
    /// Reconstructs an assembly instruction string from parsed ASM tokens.
    /// This converts the structured AST back to a string for the code generator.
    /// </summary>
    private string ReconstructAsmInstruction(NovusParser.AsmInstructionContext instruction)
    {
        var parts = new List<string>();

        // Handle label
        if (instruction.asmLabel() != null)
        {
            parts.Add(instruction.asmLabel().GetText());
        }

        // Handle mnemonic
        if (instruction.asmMnemonic() != null)
        {
            parts.Add(instruction.asmMnemonic().GetText());
        }

        // Handle operands
        if (instruction.asmOperandList() != null)
        {
            var operands = instruction.asmOperandList().asmOperand();
            if (operands.Length > 0)
            {
                var operandStrings = operands.Select(op => op.GetText()).ToArray();
                parts.Add(string.Join(",", operandStrings));
            }
        }

        // Join label and instruction with space, but mnemonic and operands with space
        if (parts is [])
        {
            return string.Empty;
        }

        // If we have a label + mnemonic + operands, format as "label: mnemonic operands"
        // If we have just a mnemonic + operands, format as "mnemonic operands"
        // If we have just a label, format as "label:"
        if (instruction.asmLabel() != null && instruction.asmMnemonic() != null)
        {
            // label: mnemonic operands
            var label = parts[0];
            var rest = string.Join(" ", parts.Skip(1));
            return $"{label} {rest}";
        }
        else if (instruction.asmMnemonic() != null)
        {
            // mnemonic operands (separated by space)
            return string.Join(" ", parts);
        }
        else
        {
            // Just a label
            return parts[0];
        }
    }

    /// <summary>
    /// Parses an asm use item and computes its value at compile time.
    /// This handles sizeof(Type), offsetof(Type, field), alignof(Type), and const IDENTIFIER.
    /// </summary>
    private IrAsmUseItem? ParseAsmUseItem(NovusParser.AsmUseItemContext useItem, SourceLocation location)
    {
        // sizeof(Type)
        if (useItem.KW_SIZEOF() != null)
        {
            var typeCtx = useItem.type();
            var irType = ParseType(typeCtx);

            // Get the type name for the substitution name
            string typeName = GetTypeNameForSubstitution(irType);

            // Compute the size at compile time
            long size = irType.SizeInBytes;

            return IrAsmUseItem.SizeOf(irType, size, typeName);
        }

        // offsetof(Type, field)
        if (useItem.KW_OFFSETOF() != null)
        {
            var typeCtx = useItem.type();
            var irType = ParseType(typeCtx);
            var fieldName = useItem.IDENTIFIER().GetText();

            // Get the type name for the substitution name
            string typeName = GetTypeNameForSubstitution(irType);

            // The type must be a struct to get field offset
            if (irType is not IrStructType structType)
            {
                _diagnostics.ReportError(
                    ErrorCodes.AsmOffsetOfRequiresStruct,
                    $"offsetof requires a struct type, got '{irType}'",
                    location);
                return null;
            }

            // Find the field
            var field = structType.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (field == null)
            {
                var availableFields = string.Join(", ", structType.Fields.Select(f => f.Name));
                _diagnostics.ReportError(
                    ErrorCodes.AsmFieldNotFound,
                    $"struct '{structType.StructName}' has no field '{fieldName}'. Available fields: {availableFields}",
                    location);
                return null;
            }

            // Trigger size calculation which sets field offsets
            _ = structType.SizeInBytes;

            return IrAsmUseItem.OffsetOf(irType, fieldName, field.Offset, typeName);
        }

        // alignof(Type)
        if (useItem.KW_ALIGNOF() != null)
        {
            var typeCtx = useItem.type();
            var irType = ParseType(typeCtx);

            // Get the type name for the substitution name
            string typeName = GetTypeNameForSubstitution(irType);

            // Compute alignment at compile time
            // For 68k: bytes are 1-aligned, everything else is 2-aligned (word)
            int alignment = irType.SizeInBytes == 1 ? 1 : 2;

            return IrAsmUseItem.AlignOf(irType, alignment, typeName);
        }

        // const IDENTIFIER
        if (useItem.KW_CONST() != null)
        {
            var constName = useItem.IDENTIFIER().GetText();

            // Look up the constant value in the module
            if (!_module.Constants.TryGetValue(constName, out var constDef))
            {
                _diagnostics.ReportError(
                    ErrorCodes.AsmConstantNotFound,
                    $"constant '{constName}' not found",
                    location);
                return null;
            }

            // Get the constant's value - must be an integer constant
            if (constDef.Value is long longVal)
            {
                return IrAsmUseItem.Const(constName, longVal);
            }
            else if (constDef.Value is int intVal)
            {
                return IrAsmUseItem.Const(constName, intVal);
            }
            else if (constDef.Value is uint uintVal)
            {
                return IrAsmUseItem.Const(constName, uintVal);
            }
            else if (constDef.Value is ulong ulongVal)
            {
                return IrAsmUseItem.Const(constName, (long)ulongVal);
            }
            else if (constDef.Value is short shortVal)
            {
                return IrAsmUseItem.Const(constName, shortVal);
            }
            else if (constDef.Value is ushort ushortVal)
            {
                return IrAsmUseItem.Const(constName, ushortVal);
            }
            else if (constDef.Value is byte byteVal)
            {
                return IrAsmUseItem.Const(constName, byteVal);
            }
            else if (constDef.Value is sbyte sbyteVal)
            {
                return IrAsmUseItem.Const(constName, sbyteVal);
            }
            else
            {
                _diagnostics.ReportError(
                    ErrorCodes.AsmConstantNotInteger,
                    $"constant '{constName}' must be an integer constant for use in assembly",
                    location);
                return null;
            }
        }

        _diagnostics.ReportError(
            ErrorCodes.AsmUnrecognizedUseItem,
            $"unrecognized use item in asm block",
            location);
        return null;
    }

    /// <summary>
    /// Gets a simplified type name suitable for substitution in assembly.
    /// Converts complex type names to C-friendly identifiers (e.g., "Player" from IrStructType).
    /// </summary>
    private static string GetTypeNameForSubstitution(IrType type)
    {
        return type switch
        {
            IrStructType structType => structType.StructName,
            IrEnumType enumType => enumType.EnumName,
            IrArrayType arrayType => $"{GetTypeNameForSubstitution(arrayType.ElementType)}_{arrayType.LengthParameter ?? arrayType.Length.ToString()}",
            IrPointerType ptrType => $"ptr_{GetTypeNameForSubstitution(ptrType.PointeeType)}",
            IrIntType intType => intType.Name,
            IrFloatType floatType => floatType.Name,
            IrBoolType => "bool",
            IrVoidType => "void",
            _ => (type.Name ?? "unknown").Replace(" ", "_").Replace("<", "_").Replace(">", "_").Replace(",", "_")
        };
    }

    /// <summary>
    /// Helper to look up a variable by name and return an IrValue for it.
    /// </summary>
    private IrValue? LookupVariable(string name)
    {
        // First check local variables
        var localVar = LookupLocalVariable(name);
        if (localVar != null)
        {
            return new IrVariable(localVar.Name, localVar.Type);
        }

        // Check function parameters
        if (_currentFunction != null)
        {
            var param = _currentFunction.Parameters.FirstOrDefault(p => p.Name == name);
            if (param != null)
            {
                return new IrVariable(name, param.Type);
            }
        }

        // Check global/static variables
        var globalVar = _module.StaticVariables.FirstOrDefault(s => s.Name == name);
        if (globalVar != null)
        {
            return new IrGlobalVariable(name, globalVar.Type);
        }

        return null;
    }

    /// <summary>
    /// Unescape a string literal (handle \n, \t, etc.)
    /// </summary>
    private static string UnescapeString(string s)
    {
        if (!s.Contains('\\'))
            return s;

        var result = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                i++;
                result.Append(s[i] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '0' => '\0',
                    '\\' => '\\',
                    '"' => '"',
                    '\'' => '\'',
                    _ => s[i]
                });
            }
            else
            {
                result.Append(s[i]);
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// Compile let...else statement: let pattern = expr else { diverging block }
    /// This desugars to: if pattern matches expr, bind variables; else execute diverging block
    /// </summary>
    public override object? VisitLetElseStatement([NotNull] NovusParser.LetElseStatementContext context)
    {
        // Set source location for this statement
        _currentStatementLocation = GetLocation(context);

        // Evaluate the expression
        var exprValue = Visit(context.expression());
        if (exprValue is not IrValue exprIr)
        {
            return null;
        }

        var pattern = context.pattern();
        var variantPattern = pattern as NovusParser.VariantPatternContext;
        var contextualOption =
            (pattern is NovusParser.IdentifierPatternContext or NovusParser.VarIdentifierPatternContext) &&
            exprIr.Type is IrEnumType { EnumName: "Option" };

        // Create labels for pattern match success and failure
        var successLabel = $"let_else_ok_{_labelCounter++}";
        var elseLabel = $"let_else_fail_{_labelCounter++}";
        var continueLabel = $"let_else_cont_{_labelCounter++}";

        // Generate pattern match code
        // For simple identifier patterns, this is just binding the variable
        // For variant patterns (e.g., Some(x)), we need to check the variant and extract data

        if (pattern is NovusParser.IdentifierPatternContext idPattern && !contextualOption)
        {
            // Simple binding: let x = expr else { ... }
            // This always succeeds, so the else block is unreachable
            var name = idPattern.IDENTIFIER().GetText();
            var localVar = new IrLocalVariable(name, exprIr.Type, false);
            _currentFunction!.LocalVariables.Add(localVar);
            _localVariables[name] = localVar;

            // Emit the declaration
            Emit(new IrLocalDecl(name, exprIr.Type, false, exprIr));
        }
        else if (pattern is NovusParser.VarIdentifierPatternContext mutIdPattern && !contextualOption)
        {
            // Mutable binding: let mut x = expr else { ... }
            var name = mutIdPattern.IDENTIFIER().GetText();
            var localVar = new IrLocalVariable(name, exprIr.Type, true);
            _currentFunction!.LocalVariables.Add(localVar);
            _localVariables[name] = localVar;

            // Emit the declaration
            Emit(new IrLocalDecl(name, exprIr.Type, true, exprIr));
        }
        else if (variantPattern != null || contextualOption)
        {
            // Enum variant pattern: let Some(value) = expr else { ... }
            // Generate: if expr.tag == variant_tag then bind data else diverge

            var enumType = exprIr.Type switch
            {
                IrEnumType value => value,
                IrPointerType pointer => pointer.PointeeType as IrEnumType,
                IrReferenceType reference => reference.PointeeType as IrEnumType,
                IrMutReferenceType reference => reference.PointeeType as IrEnumType,
                _ => null
            };

            // Materialize expressions once. Besides preserving side effects, this avoids
            // asking the C backend to access fields on VBCC-incompatible compound literals.
            IrValue matchedValue = exprIr;
            if (exprIr is not IrVariable)
            {
                var valueName = $"__let_else_value_{_tempCounter++}";
                var localVar = new IrLocalVariable(valueName, exprIr.Type, false);
                _currentFunction!.LocalVariables.Add(localVar);
                _localVariables[valueName] = localVar;
                Emit(new IrLocalDecl(valueName, exprIr.Type, false, exprIr));
                matchedValue = new IrVariable(valueName, exprIr.Type);
            }
            if (matchedValue.Type is IrPointerType or IrReferenceType or IrMutReferenceType)
                matchedValue = new IrDereferenceValue(matchedValue, enumType!);

            var variantName = contextualOption ? "Some" : variantPattern!.variantName().GetText();
            var variantNameOnly = variantName.Contains("::") ? variantName.Split("::").Last() : variantName;

            // Get the enum type
            if (enumType != null)
            {
                // Find the variant
                var variant = enumType.Variants.FirstOrDefault(v => v.Name == variantNameOnly);
                if (variant != null)
                {
                    // Check if the enum value matches this variant
                    var tagResultName = $"_tag_{_tempCounter++}";
                    Emit(new IrExtractTag(tagResultName, matchedValue));
                    var tagValue = new IrVariable(tagResultName, IrIntType.I32);

                    var tagConstant = new IrConstant(variant.Tag, IrIntType.I32);
                    var compareResultName = $"_cmp_{_tempCounter++}";
                    Emit(new IrBinaryOp(compareResultName, IrBinaryOp.OpKind.Eq, tagValue, tagConstant, IrBoolType.Instance));
                    var compareResult = new IrVariable(compareResultName, IrBoolType.Instance);

                    // Branch: if matches, go to success block; else go to else block
                    var successBlock = _currentFunction!.CreateBasicBlock(successLabel);
                    var elseBlock = _currentFunction.CreateBasicBlock(elseLabel);
                    var continueBlock = _currentFunction.CreateBasicBlock(continueLabel);

                    Emit(new IrConditionalBranch(compareResult, successLabel, elseLabel));

                    // Success block: extract and bind the data
                    _currentBlock = successBlock;

                    var subPatterns = contextualOption
                        ? new[] { pattern }
                        : variantPattern!.patternList()?.pattern() ?? [];
                    if (subPatterns.Length > 0)
                    {
                        for (int i = 0; i < subPatterns.Length && i < variant.AssociatedData.Count; i++)
                        {
                            var dataType = variant.AssociatedData[i];
                            BindPatternData(subPatterns[i], matchedValue, enumType,
                                variantNameOnly, i, dataType);
                        }
                    }

                    // Jump to continue block
                    Emit(new IrBranch(continueLabel));

                    // Else block: execute the diverging code
                    _currentBlock = elseBlock;
                    Visit(GetLetElseDivergence(context));
                    // The else block must diverge, so no need to branch to continue

                    // If else block didn't diverge (error case), add an unreachable marker
                    if (!CurrentBlockHasTerminator())
                    {
                        // Add an unreachable terminator (the semantic analyzer should have caught this)
                        Emit(new IrBranch(continueLabel));
                    }

                    // Continue block
                    _currentBlock = continueBlock;
                }
            }
        }
        else if (pattern is NovusParser.WildcardPatternContext)
        {
            // Wildcard pattern: let _ = expr else { ... }
            // Always matches, else block is unreachable
            // Just evaluate the expression for side effects
        }
        else
        {
            // Other patterns - for now, just visit the else block
            Visit(GetLetElseDivergence(context));
        }

        return null;
    }

    private static ParserRuleContext GetLetElseDivergence(
        NovusParser.LetElseStatementContext context) =>
        context.block() ?? (ParserRuleContext?)context.returnStatement() ??
        (ParserRuleContext?)context.breakStatement() ??
        (ParserRuleContext?)context.continueStatement() ?? context.panicStatement()!;

    /// <summary>
    /// Helper to bind pattern data from an enum variant.
    /// </summary>
    private void BindPatternData(NovusParser.PatternContext subPattern, IrValue enumValue, IrEnumType enumType,
        string variantName, int dataIndex, IrType dataType)
    {
        if (subPattern is NovusParser.IdentifierPatternContext idPattern)
        {
            var sourceName = idPattern.IDENTIFIER().GetText();
            var name = UniqueLocalName(sourceName);
            var localVar = new IrLocalVariable(name, dataType, false);
            _currentFunction!.LocalVariables.Add(localVar);
            _localVariables[sourceName] = localVar;
            _localVariables[name] = localVar;

            // Extract the data from the enum
            var extractResultName = $"_extract_{_tempCounter++}";
            Emit(new IrExtractVariantData(extractResultName, enumValue, variantName, dataIndex, dataType));
            var extractedValue = new IrVariable(extractResultName, dataType);

            // Emit declaration with extracted value
            Emit(new IrLocalDecl(name, dataType, false, extractedValue));

            if (EnsureDropMethodInstantiated(dataType))
            {
                InjectAutomaticDrop(name, dataType);
            }
        }
        else if (subPattern is NovusParser.VarIdentifierPatternContext mutIdPattern)
        {
            var sourceName = mutIdPattern.IDENTIFIER().GetText();
            var name = UniqueLocalName(sourceName);
            var localVar = new IrLocalVariable(name, dataType, true);
            _currentFunction!.LocalVariables.Add(localVar);
            _localVariables[sourceName] = localVar;
            _localVariables[name] = localVar;

            // Extract the data from the enum
            var extractResultName = $"_extract_{_tempCounter++}";
            Emit(new IrExtractVariantData(extractResultName, enumValue, variantName, dataIndex, dataType));
            var extractedValue = new IrVariable(extractResultName, dataType);

            // Emit declaration with extracted value
            Emit(new IrLocalDecl(name, dataType, true, extractedValue));

            if (EnsureDropMethodInstantiated(dataType))
            {
                InjectAutomaticDrop(name, dataType);
            }
        }
        else if (subPattern is NovusParser.ReferencePatternContext referencePattern)
        {
            var innerPattern = referencePattern.pattern();
            var mutable = innerPattern is NovusParser.VarIdentifierPatternContext;
            var name = innerPattern switch
            {
                NovusParser.IdentifierPatternContext identifier => identifier.IDENTIFIER().GetText(),
                NovusParser.VarIdentifierPatternContext identifier => identifier.IDENTIFIER().GetText(),
                _ => null
            };
            if (name == null)
                return;

            var sourceName = name;
            name = UniqueLocalName(sourceName);

            IrType referenceType = mutable
                ? _typeInterner.GetMutReferenceType(dataType)
                : _typeInterner.GetReferenceType(dataType);
            var localVar = new IrLocalVariable(name, referenceType, false);
            _currentFunction!.LocalVariables.Add(localVar);
            _localVariables[sourceName] = localVar;
            _localVariables[name] = localVar;

            var payload = new IrEnumPayloadAccess(enumValue, enumType, variantName, dataIndex, dataType);
            Emit(new IrLocalDecl(name, referenceType, false,
                new IrBorrowValue(payload, referenceType, mutable)));
        }
        else if (subPattern is NovusParser.WildcardPatternContext)
        {
            // Wildcard - don't bind anything
        }
        // Other sub-pattern types could be handled here
    }

    /// <summary>
    /// Saves statement-level state that must be preserved across generic instantiation.
    /// When monomorphizing a generic method that contains if-statements, the inner method's
    /// if-statement processing can corrupt the outer method's state (like _ifLabels).
    /// </summary>
    public object? SaveStatementState()
    {
        return new StatementState
        {
            IfLabels = _ifLabels,
            PendingIfLetVariable = _pendingIfLetVariable
        };
    }

    /// <summary>
    /// Restores statement-level state saved by SaveStatementState.
    /// </summary>
    public void RestoreStatementState(object? state)
    {
        if (state is StatementState s)
        {
            _ifLabels = s.IfLabels;
            _pendingIfLetVariable = s.PendingIfLetVariable;
        }
    }

    /// <summary>
    /// Internal record to hold statement-level state during generic instantiation.
    /// </summary>
    private record StatementState
    {
        public (string thenLabel, string falseTarget)? IfLabels { get; init; }
        public (string varName, string tempName, IrType type, bool isMutable)? PendingIfLetVariable { get; init; }
    }
}
