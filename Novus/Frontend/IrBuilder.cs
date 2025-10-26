using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Novus.IR;
using Novus.Parser;

namespace Novus.Frontend;

/// <summary>
/// Builds IR from the parsed AST using the visitor pattern
/// </summary>
public class IrBuilder : NovusBaseVisitor<object?>
{
    private readonly IrModule _module = new();
    private IrFunction? _currentFunction;
    private IrBasicBlock? _currentBlock;
    private int _tempCounter = 0;
    private int _labelCounter = 0;
    private int _stringCounter = 0;  // Counter for string literal labels
    private readonly Stack<string> _loopExitLabels = new(); // Track loop exit labels for break
    private readonly Dictionary<string, IrLocalVariable> _localVariables = new(); // Track local variables in current function
    private readonly Dictionary<string, IrStructType> _structs = new(); // Track struct types
    private readonly Dictionary<string, IrEnumType> _enums = new(); // Track enum types
    private readonly Dictionary<string, IrGenericType> _genericParams = new(); // Track generic type parameters
    private readonly Dictionary<string, (IrType Type, object Value)> _constants = new(); // Track constant values
    public readonly List<IrStringLiteral> StringLiterals = new(); // Track all string literals for data section
    private string _stdLibPath = "."; // Path to standard library

    /// <summary>
    /// Set the standard library path
    /// </summary>
    public void SetStdLibPath(string path)
    {
        _stdLibPath = path;
    }

    /// <summary>
    /// Check if the current block has a terminator instruction (return, branch)
    /// Used to avoid generating dead code after returns
    /// </summary>
    private bool CurrentBlockHasTerminator()
    {
        if (_currentBlock == null || _currentBlock.Instructions.Count == 0)
            return false;

        var lastInst = _currentBlock.Instructions[^1];
        return lastInst is IrReturn or IrBranch;
    }

    public IrModule BuildModule(NovusParser.CompilationUnitContext context)
    {
        // Multi-pass approach to handle forward references:
        // Pass 0a: Implicitly import all of core module
        ImportModule("core", importAll: true);

        // Pass 0b: Process explicit imports
        foreach (var importDecl in context.importDeclaration())
        {
            ProcessImport(importDecl);
        }

        // Pass 1: Register all constant values
        foreach (var constContext in context.constDeclaration())
        {
            RegisterConstant(constContext);
        }

        // Pass 2: Register all enum types
        foreach (var enumContext in context.enumDeclaration())
        {
            RegisterEnum(enumContext);
        }

        // Pass 3: Register all struct types
        foreach (var structContext in context.structDeclaration())
        {
            RegisterStruct(structContext);
        }

        // Pass 4: Collect all function signatures
        foreach (var funcContext in context.functionDeclaration())
        {
            var name = funcContext.IDENTIFIER().GetText();
            var returnType = funcContext.type() != null ? ParseType(funcContext.type()) : IrVoidType.Instance;

            // Check for extern and pub keywords
            var isExtern = false;
            var isPublic = false;
            for (int i = 0; i < Math.Min(3, funcContext.ChildCount); i++)
            {
                var childText = funcContext.GetChild(i)?.GetText();
                if (childText == "extern") isExtern = true;
                if (childText == "pub") isPublic = true;
            }

            var function = new IrFunction(name, returnType, isPublic, isExtern);

            // Parse parameters
            if (funcContext.parameterList() != null)
            {
                foreach (var paramCtx in funcContext.parameterList().parameter())
                {
                    var paramName = paramCtx.IDENTIFIER().GetText();
                    var paramType = ParseType(paramCtx.type());
                    function.Parameters.Add(new IrParameter(paramName, paramType));
                }
            }

            _module.AddFunction(function);
        }

        // Pass 5: Build function bodies
        foreach (var funcContext in context.functionDeclaration())
        {
            var funcName = funcContext.IDENTIFIER().GetText();
            _currentFunction = _module.Functions.First(f => f.Name == funcName);

            // Skip extern functions - they have no body
            if (_currentFunction.IsExtern || funcContext.block() == null)
            {
                continue;
            }

            _currentBlock = _currentFunction.CreateBasicBlock("entry");
            _localVariables.Clear(); // Clear local variables for new function

            // Visit function body and get the last expression value
            var lastValue = Visit(funcContext.block()) as IrValue;

            // Add implicit return if:
            // 1. Function has non-void return type
            // 2. Block doesn't already have a terminator
            // 3. There's a last expression value
            if (_currentFunction.ReturnType is not IrVoidType &&
                !CurrentBlockHasTerminator() &&
                lastValue != null)
            {
                _currentBlock!.AddInstruction(new IrReturn(lastValue));
            }
        }

        return _module;
    }

    private void ProcessImport(NovusParser.ImportDeclarationContext context)
    {
        var moduleName = context.IDENTIFIER().GetText();

        // Get the list of names to import
        var importList = context.importList();
        bool importAll = importList.GetText() == "*";

        ImportModule(moduleName, importAll, importList);
    }

