using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Performs semantic analysis on the parsed AST
/// Reports errors and warnings with helpful messages
/// </summary>
public class SemanticAnalyzer : NovusBaseVisitor<IrType?>
{
    private readonly DiagnosticBag _diagnostics = new();
    private readonly string _filePath;
    private readonly string[] _sourceLines;

    // Symbol tables
    private readonly Dictionary<string, FunctionSymbol> _functions = new();
    private readonly Dictionary<string, VariableSymbol> _variables = new();
    private readonly Dictionary<string, IrStructType> _structs = new();
    private readonly Dictionary<string, IrEnumType> _enums = new();
    private readonly Dictionary<string, ConstantSymbol> _constants = new();
    private readonly Dictionary<string, string> _importedNames = new(); // Maps imported name -> module name
    private FunctionSymbol? _currentFunction;
    private int _loopDepth = 0; // Track loop nesting for break validation
    private readonly string _stdLibPath; // Path to standard library

    // Generic type parameters in scope (for generic enum/struct definitions)
    private readonly Dictionary<string, IrGenericType> _genericParams = new();

    // Expected type for bidirectional type checking (flows down from context)
    private IrType? _expectedType = null;

    public DiagnosticBag Diagnostics => _diagnostics;

    public SemanticAnalyzer(string filePath, string sourceCode, string stdLibPath)
    {
        _filePath = filePath;
        _sourceLines = sourceCode.Split('\n');
        _stdLibPath = stdLibPath;
    }

    public bool Analyze(NovusParser.CompilationUnitContext context)
    {
        // Pass 0a: Implicitly import all of core module
        ImportModule("core", importAll: true);

        // Pass 0b: Process explicit imports
        foreach (var importDecl in context.importDeclaration())
        {
            ProcessImport(importDecl);
        }

        // First pass: collect all constant declarations
        foreach (var constDecl in context.constDeclaration())
        {
            RegisterConstant(constDecl);
        }

        // Second pass: collect all enum declarations
        foreach (var enumDecl in context.enumDeclaration())
        {
            RegisterEnum(enumDecl);
        }

        // Third pass: collect all struct declarations
        foreach (var structDecl in context.structDeclaration())
        {
            RegisterStruct(structDecl);
        }

        // Fourth pass: collect all function declarations
        foreach (var funcDecl in context.functionDeclaration())
        {
            RegisterFunction(funcDecl);
        }

        // Fifth pass: analyze function bodies
        foreach (var funcDecl in context.functionDeclaration())
        {
            Visit(funcDecl);
        }

        return !_diagnostics.HasErrors;
    }

    private void ProcessImport(NovusParser.ImportDeclarationContext context)
    {
        var moduleName = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Get the list of names to import
        var importList = context.importList();
        bool importAll = importList.GetText() == "*";

        ImportModule(moduleName, importAll, importList, location);
    }

