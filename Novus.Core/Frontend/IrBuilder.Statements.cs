using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;

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
        var name = context.IDENTIFIER().GetText();
        var returnType = ParseReturnType(context.type());

        // Parse visibility, extern flag, and other modifiers
        var (visibility, isExtern, _) = AstModifierHelper.ParseModifiers(context, 3);

        var function = new IrFunction(name, returnType, visibility, isExtern);
        _module.AddFunction(function);
        _currentFunction = function;

        // Check for #[export] attribute
        var attributes = ParseAttributesSimple(context.attribute());
        if (attributes.Has("export"))
        {
            function.IsExported = true;
        }

        // Parse generic parameters
        function.GenericParameters.AddRange(ParseGenericParameters(context.genericParams()));

        // Parse where clause
        function.WhereClause = ParseWhereClause(context.whereClause());

        // Parse parameters
        if (context.parameterList() != null)
        {
            var paramList = context.parameterList();
            ParseRegularParameters(paramList, function.Parameters);

            // Add variadic parameter if present
            ParseVariadicParameter(paramList, function);
        }

        // Skip body processing for extern functions
        if (isExtern)
        {
            return null;
        }

        // Create entry block
        _currentBlock = function.CreateBasicBlock("entry");

        // Visit function body
        var blockResult = Visit(context.block());

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
        IrValue? lastValue = null;

        foreach (var stmt in context.statement())
        {
            var result = Visit(stmt);
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

        return lastValue;
    }

    public override object? VisitReturnStatement([NotNull] NovusParser.ReturnStatementContext context)
    {
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
            }

            _currentBlock!.AddInstruction(new IrReturn(value));
        });

        return null;
    }

    public override object? VisitVariableDeclaration([NotNull] NovusParser.VariableDeclarationContext context)
    {
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
            // The expression has been evaluated - if it was a function call,
            // the IrCall instruction was already added to the block.
            // We just discard the result variable and don't create a local.
            return null;
        }

        // Use annotated type if specified, otherwise infer from value
        IrType type = annotatedType ?? value.Type;

        // Create local variable
        var localVar = new IrLocalVariable(name, type, isMutable);
        _currentFunction!.LocalVariables.Add(localVar);
        _localVariables[name] = localVar;

        // Generate IR for the declaration with initial value
        _currentBlock!.AddInstruction(new IrLocalDecl(name, type, isMutable, value));

        // Automatic defer for types with drop() method (RAII-style cleanup)
        // For generic types, eagerly instantiate the drop() method if it exists as a template
        if (EnsureDropMethodInstantiated(type))
        {
            InjectAutomaticDrop(name, type);
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

        return null;
    }

    public override object? VisitAssignmentStatement([NotNull] NovusParser.AssignmentStatementContext context)
    {
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

    private object? ProcessAssignmentStatement(NovusParser.AssignmentStatementContext context)
    {
        // Declare errorLocation once at method start to avoid CS0136 errors
        SourceLocation errorLocation;

        // Get the identifier or 'self' keyword
        var identifier = context.IDENTIFIER();
        var selfKeyword = context.KW_SELF();

        string name;
        if (identifier != null)
        {
            name = identifier.GetText();
        }
        else if (selfKeyword != null)
        {
            name = "self";
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
        for (int i = 0; i < context.ChildCount; i++)
        {
            if (context.GetChild(i).GetText() == "*")
                derefCount++;
            else if (context.GetChild(i) is ITerminalNode terminal && terminal.Symbol.Type == NovusLexer.IDENTIFIER)
                break;
        }

        var lvalueSuffixes = context.lvalueSuffix();

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
                    baseVar = new IrVariable(name, localVar.Type);
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
                        _currentBlock!.AddInstruction(loadMember);
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
                        var loadIndex = new IrIndexAccess(tempName, currentLValue, indexExpr, elementType);
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
                    _currentBlock!.AddInstruction(new IrMemberAccess(loadTemp, actualBase, memberName, field.Type, field.Offset));
                    var currentValue = new IrVariable(loadTemp, field.Type);

                    // Increment/decrement
                    var newValueTemp = $"%t{_tempCounter++}";
                    var opKind = (op == "++" ? IrBinaryOp.OpKind.Add : IrBinaryOp.OpKind.Sub);
                    var binOp = new IrBinaryOp(newValueTemp, opKind, currentValue, new IrConstant(1, field.Type), field.Type);
                    _currentBlock.AddInstruction(binOp);

                    // Store back
                    var newValue = new IrVariable(newValueTemp, field.Type);
                    _currentBlock.AddInstruction(new IrMemberStore(actualBase, memberName, field.Offset, newValue));

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
                    _currentBlock!.AddInstruction(new IrIndexAccess(loadTemp, currentLValue, indexExpr, elementType));
                    var currentValue = new IrVariable(loadTemp, elementType);

                    // Increment/decrement
                    var newValueTemp = $"%t{_tempCounter++}";
                    var opKind = (op == "++" ? IrBinaryOp.OpKind.Add : IrBinaryOp.OpKind.Sub);
                    var binOp = new IrBinaryOp(newValueTemp, opKind, currentValue, new IrConstant(1, elementType), elementType);
                    _currentBlock.AddInstruction(binOp);

                    // Store back
                    var newValue = new IrVariable(newValueTemp, elementType);
                    _currentBlock.AddInstruction(new IrIndexStore(currentLValue, indexExpr, newValue));

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
                    variable = new IrVariable(name, localVar.Type);
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
                _currentBlock.AddInstruction(new IrStore(name, new IrVariable(resultTemp, varType)));

                return null;
            }
        }

        // Check if this is a member or index assignment (has lvalueSuffix elements)
        if (lvalueSuffixes.Length > 0)
        {
            // Handle member/index assignments: obj.field = value, arr[index] = value
            var value = (IrValue?)Visit(context.expression());
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
            IrVariable baseVar;
            if (_localVariables.ContainsKey(name))
            {
                baseVar = new IrVariable(name, _localVariables[name].Type);
            }
            else if (_currentFunction != null && _currentFunction.Parameters.Any(p => p.Name == name))
            {
                var param = _currentFunction.Parameters.First(p => p.Name == name);
                baseVar = new IrVariable(name, param.Type);
            }
            else
            {
                errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.UndefinedVariable,
                    $"Undefined variable: {name}",
                    errorLocation
                );
                return null;
            }

            // For single member access (e.g., self.value = expr)
            if (lvalueSuffixes.Length == 1 && lvalueSuffixes[0].GetChild(0).GetText() == ".")
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

                // Find the field offset
                int fieldOffset = 0;
                bool found = false;
                foreach (var field in irStructType.Fields)
                {
                    if (field.Name == memberName)
                    {
                        fieldOffset = field.Offset;
                        found = true;
                        break;
                    }
                }

                if (!found)
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
                var storeMember = new IrMemberStore(actualBase, memberName, fieldOffset, value);
                _currentBlock!.AddInstruction(storeMember);

                return null;
            }

            // For single index access (e.g., ptr[0] = value, arr[i] = value)
            if (lvalueSuffixes.Length == 1 && lvalueSuffixes[0].GetChild(0).GetText() == "[")
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

                // Generate index store instruction
                var indexStore = new IrIndexStore(baseVar, indexExpr, value);
                _currentBlock!.AddInstruction(indexStore);

                return null;
            }

            // OPTIMIZATION: Detect array[index].field = value pattern
            // This is common and we can emit a specialized instruction to avoid creating a temporary copy
            if (lvalueSuffixes.Length == 2 &&
                lvalueSuffixes[0].GetChild(0).GetText() == "[" &&
                lvalueSuffixes[1].GetChild(0).GetText() == ".")
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

                // Get the field name
                var memberName = lvalueSuffixes[1].IDENTIFIER().GetText();

                // Determine the element type of the array
                IrType elementType;
                if (baseVar.Type is IrPointerType pt)
                {
                    elementType = pt.PointeeType;
                }
                else if (baseVar.Type is IrArrayType at)
                {
                    elementType = at.ElementType;
                }
                else
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.CannotIndexType,
                        $"Cannot index type '{baseVar.Type}' - must be pointer or array",
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

                // Emit specialized indexed field store instruction
                var indexedFieldStore = new IrIndexedFieldStore(baseVar, indexExpr, memberName, field.Offset, value);
                _currentBlock!.AddInstruction(indexedFieldStore);
                return null;
            }

            // Handle complex lvalue chains (e.g., self.ptr[index] = value, self.field1.field2 = value)
            // Build up the lvalue by processing each suffix in order
            IrValue currentLValue = baseVar;

            for (int i = 0; i < lvalueSuffixes.Length; i++)
            {
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

                    if (isLastSuffix)
                    {
                        // This is the final field - emit a store
                        var storeMember = new IrMemberStore(actualBase, memberName, field.Offset, value);
                        _currentBlock!.AddInstruction(storeMember);
                        return null;
                    }
                    else
                    {
                        // This is an intermediate field - load it for the next suffix
                        var tempName = $"_field_{memberName}_{_tempCounter++}";
                        var loadMember = new IrMemberAccess(tempName, actualBase, memberName, field.Type, field.Offset);
                        _currentBlock!.AddInstruction(loadMember);
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

                    if (isLastSuffix)
                    {
                        // This is the final index - emit an index store
                        var indexStore = new IrIndexStore(currentLValue, indexExpr, value);
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
                        var loadIndex = new IrIndexAccess(tempName, currentLValue, indexExpr, elementType);
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
            var value = (IrValue?)Visit(context.expression());

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
                variable = new IrVariable(name, localVar.Type);
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

            // Generate the dereference store instruction
            _currentBlock!.AddInstruction(new IrDereferenceStore(pointer, value));
        }
        else
        {
            // Simple variable assignment or compound operator: x = value or x += value
            var value = (IrValue?)Visit(context.expression());

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
                    variable = new IrVariable(name, localVar.Type);
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

                // Desugar compound operator: x op= y becomes x = x op y
                var resultTemp = $"%t{_tempCounter++}";
                IrBinaryOp.OpKind opKind = op switch
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
                    _ => IrBinaryOp.OpKind.Add  // ERROR: $"Unknown compound operator: {op}"
                };

                var binOp = new IrBinaryOp(resultTemp, opKind, variable, value, varType);
                _currentBlock!.AddInstruction(binOp);

                // Store the result back
                _currentBlock.AddInstruction(new IrStore(name, new IrVariable(resultTemp, varType)));
            }
            else
            {
                // Simple assignment: x = value
                // The semantic analyzer will check if the variable is mutable
                // Here we just generate the IR
                _currentBlock!.AddInstruction(new IrStore(name, value));
            }
        }

        return null;
    }

    public override object? VisitExpressionStatement([NotNull] NovusParser.ExpressionStatementContext context)
    {
        // Handle postfix conditional (if/unless)
        var postfixCondition = context.postfixCondition();

        object? result = null;
        HandlePostfixCondition(postfixCondition, () =>
        {
            // Visit the expression and return its value (for implicit returns)
            result = Visit(context.expression());
        });

        return result;
    }

    public override object? VisitIfStatement([NotNull] NovusParser.IfStatementContext context)
    {
        var thenLabel = $"if_then_{_labelCounter}";
        var elseLabel = $"if_else_{_labelCounter}";
        var endLabel = $"if_end_{_labelCounter}";
        _labelCounter++;

        var hasElse = context.GetChild(4) != null; // Check if 'else' keyword exists
        var falseTarget = hasElse ? elseLabel : endLabel;

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

        Visit(context.block(0));

        // Track whether the then block terminates
        bool thenBranchTerminates = CurrentBlockHasTerminator();

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
            if (context.ifStatement() != null)
            {
                Visit(context.ifStatement());
            }
            else if (context.block().Length > 1)
            {
                Visit(context.block(1));
            }

            // Track whether the else block terminates
            bool elseBranchTerminates = CurrentBlockHasTerminator();

            // Only emit the end label if at least one branch can reach it
            if (!thenBranchTerminates || !elseBranchTerminates)
            {
                _currentBlock!.AddInstruction(new IrLabel(endLabel));
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
        var condition = (IrValue?)Visit(context.expression());
        var (thenLabel, falseTarget) = _ifLabels!.Value;

        // Automatic pointer-to-bool coercion: if ptr { ... }
        // Convert pointer to bool by comparing with null (ptr != 0)
        if (condition!.Type is IrPointerType or IrReferenceType or IrMutReferenceType)
        {
            var resultTemp = $"%t{_tempCounter++}";
            var zeroValue = new IrConstant(0, IrIntType.U32);
            var comparison = new IrBinaryOp(resultTemp, IrBinaryOp.OpKind.Ne, condition, zeroValue, IrBoolType.Instance);
            _currentBlock!.AddInstruction(comparison);
            condition = new IrVariable(resultTemp, IrBoolType.Instance);
        }

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
            // Visit while statement body directly
            var whileCtx = context.whileStatement();

            // Jump to condition check
            _currentBlock!.AddInstruction(new IrBranch(continueLabel));

            // Continue label (condition check)
            _currentBlock!.AddInstruction(new IrLabel(continueLabel));
            var condition = (IrValue?)Visit(whileCtx.expression());

            // Automatic pointer-to-bool coercion
            if (condition!.Type is IrPointerType or IrReferenceType or IrMutReferenceType)
            {
                var ptrToBoolTemp = $"%t{_tempCounter++}";
                var zeroValue = new IrConstant(0, IrIntType.U32);
                var comparison = new IrBinaryOp(ptrToBoolTemp, IrBinaryOp.OpKind.Ne, condition, zeroValue, IrBoolType.Instance);
                _currentBlock!.AddInstruction(comparison);
                condition = new IrVariable(ptrToBoolTemp, IrBoolType.Instance);
            }

            var bodyLabel = $"labeled_{labelName}_body_{_labelCounter - 1}";
            _currentBlock!.AddInstruction(new IrConditionalBranch(condition, bodyLabel, exitLabel));

            // Body
            _currentBlock!.AddInstruction(new IrLabel(bodyLabel));
            Visit(whileCtx.block());

            // Jump back to condition
            if (!CurrentBlockHasTerminator())
            {
                _currentBlock!.AddInstruction(new IrBranch(continueLabel));
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

    public override object? VisitWhileStatement([NotNull] NovusParser.WhileStatementContext context)
    {
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

        // Automatic pointer-to-bool coercion: while ptr { ... }
        // Convert pointer to bool by comparing with null (ptr != 0)
        if (condition!.Type is IrPointerType or IrReferenceType or IrMutReferenceType)
        {
            var ptrToBoolTemp = $"%t{_tempCounter++}";
            var zeroValue = new IrConstant(0, IrIntType.U32);
            var comparison = new IrBinaryOp(ptrToBoolTemp, IrBinaryOp.OpKind.Ne, condition, zeroValue, IrBoolType.Instance);
            _currentBlock!.AddInstruction(comparison);
            condition = new IrVariable(ptrToBoolTemp, IrBoolType.Instance);
        }

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

    public override object? VisitForCStyle([NotNull] NovusParser.ForCStyleContext context)
    {
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
        // Check if this is a range expression - handle it specially for efficiency
        var exprCtx = context.expression();
        if (exprCtx is NovusParser.RangeExprContext rangeExpr)
        {
            return VisitForInRangeLoop(context, rangeExpr, false);
        }
        if (exprCtx is NovusParser.RangeInclusiveExprContext rangeInclusiveExpr)
        {
            return VisitForInRangeLoop(context, rangeInclusiveExpr, true);
        }

        // Desugar: for item in collection { body }
        // Into:    let _coll = collection
        //          let _idx = 0
        //          let _len = _coll.len()
        //          while _idx < _len {
        //              let _opt = _coll.get(_idx)
        //              match _opt {
        //                  Option::Some(item) => { body }
        //                  Option::None => break
        //              }
        //              _idx = _idx + 1
        //          }

        var itemName = context.IDENTIFIER().GetText();
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
        var collection = (IrValue)Visit(context.expression())!;
        var collectionType = collection.Type;

        // Store the collection in a local variable
        var collVar = new IrLocalVariable(collVarName, collectionType, false);
        _currentFunction!.LocalVariables.Add(collVar);
        _localVariables[collVarName] = collVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(collVarName, collectionType, false, collection));

        // Get the type name for method lookup
        var typeName = collectionType is IrStructType st ? st.StructName : collectionType.Name;

        // For-in loops require the Iterable trait (with get() and len() methods)
        // Priority: 1) Iterable trait methods, 2) regular methods (fallback for backward compatibility)

        string? lenMethodName = null;
        IrFunction? lenMethod = null;

        // First, try to find Iterable trait implementation
        lenMethodName = _module.FindTraitMethod(typeName, "len");
        if (lenMethodName != null)
        {
            lenMethod = _module.Functions.FirstOrDefault(f => f.Name == lenMethodName);
        }

        // If no trait method found, fall back to regular methods for backward compatibility
        if (lenMethod == null)
        {
            lenMethodName = $"{typeName}::len";
            lenMethod = _module.Functions.FirstOrDefault(f => f.Name == lenMethodName);

            // If method not found, try to instantiate it for monomorphized structs
            if (lenMethod == null && collectionType is IrStructType collectionStruct && collectionStruct.CacheKey != null)
            {
                lenMethod = InstantiateGenericMethod(collectionStruct, "len");
                if (lenMethod != null)
                {
                    lenMethodName = lenMethod.Name;
                }
            }
        }

        if (lenMethod == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Type '{typeName}' does not implement Iterable trait (missing len() method). For-in loops require types to implement Iterable<T>.",
                errorLocation
            );
            return null;
        }

        // Call len()
        var collVarRef = new IrVariable(collVarName, collectionType);
        var lenReceiverArg = new IrBorrowValue(collVarRef, lenMethod.Parameters[0].Type, false);
        var lenResultName = $"%t{_tempCounter++}";
        var lenCall = new IrCall(lenMethodName, lenMethod.ReturnType, lenResultName);
        lenCall.Arguments.Add(lenReceiverArg);
        _currentBlock!.AddInstruction(lenCall);

        // Store length in local variable
        var lenVar = new IrLocalVariable(lenVarName, lenMethod.ReturnType, false);
        _currentFunction!.LocalVariables.Add(lenVar);
        _localVariables[lenVarName] = lenVar;
        var lenResult = new IrVariable(lenResultName, lenMethod.ReturnType);
        _currentBlock!.AddInstruction(new IrLocalDecl(lenVarName, lenMethod.ReturnType, false, lenResult));

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
        var lenVarRef = new IrVariable(lenVarName, lenMethod.ReturnType);
        var condResultName = $"%t{_tempCounter++}";
        var condCheck = new IrBinaryOp(condResultName, IrBinaryOp.OpKind.Lt, idxVarRef, lenVarRef, IrBoolType.Instance);
        _currentBlock!.AddInstruction(condCheck);
        var condResult = new IrVariable(condResultName, IrBoolType.Instance);
        _currentBlock!.AddInstruction(new IrConditionalBranch(condResult, bodyLabel, endLabel));

        // Body label
        _currentBlock!.AddInstruction(new IrLabel(bodyLabel));

        // Call get(_idx) to get the item
        // Priority: 1) Iterable trait methods, 2) regular methods (fallback)

        string? getMethodName = null;
        IrFunction? getMethod = null;

        // First, try to find Iterable trait implementation
        getMethodName = _module.FindTraitMethod(typeName, "get");
        if (getMethodName != null)
        {
            getMethod = _module.Functions.FirstOrDefault(f => f.Name == getMethodName);
        }

        // If no trait method found, fall back to regular methods for backward compatibility
        if (getMethod == null)
        {
            getMethodName = $"{typeName}::get";
            getMethod = _module.Functions.FirstOrDefault(f => f.Name == getMethodName);

            // If method not found, try to instantiate it for monomorphized structs
            if (getMethod == null && collectionType is IrStructType collectionStruct2 && collectionStruct2.CacheKey != null)
            {
                getMethod = InstantiateGenericMethod(collectionStruct2, "get");
                if (getMethod != null)
                {
                    getMethodName = getMethod.Name;
                }
            }
        }

        if (getMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Type '{typeName}' does not implement Iterable trait (missing get() method). For-in loops require types to implement Iterable<T>.",
                errorLocation
            );
            return null;
        }

        // Call get(index)
        var getReceiverArg = new IrBorrowValue(collVarRef, getMethod.Parameters[0].Type, false);
        var getResultName = $"%t{_tempCounter++}";
        var getCall = new IrCall(getMethodName, getMethod.ReturnType, getResultName);
        getCall.Arguments.Add(getReceiverArg);
        getCall.Arguments.Add(idxVarRef);
        _currentBlock!.AddInstruction(getCall);

        // Match on the Option result
        var getResult = new IrVariable(getResultName, getMethod.ReturnType);

        // Get the Option enum type to extract the inner type T
        if (getMethod.ReturnType is not IrEnumType optionType || optionType.EnumName != "Option")
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Iterator::get must return Option<T>, but returned {getMethod.ReturnType.Name}",
                errorLocation
            );
            return null;
        }

        // Find the Some variant to get the inner type
        var someVariant = optionType.Variants.FirstOrDefault(v => v.Name == "Some");
        if (someVariant == null || someVariant.AssociatedData.Count == 0)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Option::Some variant not found or has no associated data",
                errorLocation
            );
            return null;
        }

        var innerType = someVariant.AssociatedData[0];

        // Extract the tag and check if it's Some or None
        var tagResultName = $"%t{_tempCounter++}";
        var extractTag = new IrExtractTag(tagResultName, getResult);
        _currentBlock!.AddInstruction(extractTag);

        // Get the Some and None variant tags
        var noneVariant = optionType.Variants.FirstOrDefault(v => v.Name == "None");
        if (noneVariant == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Option::None variant not found",
                errorLocation
            );
            return null;
        }

        // Compare tag with None variant tag
        var tagVar = new IrVariable(tagResultName, IrIntType.I32);
        var noneTagConst = new IrConstant(noneVariant.Tag, IrIntType.I32);
        var isNoneResultName = $"%t{_tempCounter++}";
        var isNoneCheck = new IrBinaryOp(isNoneResultName, IrBinaryOp.OpKind.Eq, tagVar, noneTagConst, IrBoolType.Instance);
        _currentBlock!.AddInstruction(isNoneCheck);

        // Branch: if None, break; otherwise continue to Some case
        var isNoneResult = new IrVariable(isNoneResultName, IrBoolType.Instance);
        _currentBlock!.AddInstruction(new IrConditionalBranch(isNoneResult, matchNoneLabel, matchSomeLabel));

        // None case: break out of loop
        _currentBlock!.AddInstruction(new IrLabel(matchNoneLabel));
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // Some case: unwrap and bind to item variable
        _currentBlock!.AddInstruction(new IrLabel(matchSomeLabel));

        // Extract the value from Option::Some
        var unwrapResultName = $"%t{_tempCounter++}";
        var unwrapInstr = new IrExtractVariantData(unwrapResultName, getResult, "Some", 0, innerType);
        _currentBlock!.AddInstruction(unwrapInstr);

        // Bind to item variable
        var itemVar = new IrLocalVariable(itemName, innerType, false);
        _currentFunction!.LocalVariables.Add(itemVar);
        _localVariables[itemName] = itemVar;
        var unwrappedValue = new IrVariable(unwrapResultName, innerType);
        _currentBlock!.AddInstruction(new IrLocalDecl(itemName, innerType, false, unwrappedValue));

        // Visit the loop body
        Visit(context.block());

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

    /// <summary>
    /// Handles for-in loops over range expressions (e.g., 0..10 or 1..=5)
    /// Generates efficient counter-based code without needing Iterable trait.
    /// </summary>
    private object? VisitForInRangeLoop(NovusParser.ForInLoopContext context, NovusParser.ExpressionContext rangeExpr, bool inclusive)
    {
        // for i in start..end { body }
        // Desugars to:
        //   let i = start
        //   while i < end {  (or i <= end for inclusive)
        //       body
        //       i = i + 1
        //   }

        var itemName = context.IDENTIFIER().GetText();
        var isMutable = context.KW_MUT() != null;
        var condLabel = $"range_cond_{_labelCounter}";
        var bodyLabel = $"range_body_{_labelCounter}";
        var incrLabel = $"range_incr_{_labelCounter}";
        var endLabel = $"range_end_{_labelCounter}";
        _labelCounter++;

        // Get the two operands of the range expression
        IrValue startValue, endValue;
        if (rangeExpr is NovusParser.RangeExprContext exclusiveRange)
        {
            startValue = (IrValue)Visit(exclusiveRange.expression(0))!;
            endValue = (IrValue)Visit(exclusiveRange.expression(1))!;
        }
        else if (rangeExpr is NovusParser.RangeInclusiveExprContext inclusiveRange)
        {
            startValue = (IrValue)Visit(inclusiveRange.expression(0))!;
            endValue = (IrValue)Visit(inclusiveRange.expression(1))!;
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

        // Determine the type for the loop variable (use the start value's type)
        var rangeType = startValue.Type;

        // Ensure both operands have the same type
        if (startValue.Type.Name != endValue.Type.Name)
        {
            // Try to coerce to a common type
            if (startValue.Type is IrIntType && endValue.Type is IrIntType)
            {
                // Use the larger type
                rangeType = GetLargerIntType((IrIntType)startValue.Type, (IrIntType)endValue.Type);
            }
        }

        // Declare the loop variable with the start value
        var loopVar = new IrLocalVariable(itemName, rangeType, true); // Always mutable for incrementing
        _currentFunction!.LocalVariables.Add(loopVar);
        _localVariables[itemName] = loopVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(itemName, rangeType, true, startValue));

        // Store the end value in a temp variable to avoid re-evaluating
        var endVarName = $"_range_end_{_labelCounter - 1}";
        var endVar = new IrLocalVariable(endVarName, rangeType, false);
        _currentFunction!.LocalVariables.Add(endVar);
        _localVariables[endVarName] = endVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(endVarName, rangeType, false, endValue));

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

        // Visit the loop body
        Visit(context.block());

        // Increment label (continue jumps here)
        _currentBlock!.AddInstruction(new IrLabel(incrLabel));

        // Increment: i = i + 1
        var incResultName = $"%t{_tempCounter++}";
        var oneLiteral = new IrConstant(1, rangeType);
        var incOp = new IrBinaryOp(incResultName, IrBinaryOp.OpKind.Add, loopVarRef, oneLiteral, rangeType);
        _currentBlock!.AddInstruction(incOp);
        var incResult = new IrVariable(incResultName, rangeType);
        _currentBlock!.AddInstruction(new IrStore(itemName, incResult));

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
}