    private void ImportModule(string moduleName, bool importAll, NovusParser.ImportListContext? importList = null)
    {

        // Resolve module path - search order:
        // 1. std/{moduleName}.novus (wrappers)
        // 2. std/ffi/{moduleName}.novus (raw FFI)
        var modulePath = System.IO.Path.Combine(_stdLibPath, moduleName + ".novus");

        if (!System.IO.File.Exists(modulePath))
        {
            // Try ffi subdirectory
            modulePath = System.IO.Path.Combine(_stdLibPath, "ffi", moduleName + ".novus");

            if (!System.IO.File.Exists(modulePath))
            {
                throw new Exception($"Module '{moduleName}' not found. Searched in std/{moduleName}.novus and std/ffi/{moduleName}.novus");
            }
        }

        // Load and parse the module
        var moduleSource = System.IO.File.ReadAllText(modulePath);
        var inputStream = new AntlrInputStream(moduleSource);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var moduleContext = parser.compilationUnit();

        if (parser.NumberOfSyntaxErrors > 0)
        {
            throw new Exception($"Module '{moduleName}' has syntax errors");
        }

        // Build the list of names to import
        var namesToImport = new HashSet<string>();

        if (importAll)
        {
            // Import all pub enums from the module
            foreach (var enumDecl in moduleContext.enumDeclaration())
            {
                // Only import pub enums
                var isPub = false;
                for (int i = 0; i < Math.Min(3, enumDecl.ChildCount); i++)
                {
                    if (enumDecl.GetChild(i)?.GetText() == "pub")
                    {
                        isPub = true;
                        break;
                    }
                }

                if (isPub)
                {
                    namesToImport.Add(enumDecl.IDENTIFIER().GetText());
                }
            }

            // Import all pub constants from the module
            foreach (var constDecl in moduleContext.constDeclaration())
            {
                // Only import pub constants
                var isPub = false;
                for (int i = 0; i < Math.Min(3, constDecl.ChildCount); i++)
                {
                    if (constDecl.GetChild(i)?.GetText() == "pub")
                    {
                        isPub = true;
                        break;
                    }
                }

                if (isPub)
                {
                    namesToImport.Add(constDecl.IDENTIFIER().GetText());
                }
            }

            // Import all pub structs from the module
            foreach (var structDecl in moduleContext.structDeclaration())
            {
                // Only import pub structs
                var isPub = false;
                for (int i = 0; i < Math.Min(3, structDecl.ChildCount); i++)
                {
                    if (structDecl.GetChild(i)?.GetText() == "pub")
                    {
                        isPub = true;
                        break;
                    }
                }

                if (isPub)
                {
                    namesToImport.Add(structDecl.IDENTIFIER().GetText());
                }
            }

            // Import all pub/extern functions from the module
            foreach (var funcDecl in moduleContext.functionDeclaration())
            {
                // Import pub or extern functions
                var isPub = false;
                var isExtern = false;
                for (int i = 0; i < Math.Min(3, funcDecl.ChildCount); i++)
                {
                    if (funcDecl.GetChild(i)?.GetText() == "pub")
                    {
                        isPub = true;
                    }
                    if (funcDecl.GetChild(i)?.GetText() == "extern")
                    {
                        isExtern = true;
                    }
                }

                if (isPub || isExtern)
                {
                    namesToImport.Add(funcDecl.IDENTIFIER().GetText());
                }
            }
        }
        else if (importList != null)
        {
            // Import specific names
            foreach (var importNameCtx in importList.importName())
            {
                namesToImport.Add(importNameCtx.IDENTIFIER(0).GetText());
            }
        }

        // Register imported enums in the module
        foreach (var enumDecl in moduleContext.enumDeclaration())
        {
            var enumName = enumDecl.IDENTIFIER().GetText();

            // Skip if not in the import list
            if (!namesToImport.Contains(enumName))
            {
                continue;
            }

            // Register the enum from the imported module
            RegisterEnum(enumDecl);
        }

        // Register imported constants in the module
        foreach (var constDecl in moduleContext.constDeclaration())
        {
            var constName = constDecl.IDENTIFIER().GetText();

            // Skip if not in the import list
            if (!namesToImport.Contains(constName))
            {
                continue;
            }

            // Register the constant from the imported module
            RegisterConstant(constDecl);
        }

        // Register imported structs in the module
        foreach (var structDecl in moduleContext.structDeclaration())
        {
            var structName = structDecl.IDENTIFIER().GetText();

            // Skip if not in the import list
            if (!namesToImport.Contains(structName))
            {
                continue;
            }

            // Register the struct from the imported module
            RegisterStruct(structDecl);
        }

        // Register imported functions in the module
        foreach (var funcDecl in moduleContext.functionDeclaration())
        {
            var funcName = funcDecl.IDENTIFIER().GetText();

            // Skip if not in the import list
            if (!namesToImport.Contains(funcName))
            {
                continue;
            }

            // Check if function is pub or extern
            var isPub = false;
            var isExtern = false;
            for (int i = 0; i < Math.Min(3, funcDecl.ChildCount); i++)
            {
                if (funcDecl.GetChild(i)?.GetText() == "pub")
                {
                    isPub = true;
                }
                if (funcDecl.GetChild(i)?.GetText() == "extern")
                {
                    isExtern = true;
                }
            }

            if (!isPub && !isExtern)
            {
                throw new Exception($"Cannot import private function '{funcName}' from module '{moduleName}'");
            }

            // Parse function signature
            var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;
            var function = new IrFunction(funcName, returnType, isPublic: false, isExtern: true);

            // Parse parameters
            if (funcDecl.parameterList() != null)
            {
                foreach (var paramCtx in funcDecl.parameterList().parameter())
                {
                    var paramName = paramCtx.IDENTIFIER().GetText();
                    var paramType = ParseType(paramCtx.type());
                    function.Parameters.Add(new IrParameter(paramName, paramType));
                }
            }

            _module.AddFunction(function);
        }
    }

    private void RegisterConstant(NovusParser.ConstDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var type = ParseType(context.type());

        // Evaluate the constant expression using the evaluator
        var valueExpr = context.expression();

        // Convert constants dict to use object values for evaluator
        var constantValues = _constants.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Value
        );

        var evaluator = new SemanticAnalysis.ConstantExpressionEvaluator(constantValues);
        int? value = evaluator.Visit(valueExpr);