    private void ImportModule(string moduleName, bool importAll, NovusParser.ImportListContext? importList = null, SourceLocation? location = null)
    {
        // Use dummy location for implicit imports
        if (location == null)
        {
            location = new SourceLocation(_filePath, 0, 0, 0, "");
        }

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
                _diagnostics.ReportError(
                    "E0026",
                    $"module '{moduleName}' not found",
                    location,
                    helpTexts: new List<string>
                    {
                        $"searched in: std/{moduleName}.novus",
                        $"searched in: std/ffi/{moduleName}.novus",
                        "create one of these files to define the module"
                    }
                );
                return;
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
            _diagnostics.ReportError(
                "E0027",
                $"module '{moduleName}' has syntax errors",
                location,
                helpTexts: new List<string>
                {
                    $"fix syntax errors in {modulePath}"
                }
            );
            return;
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
                var importedName = importNameCtx.IDENTIFIER(0).GetText();
                namesToImport.Add(importedName);

                // Handle aliases (import Printf as MyPrintf)
                if (importNameCtx.IDENTIFIER().Length > 1)
                {
                    var alias = importNameCtx.IDENTIFIER(1).GetText();
                    _importedNames[alias] = moduleName;
                }
            }
        }

        // Register imported enums in symbol table
        foreach (var enumDecl in moduleContext.enumDeclaration())
        {
            var enumName = enumDecl.IDENTIFIER().GetText();

            // Skip if not in the import list
            if (!namesToImport.Contains(enumName))
            {
                continue;
            }

            // Check for duplicate enum names
            if (_enums.ContainsKey(enumName))
            {
                var enumLocation = SourceLocationHelper.FromToken(enumDecl.IDENTIFIER().Symbol, modulePath, new string[] { });
                _diagnostics.ReportError(
                    "E0030",
                    $"imported enum '{enumName}' conflicts with existing enum",
                    location,
                    helpTexts: new List<string>
                    {
                        $"use an alias to avoid the conflict: import {enumName} as Another{enumName}"
                    }
                );
                continue;
            }

            // Register the enum from the imported module
            RegisterEnum(enumDecl);
            _importedNames[enumName] = moduleName;
        }

        // Register imported constants in symbol table
        foreach (var constDecl in moduleContext.constDeclaration())
        {
            var constName = constDecl.IDENTIFIER().GetText();

            // Skip if not in the import list
            if (!namesToImport.Contains(constName))
            {
                continue;
            }

            // Check for duplicate constant names
            if (_constants.ContainsKey(constName))
            {
                var constLocation = SourceLocationHelper.FromToken(constDecl.IDENTIFIER().Symbol, modulePath, new string[] { });
                _diagnostics.ReportError(
                    "E0033",
                    $"imported constant '{constName}' conflicts with existing constant",
                    location,
                    helpTexts: new List<string>
                    {
                        $"use an alias to avoid the conflict: import {constName} as Another{constName}"
                    }
                );
                continue;
            }

            // Register the constant from the imported module
            RegisterConstant(constDecl);
            _importedNames[constName] = moduleName;
        }

        // Register imported structs in symbol table
        foreach (var structDecl in moduleContext.structDeclaration())
        {
            var structName = structDecl.IDENTIFIER().GetText();

            // Skip if not in the import list
            if (!namesToImport.Contains(structName))
            {
                continue;
            }

            // Check for duplicate struct names
            if (_structs.ContainsKey(structName))
            {
                var structLocation = SourceLocationHelper.FromToken(structDecl.IDENTIFIER().Symbol, modulePath, new string[] { });
                _diagnostics.ReportError(
                    "E0009",
                    $"imported struct '{structName}' conflicts with existing struct",
                    location,
                    helpTexts: new List<string>
                    {
                        $"use an alias to avoid the conflict: import {structName} as Another{structName}"
                    }
                );
                continue;
            }

            // Register the struct from the imported module
            RegisterStruct(structDecl);
            _importedNames[structName] = moduleName;
        }

        // Register imported functions in symbol table
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
                _diagnostics.ReportError(
                    "E0028",
                    $"cannot import private function '{funcName}' from module '{moduleName}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "only pub or extern functions can be imported from modules"
                    }
                );
                continue;
            }

            // Check for duplicate function names
            if (_functions.ContainsKey(funcName))
            {
                var originalLocation = _functions[funcName].Location;
                _diagnostics.ReportError(
                    "E0029",
                    $"imported function '{funcName}' conflicts with existing function",
                    location,
                    helpTexts: new List<string>
                    {
                        "use an alias to avoid the conflict: import " + funcName + " as Another" + funcName
                    },
                    relatedLocations: new List<(SourceLocation, string)>
                    {
                        (originalLocation, $"existing definition of '{funcName}' here")
                    }
                );
                continue;
            }

            // Parse function signature
            var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;
            var parameters = new List<ParameterSymbol>();

            if (funcDecl.parameterList() != null)
            {
                foreach (var paramCtx in funcDecl.parameterList().parameter())
                {
                    var paramName = paramCtx.IDENTIFIER().GetText();
                    var paramType = ParseType(paramCtx.type());
                    var paramLocation = SourceLocationHelper.FromToken(paramCtx.IDENTIFIER().Symbol, modulePath, new string[] { });
                    parameters.Add(new ParameterSymbol(paramName, paramType, paramLocation));
                }
            }

            // Register the function as extern
            var funcLocation = SourceLocationHelper.FromToken(funcDecl.IDENTIFIER().Symbol, modulePath, new string[] { });
            _functions[funcName] = new FunctionSymbol(funcName, returnType, parameters, funcLocation, IsExtern: true);
            _importedNames[funcName] = moduleName;
        }
    }

    private void RegisterConstant(NovusParser.ConstDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check for duplicate constant names
        if (_constants.ContainsKey(name))
        {
            var originalLocation = _constants[name].Location;
            _diagnostics.ReportError(
                "E0031",
                $"constant '{name}' is defined multiple times",
                location,
                helpTexts: new List<string>
                {
                    $"consider renaming one of the constants"
                },
                relatedLocations: new List<(SourceLocation, string)>
                {
                    (originalLocation, $"previous definition of '{name}' here")
                }
            );
            return;
        }

        // Parse the type
        var type = ParseType(context.type());

        // Evaluate the constant expression using the evaluator
        var valueExpr = context.expression();

        // Convert constants dict to use object values for evaluator
        var constantValues = _constants.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Value
        );

        var evaluator = new ConstantExpressionEvaluator(
            constantValues,
            error => _diagnostics.ReportError(
                "E0034",
                error,
                location,
                helpTexts: new List<string>
                {
                    "constants can only reference other constants defined earlier"
                }
            )
        );

        int? value = evaluator.Visit(valueExpr);

        if (value == null)
        {
            _diagnostics.ReportError(
                "E0032",
                $"constant value must be a compile-time constant expression",
                location,
                helpTexts: new List<string>
                {
                    "supported: integer/hex/binary literals, constant references, bitwise ops (|, &, ^, <<, >>, ~), arithmetic"
                }
            );
            return;
        }

        _constants[name] = new ConstantSymbol(name, type, value, location);
    }

    private void RegisterFunction(NovusParser.FunctionDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check if function is extern by looking for 'extern' keyword
        var isExtern = false;
        for (int i = 0; i < Math.Min(3, context.ChildCount); i++)
        {
            if (context.GetChild(i)?.GetText() == "extern")
            {
                isExtern = true;
                break;
            }
        }

        // Check for duplicate function names
        if (_functions.ContainsKey(name))
        {
            var originalLocation = _functions[name].Location;
            _diagnostics.ReportError(
                "E0001",
                $"function '{name}' is defined multiple times",
                location,
                helpTexts: new List<string>
                {
                    $"consider renaming one of the functions"
                },
                relatedLocations: new List<(SourceLocation, string)>
                {
                    (originalLocation, $"previous definition of '{name}' here")
                }
            );
            return;
        }

        // Validate extern functions don't have a body
        if (isExtern && context.block() != null)
        {
            _diagnostics.ReportError(
                "E0024",
                $"extern function '{name}' cannot have a body",
                location,
                helpTexts: new List<string>
                {
                    "remove the function body or remove the 'extern' keyword"
                }
            );
            return;
        }

        // Validate non-extern functions have a body
        if (!isExtern && context.block() == null)
        {
            _diagnostics.ReportError(
                "E0025",
                $"function '{name}' must have a body",
                location,
                helpTexts: new List<string>
                {
                    "add a function body or mark the function as 'extern'"
                }
            );
            return;
        }

        var returnType = context.type() != null ? ParseType(context.type()) : IrVoidType.Instance;
        var parameters = new List<ParameterSymbol>();

        if (context.parameterList() != null)
        {
            foreach (var paramCtx in context.parameterList().parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                var paramType = ParseType(paramCtx.type());
                var paramLocation = SourceLocationHelper.FromToken(paramCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);
                parameters.Add(new ParameterSymbol(paramName, paramType, paramLocation));
            }
        }

        _functions[name] = new FunctionSymbol(name, returnType, parameters, location, isExtern);
    }

    private void RegisterStruct(NovusParser.StructDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check for duplicate struct names
        if (_structs.ContainsKey(name))
        {
            _diagnostics.ReportError(
                "E0019",
                $"struct '{name}' is defined multiple times",
                location,
                helpTexts: new List<string>
                {
                    "consider renaming one of the structs"
                }
            );
            return;
        }

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

    private void RegisterEnum(NovusParser.EnumDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check for duplicate enum names
        if (_enums.ContainsKey(name))
        {
            _diagnostics.ReportError(
                "E0030",
                $"enum '{name}' is defined multiple times",
                location,
                helpTexts: new List<string>
                {
                    "consider renaming one of the enums"
                }
            );
            return;
        }

        // Handle generic parameters if present
        var genericParams = new List<string>();
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                var paramName = paramId.GetText();
                genericParams.Add(paramName);

                // Add to generic param scope for variant parsing
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

            // Parse associated data types if present
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

        // Force size calculation
        if (genericParams.Count == 0)
        {
            _ = enumType.SizeInBytes;
        }

        _enums[name] = enumType;

        // Clear generic param scope
        _genericParams.Clear();
    }

    public override IrType? VisitFunctionDeclaration([NotNull] NovusParser.FunctionDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        _currentFunction = _functions[name];
        _variables.Clear();

        // Skip body analysis for extern functions
        if (_currentFunction.IsExtern)
        {
            _currentFunction = null;
            return null;
        }

        // Add parameters to symbol table (parameters are immutable)
        foreach (var param in _currentFunction.Parameters)
        {
            _variables[param.Name] = new VariableSymbol(param.Name, param.Type, false, param.Location);
        }

        // Analyze function body with unreachable code detection
        AnalyzeBlock(context.block());

        _currentFunction = null;
        return null;
    }

    /// <summary>
    /// Analyzes a block and detects unreachable code after return/break statements
    /// </summary>
    private void AnalyzeBlock(NovusParser.BlockContext block)
    {
        var statements = block.statement();
        bool foundTerminal = false;

        for (int i = 0; i < statements.Length; i++)
        {
            var stmt = statements[i];

            // If we've already found a terminal statement, this code is unreachable
            if (foundTerminal)
            {
                var location = SourceLocationHelper.FromContext(stmt, _filePath, _sourceLines);
                _diagnostics.ReportWarning(
                    "W0003",
                    "unreachable code detected",
                    location,
                    helpTexts: new List<string>
                    {
                        "this code will never be executed",
                        "consider removing this statement or restructuring your code"
                    }
                );
                // Continue analyzing to find more issues, but don't check for more unreachable code
                Visit(stmt);
                continue;
            }

            // Visit the statement
            Visit(stmt);

            // Check if this statement is terminal (always exits the block)
            if (IsTerminalStatement(stmt))
            {
                foundTerminal = true;
            }
        }
    }

    /// <summary>
    /// Checks if a statement always causes control flow to exit the current block
    /// </summary>
    private bool IsTerminalStatement(NovusParser.StatementContext stmt)
    {
        // Return statement is always terminal
        if (stmt.returnStatement() != null)
            return true;

        // Break statement is terminal in loop context
        if (stmt.breakStatement() != null)
            return true;

        // If statement is terminal only if both branches are terminal
        if (stmt.ifStatement() != null)
        {
            var ifStmt = stmt.ifStatement();

            // Check if there's an else clause
            if (ifStmt.block().Length > 1 || ifStmt.ifStatement() != null)
            {
                // Both then and else must be terminal
                bool thenTerminal = BlockIsTerminal(ifStmt.block(0));
                bool elseTerminal;

                if (ifStmt.ifStatement() != null)
                {
                    // else if chain - check if the chain is terminal
                    elseTerminal = IfChainIsTerminal(ifStmt.ifStatement());
                }
                else
                {
                    elseTerminal = BlockIsTerminal(ifStmt.block(1));
                }

                return thenTerminal && elseTerminal;
            }

            // If without else is not terminal
            return false;
        }

        // Other statements are not terminal
        return false;
    }

    /// <summary>
    /// Checks if a block is terminal (all paths exit)
    /// </summary>
    private bool BlockIsTerminal(NovusParser.BlockContext block)
    {
        var statements = block.statement();
        if (statements.Length == 0)
            return false;

        // Check each statement - if any is terminal, the block is terminal
        foreach (var stmt in statements)
        {
            if (IsTerminalStatement(stmt))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if an if-statement chain is terminal (all branches exit)
    /// </summary>
    private bool IfChainIsTerminal(NovusParser.IfStatementContext ifStmt)
    {
        // Check the then block
        bool thenTerminal = BlockIsTerminal(ifStmt.block(0));
        if (!thenTerminal)
            return false;

        // Check the else clause
        if (ifStmt.block().Length > 1)
        {
            // else block
            return BlockIsTerminal(ifStmt.block(1));
        }
        else if (ifStmt.ifStatement() != null)
        {
            // else if chain
            return IfChainIsTerminal(ifStmt.ifStatement());
        }
        else
        {
            // No else clause means not all paths exit
            return false;
        }
    }

    public override IrType? VisitReturnStatement([NotNull] NovusParser.ReturnStatementContext context)
    {
        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
        var exprType = Visit(context.expression());

        if (_currentFunction == null)
        {
            _diagnostics.ReportError(
                "E0002",
                "return statement outside of function",
                location
            );
            return null;
        }

        // Check return type compatibility
        if (exprType != null && !TypesCompatible(_currentFunction.ReturnType, exprType))
        {
            var expectedType = TypeToString(_currentFunction.ReturnType);
            var actualType = TypeToString(exprType);

            _diagnostics.ReportError(
                "E0003",
                $"mismatched types in return statement",
                location,
                helpTexts: new List<string>
                {
                    $"expected type '{expectedType}', found '{actualType}'",
                    $"consider using a cast: ({expectedType}){context.expression().GetText()}"
                }
            );
        }

        return null;
    }

    public override IrType? VisitVariableDeclaration([NotNull] NovusParser.VariableDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var isMutable = context.GetChild(0)?.GetText() == "var";
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check for duplicate variable names
        if (_variables.ContainsKey(name))
        {
            var originalLocation = _variables[name].Location;
            _diagnostics.ReportError(
                "E0016",
                $"variable '{name}' is already defined in this scope",
                location,
                relatedLocations: new List<(SourceLocation, string)>
                {
                    (originalLocation, $"previous definition of '{name}' here")
                }
            );
            return null;
        }

        // Determine the variable type (parse type annotation first for bidirectional checking)
        IrType varType;
        if (context.type() != null)
        {
            varType = ParseType(context.type());

            // Set expected type for bidirectional type checking
            var previousExpectedType = _expectedType;
            _expectedType = varType;

            // Analyze the initializer expression with expected type context
            var exprType = Visit(context.expression());

            // Restore previous expected type
            _expectedType = previousExpectedType;

            if (exprType == null)
                return null;

            // Check type compatibility with initializer
            if (!TypesCompatible(varType, exprType))
            {
                _diagnostics.ReportError(
                    "E0017",
                    $"mismatched types in variable declaration",
                    location,
                    helpTexts: new List<string>
                    {
                        $"expected type '{TypeToString(varType)}', found '{TypeToString(exprType)}'",
                        $"consider changing the type annotation or using a cast"
                    }
                );
            }
        }
        else
        {
            // No type annotation - infer type from initializer
            var exprType = Visit(context.expression());
            if (exprType == null)
                return null;

            varType = exprType;
        }

        // Add variable to symbol table
        _variables[name] = new VariableSymbol(name, varType, isMutable, location);

        return null;
    }

    public override IrType? VisitAssignmentStatement([NotNull] NovusParser.AssignmentStatementContext context)
    {
        var identifiers = context.IDENTIFIER();
        var name = identifiers[0].GetText();
        var location = SourceLocationHelper.FromToken(identifiers[0].Symbol, _filePath, _sourceLines);

        // Check if this is a member assignment
        if (identifiers.Length > 1)
        {
            // Member assignment: obj.field = value
            // For now, we'll just verify the base variable exists
            // Full member access checking will be implemented later
            if (!_variables.ContainsKey(name))
            {
                _diagnostics.ReportError(
                    "E0018",
                    $"cannot assign to member of undeclared variable '{name}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "this variable has not been declared",
                        "consider declaring it with 'let' or 'var'"
                    }
                );
                return null;
            }
            // TODO: Validate member chain and types
            var valueType = Visit(context.expression(0));
            return null;
        }

        // Check if variable exists
        if (!_variables.ContainsKey(name))
        {
            _diagnostics.ReportError(
                "E0018",
                $"cannot assign to undeclared variable '{name}'",
                location,
                helpTexts: new List<string>
                {
                    "this variable has not been declared",
                    "consider declaring it with 'let' or 'var'"
                }
            );
            return null;
        }

        var variable = _variables[name];

        // Check if this is an array element assignment (has 2 expressions: index and value)
        if (context.expression().Length == 2)
        {
            // Array element assignment: arr[index] = value

            // Check that variable is actually an array
            if (variable.Type is not IrArrayType arrayType)
            {
                _diagnostics.ReportError(
                    "E0021",
                    $"cannot index into non-array type",
                    location,
                    helpTexts: new List<string>
                    {
                        $"'{name}' has type '{TypeToString(variable.Type)}', which is not an array"
                    }
                );
                return null;
            }

            // Validate index expression
            var indexType = Visit(context.expression(0));
            if (indexType != null && !IsNumericType(indexType))
            {
                var indexLocation = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0022",
                    $"array index must be a numeric type",
                    indexLocation,
                    helpTexts: new List<string>
                    {
                        $"found type '{TypeToString(indexType)}', expected a numeric type"
                    }
                );
            }

            // Validate value expression type
            var valueType = Visit(context.expression(1));
            if (valueType != null && !TypesCompatible(arrayType.ElementType, valueType))
            {
                var valueLocation = SourceLocationHelper.FromContext(context.expression(1), _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0023",
                    $"mismatched types in array element assignment",
                    valueLocation,
                    helpTexts: new List<string>
                    {
                        $"expected type '{TypeToString(arrayType.ElementType)}', found '{TypeToString(valueType)}'"
                    }
                );
            }

            // Note: For array element assignment, we don't check if the array itself is mutable
            // In most languages, you can modify elements of a const/let array, just not reassign the array itself
        }
        else
        {
            // Simple variable assignment

            // Check if variable is mutable
            if (!variable.IsMutable)
            {
                _diagnostics.ReportError(
                    "E0019",
                    $"cannot assign to immutable variable '{name}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "this variable was declared with 'let', which makes it immutable",
                        "consider declaring it with 'var' if you need to reassign it"
                    },
                    relatedLocations: new List<(SourceLocation, string)>
                    {
                        (variable.Location, $"'{name}' was declared here")
                    }
                );
                return null;
            }

            // Check type compatibility
            var exprType = Visit(context.expression(0));
            if (exprType != null && !TypesCompatible(variable.Type, exprType))
            {
                _diagnostics.ReportError(
                    "E0020",
                    $"mismatched types in assignment",
                    location,
                    helpTexts: new List<string>
                    {
                        $"expected type '{TypeToString(variable.Type)}', found '{TypeToString(exprType)}'",
                        $"consider using a cast: ({TypeToString(variable.Type)}){context.expression(0).GetText()}"
                    }
                );
            }
        }

        return null;
    }

    public override IrType? VisitAdditiveExpr([NotNull] NovusParser.AdditiveExprContext context)
    {
        var leftType = Visit(context.expression(0));
        var rightType = Visit(context.expression(1));

        if (leftType == null || rightType == null)
            return null;

        // Check that both operands are numeric types
        if (!IsNumericType(leftType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0004",
                $"cannot apply operator '{context.GetChild(1).GetText()}' to non-numeric type '{TypeToString(leftType)}'",
                location
            );
            return null;
        }

        if (!IsNumericType(rightType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(1), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0004",
                $"cannot apply operator '{context.GetChild(1).GetText()}' to non-numeric type '{TypeToString(rightType)}'",
                location
            );
            return null;
        }

        // Warn if mixing signed and unsigned types
        if (IsMixedSignedness(leftType, rightType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportWarning(
                "W0001",
                $"mixing signed and unsigned types in arithmetic operation",
                location,
                helpTexts: new List<string>
                {
                    "this may produce unexpected results",
                    $"consider casting to a common type"
                }
            );
        }

        return leftType; // Use left operand's type as result type
    }

    public override IrType? VisitShiftExpr([NotNull] NovusParser.ShiftExprContext context)
    {
        var leftType = Visit(context.expression(0));
        var rightType = Visit(context.expression(1));

        if (leftType == null || rightType == null)
            return null;

        // Both operands must be numeric types
        if (!IsNumericType(leftType) || !IsNumericType(rightType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0004",
                $"shift operators require numeric types",
                location
            );
            return null;
        }

        return leftType;
    }

    public override IrType? VisitBitwiseAndExpr([NotNull] NovusParser.BitwiseAndExprContext context)
    {
        var leftType = Visit(context.expression(0));
        var rightType = Visit(context.expression(1));

        if (leftType == null || rightType == null)
            return null;

        // Both operands must be numeric types
        if (!IsNumericType(leftType) || !IsNumericType(rightType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0004",
                $"bitwise AND requires numeric types",
                location
            );
            return null;
        }

        return leftType;
    }

    public override IrType? VisitBitwiseXorExpr([NotNull] NovusParser.BitwiseXorExprContext context)
    {
        var leftType = Visit(context.expression(0));
        var rightType = Visit(context.expression(1));

        if (leftType == null || rightType == null)
            return null;

        // Both operands must be numeric types
        if (!IsNumericType(leftType) || !IsNumericType(rightType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0004",
                $"bitwise XOR requires numeric types",
                location
            );
            return null;
        }

        return leftType;
    }

    public override IrType? VisitBitwiseOrExpr([NotNull] NovusParser.BitwiseOrExprContext context)
    {
        var leftType = Visit(context.expression(0));
        var rightType = Visit(context.expression(1));

        if (leftType == null || rightType == null)
            return null;

        // Both operands must be numeric types
        if (!IsNumericType(leftType) || !IsNumericType(rightType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0004",
                $"bitwise OR requires numeric types",
                location
            );
            return null;
        }

        return leftType;
    }

    public override IrType? VisitMultiplicativeExpr([NotNull] NovusParser.MultiplicativeExprContext context)
    {
        var leftType = Visit(context.expression(0));
        var rightType = Visit(context.expression(1));

        if (leftType == null || rightType == null)
            return null;

        if (!IsNumericType(leftType) || !IsNumericType(rightType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0004",
                $"cannot apply operator '{context.GetChild(1).GetText()}' to non-numeric types",
                location
            );
            return null;
        }

        // Check for division by zero (if right is a constant 0)
        if (context.GetChild(1).GetText() == "/" && context.expression(1) is NovusParser.PrimaryExprContext primaryExpr)
        {
            var intLiteral = primaryExpr.primaryExpression() as NovusParser.IntegerLiteralContext;
            if (intLiteral?.INTEGER_LITERAL()?.GetText() == "0")
            {
                var location = SourceLocationHelper.FromContext(context.expression(1), _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0005",
                    "division by zero",
                    location,
                    helpTexts: new List<string>
                    {
                        "this would cause a runtime error"
                    }
                );
            }
        }

        if (IsMixedSignedness(leftType, rightType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportWarning(
                "W0001",
                $"mixing signed and unsigned types in arithmetic operation",
                location
            );
        }

        return leftType;
    }

    public override IrType? VisitCastExpr([NotNull] NovusParser.CastExprContext context)
    {
        var targetType = ParseType(context.type());
        var exprType = Visit(context.expression());

        if (exprType == null)
            return targetType;

        // Check if cast is valid
        if (!IsNumericType(targetType) || !IsNumericType(exprType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0006",
                $"cannot cast from '{TypeToString(exprType)}' to '{TypeToString(targetType)}'",
                location,
                helpTexts: new List<string>
                {
                    "only numeric types can be cast"
                }
            );
            return null;
        }

        // Warn about potentially lossy casts
        if (IsLossyCast(exprType, targetType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportWarning(
                "W0002",
                $"casting from '{TypeToString(exprType)}' to '{TypeToString(targetType)}' may lose precision",
                location,
                helpTexts: new List<string>
                {
                    "this cast may truncate the value"
                }
            );
        }

        return targetType;
    }

    public override IrType? VisitIfStatement([NotNull] NovusParser.IfStatementContext context)
    {
        var conditionType = Visit(context.expression());
        var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);

        // Check that condition is a valid boolean expression
        // Accept bool or any numeric type (0 = false, non-zero = true)
        if (conditionType != null && !IsBoolOrNumericType(conditionType))
        {
            _diagnostics.ReportError(
                "E0010",
                "if condition must be a boolean or numeric type",
                location,
                helpTexts: new List<string>
                {
                    $"found type '{TypeToString(conditionType)}', expected a boolean or numeric type",
                    "use a comparison expression like 'x == 0' or 'x > 10'"
                }
            );
        }

        // Analyze then block with unreachable code detection
        AnalyzeBlock(context.block(0));

        // Analyze else block if present
        if (context.ifStatement() != null)
        {
            Visit(context.ifStatement());
        }
        else if (context.block().Length > 1)
        {
            AnalyzeBlock(context.block(1));
        }

        return null;
    }

    public override IrType? VisitWhileStatement([NotNull] NovusParser.WhileStatementContext context)
    {
        var conditionType = Visit(context.expression());
        var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);

        // Check that condition is a valid boolean expression
        if (conditionType != null && !IsBoolOrNumericType(conditionType))
        {
            _diagnostics.ReportError(
                "E0010",
                "while condition must be a boolean or numeric type",
                location,
                helpTexts: new List<string>
                {
                    $"found type '{TypeToString(conditionType)}', expected a boolean or numeric type"
                }
            );
        }

        // Enter loop context and analyze block with unreachable code detection
        _loopDepth++;
        AnalyzeBlock(context.block());
        _loopDepth--;

        return null;
    }

    public override IrType? VisitForStatement([NotNull] NovusParser.ForStatementContext context)
    {
        // Visit initialization if present
        if (context.GetChild(2) is NovusParser.VariableDeclarationContext varDecl)
        {
            Visit(varDecl);
        }
        else if (context.GetChild(2) is NovusParser.AssignmentStatementContext assignment)
        {
            Visit(assignment);
        }

        // Visit condition if present
        if (context.expression() != null)
        {
            var conditionType = Visit(context.expression());
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);

            if (conditionType != null && !IsBoolOrNumericType(conditionType))
            {
                _diagnostics.ReportError(
                    "E0010",
                    "for loop condition must be a boolean or numeric type",
                    location,
                    helpTexts: new List<string>
                    {
                        $"found type '{TypeToString(conditionType)}', expected a boolean or numeric type"
                    }
                );
            }
        }

        // Enter loop context and analyze block with unreachable code detection
        _loopDepth++;
        AnalyzeBlock(context.block());
        _loopDepth--;

        // Visit increment statement if present (after exiting loop context)
        if (context.GetChild(6) is NovusParser.AssignmentStatementContext incrAssignment)
        {
            // Note: This is validated in the loop context during IR building
            // We don't validate it here separately
        }

        return null;
    }

    public override IrType? VisitForeverStatement([NotNull] NovusParser.ForeverStatementContext context)
    {
        // Enter loop context and analyze block with unreachable code detection
        _loopDepth++;
        AnalyzeBlock(context.block());
        _loopDepth--;

        return null;
    }

    public override IrType? VisitBreakStatement([NotNull] NovusParser.BreakStatementContext context)
    {
        if (_loopDepth == 0)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0011",
                "break statement outside of loop",
                location,
                helpTexts: new List<string>
                {
                    "break can only be used inside while, for, or forever loops"
                }
            );
        }

        return null;
    }

    public override IrType? VisitMatchStatement([NotNull] NovusParser.MatchStatementContext context)
    {
        // Analyze the value being matched
        var matchValueType = Visit(context.expression());
        if (matchValueType == null)
        {
            return null;
        }

        // Ensure we're matching on an enum type
        if (matchValueType is not IrEnumType enumType)
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0035",
                $"match expression can only be used with enum types, got '{matchValueType.Name}'",
                location,
                helpTexts: new List<string>
                {
                    "match is used for pattern matching on enum variants"
                }
            );
            return null;
        }

        // Track which variants are covered
        var coveredVariants = new HashSet<string>();
        bool hasWildcard = false;

        // Analyze each match arm
        foreach (var armCtx in context.matchArm())
        {
            var pattern = armCtx.pattern();

            // Save current variable scope - store list of variables added by this pattern
            var variablesBeforePattern = new HashSet<string>(_variables.Keys);

            // Analyze pattern and bind variables
            AnalyzePatternAndBind(pattern, enumType, coveredVariants, ref hasWildcard);

            // Analyze the arm body (expression or block) with bound variables in scope
            if (armCtx.expression() != null)
            {
                Visit(armCtx.expression());
            }
            else if (armCtx.block() != null)
            {
                AnalyzeBlock(armCtx.block());
            }

            // Remove pattern bindings (they're only valid in this arm)
            var keysToRemove = _variables.Keys.Where(k => !variablesBeforePattern.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _variables.Remove(key);
            }
        }

        // Check exhaustiveness - either all variants covered or wildcard present
        if (!hasWildcard)
        {
            var uncoveredVariants = enumType.Variants
                .Select(v => v.Name)
                .Where(v => !coveredVariants.Contains(v))
                .ToList();

            if (uncoveredVariants.Any())
            {
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0036",
                    "match is not exhaustive",
                    location,
                    helpTexts: new List<string>
                    {
                        $"missing patterns: {string.Join(", ", uncoveredVariants)}",
                        "add missing patterns or use a wildcard pattern '_'"
                    }
                );
            }
        }

        return null;
    }

    private void AnalyzePatternAndBind(NovusParser.PatternContext pattern, IrEnumType enumType,
        HashSet<string> coveredVariants, ref bool hasWildcard)
    {
        switch (pattern)
        {
            case NovusParser.WildcardPatternContext:
                hasWildcard = true;
                break;

            case NovusParser.VariantPatternContext variantPattern:
            {
                var variantName = variantPattern.IDENTIFIER().GetText();

                // Check if this variant exists
                var variant = enumType.GetVariant(variantName);
                if (variant == null)
                {
                    var location = SourceLocationHelper.FromToken(variantPattern.IDENTIFIER().Symbol, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0037",
                        $"enum '{enumType.EnumName}' has no variant '{variantName}'",
                        location,
                        helpTexts: new List<string>
                        {
                            $"available variants: {string.Join(", ", enumType.Variants.Select(v => v.Name))}"
                        }
                    );
                    return;
                }

                // Check if pattern bindings match associated data count
                var patternList = variantPattern.patternList();
                int bindingCount = patternList?.pattern()?.Length ?? 0;

                if (bindingCount != variant.AssociatedData.Count)
                {
                    var location = SourceLocationHelper.FromToken(variantPattern.IDENTIFIER().Symbol, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0038",
                        $"variant '{variantName}' expects {variant.AssociatedData.Count} values but pattern has {bindingCount}",
                        location
                    );
                }

                // Bind pattern variables to their types
                if (patternList != null)
                {
                    var patterns = patternList.pattern();
                    for (int i = 0; i < Math.Min(patterns.Length, variant.AssociatedData.Count); i++)
                    {
                        var subPattern = patterns[i];

                        // Only bind identifier patterns (e.g., Some(x) binds x)
                        if (subPattern is NovusParser.IdentifierPatternContext idPattern)
                        {
                            var bindingName = idPattern.IDENTIFIER().GetText();
                            var bindingType = variant.AssociatedData[i];
                            var location = SourceLocationHelper.FromToken(idPattern.IDENTIFIER().Symbol, _filePath, _sourceLines);

                            // Register this variable as immutable (pattern bindings are always immutable)
                            _variables[bindingName] = new VariableSymbol(bindingName, bindingType, false, location);
                        }
                    }
                }

                coveredVariants.Add(variantName);
                break;
            }

            case NovusParser.IdentifierPatternContext identPattern:
            {
                var variantName = identPattern.IDENTIFIER().GetText();
                var variant = enumType.GetVariant(variantName);
                if (variant == null)
                {
                    var location = SourceLocationHelper.FromToken(identPattern.IDENTIFIER().Symbol, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0037",
                        $"enum '{enumType.EnumName}' has no variant '{variantName}'",
                        location
                    );
                    return;
                }

                // Identifier pattern should only be used for variants without data
                if (variant.HasAssociatedData)
                {
                    var location = SourceLocationHelper.FromToken(identPattern.IDENTIFIER().Symbol, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0039",
                        $"variant '{variantName}' has associated data and requires a pattern with bindings",
                        location,
                        helpTexts: new List<string>
                        {
                            $"use pattern: {variantName}({string.Join(", ", Enumerable.Range(0, variant.AssociatedData.Count).Select(i => $"_"))})"
                        }
                    );
                }

                coveredVariants.Add(variantName);
                break;
            }
        }
    }

    public override IrType? VisitCallExpr([NotNull] NovusParser.CallExprContext context)
    {
        // Get the function name from the expression (should be an identifier or path expression)
        var funcExpr = context.expression();

        // Handle path expressions (enum constructors like Option::Some)
        if (funcExpr is NovusParser.PathExprContext pathCtx)
        {
            // Visit the path expression to get the enum type
            var enumType = Visit(pathCtx);
            if (enumType == null || enumType is not IrEnumType)
            {
                // Error already reported by VisitPathExpr
                return null;
            }

            // Validate arguments match the variant's associated data
            var variantName = pathCtx.IDENTIFIER().GetText();
            var variant = ((IrEnumType)enumType).GetVariant(variantName);

            if (variant != null)
            {
                var variantArgCount = context.argumentList()?.expression().Length ?? 0;
                if (variantArgCount != variant.AssociatedData.Count)
                {
                    var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0040",
                        $"variant '{variantName}' expects {variant.AssociatedData.Count} argument(s), but {variantArgCount} were provided",
                        location
                    );
                }

                // Perform generic type inference and validate argument types
                var irEnumType = (IrEnumType)enumType;
                Dictionary<string, IrType>? typeSubstitutions = null;

                if (irEnumType.GenericParameters.Count > 0)
                {
                    // Infer generic type parameters from arguments
                    typeSubstitutions = new Dictionary<string, IrType>();

                    if (context.argumentList() != null)
                    {
                        var arguments = context.argumentList().expression();

                        for (int i = 0; i < Math.Min(arguments.Length, variant.AssociatedData.Count); i++)
                        {
                            var argType = Visit(arguments[i]);
                            var expectedParamType = variant.AssociatedData[i];

                            // If expected type is a generic parameter, infer it from the argument
                            if (expectedParamType is IrGenericType genericType)
                            {
                                var paramName = genericType.ParameterName;
                                if (!typeSubstitutions.ContainsKey(paramName))
                                {
                                    if (argType != null)
                                    {
                                        typeSubstitutions[paramName] = argType;
                                    }
                                }
                                else
                                {
                                    // Check consistency - all uses of T must have same type
                                    if (argType != null && !TypesCompatible(typeSubstitutions[paramName], argType))
                                    {
                                        var location = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);
                                        _diagnostics.ReportError(
                                            "E0042",
                                            $"type parameter '{paramName}' inferred as both '{TypeToString(typeSubstitutions[paramName])}' and '{TypeToString(argType)}'",
                                            location
                                        );
                                    }
                                }
                            }
                            else
                            {
                                // Concrete type - validate compatibility
                                if (argType != null && !TypesCompatible(expectedParamType, argType))
                                {
                                    var location = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);
                                    _diagnostics.ReportError(
                                        "E0041",
                                        $"argument {i + 1} type mismatch",
                                        location,
                                        helpTexts: new List<string>
                                        {
                                            $"expected '{TypeToString(expectedParamType)}', got '{TypeToString(argType)}'"
                                        }
                                    );
                                }
                            }
                        }
                    }

                    // Bidirectional type checking: use expected type to fill in missing parameters
                    if (_expectedType is IrEnumType expectedEnumType &&
                        expectedEnumType.EnumName == irEnumType.EnumName &&
                        expectedEnumType.GenericParameters.Count == 0) // Expected type is monomorphized
                    {
                        // The expected type has concrete types for all parameters
                        // Use those to fill in any parameters we couldn't infer from arguments
                        // We need to match the generic parameters to the concrete types in the expected enum

                        // Build a mapping from generic parameters to concrete types by comparing variants
                        // Find a variant that uses each parameter and extract the concrete type
                        for (int paramIdx = 0; paramIdx < irEnumType.GenericParameters.Count; paramIdx++)
                        {
                            var paramName = irEnumType.GenericParameters[paramIdx];
                            if (!typeSubstitutions.ContainsKey(paramName))
                            {
                                // Look through all variants to find one that uses this parameter
                                bool found = false;
                                for (int varIdx = 0; varIdx < irEnumType.Variants.Count && !found; varIdx++)
                                {
                                    var origVariant = irEnumType.Variants[varIdx];
                                    var expectedVar = expectedEnumType.Variants[varIdx];

                                    for (int dataIdx = 0; dataIdx < origVariant.AssociatedData.Count && !found; dataIdx++)
                                    {
                                        if (origVariant.AssociatedData[dataIdx] is IrGenericType gt &&
                                            gt.ParameterName == paramName)
                                        {
                                            // Found the parameter! Use the concrete type from expected variant
                                            typeSubstitutions[paramName] = expectedVar.AssociatedData[dataIdx];
                                            found = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // If we inferred type parameters, create a monomorphized instance
                if (typeSubstitutions != null && typeSubstitutions.Count > 0)
                {
                    // Create monomorphized enum type
                    var monomorphizedVariants = new List<IrEnumVariant>();
                    foreach (var origVariant in irEnumType.Variants)
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
                    var monomorphizedEnum = new IrEnumType(irEnumType.EnumName, monomorphizedVariants, null);
                    return monomorphizedEnum;
                }
            }

            return enumType;
        }

        // The function name should be in a primary expression (identifier)
        if (funcExpr is not NovusParser.PrimaryExprContext primaryCtx)
        {
            var location = SourceLocationHelper.FromContext(funcExpr, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0012",
                "function call target must be an identifier or path expression",
                location
            );
            return null;
        }

        // Check if the primary expression is an identifier
        var identifierExpr = primaryCtx.primaryExpression() as NovusParser.IdentifierExprContext;
        if (identifierExpr == null)
        {
            var location = SourceLocationHelper.FromContext(primaryCtx, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0012",
                "function call target must be an identifier or path expression",
                location
            );
            return null;
        }

        var functionName = identifierExpr.IDENTIFIER().GetText();
        var argCount = context.argumentList()?.expression().Length ?? 0;

        // Check if this is a variable with a function pointer type
        if (_variables.ContainsKey(functionName))
        {
            var variable = _variables[functionName];
            if (variable.Type is IrFunctionPointerType fpType)
            {
                // Validate argument count for function pointer call
                if (argCount != fpType.ParameterTypes.Count)
                {
                    var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0014",
                        $"function pointer expects {fpType.ParameterTypes.Count} argument(s), but {argCount} were provided",
                        location
                    );
                    return fpType.ReturnType;
                }

                // Validate argument types
                if (context.argumentList() != null)
                {
                    var arguments = context.argumentList().expression();
                    for (int i = 0; i < arguments.Length; i++)
                    {
                        var argType = Visit(arguments[i]);
                        var paramType = fpType.ParameterTypes[i];

                        if (argType != null && !TypesCompatible(paramType, argType))
                        {
                            var location = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);
                            _diagnostics.ReportError(
                                "E0015",
                                $"argument {i + 1} type mismatch",
                                location,
                                helpTexts: new List<string>
                                {
                                    $"expected '{TypeToString(paramType)}', got '{TypeToString(argType)}'"
                                }
                            );
                        }
                    }
                }

                return fpType.ReturnType;
            }
        }

        // Check if function exists
        if (!_functions.ContainsKey(functionName))
        {
            var location = SourceLocationHelper.FromToken(identifierExpr.IDENTIFIER().Symbol, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0013",
                $"undefined function '{functionName}'",
                location,
                helpTexts: new List<string>
                {
                    "this function has not been declared"
                }
            );
            return null;
        }

        var function = _functions[functionName];

        // Validate argument count
        if (argCount != function.Parameters.Count)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0014",
                $"function '{functionName}' expects {function.Parameters.Count} argument(s), but {argCount} were provided",
                location,
                helpTexts: new List<string>
                {
                    function.Parameters.Count == 0
                        ? $"try calling: {functionName}()"
                        : $"expected: {functionName}({string.Join(", ", function.Parameters.Select(p => $"{p.Name}: {TypeToString(p.Type)}"))})"
                }
            );
            return function.ReturnType;
        }

        // Validate argument types
        if (context.argumentList() != null)
        {
            var arguments = context.argumentList().expression();
            for (int i = 0; i < arguments.Length; i++)
            {
                var argType = Visit(arguments[i]);
                var paramType = function.Parameters[i].Type;

                if (argType != null && !TypesCompatible(paramType, argType))
                {
                    var location = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0015",
                        $"mismatched types in function call",
                        location,
                        helpTexts: new List<string>
                        {
                            $"argument {i + 1} ('{function.Parameters[i].Name}'): expected '{TypeToString(paramType)}', found '{TypeToString(argType)}'",
                            $"consider using a cast: ({TypeToString(paramType)}){arguments[i].GetText()}"
                        }
                    );
                }
            }
        }

        return function.ReturnType;
    }

    public override IrType? VisitBoolLiteral([NotNull] NovusParser.BoolLiteralContext context)
    {
        return IrBoolType.Instance;
    }

    public override IrType? VisitStringLiteral([NotNull] NovusParser.StringLiteralContext context)
    {
        // String literals have type *u8 (pointer to u8)
        return new IrPointerType(IrIntType.U8);
    }

    public override IrType? VisitFloatLiteral([NotNull] NovusParser.FloatLiteralContext context)
    {
        var text = context.FLOAT_LITERAL().GetText();

        // Parse the literal to determine type
        if (text.EndsWith("fixed32"))
        {
            return IrFixedType.Fixed32;
        }
        if (text.EndsWith("fixed16"))
        {
            return IrFixedType.Fixed16;
        }
        if (text.EndsWith("f64"))
        {
            return IrFloatType.F64;
        }
        if (text.EndsWith("f32"))
        {
            return IrFloatType.F32;
        }

        // Default to f32
        return IrFloatType.F32;
    }

    public override IrType? VisitIntegerLiteral([NotNull] NovusParser.IntegerLiteralContext context)
    {
        return ParseAndValidateIntegerLiteral(context, context.INTEGER_LITERAL().GetText());
    }

    public override IrType? VisitBinaryLiteral([NotNull] NovusParser.BinaryLiteralContext context)
    {
        return ParseAndValidateBinaryLiteral(context, context.BINARY_LITERAL().GetText());
    }

    public override IrType? VisitHexLiteral([NotNull] NovusParser.HexLiteralContext context)
    {
        return ParseAndValidateHexLiteral(context, context.HEX_LITERAL().GetText());
    }

    public override IrType? VisitIdentifierExpr([NotNull] NovusParser.IdentifierExprContext context)
    {
        var name = context.IDENTIFIER().GetText();
        if (!_variables.ContainsKey(name) && !_functions.ContainsKey(name) && !_constants.ContainsKey(name))
        {
            var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0007",
                $"undefined variable '{name}'",
                location,
                helpTexts: new List<string>
                {
                    "this variable has not been declared",
                    _currentFunction != null && _currentFunction.Parameters.Any(p => p.Name.ToLower() == name.ToLower())
                        ? $"did you mean '{_currentFunction.Parameters.First(p => p.Name.ToLower() == name.ToLower()).Name}'?"
                        : null!
                }.Where(h => h != null).ToList()
            );
            return null;
        }

        // If it's a constant, return its type
        if (_constants.ContainsKey(name))
        {
            return _constants[name].Type;
        }

        // If it's a function name, it will be handled by CallExpr
        if (_functions.ContainsKey(name))
        {
            return _functions[name].ReturnType;
        }

        return _variables[name].Type;
    }

    public override IrType? VisitMemberAccessExpr([NotNull] NovusParser.MemberAccessExprContext context)
    {
        var baseType = Visit(context.expression());
        if (baseType == null)
        {
            return null;
        }

        var memberName = context.IDENTIFIER().GetText();

        // Check if the base type is a struct
        if (baseType is not IrStructType structType)
        {
            var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0021",
                $"cannot access member '{memberName}' on non-struct type '{baseType.Name}'",
                location,
                helpTexts: new List<string>
                {
                    "member access is only valid on struct types"
                }
            );
            return null;
        }

        // Find the field
        var field = structType.GetField(memberName);
        if (field == null)
        {
            var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0022",
                $"struct '{structType.Name}' does not have a field named '{memberName}'",
                location,
                helpTexts: new List<string>
                {
                    $"available fields: {string.Join(", ", structType.Fields.Select(f => f.Name))}"
                }
            );
            return null;
        }

        return field.Type;
    }

    public override IrType? VisitAddressOfExpr([NotNull] NovusParser.AddressOfExprContext context)
    {
        var functionName = context.IDENTIFIER().GetText();

        // Check if function exists
        if (!_functions.ContainsKey(functionName))
        {
            var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0013",
                $"undefined function '{functionName}'",
                location,
                helpTexts: new List<string>
                {
                    "cannot take address of undefined function"
                }
            );
            return null;
        }

        var function = _functions[functionName];
        var paramTypes = function.Parameters.Select(p => p.Type).ToList();
        return new IrFunctionPointerType(paramTypes, function.ReturnType);
    }

    public override IrType? VisitComparisonExpr([NotNull] NovusParser.ComparisonExprContext context)
    {
        var leftType = Visit(context.expression(0));
        var rightType = Visit(context.expression(1));

        if (leftType == null || rightType == null)
            return IrBoolType.Instance;

        // Check that both operands are numeric types
        if (!IsNumericType(leftType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0004",
                $"cannot compare non-numeric type '{TypeToString(leftType)}'",
                location
            );
            return IrBoolType.Instance;
        }

        if (!IsNumericType(rightType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(1), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0004",
                $"cannot compare non-numeric type '{TypeToString(rightType)}'",
                location
            );
            return IrBoolType.Instance;
        }

        // Comparisons always return bool
        return IrBoolType.Instance;
    }

    public override IrType? VisitLogicalAndExpr([NotNull] NovusParser.LogicalAndExprContext context)
    {
        var leftType = Visit(context.expression(0));
        var rightType = Visit(context.expression(1));

        if (leftType == null || rightType == null)
            return IrBoolType.Instance;

        // Check that both operands are boolean or numeric types
        if (!IsBoolOrNumericType(leftType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0024",
                $"logical operator '&&' requires boolean or numeric type, found '{TypeToString(leftType)}'",
                location
            );
        }

        if (!IsBoolOrNumericType(rightType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(1), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0024",
                $"logical operator '&&' requires boolean or numeric type, found '{TypeToString(rightType)}'",
                location
            );
        }

        return IrBoolType.Instance;
    }

    public override IrType? VisitLogicalOrExpr([NotNull] NovusParser.LogicalOrExprContext context)
    {
        var leftType = Visit(context.expression(0));
        var rightType = Visit(context.expression(1));

        if (leftType == null || rightType == null)
            return IrBoolType.Instance;

        // Check that both operands are boolean or numeric types
        if (!IsBoolOrNumericType(leftType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0024",
                $"logical operator '||' requires boolean or numeric type, found '{TypeToString(leftType)}'",
                location
            );
        }

        if (!IsBoolOrNumericType(rightType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(1), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0024",
                $"logical operator '||' requires boolean or numeric type, found '{TypeToString(rightType)}'",
                location
            );
        }

        return IrBoolType.Instance;
    }

    public override IrType? VisitUnaryExpr([NotNull] NovusParser.UnaryExprContext context)
    {
        var operandType = Visit(context.expression());
        var op = context.GetChild(0).GetText();

        if (operandType == null)
            return IrIntType.I32;

        if (op == "!")
        {
            // Logical NOT: requires boolean or numeric type
            if (!IsBoolOrNumericType(operandType))
            {
                var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0024",
                    $"logical operator '!' requires boolean or numeric type, found '{TypeToString(operandType)}'",
                    location
                );
            }
            return IrBoolType.Instance;
        }
        else if (op == "~" || op == "-")
        {
            // Bitwise NOT and unary minus: require numeric type
            if (!IsNumericType(operandType))
            {
                var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0024",
                    $"unary operator '{op}' requires numeric type, found '{TypeToString(operandType)}'",
                    location
                );
            }
            return operandType;
        }

        throw new Exception($"Unknown unary operator: {op}");
    }

    public override IrType? VisitParenExpr([NotNull] NovusParser.ParenExprContext context)
    {
        return Visit(context.expression());
    }

    public override IrType? VisitPathExpr([NotNull] NovusParser.PathExprContext context)
    {
        // Handle enum variant path expressions like Option::Some, Result::Ok
        var baseExpr = context.expression();
        var variantName = context.IDENTIFIER().GetText();

        // The base expression should be a primary expression containing an identifier
        string? enumName = null;
        if (baseExpr is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            enumName = identCtx.IDENTIFIER().GetText();
        }

        if (enumName == null)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0032",
                "path expression base must be an enum type identifier",
                location,
                helpTexts: new List<string>
                {
                    "expected format: EnumName::VariantName"
                }
            );
            return null;
        }

        // Look up the enum type
        if (!_enums.ContainsKey(enumName))
        {
            var location = SourceLocationHelper.FromContext(baseExpr, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0033",
                $"'{enumName}' is not an enum type",
                location,
                helpTexts: new List<string>
                {
                    "path expressions can only be used with enum types"
                }
            );
            return null;
        }

        var enumType = _enums[enumName];

        // Check if the variant exists
        var variant = enumType.GetVariant(variantName);
        if (variant == null)
        {
            var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0034",
                $"enum '{enumName}' has no variant '{variantName}'",
                location,
                helpTexts: new List<string>
                {
                    $"available variants: {string.Join(", ", enumType.Variants.Select(v => v.Name))}"
                }
            );
            return null;
        }

        // Return the enum type - this will be used when constructing the variant
        return enumType;
    }

    public override IrType? VisitArrayLiteral([NotNull] NovusParser.ArrayLiteralContext context)
    {
        // Array literals - validate all elements have same type
        var expressions = context.expression();
        if (expressions.Length == 0)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError("E0999", "array literals cannot be empty", location);
            return null;
        }

        var firstType = Visit(expressions[0]);
        foreach (var expr in expressions.Skip(1))
        {
            var exprType = Visit(expr);
            // TODO: Check type compatibility
        }

        return new IrArrayType(firstType!, expressions.Length);
    }

    public override IrType? VisitStructLiteral([NotNull] NovusParser.StructLiteralContext context)
    {
        var structName = context.IDENTIFIER().GetText();

        // Check if struct type exists
        if (!_structs.ContainsKey(structName))
        {
            var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0023",
                $"unknown struct type '{structName}'",
                location,
                helpTexts: new List<string>
                {
                    "this struct has not been defined",
                    "consider defining a struct with this name"
                }
            );
            return null;
        }

        var structType = _structs[structName];
        var initializedFields = new HashSet<string>();

        // Validate field initializers
        foreach (var fieldInit in context.structFieldInit())
        {
            var fieldName = fieldInit.IDENTIFIER().GetText();

            // Check if field exists
            var field = structType.GetField(fieldName);
            if (field == null)
            {
                var location = SourceLocationHelper.FromToken(fieldInit.IDENTIFIER().Symbol, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0024",
                    $"struct '{structName}' does not have a field named '{fieldName}'",
                    location,
                    helpTexts: new List<string>
                    {
                        $"available fields: {string.Join(", ", structType.Fields.Select(f => f.Name))}"
                    }
                );
                continue;
            }

            // Check for duplicate initialization
            if (initializedFields.Contains(fieldName))
            {
                var location = SourceLocationHelper.FromToken(fieldInit.IDENTIFIER().Symbol, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0025",
                    $"field '{fieldName}' is initialized multiple times",
                    location,
                    helpTexts: new List<string>
                    {
                        "remove duplicate initialization"
                    }
                );
                continue;
            }

            initializedFields.Add(fieldName);

            // Validate field value type
            var fieldValueType = Visit(fieldInit.expression());
            if (fieldValueType != null && !TypesCompatible(field.Type, fieldValueType))
            {
                var location = SourceLocationHelper.FromContext(fieldInit.expression(), _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0011",
                    $"type mismatch: expected '{field.Type.Name}', got '{fieldValueType.Name}'",
                    location,
                    helpTexts: new List<string>
                    {
                        $"field '{fieldName}' expects type '{field.Type.Name}'"
                    }
                );
            }
        }

        // Check that all fields are initialized
        foreach (var field in structType.Fields)
        {
            if (!initializedFields.Contains(field.Name))
            {
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0026",
                    $"field '{field.Name}' of struct '{structName}' is not initialized",
                    location,
                    helpTexts: new List<string>
                    {
                        $"all struct fields must be initialized",
                        $"missing field: {field.Name}: {field.Type.Name}"
                    }
                );
            }
        }

        return structType;
    }

    private IrType ParseAndValidateIntegerLiteral(ParserRuleContext context, string text)
    {
        text = text.Replace("_", "");
        var isNegative = context.GetText().StartsWith("-");

        // Determine type from suffix
        IrType type;
        string numberText;

        if (text.EndsWith("u8"))
        {
            type = IrIntType.U8;
            numberText = text[..^2];
        }
        else if (text.EndsWith("u16"))
        {
            type = IrIntType.U16;
            numberText = text[..^3];
        }
        else if (text.EndsWith("u32"))
        {
            type = IrIntType.U32;
            numberText = text[..^3];
        }
        else if (text.EndsWith("u64"))
        {
            type = IrIntType.U64;
            numberText = text[..^3];
        }
        else if (text.EndsWith("i8"))
        {
            type = IrIntType.I8;
            numberText = text[..^2];
        }
        else if (text.EndsWith("i16"))
        {
            type = IrIntType.I16;
            numberText = text[..^3];
        }
        else if (text.EndsWith("i32"))
        {
            type = IrIntType.I32;
            numberText = text[..^3];
        }
        else if (text.EndsWith("i64"))
        {
            type = IrIntType.I64;
            numberText = text[..^3];
        }
        else
        {
            type = IrIntType.I32;
            numberText = text;
        }

        if (!long.TryParse(numberText, out var value))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0008",
                $"invalid integer literal '{context.GetText()}'",
                location
            );
            return type;
        }

        if (isNegative)
            value = -value;

        ValidateLiteralRange(context, value, type);
        return type;
    }

    private IrType ParseAndValidateBinaryLiteral(ParserRuleContext context, string text)
    {
        text = text[1..].Replace("_", ""); // Remove % prefix and underscores
        var (type, numberText) = ExtractTypeSuffix(text);

        try
        {
            var value = Convert.ToInt64(numberText, 2);
            if (context.GetText().StartsWith("-"))
                value = -value;
            ValidateLiteralRange(context, value, type);
        }
        catch (Exception)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0008",
                $"invalid binary literal '{context.GetText()}'",
                location
            );
        }

        return type;
    }

    private IrType ParseAndValidateHexLiteral(ParserRuleContext context, string text)
    {
        text = text[1..].Replace("_", ""); // Remove $ prefix and underscores
        var (type, numberText) = ExtractTypeSuffix(text);

        try
        {
            var value = Convert.ToInt64(numberText, 16);
            if (context.GetText().StartsWith("-"))
                value = -value;
            ValidateLiteralRange(context, value, type);
        }
        catch (Exception)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0008",
                $"invalid hexadecimal literal '{context.GetText()}'",
                location
            );
        }

        return type;
    }

    private (IrType type, string numberText) ExtractTypeSuffix(string text)
    {
        if (text.EndsWith("u8")) return (IrIntType.U8, text[..^2]);
        if (text.EndsWith("u16")) return (IrIntType.U16, text[..^3]);
        if (text.EndsWith("u32")) return (IrIntType.U32, text[..^3]);
        if (text.EndsWith("u64")) return (IrIntType.U64, text[..^3]);
        if (text.EndsWith("i8")) return (IrIntType.I8, text[..^2]);
        if (text.EndsWith("i16")) return (IrIntType.I16, text[..^3]);
        if (text.EndsWith("i32")) return (IrIntType.I32, text[..^3]);
        if (text.EndsWith("i64")) return (IrIntType.I64, text[..^3]);
        return (IrIntType.I32, text);
    }

    private void ValidateLiteralRange(ParserRuleContext context, long value, IrType type)
    {
        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);

        if (type is IrIntType intType)
        {
            var (min, max) = GetTypeRange(intType);

            if (value < min || value > max)
            {
                _diagnostics.ReportError(
                    "E0009",
                    $"literal value {value} is out of range for type '{TypeToString(type)}'",
                    location,
                    helpTexts: new List<string>
                    {
                        $"the range for '{TypeToString(type)}' is {min} to {max}",
                        "consider using a larger type or adjusting the value"
                    }
                );
            }
        }
    }

    private (long min, long max) GetTypeRange(IrIntType type)
    {
        return type.IsSigned switch
        {
            true when type.BitWidth == 8 => (sbyte.MinValue, sbyte.MaxValue),
            true when type.BitWidth == 16 => (short.MinValue, short.MaxValue),
            true when type.BitWidth == 32 => (int.MinValue, int.MaxValue),
            true when type.BitWidth == 64 => (long.MinValue, long.MaxValue),
            false when type.BitWidth == 8 => (0, byte.MaxValue),
            false when type.BitWidth == 16 => (0, ushort.MaxValue),
            false when type.BitWidth == 32 => (0, uint.MaxValue),
            false when type.BitWidth == 64 => (0, long.MaxValue), // Note: can't represent full ulong range
            _ => (long.MinValue, long.MaxValue)
        };
    }

    private IrType ParseType(NovusParser.TypeContext context)
    {
        return context switch
        {
            NovusParser.PointerTypeContext ptrCtx => ParsePointerType(ptrCtx),
            NovusParser.ArrayTypeContext arrayCtx => ParseArrayType(arrayCtx),
            NovusParser.FunctionPointerTypeContext fpCtx => ParseFunctionPointerType(fpCtx),
            NovusParser.PrimitiveTypeContext primCtx => ParsePrimitiveType(primCtx),
            NovusParser.NamedTypeContext namedCtx => ParseNamedType(namedCtx),
            _ => IrIntType.I32
        };
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

                // Validate number of type arguments matches generic parameters
                if (typeArgs.Count != enumType.GenericParameters.Count)
                {
                    var loc = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0031",
                        $"enum '{typeName}' expects {enumType.GenericParameters.Count} type arguments but got {typeArgs.Count}",
                        loc
                    );
                    return IrIntType.I32;
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
                var monomorphizedEnum = new IrEnumType(enumType.EnumName, monomorphizedVariants, null);
                return monomorphizedEnum;
            }

            return enumType;
        }

        // Unknown type - report error and return i32 as fallback
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
        _diagnostics.ReportError(
            "E0020",
            $"unknown type '{typeName}'",
            location,
            helpTexts: new List<string>
            {
                "this type has not been defined",
                "consider defining a struct, enum with this name or using a primitive type"
            }
        );
        return IrIntType.I32;
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
            _ => IrIntType.I32
        };
    }

    private bool TypesCompatible(IrType expected, IrType actual)
    {
        // Exact match
        if (expected.Equals(actual))
            return true;

        // Both are bool - exact match already handled above
        if (expected is IrBoolType || actual is IrBoolType)
        {
            // Bool types must match exactly
            return false;
        }

        // Pointer types - must point to compatible types
        if (expected is IrPointerType expectedPtr && actual is IrPointerType actualPtr)
        {
            return TypesCompatible(expectedPtr.PointeeType, actualPtr.PointeeType);
        }

        // Array types - must have same element type and length
        if (expected is IrArrayType expectedArray && actual is IrArrayType actualArray)
        {
            return expectedArray.Length == actualArray.Length &&
                   TypesCompatible(expectedArray.ElementType, actualArray.ElementType);
        }

        // Function pointer types - must have same parameter types and return type
        if (expected is IrFunctionPointerType expectedFp && actual is IrFunctionPointerType actualFp)
        {
            if (expectedFp.ParameterTypes.Count != actualFp.ParameterTypes.Count)
                return false;

            for (int i = 0; i < expectedFp.ParameterTypes.Count; i++)
            {
                if (!TypesCompatible(expectedFp.ParameterTypes[i], actualFp.ParameterTypes[i]))
                    return false;
            }

            return TypesCompatible(expectedFp.ReturnType, actualFp.ReturnType);
        }

        // Enum types - must have same name and variant structure
        if (expected is IrEnumType expectedEnum && actual is IrEnumType actualEnum)
        {
            // Same enum name
            if (expectedEnum.EnumName != actualEnum.EnumName)
                return false;

            // Same number of variants
            if (expectedEnum.Variants.Count != actualEnum.Variants.Count)
                return false;

            // Each variant must match
            for (int i = 0; i < expectedEnum.Variants.Count; i++)
            {
                var expVariant = expectedEnum.Variants[i];
                var actVariant = actualEnum.Variants[i];

                // Same variant name and tag
                if (expVariant.Name != actVariant.Name || expVariant.Tag != actVariant.Tag)
                    return false;

                // Same associated data count
                if (expVariant.AssociatedData.Count != actVariant.AssociatedData.Count)
                    return false;

                // Each associated data type must be compatible
                for (int j = 0; j < expVariant.AssociatedData.Count; j++)
                {
                    if (!TypesCompatible(expVariant.AssociatedData[j], actVariant.AssociatedData[j]))
                        return false;
                }
            }

            return true;
        }

        // Both are integers - allow safe implicit conversions
        if (expected is IrIntType expectedInt && actual is IrIntType actualInt)
        {
            // Same signedness and target is larger or equal
            if (expectedInt.IsSigned == actualInt.IsSigned &&
                expectedInt.BitWidth >= actualInt.BitWidth)
            {
                return true;
            }

            // Allow implicit conversion between signed/unsigned of same bit width
            // This is safe as it's just a reinterpretation of bits (e.g., u32 -> i32)
            // Common use case: passing unsigned flags to C APIs that take signed int
            if (expectedInt.BitWidth == actualInt.BitWidth)
            {
                return true;
            }

            // Allow default i32 literals to be compatible with any integer type
            // This matches common behavior where literal 42 can be u32, u8, i32, etc.
            // We already validate that the literal value fits in the range during literal validation
            if (actualInt == IrIntType.I32)
            {
                return true;
            }
        }

        // Both are floats - allow f32 to f64 widening
        if (expected is IrFloatType expectedFloat && actual is IrFloatType actualFloat)
        {
            // Same type or widening (f32 -> f64)
            if (expectedFloat.BitWidth >= actualFloat.BitWidth)
            {
                return true;
            }
        }

        // Both are fixed-point - allow fixed16 to fixed32 widening
        if (expected is IrFixedType expectedFixed && actual is IrFixedType actualFixed)
        {
            // Same type or widening (fixed16 -> fixed32)
            if (expectedFixed.BitWidth >= actualFixed.BitWidth)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsNumericType(IrType type)
    {
        return type is IrIntType || type is IrFloatType || type is IrFixedType;
    }

    private bool IsBoolOrNumericType(IrType type)
    {
        return type is IrBoolType || IsNumericType(type);
    }

    private bool IsMixedSignedness(IrType left, IrType right)
    {
        if (left is IrIntType leftInt && right is IrIntType rightInt)
        {
            return leftInt.IsSigned != rightInt.IsSigned;
        }
        return false;
    }

    private bool IsLossyCast(IrType from, IrType to)
    {
        if (from is IrIntType fromInt && to is IrIntType toInt)
        {
            // Casting to a smaller type is potentially lossy
            if (toInt.BitWidth < fromInt.BitWidth)
                return true;

            // Casting signed to unsigned or vice versa can be lossy
            if (fromInt.IsSigned != toInt.IsSigned)
                return true;
        }
        return false;
    }

    private string TypeToString(IrType type)
    {
        if (type is IrPointerType ptrType)
        {
            return $"*{TypeToString(ptrType.PointeeType)}";
        }
        if (type is IrFunctionPointerType fpType)
        {
            var paramStr = fpType.ParameterTypes.Count > 0
                ? string.Join(", ", fpType.ParameterTypes.Select(TypeToString))
                : "";
            var retStr = fpType.ReturnType is IrVoidType ? "" : $" -> {TypeToString(fpType.ReturnType)}";
            return $"fn({paramStr}){retStr}";
        }
        if (type is IrArrayType arrayType)
        {
            return $"[{arrayType.Length}]{TypeToString(arrayType.ElementType)}";
        }
        if (type is IrIntType intType)
        {
            var sign = intType.IsSigned ? "i" : "u";
            return $"{sign}{intType.BitWidth}";
        }
        if (type is IrBoolType)
        {
            return "bool";
        }
        if (type is IrVoidType)
        {
            return "void";
        }
        if (type is IrFloatType floatType)
        {
            return $"f{floatType.BitWidth}";
        }
        if (type is IrFixedType fixedType)
        {
            return $"fixed{fixedType.BitWidth}";
        }
        if (type is IrStructType structType)
        {
            return structType.Name;
        }
        if (type is IrEnumType enumType)
        {
            return enumType.Name;
        }
        if (type is IrGenericType genericType)
        {
            return genericType.ParameterName;
        }
        return "unknown";
    }
}

// Symbol table classes
public record FunctionSymbol(string Name, IrType ReturnType, List<ParameterSymbol> Parameters, SourceLocation Location, bool IsExtern = false);
public record ParameterSymbol(string Name, IrType Type, SourceLocation Location);
public record VariableSymbol(string Name, IrType Type, bool IsMutable, SourceLocation Location);
public record ConstantSymbol(string Name, IrType Type, object Value, SourceLocation Location);