        if (value != null)
        {
            _constants[name] = (type, value);
        }
    }

    private void RegisterEnum(NovusParser.EnumDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Handle generic parameters if present
        var genericParams = new List<string>();
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                var paramName = paramId.GetText();
                genericParams.Add(paramName);
                _genericParams[paramName] = new IrGenericType(paramName);
            }
        }

        // Parse enum variants
        var variants = new List<IrEnumVariant>();
        int tag = 0;

        foreach (var variantCtx in context.enumVariant())
        {
            var variantName = variantCtx.IDENTIFIER().GetText();
            var associatedData = new List<IrType>();

            if (variantCtx.typeList() != null)
            {
                foreach (var typeCtx in variantCtx.typeList().type())
                {
                    var dataType = ParseType(typeCtx);
                    associatedData.Add(dataType);
                }
            }

            variants.Add(new IrEnumVariant(variantName, tag++, associatedData));
        }

        var enumType = new IrEnumType(name, variants, genericParams.Count > 0 ? genericParams : null);

        // Force size calculation for non-generic enums
        if (genericParams.Count == 0)
        {
            _ = enumType.SizeInBytes;
        }

        _enums[name] = enumType;
        _module.AddEnum(enumType);

        // Clear generic parameters after enum registration
        _genericParams.Clear();
    }

    private void RegisterStruct(NovusParser.StructDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Parse struct fields
        var fields = new List<IrStructField>();
        foreach (var fieldCtx in context.structField())
        {
            var fieldName = fieldCtx.IDENTIFIER().GetText();
            var fieldType = ParseType(fieldCtx.type());
            fields.Add(new IrStructField(fieldName, fieldType));
        }

        var structType = new IrStructType(name, fields);

        // Force offset calculation by accessing SizeInBytes
        _ = structType.SizeInBytes;

        _structs[name] = structType;
    }

    public override object? VisitFunctionDeclaration([NotNull] NovusParser.FunctionDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var returnType = context.type() != null ? ParseType(context.type()) : IrVoidType.Instance;

        // Check if function is extern by looking for 'extern' keyword in children
        var isExtern = false;
        var isPublic = false;
        for (int i = 0; i < Math.Min(3, context.ChildCount); i++)
        {
            var childText = context.GetChild(i)?.GetText();
            if (childText == "extern") isExtern = true;
            if (childText == "pub") isPublic = true;
        }

        var function = new IrFunction(name, returnType, isPublic, isExtern);
        _module.AddFunction(function);
        _currentFunction = function;

        // Parse parameters
        if (context.parameterList() != null)
        {
            foreach (var paramCtx in context.parameterList().parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                var paramType = ParseType(paramCtx.type());
                function.Parameters.Add(new IrParameter(paramName, paramType));
            }
        }

        // Skip body processing for extern functions
        if (isExtern)
        {
            return null;
        }

        // Create entry block
        _currentBlock = function.CreateBasicBlock("entry");

        // Visit function body
        Visit(context.block());

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
        var value = (IrValue?)Visit(context.expression());
        _currentBlock!.AddInstruction(new IrReturn(value));
        return null;
    }

    public override object? VisitVariableDeclaration([NotNull] NovusParser.VariableDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var isMutable = context.GetChild(0)?.GetText() == "var";
        var value = (IrValue?)Visit(context.expression());

        if (value == null)
        {
            throw new Exception($"Variable {name} must have an initial value");
        }

        // Infer type from value if not specified, otherwise parse the type annotation
        IrType type;
        if (context.type() != null)
        {
            type = ParseType(context.type());
        }
        else
        {
            type = value.Type;
        }

        // Create local variable
        var localVar = new IrLocalVariable(name, type, isMutable);
        _currentFunction!.LocalVariables.Add(localVar);
        _localVariables[name] = localVar;

        // Generate IR for the declaration with initial value
        _currentBlock!.AddInstruction(new IrLocalDecl(name, type, isMutable, value));

        return null;
    }

    public override object? VisitAssignmentStatement([NotNull] NovusParser.AssignmentStatementContext context)
    {
        var identifiers = context.IDENTIFIER();
        var name = identifiers[0].GetText();

        // Check if this is a member assignment (has multiple identifiers for member chain)
        if (identifiers.Length > 1)
        {
            // Member assignment: obj.field = value
            // For now, we'll throw an exception as we need to implement struct member stores
            throw new NotImplementedException("Struct member assignment not yet implemented in IR builder");
        }

        // Check if this is an array element assignment (has index expression)
        if (context.expression().Length == 2)
        {
            // Array element assignment: arr[index] = value
            var indexExpr = (IrValue?)Visit(context.expression(0));
            var valueExpr = (IrValue?)Visit(context.expression(1));

            if (indexExpr == null || valueExpr == null)
            {
                throw new Exception($"Array assignment requires index and value");
            }

            // Get the array variable
            IrVariable? arrayVar = null;
            if (_localVariables.ContainsKey(name))
            {
                var localVar = _localVariables[name];
                arrayVar = new IrVariable(name, localVar.Type);
            }
            else if (_currentFunction != null)
            {
                var param = _currentFunction.Parameters.FirstOrDefault(p => p.Name == name);
                if (param != null)
                {
                    arrayVar = new IrVariable(name, param.Type);
                }
            }

            if (arrayVar == null)
            {
                throw new Exception($"Array variable {name} not found");
            }

            _currentBlock!.AddInstruction(new IrIndexStore(arrayVar, indexExpr, valueExpr));
        }
        else
        {
            // Simple variable assignment: x = value
            var value = (IrValue?)Visit(context.expression(0));

            if (value == null)
            {
                throw new Exception($"Assignment to {name} requires a value");
            }

            // The semantic analyzer will check if the variable is mutable
            // Here we just generate the IR
            _currentBlock!.AddInstruction(new IrStore(name, value));
        }

        return null;
    }

    public override object? VisitExpressionStatement([NotNull] NovusParser.ExpressionStatementContext context)
    {
        // Visit the expression and return its value (for implicit returns)
        return Visit(context.expression());
    }

    public override object? VisitIfStatement([NotNull] NovusParser.IfStatementContext context)
    {
        var condition = (IrValue?)Visit(context.expression());

        var thenLabel = $"if_then_{_labelCounter}";
        var elseLabel = $"if_else_{_labelCounter}";
        var endLabel = $"if_end_{_labelCounter}";
        _labelCounter++;

        // Branch based on condition
        var hasElse = context.GetChild(4) != null; // Check if 'else' keyword exists
        var falseTarget = hasElse ? elseLabel : endLabel;
        _currentBlock!.AddInstruction(new IrConditionalBranch(condition!, thenLabel, falseTarget));

        // Then block
        _currentBlock!.AddInstruction(new IrLabel(thenLabel));
        Visit(context.block(0));

        // Jump to end if there's an else clause (but only if block doesn't already end with return/branch)
        if (hasElse)
        {
            if (!CurrentBlockHasTerminator())
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
        }

        // End label
        _currentBlock!.AddInstruction(new IrLabel(endLabel));
        return null;
    }

    public override object? VisitWhileStatement([NotNull] NovusParser.WhileStatementContext context)
    {
        var condLabel = $"while_cond_{_labelCounter}";
        var bodyLabel = $"while_body_{_labelCounter}";
        var endLabel = $"while_end_{_labelCounter}";
        _labelCounter++;

        // Push exit label for break statements
        _loopExitLabels.Push(endLabel);

        // Jump to condition check
        _currentBlock!.AddInstruction(new IrBranch(condLabel));

        // Condition label
        _currentBlock!.AddInstruction(new IrLabel(condLabel));
        var condition = (IrValue?)Visit(context.expression());
        _currentBlock!.AddInstruction(new IrConditionalBranch(condition!, bodyLabel, endLabel));

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

        // Pop exit label
        _loopExitLabels.Pop();
        return null;
    }

    public override object? VisitForStatement([NotNull] NovusParser.ForStatementContext context)
    {
        var condLabel = $"for_cond_{_labelCounter}";
        var bodyLabel = $"for_body_{_labelCounter}";
        var incrLabel = $"for_incr_{_labelCounter}";
        var endLabel = $"for_end_{_labelCounter}";
        _labelCounter++;

        // Push exit label for break statements
        _loopExitLabels.Push(endLabel);

        // Initialization (optional)
        if (context.GetChild(2) is NovusParser.VariableDeclarationContext varDecl)
        {
            Visit(varDecl);
        }
        else if (context.GetChild(2) is NovusParser.AssignmentStatementContext assignment)
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

        // Increment label (only if block doesn't end with return/break)
        if (!CurrentBlockHasTerminator())
        {
            _currentBlock!.AddInstruction(new IrLabel(incrLabel));

            // Increment statement (optional)
            if (context.GetChild(6) is NovusParser.AssignmentStatementContext incrAssignment)
            {
                Visit(incrAssignment);
            }

            // Jump back to condition
            _currentBlock!.AddInstruction(new IrBranch(condLabel));
        }

        // End label
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        // Pop exit label
        _loopExitLabels.Pop();

        return null;
    }

    public override object? VisitForeverStatement([NotNull] NovusParser.ForeverStatementContext context)
    {
        var bodyLabel = $"forever_body_{_labelCounter}";
        var endLabel = $"forever_end_{_labelCounter}";
        _labelCounter++;

        // Push exit label for break statements
        _loopExitLabels.Push(endLabel);

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

        // Pop exit label
        _loopExitLabels.Pop();
        return null;
    }

    public override object? VisitBreakStatement([NotNull] NovusParser.BreakStatementContext context)
    {
        if (_loopExitLabels.Count == 0)
        {
            throw new Exception("break statement outside of loop");
        }

        var exitLabel = _loopExitLabels.Peek();
        _currentBlock!.AddInstruction(new IrBranch(exitLabel));
        return null;
    }

    public override object? VisitPrimaryExpr([NotNull] NovusParser.PrimaryExprContext context)
    {
        return Visit(context.primaryExpression());
    }

    public override object? VisitCallExpr([NotNull] NovusParser.CallExprContext context)
    {
        var funcExpr = (IrValue?)Visit(context.expression());

        // Parse arguments
        var arguments = new List<IrValue>();
        if (context.argumentList() != null)
        {
            foreach (var argCtx in context.argumentList().expression())
            {
                var argValue = (IrValue?)Visit(argCtx);
                if (argValue != null)
                {
                    arguments.Add(argValue);
                }
            }
        }

        // Check if this is an enum constructor call
        if (funcExpr is IrEnumConstructor enumCtor)
        {
            // Create an enum value with the provided arguments
            var enumType = enumCtor.Type as IrEnumType;
            if (enumType == null)
            {
                throw new Exception("Enum constructor must have enum type");
            }

            var variant = enumType.GetVariant(enumCtor.VariantName);
            if (variant == null)
            {
                throw new Exception($"Variant '{enumCtor.VariantName}' not found in enum '{enumType.EnumName}'");
            }

            // Validate argument count
            if (arguments.Count != variant.AssociatedData.Count)
            {
                throw new Exception($"Variant '{enumCtor.VariantName}' expects {variant.AssociatedData.Count} arguments, got {arguments.Count}");
            }

            // Create the enum value
            return new IrEnumValue(enumType, enumCtor.VariantName, enumCtor.VariantTag, arguments);
        }

        string? resultName;
        IrType returnType;

        // Check if this is an indirect call through a function pointer
        if (funcExpr!.Type is IrFunctionPointerType fpType)
        {
            // Indirect call through function pointer
            if (arguments.Count != fpType.ParameterTypes.Count)
            {
                throw new Exception($"Function pointer expects {fpType.ParameterTypes.Count} arguments, got {arguments.Count}");
            }

            returnType = fpType.ReturnType;
            resultName = returnType is not IrVoidType ? $"%t{_tempCounter++}" : null;

            var indirectCall = new IrIndirectCall(funcExpr, returnType, resultName);
            foreach (var arg in arguments)
            {
                indirectCall.Arguments.Add(arg);
            }

            _currentBlock!.AddInstruction(indirectCall);

            if (resultName != null)
            {
                return new IrVariable(resultName, returnType);
            }

            return null;
        }

        // Direct call - funcExpr should be an identifier
        if (funcExpr is not IrVariable funcVar)
        {
            throw new Exception("Function call target must be an identifier or function pointer");
        }

        var functionName = funcVar.Name;

        // Look up the function in the module to get its return type
        var function = _module.Functions.FirstOrDefault(f => f.Name == functionName);
        if (function == null)
        {
            throw new Exception($"Unknown function: {functionName}");
        }

        // Check argument count matches parameter count
        if (arguments.Count != function.Parameters.Count)
        {
            throw new Exception($"Function {functionName} expects {function.Parameters.Count} arguments, got {arguments.Count}");
        }

        // Insert implicit casts for arguments where needed
        // (e.g., u32 -> i32 for same-bit-width conversions)
        for (int i = 0; i < arguments.Count; i++)
        {
            var argType = arguments[i].Type;
            var paramType = function.Parameters[i].Type;

            // If types don't exactly match but are compatible integer types of same width
            if (!argType.Equals(paramType) &&
                argType is IrIntType argInt &&
                paramType is IrIntType paramInt &&
                argInt.BitWidth == paramInt.BitWidth)
            {
                // Same-size integer cast - just reinterpret the bits
                if (arguments[i] is IrConstant constant)
                {
                    // For constants, create new constant with target type
                    arguments[i] = new IrConstant(constant.Value, paramType);
                }
                else if (arguments[i] is IrVariable variable)
                {
                    // For variables, create new variable reference with target type (zero-cost cast)
                    arguments[i] = new IrVariable(variable.Name, paramType);
                }
                else
                {
                    // For other expressions, create a temp variable with the target type
                    // The value is already computed, we just need to reference it with the new type
                    var castTempName = $"%t{_tempCounter++}";
                    var moveOp = new IrBinaryOp(castTempName, IrBinaryOp.OpKind.Add,
                        arguments[i], new IrConstant(0, arguments[i].Type), arguments[i].Type);
                    _currentBlock!.AddInstruction(moveOp);
                    arguments[i] = new IrVariable(castTempName, paramType);
                }
            }
        }

        // Create the call instruction
        returnType = function.ReturnType;
        resultName = returnType is not IrVoidType ? $"%t{_tempCounter++}" : null;

        var call = new IrCall(functionName, returnType, resultName);
        foreach (var arg in arguments)
        {
            call.Arguments.Add(arg);
        }

        _currentBlock!.AddInstruction(call);

        // Return the result variable if non-void
        if (resultName != null)
        {
            return new IrVariable(resultName, returnType);
        }

        return null;
    }

    public override object? VisitBorrowExpr([NotNull] NovusParser.BorrowExprContext context)
    {
        var exprContext = context.expression();

        // Check if this is a mutable borrow (&mut) or immutable borrow (&)
        bool isMutable = context.GetChild(1)?.GetText() == "mut";

        // Handle function pointers specially (backward compatibility)
        if (exprContext.Start.Type == NovusLexer.IDENTIFIER &&
            exprContext.ChildCount == 1)
        {
            var name = exprContext.GetText();

            // Check if it's a function (for function pointers)
            var function = _module.Functions.FirstOrDefault(f => f.Name == name);
            if (function != null)
            {
                // Create function pointer type from function signature
                var paramTypes = function.Parameters.Select(p => p.Type).ToList();
                var fpType = new IrFunctionPointerType(paramTypes, function.ReturnType);
                return new IrFunctionAddress(name, fpType);
            }
        }

        // For variables, struct members, array elements, etc., create a reference
        // Visit the expression to get its value
        var value = (IrValue)Visit(exprContext)!;

        // Create the appropriate reference type
        var refType = isMutable
            ? (IrType)new IrMutReferenceType(value.Type)
            : new IrReferenceType(value.Type);

        // For code generation, references are just pointers (addresses)
        // We return the value itself - the semantic analyzer will track that it's a reference
        // At codegen time, we'll take the address of the value

        // Create a "borrow" value that wraps the original value with reference type
        return new IrBorrowValue(value, refType, isMutable);
    }

    public override object? VisitIndexExpr([NotNull] NovusParser.IndexExprContext context)
    {
        var arrayExpr = (IrValue)Visit(context.expression(0))!;
        var indexExpr = (IrValue)Visit(context.expression(1))!;

        // Get the array type to determine element type
        if (arrayExpr.Type is not IrArrayType arrayType)
        {
            throw new Exception($"Cannot index into non-array type: {arrayExpr.Type.Name}");
        }

        // Create an index access instruction
        var tempName = $"%t{_tempCounter++}";
        var indexAccess = new IrIndexAccess(tempName, arrayExpr, indexExpr, arrayType.ElementType);
        _currentBlock!.AddInstruction(indexAccess);

        return new IrVariable(tempName, arrayType.ElementType);
    }

    public override object? VisitArrayLiteral([NotNull] NovusParser.ArrayLiteralContext context)
    {
        var expressions = context.expression();
        var elements = new List<IrValue>();

        // Visit all element expressions
        foreach (var exprCtx in expressions)
        {
            var value = (IrValue)Visit(exprCtx)!;
            elements.Add(value);
        }

        if (elements.Count == 0)
        {
            throw new Exception("Array literals cannot be empty");
        }

        // Infer array type from first element
        var elementType = elements[0].Type;
        var arrayType = new IrArrayType(elementType, elements.Count);

        // Create array literal value
        var arrayLiteral = new IrArrayLiteral(arrayType);
        foreach (var elem in elements)
        {
            // TODO: Check that all elements have compatible types
            arrayLiteral.Elements.Add(elem);
        }

        return arrayLiteral;
    }

    public override object? VisitAdditiveExpr([NotNull] NovusParser.AdditiveExprContext context)
    {
        var left = (IrValue)Visit(context.expression(0))!;
        var right = (IrValue)Visit(context.expression(1))!;
        var op = context.GetChild(1).GetText() == "+" ? IrBinaryOp.OpKind.Add : IrBinaryOp.OpKind.Sub;

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, op, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitShiftExpr([NotNull] NovusParser.ShiftExprContext context)
    {
        var left = (IrValue)Visit(context.expression(0))!;
        var right = (IrValue)Visit(context.expression(1))!;
        var op = context.GetChild(1).GetText() == "<<" ? IrBinaryOp.OpKind.Shl : IrBinaryOp.OpKind.Shr;

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, op, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitBitwiseAndExpr([NotNull] NovusParser.BitwiseAndExprContext context)
    {
        var left = (IrValue)Visit(context.expression(0))!;
        var right = (IrValue)Visit(context.expression(1))!;

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.And, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitBitwiseXorExpr([NotNull] NovusParser.BitwiseXorExprContext context)
    {
        var left = (IrValue)Visit(context.expression(0))!;
        var right = (IrValue)Visit(context.expression(1))!;

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Xor, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitBitwiseOrExpr([NotNull] NovusParser.BitwiseOrExprContext context)
    {
        var left = (IrValue)Visit(context.expression(0))!;
        var right = (IrValue)Visit(context.expression(1))!;

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Or, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitCastExpr([NotNull] NovusParser.CastExprContext context)
    {
        var targetType = ParseType(context.type());
        var value = (IrValue)Visit(context.expression())!;

        // If it's already a constant, just change its type
        if (value is IrConstant constant)
        {
            return new IrConstant(constant.Value, targetType);
        }

        // For variables, if it's a bitwise cast (same size), just return the variable with new type
        // This allows the value to be read from its current location
        if (value is IrVariable variable && value.Type.SizeInBytes == targetType.SizeInBytes)
        {
            return new IrVariable(variable.Name, targetType);
        }

        // For other cases, we'd need a proper cast instruction
        // For now, return the variable with the new type (works for bitwise casts)
        if (value is IrVariable var2)
        {
            return new IrVariable(var2.Name, targetType);
        }

        // Fallback: create temp (this shouldn't happen often)
        var tempName = $"%t{_tempCounter++}";
        return new IrVariable(tempName, targetType);
    }

    public override object? VisitMultiplicativeExpr([NotNull] NovusParser.MultiplicativeExprContext context)
    {
        var left = (IrValue)Visit(context.expression(0))!;
        var right = (IrValue)Visit(context.expression(1))!;

        var opText = context.GetChild(1).GetText();
        var op = opText switch
        {
            "*" => IrBinaryOp.OpKind.Mul,
            "/" => IrBinaryOp.OpKind.Div,
            "%" => IrBinaryOp.OpKind.Mod,
            _ => throw new Exception($"Unknown operator: {opText}")
        };

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, op, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitComparisonExpr([NotNull] NovusParser.ComparisonExprContext context)
    {
        var left = (IrValue)Visit(context.expression(0))!;
        var right = (IrValue)Visit(context.expression(1))!;

        var opText = context.GetChild(1).GetText();
        var op = opText switch
        {
            "==" => IrBinaryOp.OpKind.Eq,
            "!=" => IrBinaryOp.OpKind.Ne,
            "<" => IrBinaryOp.OpKind.Lt,
            "<=" => IrBinaryOp.OpKind.Le,
            ">" => IrBinaryOp.OpKind.Gt,
            ">=" => IrBinaryOp.OpKind.Ge,
            _ => throw new Exception($"Unknown comparison operator: {opText}")
        };

        var tempName = $"%t{_tempCounter++}";
        // Comparison result is a boolean
        var binOp = new IrBinaryOp(tempName, op, left, right, IrBoolType.Instance);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, IrBoolType.Instance);
    }

    public override object? VisitUnaryExpr([NotNull] NovusParser.UnaryExprContext context)
    {
        var op = context.GetChild(0).GetText();

        // Handle dereference specially - we need to determine the type first
        if (op == "*")
        {
            var operand = (IrValue)Visit(context.expression())!;

            // Determine the pointee type
            IrType pointeeType;
            if (operand.Type is IrPointerType ptrType)
            {
                pointeeType = ptrType.PointeeType;
            }
            else if (operand.Type is IrReferenceType refType)
            {
                pointeeType = refType.PointeeType;
            }
            else if (operand.Type is IrMutReferenceType mutRefType)
            {
                pointeeType = mutRefType.PointeeType;
            }
            else
            {
                throw new Exception($"Cannot dereference non-pointer/reference type: {operand.Type.Name}");
            }

            // Create a dereference value
            return new IrDereferenceValue(operand, pointeeType);
        }

        // For other unary ops, visit the operand first
        var operandValue = (IrValue)Visit(context.expression())!;

        if (op == "!")
        {
            // Logical NOT: false becomes true, true becomes false
            // Implemented as: result = (operand == false)
            var tempName = $"%t{_tempCounter++}";
            var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Eq, operandValue, new IrBoolConstant(false), IrBoolType.Instance);
            _currentBlock!.AddInstruction(binOp);
            return new IrVariable(tempName, IrBoolType.Instance);
        }
        else if (op == "~")
        {
            // Bitwise NOT: XOR with -1 (all bits set)
            var tempName = $"%t{_tempCounter++}";
            var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Xor, operandValue, new IrConstant(-1, operandValue.Type), operandValue.Type);
            _currentBlock!.AddInstruction(binOp);
            return new IrVariable(tempName, operandValue.Type);
        }
        else if (op == "-")
        {
            // Unary minus: subtract from 0
            var tempName = $"%t{_tempCounter++}";
            var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Sub, new IrConstant(0, operandValue.Type), operandValue, operandValue.Type);
            _currentBlock!.AddInstruction(binOp);
            return new IrVariable(tempName, operandValue.Type);
        }

        throw new Exception($"Unknown unary operator: {op}");
    }

    public override object? VisitLogicalAndExpr([NotNull] NovusParser.LogicalAndExprContext context)
    {
        // Short-circuit evaluation: if left is false, don't evaluate right
        var left = (IrValue)Visit(context.expression(0))!;

        var evalRightLabel = $"and_right_{_labelCounter}";
        var setTrueLabel = $"and_true_{_labelCounter}";
        var setFalseLabel = $"and_false_{_labelCounter}";
        var endLabel = $"and_end_{_labelCounter}";
        _labelCounter++;

        var resultTemp = $"%t{_tempCounter++}";

        // Add to function's local variables for stack allocation
        var localVar = new IrLocalVariable(resultTemp, IrIntType.I32, false);
        _currentFunction!.LocalVariables.Add(localVar);

        // If left is false, short-circuit to false result
        _currentBlock!.AddInstruction(new IrConditionalBranch(left, evalRightLabel, setFalseLabel));

        // Evaluate right
        _currentBlock!.AddInstruction(new IrLabel(evalRightLabel));
        var right = (IrValue)Visit(context.expression(1))!;

        // If right is true, set result to 1, otherwise 0
        _currentBlock!.AddInstruction(new IrConditionalBranch(right, setTrueLabel, setFalseLabel));

        // Both are true, set result to 1
        _currentBlock!.AddInstruction(new IrLabel(setTrueLabel));
        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, IrIntType.I32, false, new IrConstant(1, IrIntType.I32)));
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // Set result to 0 (false)
        _currentBlock!.AddInstruction(new IrLabel(setFalseLabel));
        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));

        // End
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        return new IrVariable(resultTemp, IrIntType.I32);
    }

    public override object? VisitLogicalOrExpr([NotNull] NovusParser.LogicalOrExprContext context)
    {
        // Short-circuit evaluation: if left is true, don't evaluate right
        var left = (IrValue)Visit(context.expression(0))!;

        var evalRightLabel = $"or_right_{_labelCounter}";
        var setTrueLabel = $"or_true_{_labelCounter}";
        var setFalseLabel = $"or_false_{_labelCounter}";
        var endLabel = $"or_end_{_labelCounter}";
        _labelCounter++;

        var resultTemp = $"%t{_tempCounter++}";

        // Add to function's local variables for stack allocation
        var localVar = new IrLocalVariable(resultTemp, IrIntType.I32, false);
        _currentFunction!.LocalVariables.Add(localVar);

        // If left is true, short-circuit to true result
        _currentBlock!.AddInstruction(new IrConditionalBranch(left, setTrueLabel, evalRightLabel));

        // Evaluate right
        _currentBlock!.AddInstruction(new IrLabel(evalRightLabel));
        var right = (IrValue)Visit(context.expression(1))!;

        // If right is true, set result to 1, otherwise 0
        _currentBlock!.AddInstruction(new IrConditionalBranch(right, setTrueLabel, setFalseLabel));

        // Set result to true
        _currentBlock!.AddInstruction(new IrLabel(setTrueLabel));
        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, IrIntType.I32, false, new IrConstant(1, IrIntType.I32)));
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // Set result to 0 (false)
        _currentBlock!.AddInstruction(new IrLabel(setFalseLabel));
        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));

        // End
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        return new IrVariable(resultTemp, IrIntType.I32);
    }

    public override object? VisitFloatLiteral([NotNull] NovusParser.FloatLiteralContext context)
    {
        var isNegative = context.GetText().StartsWith("-");
        var text = context.FLOAT_LITERAL().GetText();
        var (value, type) = ParseFloatLiteral(text);

        if (isNegative)
        {
            value = -value;
        }

        // Return appropriate constant type based on whether it's float or fixed
        if (type is IrFloatType floatType)
        {
            return new IrFloatConstant(value, floatType);
        }
        else if (type is IrFixedType fixedType)
        {
            return new IrFixedConstant(value, fixedType);
        }
        else
        {
            throw new Exception($"Unexpected float literal type: {type.Name}");
        }
    }

    public override object? VisitIntegerLiteral([NotNull] NovusParser.IntegerLiteralContext context)
    {
        var isNegative = context.GetText().StartsWith("-");
        var text = context.INTEGER_LITERAL().GetText();
        var (value, type) = ParseIntegerLiteral(text);

        if (isNegative)
        {
            value = -value;
        }

        return new IrConstant(value, type);
    }

    public override object? VisitBinaryLiteral([NotNull] NovusParser.BinaryLiteralContext context)
    {
        var isNegative = context.GetText().StartsWith("-");
        var text = context.BINARY_LITERAL().GetText();
        var (value, type) = ParseBinaryLiteral(text);

        if (isNegative)
        {
            value = -value;
        }

        return new IrConstant(value, type);
    }

    public override object? VisitHexLiteral([NotNull] NovusParser.HexLiteralContext context)
    {
        var isNegative = context.GetText().StartsWith("-");
        var text = context.HEX_LITERAL().GetText();
        var (value, type) = ParseHexLiteral(text);

        if (isNegative)
        {
            value = -value;
        }

        return new IrConstant(value, type);
    }

    public override object? VisitBoolLiteral([NotNull] NovusParser.BoolLiteralContext context)
    {
        var text = context.GetText();
        var value = text == "true";
        return new IrBoolConstant(value);
    }

    public override object? VisitStringLiteral([NotNull] NovusParser.StringLiteralContext context)
    {
        var text = context.STRING_LITERAL().GetText();
        // Remove quotes
        var stringValue = text[1..^1];

        // Process escape sequences
        stringValue = ProcessEscapeSequences(stringValue);

        // Create unique label for this string
        var label = $"_str{_stringCounter++}";
        var stringLiteral = new IrStringLiteral(stringValue, label);
        StringLiterals.Add(stringLiteral);

        return stringLiteral;
    }

    private string ProcessEscapeSequences(string input)
    {
        return input
            .Replace("\\n", "\n")
            .Replace("\\t", "\t")
            .Replace("\\r", "\r")
            .Replace("\\b", "\b")
            .Replace("\\f", "\f")
            .Replace("\\\"", "\"")
            .Replace("\\'", "'")
            .Replace("\\\\", "\\");
        // TODO: Handle \xNN hex escapes
    }

    public override object? VisitIdentifierExpr([NotNull] NovusParser.IdentifierExprContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Check if it's a constant - inline the value
        if (_constants.ContainsKey(name))
        {
            var (type, value) = _constants[name];
            return new IrConstant((int)value, type);
        }

        // Check if it's a local variable
        if (_localVariables.ContainsKey(name))
        {
            var localVar = _localVariables[name];
            return new IrVariable(name, localVar.Type);
        }

        // Check if it's a parameter
        if (_currentFunction != null)
        {
            var param = _currentFunction.Parameters.FirstOrDefault(p => p.Name == name);
            if (param != null)
            {
                return new IrVariable(name, param.Type);
            }
        }

        // Otherwise, assume it's a temporary variable or function name
        return new IrVariable(name, IrIntType.I32); // Default type for temps
    }

    public override object? VisitParenExpr([NotNull] NovusParser.ParenExprContext context)
    {
        return Visit(context.expression());
    }

    public override object? VisitStructLiteral([NotNull] NovusParser.StructLiteralContext context)
    {
        var structName = context.IDENTIFIER().GetText();

        if (!_structs.ContainsKey(structName))
        {
            throw new Exception($"Unknown struct type '{structName}'");
        }

        var structType = _structs[structName];
        var fieldValues = new Dictionary<string, IrValue>();

        // Process field initializers
        foreach (var fieldInit in context.structFieldInit())
        {
            var fieldName = fieldInit.IDENTIFIER().GetText();
            var fieldValue = (IrValue?)Visit(fieldInit.expression());

            if (fieldValue == null)
            {
                throw new Exception($"Field '{fieldName}' in struct '{structName}' requires a value");
            }

            fieldValues[fieldName] = fieldValue;
        }

        // Validate that all fields are initialized
        foreach (var field in structType.Fields)
        {
            if (!fieldValues.ContainsKey(field.Name))
            {
                throw new Exception($"Field '{field.Name}' in struct '{structName}' is not initialized");
            }
        }

        return new IrStructLiteral(structType, fieldValues);
    }

    public override object? VisitMemberAccessExpr([NotNull] NovusParser.MemberAccessExprContext context)
    {
        var baseExpr = (IrValue?)Visit(context.expression());
        if (baseExpr == null)
        {
            throw new Exception("Member access requires a base expression");
        }

        var memberName = context.IDENTIFIER().GetText();

        // Check if the base expression is a struct type
        if (baseExpr.Type is not IrStructType structType)
        {
            throw new Exception($"Cannot access member '{memberName}' on non-struct type '{baseExpr.Type.Name}'");
        }

        // Find the field
        var field = structType.GetField(memberName);
        if (field == null)
        {
            throw new Exception($"Struct '{structType.Name}' does not have a field named '{memberName}'");
        }

        // Generate a member access instruction
        var resultName = $"%t{_tempCounter++}";
        var memberAccess = new IrMemberAccess(resultName, baseExpr, memberName, field.Type, field.Offset);
        _currentBlock!.AddInstruction(memberAccess);

        return new IrVariable(resultName, field.Type);
    }

    public override object? VisitPathExpr([NotNull] NovusParser.PathExprContext context)
    {
        // Handle enum variant path expressions like Option::Some, Result::Ok
        // This returns an IrEnumValue which can then be called with arguments
        var baseExpr = context.expression();
        var variantName = context.IDENTIFIER().GetText();

        // The base expression should be an identifier for the enum type
        string? enumName = null;
        if (baseExpr is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            enumName = identCtx.IDENTIFIER().GetText();
        }

        if (enumName == null || !_enums.ContainsKey(enumName))
        {
            throw new Exception($"Path expression must reference an enum type, got '{enumName}'");
        }

        var enumType = _enums[enumName];
        var variant = enumType.GetVariant(variantName);

        if (variant == null)
        {
            throw new Exception($"Enum '{enumName}' has no variant '{variantName}'");
        }

        // For now, we'll return a special marker value
        // The actual construction will happen when this is called as a function
        // This is a placeholder that indicates "this is an enum constructor"
        return new IrEnumConstructor(enumType, variantName, variant.Tag);
    }

    public override object? VisitMatchStatement([NotNull] NovusParser.MatchStatementContext context)
    {
        var matchValue = (IrValue?)Visit(context.expression());
        if (matchValue == null)
        {
            throw new Exception("Match expression requires a value");
        }

        if (matchValue.Type is not IrEnumType enumType)
        {
            throw new Exception($"Match can only be used with enum types, got '{matchValue.Type.Name}'");
        }

        // Generate labels for match arms and end
        var matchEndLabel = $"match_end_{_labelCounter}";
        var armLabels = new List<string>();

        for (int i = 0; i < context.matchArm().Length; i++)
        {
            armLabels.Add($"match_arm_{_labelCounter}_{i}");
        }
        _labelCounter++;

        // Extract tag from enum value
        var tagName = $"%t{_tempCounter++}";
        _currentBlock!.AddInstruction(new IrExtractTag(tagName, matchValue));
        var tagVar = new IrVariable(tagName, IrIntType.I32);

        // Generate comparisons and branches for each arm
        for (int i = 0; i < context.matchArm().Length; i++)
        {
            var armCtx = context.matchArm()[i];
            var pattern = armCtx.pattern();

            // Check if this is a wildcard pattern
            if (pattern is NovusParser.WildcardPatternContext)
            {
                // Wildcard always matches, jump directly
                _currentBlock!.AddInstruction(new IrBranch(armLabels[i]));
                break;
            }

            // Handle variant patterns
            string? variantName = null;
            if (pattern is NovusParser.VariantPatternContext variantPattern)
            {
                variantName = variantPattern.IDENTIFIER().GetText();
            }
            else if (pattern is NovusParser.IdentifierPatternContext identPattern)
            {
                variantName = identPattern.IDENTIFIER().GetText();
            }

            if (variantName != null)
            {
                var variant = enumType.GetVariant(variantName);
                if (variant == null)
                {
                    throw new Exception($"Enum '{enumType.EnumName}' has no variant '{variantName}'");
                }

                // Compare tag with variant tag
                var cmpName = $"%t{_tempCounter++}";
                var tagConst = new IrConstant(variant.Tag, IrIntType.I32);
                _currentBlock!.AddInstruction(new IrBinaryOp(cmpName, IrBinaryOp.OpKind.Eq, tagVar, tagConst, IrBoolType.Instance));
                var cmpVar = new IrVariable(cmpName, IrBoolType.Instance);

                // Branch: if match, go to arm, otherwise continue to next pattern
                var nextLabel = i < armLabels.Count - 1 ? armLabels[i + 1] : matchEndLabel;
                _currentBlock!.AddInstruction(new IrConditionalBranch(cmpVar, armLabels[i], nextLabel));
            }
        }

        // Generate code for each arm
        for (int i = 0; i < context.matchArm().Length; i++)
        {
            var armCtx = context.matchArm()[i];
            var pattern = armCtx.pattern();

            _currentBlock!.AddInstruction(new IrLabel(armLabels[i]));

            // Extract associated data for variant patterns
            if (pattern is NovusParser.VariantPatternContext variantPattern)
            {
                var variantName = variantPattern.IDENTIFIER().GetText();
                var variant = enumType.GetVariant(variantName);

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

                            // Extract the data
                            var extractName = $"%t{_tempCounter++}";
                            _currentBlock!.AddInstruction(new IrExtractVariantData(extractName, matchValue, dataIdx, dataType));

                            // Store in a local variable
                            var localVar = new IrLocalVariable(bindingName, dataType, false);
                            _currentFunction!.LocalVariables.Add(localVar);
                            _localVariables[bindingName] = localVar;

                            var extractedValue = new IrVariable(extractName, dataType);
                            _currentBlock!.AddInstruction(new IrLocalDecl(bindingName, dataType, false, extractedValue));
                        }
                    }
                }
            }

            // Visit the arm body
            if (armCtx.expression() != null)
            {
                Visit(armCtx.expression());
            }
            else if (armCtx.block() != null)
            {
                Visit(armCtx.block());
            }

            // Jump to end (if not already terminated)
            if (!CurrentBlockHasTerminator())
            {
                _currentBlock!.AddInstruction(new IrBranch(matchEndLabel));
            }
        }

        // End label
        _currentBlock!.AddInstruction(new IrLabel(matchEndLabel));

        return null;
    }

    private IrType ParseType(NovusParser.TypeContext context)
    {
        return context switch
        {
            NovusParser.ReferenceTypeContext refCtx => ParseReferenceType(refCtx),
            NovusParser.PointerTypeContext ptrCtx => ParsePointerType(ptrCtx),
            NovusParser.ArrayTypeContext arrayCtx => ParseArrayType(arrayCtx),
            NovusParser.FunctionPointerTypeContext fpCtx => ParseFunctionPointerType(fpCtx),
            NovusParser.PrimitiveTypeContext primCtx => ParsePrimitiveType(primCtx),
            NovusParser.NamedTypeContext namedCtx => ParseNamedType(namedCtx),
            _ => throw new Exception($"Unknown type context: {context.GetType().Name}")
        };
    }

    private IrType ParseReferenceType(NovusParser.ReferenceTypeContext context)
    {
        var pointeeType = ParseType(context.type());

        // Check if this is a mutable reference (&mut T) or immutable reference (&T)
        bool isMutable = context.GetChild(1)?.GetText() == "mut";

        return isMutable
            ? new IrMutReferenceType(pointeeType)
            : new IrReferenceType(pointeeType);
    }

    private IrType ParsePointerType(NovusParser.PointerTypeContext context)
    {
        var pointeeType = ParseType(context.type());
        return new IrPointerType(pointeeType);
    }

    private IrType ParseNamedType(NovusParser.NamedTypeContext context)
    {
        var typeName = context.IDENTIFIER().GetText();

        // Check if it's a generic type parameter (T, E, etc.)
        if (_genericParams.ContainsKey(typeName))
        {
            return _genericParams[typeName];
        }

        // Check if it's a struct type
        if (_structs.ContainsKey(typeName))
        {
            return _structs[typeName];
        }

        // Check if it's an enum type
        if (_enums.ContainsKey(typeName))
        {
            var enumType = _enums[typeName];

            // Handle generic instantiation (e.g., Option<i32>)
            if (context.typeList() != null)
            {
                var typeArgs = new List<IrType>();
                foreach (var typeCtx in context.typeList().type())
                {
                    typeArgs.Add(ParseType(typeCtx));
                }

                // Create monomorphized enum with concrete types
                var typeSubstitutions = new Dictionary<string, IrType>();
                for (int i = 0; i < enumType.GenericParameters.Count; i++)
                {
                    typeSubstitutions[enumType.GenericParameters[i]] = typeArgs[i];
                }

                // Create monomorphized variants
                var monomorphizedVariants = new List<IrEnumVariant>();
                foreach (var origVariant in enumType.Variants)
                {
                    var monomorphizedData = new List<IrType>();
                    foreach (var dataType in origVariant.AssociatedData)
                    {
                        if (dataType is IrGenericType gt && typeSubstitutions.ContainsKey(gt.ParameterName))
                        {
                            monomorphizedData.Add(typeSubstitutions[gt.ParameterName]);
                        }
                        else
                        {
                            monomorphizedData.Add(dataType);
                        }
                    }
                    monomorphizedVariants.Add(new IrEnumVariant(origVariant.Name, origVariant.Tag, monomorphizedData));
                }

                // Create new enum type with concrete types (no generic parameters)
                return new IrEnumType(enumType.EnumName, monomorphizedVariants, null);
            }

            return enumType;
        }

        throw new Exception($"Unknown type '{typeName}'");
    }

    private IrType ParseArrayType(NovusParser.ArrayTypeContext context)
    {
        var sizeText = context.INTEGER_LITERAL().GetText();
        var size = int.Parse(sizeText);
        var elementType = ParseType(context.type());
        return new IrArrayType(elementType, size);
    }

    private IrType ParseFunctionPointerType(NovusParser.FunctionPointerTypeContext context)
    {
        var paramTypes = new List<IrType>();

        if (context.typeList() != null)
        {
            foreach (var typeCtx in context.typeList().type())
            {
                paramTypes.Add(ParseType(typeCtx));
            }
        }

        var returnType = context.type() != null ? ParseType(context.type()) : IrVoidType.Instance;

        return new IrFunctionPointerType(paramTypes, returnType);
    }

    private IrType ParsePrimitiveType(NovusParser.PrimitiveTypeContext context)
    {
        var typeText = context.GetText();
        return typeText switch
        {
            "u8" => IrIntType.U8,
            "u16" => IrIntType.U16,
            "u32" => IrIntType.U32,
            "u64" => IrIntType.U64,
            "i8" => IrIntType.I8,
            "i16" => IrIntType.I16,
            "i32" => IrIntType.I32,
            "i64" => IrIntType.I64,
            "bool" => IrBoolType.Instance,
            "f32" => IrFloatType.F32,
            "f64" => IrFloatType.F64,
            "fixed16" => IrFixedType.Fixed16,
            "fixed32" => IrFixedType.Fixed32,
            _ => throw new Exception($"Unknown primitive type: {typeText}")
        };
    }

    private (long value, IrType type) ParseIntegerLiteral(string text)
    {
        // Strip underscores for readability (e.g., 1_000_000)
        text = text.Replace("_", "");

        // Check for type suffix
        if (text.EndsWith("u8"))
            return (long.Parse(text[..^2]), IrIntType.U8);
        if (text.EndsWith("u16"))
            return (long.Parse(text[..^3]), IrIntType.U16);
        if (text.EndsWith("u32"))
            return (long.Parse(text[..^3]), IrIntType.U32);
        if (text.EndsWith("u64"))
            return (long.Parse(text[..^3]), IrIntType.U64);
        if (text.EndsWith("i8"))
            return (long.Parse(text[..^2]), IrIntType.I8);
        if (text.EndsWith("i16"))
            return (long.Parse(text[..^3]), IrIntType.I16);
        if (text.EndsWith("i32"))
            return (long.Parse(text[..^3]), IrIntType.I32);
        if (text.EndsWith("i64"))
            return (long.Parse(text[..^3]), IrIntType.I64);

        // Default to i32
        return (long.Parse(text), IrIntType.I32);
    }

    private (double value, IrType type) ParseFloatLiteral(string text)
    {
        // Check for type suffix
        if (text.EndsWith("fixed32"))
        {
            var numText = text[..^7];
            return (double.Parse(numText), IrFixedType.Fixed32);
        }
        if (text.EndsWith("fixed16"))
        {
            var numText = text[..^7];
            return (double.Parse(numText), IrFixedType.Fixed16);
        }
        if (text.EndsWith("f64"))
        {
            var numText = text[..^3];
            return (double.Parse(numText), IrFloatType.F64);
        }
        if (text.EndsWith("f32"))
        {
            var numText = text[..^3];
            return (double.Parse(numText), IrFloatType.F32);
        }

        // Default to f32
        return (double.Parse(text), IrFloatType.F32);
    }

    private (long value, IrType type) ParseBinaryLiteral(string text)
    {
        // Remove '%' prefix and underscores
        text = text[1..].Replace("_", "");

        // Extract type suffix if present
        IrType type = IrIntType.I32;
        string binaryText = text;

        if (text.EndsWith("u8"))
        {
            type = IrIntType.U8;
            binaryText = text[..^2];
        }
        else if (text.EndsWith("u16"))
        {
            type = IrIntType.U16;
            binaryText = text[..^3];
        }
        else if (text.EndsWith("u32"))
        {
            type = IrIntType.U32;
            binaryText = text[..^3];
        }
        else if (text.EndsWith("u64"))
        {
            type = IrIntType.U64;
            binaryText = text[..^3];
        }
        else if (text.EndsWith("i8"))
        {
            type = IrIntType.I8;
            binaryText = text[..^2];
        }
        else if (text.EndsWith("i16"))
        {
            type = IrIntType.I16;
            binaryText = text[..^3];
        }
        else if (text.EndsWith("i32"))
        {
            type = IrIntType.I32;
            binaryText = text[..^3];
        }
        else if (text.EndsWith("i64"))
        {
            type = IrIntType.I64;
            binaryText = text[..^3];
        }

        // Parse binary string to long
        var value = Convert.ToInt64(binaryText, 2);
        return (value, type);
    }

    private (long value, IrType type) ParseHexLiteral(string text)
    {
        // Remove '$' prefix and underscores
        text = text[1..].Replace("_", "");

        // Extract type suffix if present
        IrType type = IrIntType.I32;
        string hexText = text;

        if (text.EndsWith("u8"))
        {
            type = IrIntType.U8;
            hexText = text[..^2];
        }
        else if (text.EndsWith("u16"))
        {
            type = IrIntType.U16;
            hexText = text[..^3];
        }
        else if (text.EndsWith("u32"))
        {
            type = IrIntType.U32;
            hexText = text[..^3];
        }
        else if (text.EndsWith("u64"))
        {
            type = IrIntType.U64;
            hexText = text[..^3];
        }
        else if (text.EndsWith("i8"))
        {
            type = IrIntType.I8;
            hexText = text[..^2];
        }
        else if (text.EndsWith("i16"))
        {
            type = IrIntType.I16;
            hexText = text[..^3];
        }
        else if (text.EndsWith("i32"))
        {
            type = IrIntType.I32;
            hexText = text[..^3];
        }
        else if (text.EndsWith("i64"))
        {
            type = IrIntType.I64;
            hexText = text[..^3];
        }

        // Parse hex string to long
        var value = Convert.ToInt64(hexText, 16);
        return (value, type);
    }
}
