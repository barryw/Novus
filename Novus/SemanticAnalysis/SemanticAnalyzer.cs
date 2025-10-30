using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
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
    private readonly Dictionary<string, VariableSymbol> _globalVariables = new(); // Module-level extern vars
    private readonly Dictionary<string, IrStructType> _structs = new();
    private readonly Dictionary<string, IrEnumType> _enums = new();
    private readonly Dictionary<string, ConstantSymbol> _constants = new();
    private readonly Dictionary<string, string> _importedNames = new(); // Maps imported name -> module name
    private readonly HashSet<string> _importedModules = new(); // Track which modules have been imported (by path)
    private FunctionSymbol? _currentFunction;
    private int _loopDepth = 0; // Track loop nesting for break validation
    private readonly string _stdLibPath; // Path to standard library

    // Generic type parameters in scope (for generic enum/struct definitions)
    private readonly Dictionary<string, IrGenericType> _genericParams = new();

    // Cache for monomorphized generic enums (ensures same instance for same type)
    private readonly Dictionary<string, IrEnumType> _monomorphizedEnums = new();

    // Cache for monomorphized generic structs (ensures same instance for same type)
    private readonly Dictionary<string, IrStructType> _monomorphizedStructs = new();

    // Expected type for bidirectional type checking (flows down from context)
    private IrType? _expectedType = null;

    // Type interning system for efficient type equality
    private readonly TypeInterner _typeInterner = new();

    public DiagnosticBag Diagnostics => _diagnostics;

    public SemanticAnalyzer(string filePath, string sourceCode, string stdLibPath)
    {
        _filePath = filePath;
        _sourceLines = sourceCode.Split('\n');
        _stdLibPath = stdLibPath;
    }

    public bool Analyze(NovusParser.CompilationUnitContext context)
    {
        // Pass 0a: Implicitly import all of core module (unless compiling a std library module)
        // Don't auto-import std::core when compiling std library modules to prevent circular dependencies
        bool isStdLibraryModule = _filePath.Contains(System.IO.Path.DirectorySeparatorChar + "std" + System.IO.Path.DirectorySeparatorChar);

        if (!isStdLibraryModule)
        {
            ImportModule("std::core", importAll: true);
        }

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

        // Fourth pass: collect all global variable declarations
        foreach (var globalVarDecl in context.globalVariableDeclaration())
        {
            RegisterGlobalVariable(globalVarDecl);
        }

        // Fifth pass: collect all impl block methods
        foreach (var implDecl in context.implDeclaration())
        {
            RegisterImpl(implDecl);
        }

        // Sixth pass: collect all function declarations
        foreach (var funcDecl in context.functionDeclaration())
        {
            RegisterFunction(funcDecl);
        }

        // Seventh pass: analyze function bodies (including methods from impl blocks)
        foreach (var funcDecl in context.functionDeclaration())
        {
            Visit(funcDecl);
        }

        // Eighth pass: analyze impl block method bodies
        foreach (var implDecl in context.implDeclaration())
        {
            AnalyzeImplBlock(implDecl);
        }

        return !_diagnostics.HasErrors;
    }

    private void ProcessImport(NovusParser.ImportDeclarationContext context)
    {
        // Get the module path (e.g., "std::dos" or "std::ffi::exec")
        var moduleNamespace = context.modulePath().GetText();
        var location = SourceLocationHelper.FromToken(context.modulePath().Start, _filePath, _sourceLines);

        // Get the list of names to import
        var importList = context.importList();
        bool importAll = importList.GetText() == "*";

        ImportModule(moduleNamespace, importAll, importList, location);
    }

    private void ImportModuleSpecificSymbols(string moduleNamespace, List<string> symbolNames, SourceLocation? location = null)
    {
        // Import specific symbols from a module (for pub use reexports)
        foreach (var symbolName in symbolNames)
        {
            // Parse the module to get the symbols
            var pathParts = moduleNamespace.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
            string modulePath;
            if (pathParts[0] == "std")
            {
                var relativePath = string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), pathParts.Skip(1));
                modulePath = System.IO.Path.Combine(_stdLibPath, relativePath + ".novus");
            }
            else
            {
                var relativePath = string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), pathParts);
                modulePath = relativePath + ".novus";
            }

            if (!System.IO.File.Exists(modulePath))
            {
                _diagnostics.ReportError(
                    "E0026",
                    $"module '{moduleNamespace}' not found in reexport",
                    location
                );
                return;
            }

            var moduleSource = System.IO.File.ReadAllText(modulePath);
            var inputStream = new AntlrInputStream(moduleSource);
            var lexer = new NovusLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new NovusParser(tokenStream);
            var moduleContext = parser.compilationUnit();

            // Find and register the specific symbol
            // Check enums
            foreach (var enumDecl in moduleContext.enumDeclaration())
            {
                if (enumDecl.IDENTIFIER().GetText() == symbolName)
                {
                    RegisterEnum(enumDecl);
                    return; // Found it
                }
            }
            // Check structs
            foreach (var structDecl in moduleContext.structDeclaration())
            {
                if (structDecl.IDENTIFIER().GetText() == symbolName)
                {
                    RegisterStruct(structDecl);
                    return; // Found it
                }
            }
            // Check constants
            foreach (var constDecl in moduleContext.constDeclaration())
            {
                if (constDecl.IDENTIFIER().GetText() == symbolName)
                {
                    RegisterConstant(constDecl);
                    return; // Found it
                }
            }
        }
    }

    private void ImportModule(string moduleNamespace, bool importAll, NovusParser.ImportListContext? importList = null, SourceLocation? location = null)
    {
        // Use dummy location for implicit imports
        if (location == null)
        {
            location = new SourceLocation(_filePath, 0, 0, 0, "");
        }

        // Convert namespace path to file path
        // std::dos → std/dos.novus
        // std::ffi::exec → std/ffi/exec.novus
        var pathParts = moduleNamespace.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);

        if (pathParts.Length == 0)
        {
            _diagnostics.ReportError(
                "E0026",
                $"invalid module namespace: {moduleNamespace}",
                location
            );
            return;
        }

        // Build file path
        string modulePath;
        if (pathParts[0] == "std")
        {
            // std library module - relative to std lib path
            var relativePath = string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), pathParts.Skip(1));
            modulePath = System.IO.Path.Combine(_stdLibPath, relativePath + ".novus");
        }
        else
        {
            // User module (future: will use package resolution)
            var relativePath = string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), pathParts);
            modulePath = relativePath + ".novus";
        }

        if (!System.IO.File.Exists(modulePath))
        {
            _diagnostics.ReportError(
                "E0026",
                $"module '{moduleNamespace}' not found",
                location,
                helpTexts: new List<string>
                {
                    $"expected file at: {modulePath}",
                    "ensure the module file exists"
                }
            );
            return;
        }

        // Skip if this module has already been imported
        if (_importedModules.Contains(modulePath))
        {
            return;
        }

        // Mark this module as imported
        _importedModules.Add(modulePath);

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
                $"module '{moduleNamespace}' has syntax errors",
                location,
                helpTexts: new List<string>
                {
                    $"fix syntax errors in {modulePath}"
                }
            );
            return;
        }

        // Note: We DON'T process the module's own imports here (transitive dependencies)
        // Each module handles its own imports when it's compiled as a separate dependency
        // This prevents duplicate symbols and circular dependencies
        // Only process reexports, which are explicitly made public by the module

        // CRITICAL: Process pub use reexports, before parsing any function signatures
        // Function signatures may reference reexported types, so those types must be in scope
        foreach (var reexportDecl in moduleContext.reexportDeclaration())
        {
            var reexportPath = reexportDecl.modulePath().GetText();
            var text = reexportDecl.GetText();
            bool reexportAll = text.EndsWith("::*");

            if (reexportAll)
            {
                // pub use std::error::* - import all symbols
                ImportModule(reexportPath, importAll: true, importList: null, location);
            }
            else
            {
                // pub use std::error::DosError - import specific symbols
                var reexportList = reexportDecl.reexportList();
                if (reexportList != null)
                {
                    var symbolNames = new List<string>();
                    foreach (var id in reexportList.IDENTIFIER())
                    {
                        symbolNames.Add(id.GetText());
                    }
                    ImportModuleSpecificSymbols(reexportPath, symbolNames, location);
                }
            }
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

            // Import all extern global variables from the module
            foreach (var globalVarDecl in moduleContext.globalVariableDeclaration())
            {
                // All global variables are extern by definition
                namesToImport.Add(globalVarDecl.IDENTIFIER().GetText());
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
                    _importedNames[alias] = moduleNamespace;
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

            // Skip if this enum has already been imported (transitive dependencies)
            if (_enums.ContainsKey(enumName))
            {
                continue;
            }

            // Register the enum from the imported module
            RegisterEnum(enumDecl);
            _importedNames[enumName] = moduleNamespace;
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

            // Skip if this constant has already been imported (transitive dependencies)
            if (_constants.ContainsKey(constName))
            {
                continue;
            }

            // Register the constant from the imported module
            RegisterConstant(constDecl);
            _importedNames[constName] = moduleNamespace;
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

            // Skip if this struct has already been imported (transitive dependencies)
            if (_structs.ContainsKey(structName))
            {
                continue;
            }

            // Register the struct from the imported module
            RegisterStruct(structDecl);
            _importedNames[structName] = moduleNamespace;
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
                    $"cannot import private function '{funcName}' from module '{moduleNamespace}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "only pub or extern functions can be imported from modules"
                    }
                );
                continue;
            }

            // Skip if this function has already been imported (transitive dependencies)
            // This allows the same function to be imported through multiple paths without conflict
            if (_functions.ContainsKey(funcName))
            {
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
            _importedNames[funcName] = moduleNamespace;
        }

        // Register imported global variables in symbol table
        foreach (var globalVarDecl in moduleContext.globalVariableDeclaration())
        {
            var varName = globalVarDecl.IDENTIFIER().GetText();

            // Skip if not in the import list
            if (!namesToImport.Contains(varName))
            {
                continue;
            }

            // Check for duplicate names
            if (_globalVariables.ContainsKey(varName))
            {
                var originalLocation = _globalVariables[varName].Location;
                _diagnostics.ReportError(
                    "E0001",
                    $"global variable '{varName}' is defined multiple times",
                    location,
                    relatedLocations: new List<(SourceLocation, string)>
                    {
                        (originalLocation, $"previous definition of '{varName}' here")
                    }
                );
                continue;
            }

            var varType = ParseType(globalVarDecl.type());
            var varLocation = SourceLocationHelper.FromToken(globalVarDecl.IDENTIFIER().Symbol, modulePath, new string[] { });
            _globalVariables[varName] = new VariableSymbol(varName, varType, IsMutable: false, varLocation);
            _importedNames[varName] = moduleNamespace;
        }

        // Register all impl blocks from the module (methods are always imported with their types)
        foreach (var implDecl in moduleContext.implDeclaration())
        {
            RegisterImpl(implDecl);
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

    private void RegisterGlobalVariable(NovusParser.GlobalVariableDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
        var type = ParseType(context.type());

        // Global variables must always be extern (defined externally, e.g. in assembly)
        // Check for duplicate names
        if (_globalVariables.ContainsKey(name))
        {
            var originalLocation = _globalVariables[name].Location;
            _diagnostics.ReportError(
                "E0001",
                $"global variable '{name}' is defined multiple times",
                location,
                helpTexts: new List<string>
                {
                    $"consider renaming one of the variables"
                },
                relatedLocations: new List<(SourceLocation, string)>
                {
                    (originalLocation, $"previous definition of '{name}' here")
                }
            );
            return;
        }

        _globalVariables[name] = new VariableSymbol(name, type, IsMutable: false, location);
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

    private void RegisterImpl(NovusParser.ImplDeclarationContext context)
    {
        // Handle generic parameters if present (e.g., impl<T> Vec<T>)
        var genericParams = new List<string>();
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                var paramName = paramId.GetText();
                genericParams.Add(paramName);

                // Add to generic param scope for method parsing
                _genericParams[paramName] = new IrGenericType(paramName);
            }
        }

        // Get the type being implemented (e.g., Vec, Vec<T>, Point)
        var implTypeName = context.typeName().IDENTIFIER(0).GetText();

        // Register each method in the impl block
        foreach (var item in context.implItem())
        {
            if (item.functionDeclaration() != null)
            {
                RegisterImplMethod(item.functionDeclaration(), implTypeName, genericParams);
            }
        }

        // Clear generic params from scope after impl registration
        foreach (var paramName in genericParams)
        {
            _genericParams.Remove(paramName);
        }
    }

    private void RegisterImplMethod(NovusParser.FunctionDeclarationContext context, string implTypeName, List<string> genericParams)
    {
        var methodName = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Generate mangled name for the method: TypeName::methodName
        // For generic types, we'll need monomorphization later
        var mangledName = $"{implTypeName}::{methodName}";

        // Check for duplicate function names
        if (_functions.ContainsKey(mangledName))
        {
            var originalLocation = _functions[mangledName].Location;
            _diagnostics.ReportError(
                "E0001",
                $"method '{methodName}' for type '{implTypeName}' is defined multiple times",
                location,
                helpTexts: new List<string>
                {
                    $"consider renaming one of the methods or removing the duplicate"
                },
                relatedLocations: new List<(SourceLocation, string)>
                {
                    (originalLocation, $"previous definition of '{methodName}' here")
                }
            );
            return;
        }

        var returnType = context.type() != null ? ParseType(context.type()) : IrVoidType.Instance;
        var parameters = new List<ParameterSymbol>();

        // Parse parameters (including self parameter if present)
        if (context.parameterList() != null)
        {
            // Check for self parameter
            if (context.parameterList().selfParameter() != null)
            {
                var selfParam = context.parameterList().selfParameter();
                var selfLocation = SourceLocationHelper.FromToken(selfParam.KW_SELF().Symbol, _filePath, _sourceLines);

                // Determine self type based on parameter form
                IrType selfType;
                if (selfParam.GetText().StartsWith("&mut"))
                {
                    // &mut self
                    selfType = new IrPointerType(_structs.ContainsKey(implTypeName) ? _structs[implTypeName] : new IrGenericType(implTypeName));
                }
                else if (selfParam.GetText().StartsWith("&"))
                {
                    // &self (immutable reference - treat as pointer for now)
                    selfType = new IrPointerType(_structs.ContainsKey(implTypeName) ? _structs[implTypeName] : new IrGenericType(implTypeName));
                }
                else
                {
                    // self (by value)
                    selfType = _structs.ContainsKey(implTypeName) ? _structs[implTypeName] : new IrGenericType(implTypeName);
                }

                parameters.Add(new ParameterSymbol("self", selfType, selfLocation));
            }

            // Parse regular parameters
            foreach (var paramCtx in context.parameterList().parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                var paramType = ParseType(paramCtx.type());
                var paramLocation = SourceLocationHelper.FromToken(paramCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);
                parameters.Add(new ParameterSymbol(paramName, paramType, paramLocation));
            }
        }

        _functions[mangledName] = new FunctionSymbol(mangledName, returnType, parameters, location, false);
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

        // Handle generic parameters if present
        var genericParams = new List<string>();
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                var paramName = paramId.GetText();
                genericParams.Add(paramName);

                // Add to generic param scope for field parsing
                _genericParams[paramName] = new IrGenericType(paramName);
            }
        }

        // Parse struct fields
        var fields = new List<IrStructField>();
        foreach (var fieldCtx in context.structField())
        {
            var fieldName = fieldCtx.IDENTIFIER().GetText();
            var fieldType = ParseType(fieldCtx.type());
            fields.Add(new IrStructField(fieldName, fieldType));
        }

        // Clear generic params from scope after struct registration
        foreach (var paramName in genericParams)
        {
            _genericParams.Remove(paramName);
        }

        var structType = new IrStructType(name, fields, genericParams);

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

    private void AnalyzeImplBlock(NovusParser.ImplDeclarationContext context)
    {
        // Restore generic parameters to scope for method body analysis
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

        // Get the type being implemented
        var implTypeName = context.typeName().IDENTIFIER(0).GetText();

        // Analyze each method
        foreach (var item in context.implItem())
        {
            if (item.functionDeclaration() != null)
            {
                AnalyzeImplMethod(item.functionDeclaration(), implTypeName);
            }
        }

        // Clear generic params after analysis
        foreach (var paramName in genericParams)
        {
            _genericParams.Remove(paramName);
        }
    }

    private void AnalyzeImplMethod(NovusParser.FunctionDeclarationContext context, string implTypeName)
    {
        var methodName = context.IDENTIFIER().GetText();
        var mangledName = $"{implTypeName}::{methodName}";

        // Look up the method using the mangled name
        if (!_functions.ContainsKey(mangledName))
        {
            // This shouldn't happen if RegisterImpl worked correctly
            var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0051",
                $"internal error: method '{methodName}' not found in function table",
                location
            );
            return;
        }

        _currentFunction = _functions[mangledName];
        _variables.Clear();

        // Add parameters to symbol table (including self if present)
        foreach (var param in _currentFunction.Parameters)
        {
            _variables[param.Name] = new VariableSymbol(param.Name, param.Type, false, param.Location);
        }

        // Analyze function body with unreachable code detection
        if (context.block() != null)
        {
            AnalyzeBlock(context.block());
        }

        _currentFunction = null;
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

        if (_currentFunction == null)
        {
            _diagnostics.ReportError(
                "E0002",
                "return statement outside of function",
                location
            );
            return null;
        }

        // Set expected type for bidirectional type checking (enables type inference)
        var savedExpectedType = _expectedType;
        _expectedType = _currentFunction.ReturnType;

        var exprType = Visit(context.expression());

        // Restore previous expected type
        _expectedType = savedExpectedType;

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
        // Check if this is a throwaway binding (_)
        var identifierNode = context.IDENTIFIER();
        var name = identifierNode?.GetText() ?? "_";
        var isThrowaway = name == "_";
        var isMutable = context.GetChild(0)?.GetText() == "var";

        // For location, use identifier if present, otherwise use the first token (let/var)
        var location = identifierNode != null
            ? SourceLocationHelper.FromToken(identifierNode.Symbol, _filePath, _sourceLines)
            : SourceLocationHelper.FromToken(context.Start, _filePath, _sourceLines);

        // Skip duplicate check for throwaway bindings
        if (!isThrowaway && _variables.ContainsKey(name))
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

        // Add variable to symbol table (skip for throwaway bindings)
        if (!isThrowaway)
        {
            _variables[name] = new VariableSymbol(name, varType, isMutable, location);
        }

        return null;
    }

    public override IrType? VisitAssignmentStatement([NotNull] NovusParser.AssignmentStatementContext context)
    {
        // Get the identifier or 'self' keyword
        var identifier = context.IDENTIFIER();
        var selfKeyword = context.KW_SELF();

        string name;
        SourceLocation location;

        if (identifier != null)
        {
            name = identifier.GetText();
            location = SourceLocationHelper.FromToken(identifier.Symbol, _filePath, _sourceLines);
        }
        else if (selfKeyword != null)
        {
            name = "self";
            location = SourceLocationHelper.FromToken(selfKeyword.Symbol, _filePath, _sourceLines);
        }
        else
        {
            throw new Exception("Assignment statement must have either IDENTIFIER or KW_SELF");
        }

        // Detect which kind of assignment this is
        string? op = null;
        bool isPostIncDec = false;
        bool isPreIncDec = false;

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
                // Check if it's at the beginning (pre) or after identifier (post)
                if (i == 0)
                {
                    isPreIncDec = true;
                    op = childText;
                }
                else
                {
                    isPostIncDec = true;
                    op = childText;
                }
                break;
            }
        }

        // Count dereference operators before the identifier
        int derefCount = 0;
        for (int i = 0; i < context.ChildCount; i++)
        {
            if (context.GetChild(i).GetText() == "*")
            {
                derefCount++;
            }
            else if (context.GetChild(i) is ITerminalNode terminal && terminal.Symbol.Type == NovusLexer.IDENTIFIER)
            {
                break; // Stop at the first identifier
            }
        }

        var lvalueSuffixes = context.lvalueSuffix();

        // Handle increment/decrement statements (no expression)
        if (isPostIncDec || isPreIncDec)
        {
            // Check if variable exists
            if (!_variables.ContainsKey(name))
            {
                _diagnostics.ReportError(
                    "E0018",
                    $"cannot apply operator '{op}' to undeclared variable '{name}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "this variable has not been declared",
                        "consider declaring it with 'let' or 'var'"
                    }
                );
                return null;
            }

            var incDecVariable = _variables[name];

            // Check if variable is mutable
            if (!incDecVariable.IsMutable)
            {
                _diagnostics.ReportError(
                    "E0019",
                    $"cannot apply operator '{op}' to immutable variable '{name}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "this variable was declared with 'let', which makes it immutable",
                        "consider declaring it with 'var' if you need to modify it"
                    }
                );
                return null;
            }

            // Check that it's a numeric type
            if (!IsNumericType(incDecVariable.Type))
            {
                _diagnostics.ReportError(
                    "E0024",
                    $"operator '{op}' requires numeric type, found '{TypeToString(incDecVariable.Type)}'",
                    location
                );
            }

            return null;
        }

        // Check if this is a complex lvalue (member or index access)
        if (lvalueSuffixes.Length > 0)
        {
            // Complex lvalue: obj.field, arr[index], or mixed obj.arr[0].field
            // For now, we'll just verify the base variable exists
            // Full member/index chain checking will be implemented later
            if (!_variables.ContainsKey(name))
            {
                _diagnostics.ReportError(
                    "E0018",
                    $"cannot assign to member/element of undeclared variable '{name}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "this variable has not been declared",
                        "consider declaring it with 'let' or 'var'"
                    }
                );
                return null;
            }
            // TODO: Validate lvalue suffix chain and types
            var valueType = Visit(context.expression());
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

        if (derefCount > 0)
        {
            // Dereference assignment: *x = value or **x = value, etc.

            // Dereference the variable type to get the target type
            IrType targetType = variable.Type;
            for (int i = 0; i < derefCount; i++)
            {
                if (targetType is IrPointerType ptrType)
                {
                    targetType = ptrType.PointeeType;
                }
                else if (targetType is IrMutReferenceType mutRefType)
                {
                    targetType = mutRefType.PointeeType;
                }
                else if (targetType is IrReferenceType refType)
                {
                    // Trying to assign through immutable reference
                    _diagnostics.ReportError(
                        "E0026",
                        $"cannot assign through immutable reference",
                        location,
                        helpTexts: new List<string>
                        {
                            $"'{name}' is an immutable reference (&{TypeToString(refType.PointeeType)})",
                            "consider using a mutable reference (&mut) if you need to modify the value"
                        }
                    );
                    return null;
                }
                else
                {
                    _diagnostics.ReportError(
                        "E0025",
                        $"cannot dereference non-pointer/reference type",
                        location,
                        helpTexts: new List<string>
                        {
                            $"'{name}' has type '{TypeToString(targetType)}', which cannot be dereferenced"
                        }
                    );
                    return null;
                }
            }

            // Check type compatibility of the assigned value
            var exprType = Visit(context.expression());
            if (exprType != null && !TypesCompatible(targetType, exprType))
            {
                _diagnostics.ReportError(
                    "E0020",
                    $"mismatched types in assignment",
                    location,
                    helpTexts: new List<string>
                    {
                        $"expected type '{TypeToString(targetType)}', found '{TypeToString(exprType)}'"
                    }
                );
            }
        }
        else
        {
            // Simple variable assignment (no dereferences)

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
            var exprType = Visit(context.expression());

            // Handle compound operators
            if (op != "=")
            {
                // For compound operators, verify the variable type supports the operation
                if (op == "+=" || op == "-=" || op == "*=" || op == "/=" || op == "%=")
                {
                    // Arithmetic operators require numeric types
                    if (!IsNumericType(variable.Type))
                    {
                        _diagnostics.ReportError(
                            "E0024",
                            $"operator '{op}' requires numeric type, found '{TypeToString(variable.Type)}'",
                            location
                        );
                    }
                    if (exprType != null && !IsNumericType(exprType))
                    {
                        var exprLocation = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0024",
                            $"operator '{op}' requires numeric type, found '{TypeToString(exprType)}'",
                            exprLocation
                        );
                    }
                }
                else if (op == "&=" || op == "|=" || op == "^=")
                {
                    // Bitwise operators require integer or boolean types
                    if (!IsIntegralType(variable.Type) && !(variable.Type is IrBoolType))
                    {
                        _diagnostics.ReportError(
                            "E0024",
                            $"operator '{op}' requires integer or boolean type, found '{TypeToString(variable.Type)}'",
                            location
                        );
                    }
                    if (exprType != null && !IsIntegralType(exprType) && !(exprType is IrBoolType))
                    {
                        var exprLocation = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0024",
                            $"operator '{op}' requires integer or boolean type, found '{TypeToString(exprType)}'",
                            exprLocation
                        );
                    }
                }
                else if (op == "<<=" || op == ">>=")
                {
                    // Shift operators require integer types
                    if (!IsIntegralType(variable.Type))
                    {
                        _diagnostics.ReportError(
                            "E0024",
                            $"operator '{op}' requires integer type, found '{TypeToString(variable.Type)}'",
                            location
                        );
                    }
                    if (exprType != null && !IsIntegralType(exprType))
                    {
                        var exprLocation = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0024",
                            $"operator '{op}' requires integer type, found '{TypeToString(exprType)}'",
                            exprLocation
                        );
                    }
                }
            }
            else
            {
                // Simple assignment: check type compatibility
                if (exprType != null && !TypesCompatible(variable.Type, exprType))
                {
                    _diagnostics.ReportError(
                        "E0020",
                        $"mismatched types in assignment",
                        location,
                        helpTexts: new List<string>
                        {
                            $"expected type '{TypeToString(variable.Type)}', found '{TypeToString(exprType)}'",
                            $"consider using a cast: ({TypeToString(variable.Type)}){context.expression().GetText()}"
                        }
                    );
                }
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

        var op = context.GetChild(1).GetText();

        // Handle pointer arithmetic: ptr + offset or ptr - offset
        if (leftType is IrPointerType && IsNumericType(rightType))
        {
            // ptr + offset or ptr - offset => ptr
            return leftType;
        }

        // Handle pointer difference: ptr - ptr => integer
        if (op == "-" && leftType is IrPointerType && rightType is IrPointerType)
        {
            // ptr - ptr => u32 (byte difference)
            return new IrIntType(32, false);
        }

        // Check that both operands are numeric types
        if (!IsNumericType(leftType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0004",
                $"cannot apply operator '{op}' to non-numeric type '{TypeToString(leftType)}'",
                location
            );
            return null;
        }

        if (!IsNumericType(rightType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(1), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0004",
                $"cannot apply operator '{op}' to non-numeric type '{TypeToString(rightType)}'",
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

    public override IrType? VisitShiftLeftExpr([NotNull] NovusParser.ShiftLeftExprContext context)
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

    public override IrType? VisitShiftRightExpr([NotNull] NovusParser.ShiftRightExprContext context)
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
        // Allow: numeric -> numeric, pointer -> integer, integer -> pointer
        bool isValidCast = (IsNumericType(targetType) && IsNumericType(exprType)) ||
                           (IsNumericType(targetType) && exprType is IrPointerType) ||
                           (targetType is IrPointerType && IsNumericType(exprType));

        if (!isValidCast)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0006",
                $"cannot cast from '{TypeToString(exprType)}' to '{TypeToString(targetType)}'",
                location,
                helpTexts: new List<string>
                {
                    "only numeric types and pointers to integers can be cast"
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

    public override IrType? VisitDeferStatement([NotNull] NovusParser.DeferStatementContext context)
    {
        // Analyze the deferred block
        // Variables captured in defer have their values at the time defer executes (end of scope)
        // not at the time defer is registered
        AnalyzeBlock(context.block());
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
                // Extract the last identifier from the qualified name (e.g., SimpleResult::Ok -> Ok)
                var variantNameCtx = variantPattern.variantName();
                var identifiers = variantNameCtx.IDENTIFIER();
                var variantName = identifiers[identifiers.Length - 1].GetText();

                // Check if this variant exists
                var variant = enumType.GetVariant(variantName);
                if (variant == null)
                {
                    var location = SourceLocationHelper.FromToken(variantPattern.variantName().Start, _filePath, _sourceLines);
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
                    var location = SourceLocationHelper.FromToken(variantPattern.variantName().Start, _filePath, _sourceLines);
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

            case NovusParser.SimpleVariantPatternContext simpleVariantPattern:
            {
                // SimpleVariantPattern is IDENTIFIER '::' IDENTIFIER ('::' IDENTIFIER)*
                // Extract the last identifier from the qualified name (e.g., SimpleResult::Ok -> Ok)
                var identifiers = simpleVariantPattern.IDENTIFIER();
                var variantName = identifiers[identifiers.Length - 1].GetText();

                var variant = enumType.GetVariant(variantName);
                if (variant == null)
                {
                    var location = SourceLocationHelper.FromToken(simpleVariantPattern.Start, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0037",
                        $"enum '{enumType.EnumName}' has no variant '{variantName}'",
                        location
                    );
                    return;
                }

                // Simple variant pattern should only be used for variants without data
                if (variant.HasAssociatedData)
                {
                    var location = SourceLocationHelper.FromToken(simpleVariantPattern.Start, _filePath, _sourceLines);
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

        // Handle method calls (e.g., v.len())
        // Method calls are CallExpr with MemberAccessExpr as the function expression
        if (funcExpr is NovusParser.MemberAccessExprContext memberAccessCtx)
        {
            return HandleMethodCall(context, memberAccessCtx);
        }

        // Handle path expressions (enum constructors or associated functions)
        if (funcExpr is NovusParser.PathExprContext pathCtx)
        {
            // Visit the path expression to get the type
            var resultType = Visit(pathCtx);

            if (resultType == null)
            {
                // Error already reported by VisitPathExpr
                return null;
            }

            // Handle enum constructors
            if (resultType is IrEnumType enumType)
            {

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

                    // FIRST: Extract type substitutions from expected type (if available)
                    // This enables nested generic constructors to work correctly
                    if (_expectedType is IrEnumType expectedEnumType &&
                        expectedEnumType.EnumName == irEnumType.EnumName &&
                        expectedEnumType.GenericParameters.Count == 0) // Expected type is monomorphized
                    {
                        // Build a mapping from generic parameters to concrete types by comparing variants
                        for (int paramIdx = 0; paramIdx < irEnumType.GenericParameters.Count; paramIdx++)
                        {
                            var paramName = irEnumType.GenericParameters[paramIdx];

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

                    // Special case: If this is a unit variant (no arguments) and we have an expected type
                    // that's fully monomorphized, use it directly (no need to extract type parameters)
                    if ((context.argumentList() == null || context.argumentList().expression().Length == 0) &&
                        _expectedType is IrEnumType expectedEnumType2 &&
                        expectedEnumType2.EnumName == irEnumType.EnumName &&
                        expectedEnumType2.GenericParameters.Count == 0)
                    {
                        // Return the expected type directly - it's already fully monomorphized
                        return expectedEnumType2;
                    }

                    if (context.argumentList() != null)
                    {
                        var arguments = context.argumentList().expression();

                        for (int i = 0; i < Math.Min(arguments.Length, variant.AssociatedData.Count); i++)
                        {
                            var expectedParamType = variant.AssociatedData[i];

                            // Compute the concrete expected type for this argument by applying substitutions
                            IrType? concreteExpectedType = expectedParamType;
                            if (expectedParamType is IrGenericType gt && typeSubstitutions.ContainsKey(gt.ParameterName))
                            {
                                concreteExpectedType = typeSubstitutions[gt.ParameterName];
                            }

                            // Set expected type for bidirectional type checking before visiting argument
                            var savedExpectedType = _expectedType;
                            _expectedType = concreteExpectedType;

                            // Visit the argument with the expected type context
                            var argType = Visit(arguments[i]);

                            // Restore previous expected type
                            _expectedType = savedExpectedType;

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
                }

                // If we inferred type parameters, create a monomorphized instance
                if (typeSubstitutions != null && typeSubstitutions.Count > 0)
                {
                    // Create cache key: EnumName<TypeArg1CacheKey,TypeArg2CacheKey,...>
                    // Use GetTypeCacheKey to handle nested types correctly
                    var typeArgKeys = irEnumType.GenericParameters.Select(p =>
                        typeSubstitutions.ContainsKey(p) ? GetTypeCacheKey(typeSubstitutions[p]) : p);
                    var cacheKey = $"{irEnumType.EnumName}<{string.Join(",", typeArgKeys)}>";

                    // Check cache first
                    if (_monomorphizedEnums.ContainsKey(cacheKey))
                    {
                        return _monomorphizedEnums[cacheKey];
                    }


                    // Create monomorphized enum type
                    var monomorphizedVariants = new List<IrEnumVariant>();
                    foreach (var origVariant in irEnumType.Variants)
                    {
                        var monomorphizedData = new List<IrType>();
                        foreach (var dataType in origVariant.AssociatedData)
                        {
                            if (dataType is IrGenericType gt && typeSubstitutions.ContainsKey(gt.ParameterName))
                            {
                                var substitutedType = typeSubstitutions[gt.ParameterName];
                                monomorphizedData.Add(substitutedType);
                            }
                            else
                            {
                                monomorphizedData.Add(dataType);
                            }
                        }
                        monomorphizedVariants.Add(new IrEnumVariant(origVariant.Name, origVariant.Tag, monomorphizedData));
                    }

                    // Create new enum type with concrete types (no generic parameters)
                    var monomorphizedEnum = new IrEnumType(irEnumType.EnumName, monomorphizedVariants, null, cacheKey);

                    // Cache it for future use
                    _monomorphizedEnums[cacheKey] = monomorphizedEnum;

                    return monomorphizedEnum;
                }
            }

            return enumType;
            }
            else
            {
                // Handle associated function calls (e.g., Vec::new())
                // resultType is the function's return type
                // If the return type is generic and we have an expected type, use the expected type
                if (_expectedType != null && TypesCompatible(resultType, _expectedType))
                {
                    // Return the expected type for generic inference
                    // e.g., Vec::new() with expected type Vec<i32> returns Vec<i32>
                    return _expectedType;
                }

                // No expected type or not compatible - return the function's declared return type
                return resultType;
            }
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

        var functionName = identifierExpr.identifier().GetText();
        var argCount = context.argumentList()?.expression().Length ?? 0;

        // Check if this is a qualified enum constructor (e.g., Result::Ok)
        if (functionName.Contains("::"))
        {
            var parts = functionName.Split("::");
            if (parts.Length == 2)
            {
                var enumName = parts[0];
                var variantName = parts[1];

                if (_enums.ContainsKey(enumName))
                {
                    var enumType = _enums[enumName];
                    var variant = enumType.GetVariant(variantName);

                    if (variant == null)
                    {
                        var location = SourceLocationHelper.FromToken(identifierExpr.identifier().Start, _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0037",
                            $"enum '{enumName}' has no variant '{variantName}'",
                            location
                        );
                        return null;
                    }

                    // Validate argument count
                    if (argCount != variant.AssociatedData.Count)
                    {
                        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0040",
                            $"variant '{variantName}' expects {variant.AssociatedData.Count} argument(s), but {argCount} were provided",
                            location
                        );
                        return enumType;
                    }

                    // Perform generic type inference and validate argument types
                    Dictionary<string, IrType>? typeSubstitutions = null;

                    if (enumType.GenericParameters.Count > 0)
                    {
                        // Infer generic type parameters from arguments
                        typeSubstitutions = new Dictionary<string, IrType>();

                        // FIRST: Extract type substitutions from expected type (if available)
                        // This enables nested generic constructors to work correctly
                        if (_expectedType is IrEnumType expectedEnumType &&
                            expectedEnumType.EnumName == enumType.EnumName &&
                            expectedEnumType.GenericParameters.Count == 0) // Expected type is monomorphized
                        {
                            // Build a mapping from generic parameters to concrete types by comparing variants
                            for (int paramIdx = 0; paramIdx < enumType.GenericParameters.Count; paramIdx++)
                            {
                                var paramName = enumType.GenericParameters[paramIdx];

                                // Look through all variants to find one that uses this parameter
                                bool found = false;
                                for (int varIdx = 0; varIdx < enumType.Variants.Count && !found; varIdx++)
                                {
                                    var origVariant = enumType.Variants[varIdx];
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

                        if (context.argumentList() != null)
                        {
                            var arguments = context.argumentList().expression();

                            for (int i = 0; i < Math.Min(arguments.Length, variant.AssociatedData.Count); i++)
                            {
                                var expectedParamType = variant.AssociatedData[i];

                                // Compute the concrete expected type for this argument by applying substitutions
                                IrType? concreteExpectedType = expectedParamType;
                                if (expectedParamType is IrGenericType gt && typeSubstitutions.ContainsKey(gt.ParameterName))
                                {
                                    concreteExpectedType = typeSubstitutions[gt.ParameterName];
                                }

                                // Set expected type for bidirectional type checking before visiting argument
                                var savedExpectedType = _expectedType;
                                _expectedType = concreteExpectedType;

                                // Visit the argument with the expected type context
                                var argType = Visit(arguments[i]);

                                // Restore previous expected type
                                _expectedType = savedExpectedType;

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
                    }
                    else
                    {
                        // Non-generic enum - just validate argument types
                        if (context.argumentList() != null)
                        {
                            var arguments = context.argumentList().expression();
                            for (int i = 0; i < arguments.Length; i++)
                            {
                                var argType = Visit(arguments[i]);
                                var expectedType = variant.AssociatedData[i];

                                if (argType != null && !TypesCompatible(expectedType, argType))
                                {
                                    var location = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);
                                    _diagnostics.ReportError(
                                        "E0015",
                                        $"argument {i + 1} type mismatch for variant '{variantName}'",
                                        location,
                                        helpTexts: new List<string>
                                        {
                                            $"expected '{TypeToString(expectedType)}', got '{TypeToString(argType)}'"
                                        }
                                    );
                                }
                            }
                        }
                    }

                    // If we inferred type parameters, create a monomorphized instance
                    if (typeSubstitutions != null && typeSubstitutions.Count > 0)
                    {
                        // Create cache key: EnumName<TypeArg1CacheKey,TypeArg2CacheKey,...>
                        // Use GetTypeCacheKey to handle nested types correctly
                        var typeArgKeys = enumType.GenericParameters.Select(p =>
                            typeSubstitutions.ContainsKey(p) ? GetTypeCacheKey(typeSubstitutions[p]) : p);
                        var cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";

                        // Check cache first
                        if (_monomorphizedEnums.ContainsKey(cacheKey))
                        {
                            return _monomorphizedEnums[cacheKey];
                        }

                        // Create monomorphized enum type
                        var monomorphizedVariants = new List<IrEnumVariant>();
                        foreach (var origVariant in enumType.Variants)
                        {
                            var monomorphizedData = new List<IrType>();
                            foreach (var dataType in origVariant.AssociatedData)
                            {
                                if (dataType is IrGenericType gt && typeSubstitutions.ContainsKey(gt.ParameterName))
                                {
                                    var substitutedType = typeSubstitutions[gt.ParameterName];
                                    monomorphizedData.Add(substitutedType);
                                }
                                else
                                {
                                    monomorphizedData.Add(dataType);
                                }
                            }
                            monomorphizedVariants.Add(new IrEnumVariant(origVariant.Name, origVariant.Tag, monomorphizedData));
                        }

                        // Create new enum type with concrete types (no generic parameters)
                        var monomorphizedEnum = new IrEnumType(enumType.EnumName, monomorphizedVariants, null, cacheKey);

                        // Cache it for future use
                        _monomorphizedEnums[cacheKey] = monomorphizedEnum;

                        return monomorphizedEnum;
                    }

                    // Return the enum type
                    return enumType;
                }
            }
        }

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
            var location = SourceLocationHelper.FromToken(identifierExpr.identifier().Start, _filePath, _sourceLines);
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

    /// <summary>
    /// Handle method calls (e.g., v.len())
    /// Desugars: v.len() → Type::len(&v)
    /// </summary>
    private IrType? HandleMethodCall(NovusParser.CallExprContext callCtx, NovusParser.MemberAccessExprContext memberAccessCtx)
    {
        // Get the receiver (the thing before the dot)
        var receiverExpr = memberAccessCtx.expression();
        var methodName = memberAccessCtx.IDENTIFIER().GetText();

        // Evaluate the receiver to get its type
        var receiverType = Visit(receiverExpr);
        if (receiverType == null)
        {
            return null;
        }

        // Get the base type name (struct/enum name)
        string typeName;
        if (receiverType is IrStructType structType)
        {
            typeName = structType.Name;
        }
        else if (receiverType is IrEnumType enumType)
        {
            typeName = enumType.EnumName;
        }
        else if (receiverType is IrPointerType ptrType)
        {
            // Auto-dereference pointers
            if (ptrType.PointeeType is IrStructType pointeeStruct)
            {
                typeName = pointeeStruct.Name;
            }
            else if (ptrType.PointeeType is IrEnumType pointeeEnum)
            {
                typeName = pointeeEnum.EnumName;
            }
            else
            {
                var location = SourceLocationHelper.FromContext(memberAccessCtx, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0052",
                    $"cannot call method on type '{receiverType.Name}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "methods can only be called on struct or enum types"
                    }
                );
                return null;
            }
        }
        else
        {
            var location = SourceLocationHelper.FromContext(memberAccessCtx, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0052",
                $"cannot call method on type '{receiverType.Name}'",
                location,
                helpTexts: new List<string>
                {
                    "methods can only be called on struct or enum types"
                }
            );
            return null;
        }

        // Look up the method using the mangled name: Type::method
        var mangledMethodName = $"{typeName}::{methodName}";

        if (!_functions.ContainsKey(mangledMethodName))
        {
            var location = SourceLocationHelper.FromContext(memberAccessCtx, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0053",
                $"no method named '{methodName}' found for type '{typeName}'",
                location,
                helpTexts: new List<string>
                {
                    $"consider implementing this method in an impl block: impl {typeName} {{ fn {methodName}(...) {{ ... }} }}"
                }
            );
            return null;
        }

        var method = _functions[mangledMethodName];

        // Build type substitution map for generic methods
        var typeSubstitutions = new Dictionary<string, IrType>();
        if (receiverType is IrStructType receiverStruct && receiverStruct.CacheKey != null)
        {
            // Receiver is a monomorphized struct (e.g., Vec<i32>)
            // Get the base generic struct to find generic parameter names
            var baseStruct = _structs[receiverStruct.StructName];
            if (baseStruct.GenericParameters.Count > 0)
            {
                // Extract type arguments from the monomorphized struct fields
                // For now, we'll match them based on field positions
                for (int i = 0; i < baseStruct.GenericParameters.Count && i < baseStruct.Fields.Count; i++)
                {
                    var genericParam = baseStruct.GenericParameters[i];
                    var baseFieldType = baseStruct.Fields[i].Type;
                    var monomorphizedFieldType = receiverStruct.Fields[i].Type;

                    // If base field is generic type T, map T to the monomorphized type
                    if (baseFieldType is IrGenericType gt)
                    {
                        typeSubstitutions[gt.ParameterName] = monomorphizedFieldType;
                    }
                    // If base field is *T, extract T from monomorphized *i32
                    else if (baseFieldType is IrPointerType basePtrType && basePtrType.PointeeType is IrGenericType ptrGt)
                    {
                        if (monomorphizedFieldType is IrPointerType monoPtrType)
                        {
                            typeSubstitutions[ptrGt.ParameterName] = monoPtrType.PointeeType;
                        }
                    }
                }
            }
        }

        // Check if method has a self parameter
        bool hasSelfParam = method.Parameters.Count > 0 && method.Parameters[0].Name == "self";

        // Count the arguments (excluding self, which we'll add)
        var providedArgCount = callCtx.argumentList()?.expression().Length ?? 0;
        var expectedArgCount = hasSelfParam ? method.Parameters.Count - 1 : method.Parameters.Count;

        if (providedArgCount != expectedArgCount)
        {
            var location = SourceLocationHelper.FromContext(callCtx, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0014",
                $"method '{methodName}' expects {expectedArgCount} argument(s), but {providedArgCount} were provided",
                location
            );
            return method.ReturnType;
        }

        // Validate argument types (skip self parameter)
        if (callCtx.argumentList() != null)
        {
            var arguments = callCtx.argumentList().expression();
            var paramStartIndex = hasSelfParam ? 1 : 0;

            for (int i = 0; i < arguments.Length; i++)
            {
                var argType = Visit(arguments[i]);
                var paramType = method.Parameters[paramStartIndex + i].Type;

                // Substitute generic types in parameter type
                if (paramType is IrGenericType genericParam && typeSubstitutions.ContainsKey(genericParam.ParameterName))
                {
                    paramType = typeSubstitutions[genericParam.ParameterName];
                }

                if (argType != null && !TypesCompatible(paramType, argType))
                {
                    var location = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0015",
                        $"mismatched types in method call",
                        location,
                        helpTexts: new List<string>
                        {
                            $"argument {i + 1} ('{method.Parameters[paramStartIndex + i].Name}'): expected '{TypeToString(paramType)}', found '{TypeToString(argType)}'"
                        }
                    );
                }
            }
        }

        return method.ReturnType;
    }

    public override IrType? VisitBoolLiteral([NotNull] NovusParser.BoolLiteralContext context)
    {
        return IrBoolType.Instance;
    }

    public override IrType? VisitStringLiteral([NotNull] NovusParser.StringLiteralContext context)
    {
        // String literals are String type (fat pointer: {ptr: *u8, len: i32})
        // This is safer than raw *u8 and matches Rust/Swift string semantics
        // To get the raw pointer for FFI, use string_literal.ptr
        return IrStringType.Instance;
    }

    public override IrType? VisitSizeofExpr([NotNull] NovusParser.SizeofExprContext context)
    {
        // @sizeof(Type) returns a u32 representing the size in bytes
        var typeCtx = context.type();
        var targetType = ParseType(typeCtx);

        if (targetType == null)
        {
            var location = new SourceLocation(_filePath,
                typeCtx.Start.Line, typeCtx.Start.Column, 0, "");
            _diagnostics.ReportError("E0054",
                $"could not determine type for @sizeof",
                location);
            return IrIntType.U32;
        }

        // The result is always u32
        return IrIntType.U32;
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

    public override IrType? VisitSelfExpr([NotNull] NovusParser.SelfExprContext context)
    {
        // Look up 'self' in the variable table
        if (_variables.ContainsKey("self"))
        {
            return _variables["self"].Type;
        }

        var location = SourceLocationHelper.FromToken(context.KW_SELF().Symbol, _filePath, _sourceLines);
        _diagnostics.ReportError(
            "E0050",
            "'self' is only valid within method bodies",
            location,
            helpTexts: new List<string>
            {
                "'self' can only be used inside methods defined in impl blocks"
            }
        );
        return null;
    }

    public override IrType? VisitIdentifierExpr([NotNull] NovusParser.IdentifierExprContext context)
    {
        var name = context.identifier().GetText();

        // Check if this is a qualified name (e.g., Result::Ok, Option::Some)
        if (name.Contains("::"))
        {
            var parts = name.Split("::");
            if (parts.Length == 2)
            {
                var enumName = parts[0];
                var variantName = parts[1];

                // Check if the enum exists
                if (_enums.ContainsKey(enumName))
                {
                    var enumType = _enums[enumName];
                    var variant = enumType.GetVariant(variantName);

                    if (variant == null)
                    {
                        var location = SourceLocationHelper.FromToken(context.identifier().Start, _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0037",
                            $"enum '{enumName}' has no variant '{variantName}'",
                            location
                        );
                        return null;
                    }

                    // Return the enum type - this will be used when called as a constructor
                    return enumType;
                }
            }
        }

        if (!_variables.ContainsKey(name) && !_globalVariables.ContainsKey(name) && !_functions.ContainsKey(name) && !_constants.ContainsKey(name))
        {
            var location = SourceLocationHelper.FromToken(context.identifier().Start, _filePath, _sourceLines);
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

        // Check global variables
        if (_globalVariables.ContainsKey(name))
        {
            return _globalVariables[name].Type;
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

        // Auto-dereference pointers (like Rust/Swift)
        // This allows `&self` to work like `self` for member access
        if (baseType is IrPointerType ptrType)
        {
            baseType = ptrType.PointeeType;
        }
        else if (baseType is IrReferenceType refType)
        {
            baseType = refType.PointeeType;
        }
        else if (baseType is IrMutReferenceType mutRefType)
        {
            baseType = mutRefType.PointeeType;
        }

        // Handle String type member access (.ptr and .len)
        if (baseType is IrStringType)
        {
            if (memberName == "ptr")
            {
                return _typeInterner.GetPointerType(IrIntType.U8);
            }
            else if (memberName == "len")
            {
                return IrIntType.I32;
            }
            else
            {
                var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0022",
                    $"String type does not have a field named '{memberName}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "available fields: ptr, len"
                    }
                );
                return null;
            }
        }

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

    public override IrType? VisitBorrowExpr([NotNull] NovusParser.BorrowExprContext context)
    {
        var exprContext = context.expression();
        bool isMutable = context.GetChild(1)?.GetText() == "mut";

        // Check if this is a simple identifier (for function pointers)
        if (exprContext.Start.Type == NovusLexer.IDENTIFIER &&
            exprContext.ChildCount == 1)
        {
            var name = exprContext.GetText();

            // Check if it's a function (for function pointers)
            if (_functions.ContainsKey(name))
            {
                var function = _functions[name];
                var paramTypes = function.Parameters.Select(p => p.Type).ToList();
                return _typeInterner.GetFunctionPointerType(paramTypes, function.ReturnType);
            }
        }

        // For variables, struct fields, etc., create a reference type
        var valueType = Visit(exprContext);
        if (valueType == null)
        {
            return null;
        }

        // Return the appropriate reference type
        return isMutable
            ? (IrType)_typeInterner.GetMutReferenceType(valueType)
            : _typeInterner.GetReferenceType(valueType);
    }

    public override IrType? VisitComparisonExpr([NotNull] NovusParser.ComparisonExprContext context)
    {
        var leftType = Visit(context.expression(0));
        var rightType = Visit(context.expression(1));

        if (leftType == null || rightType == null)
            return IrBoolType.Instance;

        var op = context.GetChild(1).GetText(); // Get the comparison operator

        // Enum types can only be compared with == and !=
        if (leftType is IrEnumType || rightType is IrEnumType)
        {
            if (op != "==" && op != "!=")
            {
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0004",
                    $"enum types can only be compared with == and !=, not '{op}'",
                    location
                );
                return IrBoolType.Instance;
            }

            // Both sides must be the same enum type
            if (leftType is IrEnumType leftEnum && rightType is IrEnumType rightEnum)
            {
                if (leftEnum.Name != rightEnum.Name)
                {
                    var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0004",
                        $"cannot compare different enum types '{TypeToString(leftType)}' and '{TypeToString(rightType)}'",
                        location
                    );
                }
            }

            return IrBoolType.Instance;
        }

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
        var op = context.GetChild(0).GetText();

        // Handle dereference specially
        if (op == "*")
        {
            var derefOperandType = Visit(context.expression());
            if (derefOperandType == null)
                return IrIntType.I32;

            // Check if it's a pointer or reference type
            if (derefOperandType is IrPointerType ptrType)
            {
                return ptrType.PointeeType;
            }
            else if (derefOperandType is IrReferenceType refType)
            {
                return refType.PointeeType;
            }
            else if (derefOperandType is IrMutReferenceType mutRefType)
            {
                return mutRefType.PointeeType;
            }
            else
            {
                var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0025",
                    $"cannot dereference non-pointer/reference type '{TypeToString(derefOperandType)}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "only pointers (*T) and references (&T, &mut T) can be dereferenced"
                    }
                );
                return IrIntType.I32; // Fallback
            }
        }

        // For other operators, visit operand first
        var operandType = Visit(context.expression());
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

    public override IrType? VisitPostIncrementExpr([NotNull] NovusParser.PostIncrementExprContext context)
    {
        var operandType = Visit(context.expression());
        if (operandType == null)
            return IrIntType.I32;

        // Verify it's a numeric type
        if (!IsNumericType(operandType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0024",
                $"operator '++' requires numeric type, found '{TypeToString(operandType)}'",
                location
            );
        }

        // Verify it's an lvalue (for now, just check it's an identifier)
        if (!(context.expression() is NovusParser.PrimaryExprContext primaryCtx &&
              primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext))
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0027",
                "operator '++' requires an lvalue",
                location,
                helpTexts: new List<string>
                {
                    "only variables, not arbitrary expressions, can be incremented"
                }
            );
        }

        return operandType;
    }

    public override IrType? VisitPostDecrementExpr([NotNull] NovusParser.PostDecrementExprContext context)
    {
        var operandType = Visit(context.expression());
        if (operandType == null)
            return IrIntType.I32;

        // Verify it's a numeric type
        if (!IsNumericType(operandType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0024",
                $"operator '--' requires numeric type, found '{TypeToString(operandType)}'",
                location
            );
        }

        // Verify it's an lvalue (for now, just check it's an identifier)
        if (!(context.expression() is NovusParser.PrimaryExprContext primaryCtx &&
              primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext))
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0027",
                "operator '--' requires an lvalue",
                location,
                helpTexts: new List<string>
                {
                    "only variables, not arbitrary expressions, can be decremented"
                }
            );
        }

        return operandType;
    }

    public override IrType? VisitPreIncrementExpr([NotNull] NovusParser.PreIncrementExprContext context)
    {
        var operandType = Visit(context.expression());
        if (operandType == null)
            return IrIntType.I32;

        // Verify it's a numeric type
        if (!IsNumericType(operandType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0024",
                $"operator '++' requires numeric type, found '{TypeToString(operandType)}'",
                location
            );
        }

        // Verify it's an lvalue (for now, just check it's an identifier)
        if (!(context.expression() is NovusParser.PrimaryExprContext primaryCtx &&
              primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext))
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0027",
                "operator '++' requires an lvalue",
                location,
                helpTexts: new List<string>
                {
                    "only variables, not arbitrary expressions, can be incremented"
                }
            );
        }

        return operandType;
    }

    public override IrType? VisitPreDecrementExpr([NotNull] NovusParser.PreDecrementExprContext context)
    {
        var operandType = Visit(context.expression());
        if (operandType == null)
            return IrIntType.I32;

        // Verify it's a numeric type
        if (!IsNumericType(operandType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0024",
                $"operator '--' requires numeric type, found '{TypeToString(operandType)}'",
                location
            );
        }

        // Verify it's an lvalue (for now, just check it's an identifier)
        if (!(context.expression() is NovusParser.PrimaryExprContext primaryCtx &&
              primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext))
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0027",
                "operator '--' requires an lvalue",
                location,
                helpTexts: new List<string>
                {
                    "only variables, not arbitrary expressions, can be decremented"
                }
            );
        }

        return operandType;
    }

    public override IrType? VisitParenExpr([NotNull] NovusParser.ParenExprContext context)
    {
        return Visit(context.expression());
    }

    public override IrType? VisitPathExpr([NotNull] NovusParser.PathExprContext context)
    {
        // Handle path expressions: Type::name
        // This can be:
        // 1. Enum variants: Option::Some, Result::Ok
        // 2. Associated functions (static methods): Vec::new, Vec::with_capacity
        var baseExpr = context.expression();
        var memberName = context.IDENTIFIER().GetText();

        // The base expression should be a primary expression containing an identifier
        string? typeName = null;
        if (baseExpr is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            typeName = identCtx.identifier().GetText();
        }

        if (typeName == null)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0032",
                "path expression base must be a type identifier",
                location,
                helpTexts: new List<string>
                {
                    "expected format: TypeName::member"
                }
            );
            return null;
        }

        // Try enum variant first
        if (_enums.ContainsKey(typeName))
        {
            var enumType = _enums[typeName];

            // Check if the variant exists
            var variant = enumType.GetVariant(memberName);
            if (variant == null)
            {
                var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0034",
                    $"enum '{typeName}' has no variant '{memberName}'",
                    location,
                    helpTexts: new List<string>
                    {
                        $"available variants: {string.Join(", ", enumType.Variants.Select(v => v.Name))}"
                    }
                );
                return null;
            }

            // If we have an expected type that's a monomorphized version of this enum,
            // and this is a unit variant (no associated data), use the expected type
            if (variant.AssociatedData.Count == 0 &&
                _expectedType is IrEnumType expectedEnumType &&
                expectedEnumType.EnumName == enumType.EnumName &&
                expectedEnumType.GenericParameters.Count == 0)
            {
                // Return the expected monomorphized type
                return expectedEnumType;
            }

            // Return the enum type - this will be used when constructing the variant
            return enumType;
        }

        // Try associated function (struct method without self parameter)
        var mangledName = $"{typeName}::{memberName}";

        if (_functions.ContainsKey(mangledName))
        {
            var funcSymbol = _functions[mangledName];

            // Check if this is an associated function (no self parameter)
            var hasSelf = funcSymbol.Parameters.Count > 0 && funcSymbol.Parameters[0].Name == "self";

            if (hasSelf)
            {
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0035",
                    $"cannot call method '{memberName}' of type '{typeName}' without an instance (it requires 'self')",
                    location,
                    helpTexts: new List<string>
                    {
                        "use an instance: let v = ...; v.method()",
                        "or create an instance first"
                    }
                );
                return null;
            }

            // Return the function's return type
            // This is a special marker that indicates "this is an associated function reference"
            return funcSymbol.ReturnType;
        }

        // Type not found or member doesn't exist
        var errorLocation = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
        if (_structs.ContainsKey(typeName))
        {
            _diagnostics.ReportError(
                "E0036",
                $"type '{typeName}' has no associated function or method '{memberName}'",
                errorLocation,
                helpTexts: new List<string>
                {
                    "check the spelling of the function name",
                    "make sure the function is marked 'pub' if imported"
                }
            );
        }
        else
        {
            _diagnostics.ReportError(
                "E0033",
                $"type '{typeName}' not found",
                errorLocation,
                helpTexts: new List<string>
                {
                    "path expressions require a valid type name",
                    "expected an enum, struct, or other type"
                }
            );
        }
        return null;
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

        return _typeInterner.GetArrayType(firstType!, expressions.Length);
    }

    public override IrType? VisitStructLiteral([NotNull] NovusParser.StructLiteralContext context)
    {
        var structName = context.typeName().GetText();

        // Check if struct type exists
        if (!_structs.ContainsKey(structName))
        {
            var location = SourceLocationHelper.FromContext(context.typeName(), _filePath, _sourceLines);
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

        // Use expected type for bidirectional type checking if it's a monomorphized version of this struct
        IrStructType structType;
        if (_expectedType is IrStructType expectedStruct && expectedStruct.StructName == structName)
        {
            // Use the expected monomorphized type (e.g., Vec<i32>)
            structType = expectedStruct;
        }
        else
        {
            // Use the base generic type (e.g., Vec<T>)
            structType = _structs[structName];
        }
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
            NovusParser.ReferenceTypeContext refCtx => ParseReferenceType(refCtx),
            NovusParser.PointerTypeContext ptrCtx => ParsePointerType(ptrCtx),
            NovusParser.ArrayTypeContext arrayCtx => ParseArrayType(arrayCtx),
            NovusParser.FunctionPointerTypeContext fpCtx => ParseFunctionPointerType(fpCtx),
            NovusParser.PrimitiveTypeContext primCtx => ParsePrimitiveType(primCtx),
            NovusParser.NamedTypeContext namedCtx => ParseNamedType(namedCtx),
            _ => IrIntType.I32
        };
    }

    private IrType ParseReferenceType(NovusParser.ReferenceTypeContext context)
    {
        var pointeeType = ParseType(context.type());

        // Check if this is a mutable reference (&mut T) or immutable reference (&T)
        bool isMutable = context.GetChild(1)?.GetText() == "mut";

        return isMutable
            ? _typeInterner.GetMutReferenceType(pointeeType)
            : _typeInterner.GetReferenceType(pointeeType);
    }

    private IrType ParsePointerType(NovusParser.PointerTypeContext context)
    {
        var pointeeType = ParseType(context.type());
        return _typeInterner.GetPointerType(pointeeType);
    }

    private IrType ParseNamedType(NovusParser.NamedTypeContext context)
    {
        var typeName = context.typeName().GetText();

        // Check if it's a generic type parameter (T, E, etc.)
        if (_genericParams.ContainsKey(typeName))
        {
            return _genericParams[typeName];
        }

        // Check if it's a struct type
        if (_structs.ContainsKey(typeName))
        {
            var structType = _structs[typeName];

            // Handle generic instantiation (e.g., Vec<i32>)
            if (context.typeList() != null)
            {
                var typeArgs = new List<IrType>();
                foreach (var typeCtx in context.typeList().type())
                {
                    typeArgs.Add(ParseType(typeCtx));
                }

                // Validate number of type arguments matches generic parameters
                if (typeArgs.Count != structType.GenericParameters.Count)
                {
                    var loc = SourceLocationHelper.FromToken(context.typeName().Start, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0031",
                        $"struct '{typeName}' expects {structType.GenericParameters.Count} type arguments but got {typeArgs.Count}",
                        loc
                    );
                    return IrIntType.I32;
                }

                // If any type argument is still generic (IrGenericType), return the original generic struct
                // This happens when parsing types in generic context (e.g., Vec<T> in fn new() -> Vec<T>)
                if (typeArgs.Any(t => t is IrGenericType))
                {
                    return structType;
                }

                // Create cache key: StructName<TypeArg1CacheKey,TypeArg2CacheKey,...>
                var cacheKey = $"{structType.StructName}<{string.Join(",", typeArgs.Select(t => GetTypeCacheKey(t)))}>";

                // Check cache first
                if (_monomorphizedStructs.ContainsKey(cacheKey))
                {
                    return _monomorphizedStructs[cacheKey];
                }

                // Create monomorphized struct with concrete types
                var typeSubstitutions = new Dictionary<string, IrType>();
                for (int i = 0; i < structType.GenericParameters.Count; i++)
                {
                    typeSubstitutions[structType.GenericParameters[i]] = typeArgs[i];
                }

                // Create monomorphized fields
                var monomorphizedFields = new List<IrStructField>();
                foreach (var origField in structType.Fields)
                {
                    var fieldType = origField.Type;

                    // Substitute generic types in field
                    if (fieldType is IrGenericType gt && typeSubstitutions.ContainsKey(gt.ParameterName))
                    {
                        fieldType = typeSubstitutions[gt.ParameterName];
                    }
                    // Handle nested generic types (e.g., *T where T is generic)
                    else if (fieldType is IrPointerType ptrType && ptrType.PointeeType is IrGenericType ptrGt && typeSubstitutions.ContainsKey(ptrGt.ParameterName))
                    {
                        fieldType = _typeInterner.GetPointerType(typeSubstitutions[ptrGt.ParameterName]);
                    }

                    monomorphizedFields.Add(new IrStructField(origField.Name, fieldType));
                }

                // Create new struct type with concrete types (no generic parameters)
                var monomorphizedStruct = new IrStructType(structType.StructName, monomorphizedFields, null, cacheKey);

                // Cache it for future use
                _monomorphizedStructs[cacheKey] = monomorphizedStruct;

                return monomorphizedStruct;
            }

            return structType;
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
                    var loc = SourceLocationHelper.FromToken(context.typeName().Start, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0031",
                        $"enum '{typeName}' expects {enumType.GenericParameters.Count} type arguments but got {typeArgs.Count}",
                        loc
                    );
                    return IrIntType.I32;
                }

                // If any type argument is still generic (IrGenericType), return the original generic enum
                // This happens when parsing types in generic context (e.g., Option<T> in a generic function)
                if (typeArgs.Any(t => t is IrGenericType))
                {
                    return enumType;
                }

                // Create cache key: EnumName<TypeArg1CacheKey,TypeArg2CacheKey,...>
                // Use GetTypeCacheKey to handle nested types correctly
                var cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgs.Select(t => GetTypeCacheKey(t)))}>";
                // Check cache first
                if (_monomorphizedEnums.ContainsKey(cacheKey))
                {
                    return _monomorphizedEnums[cacheKey];
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
                var monomorphizedEnum = new IrEnumType(enumType.EnumName, monomorphizedVariants, null, cacheKey);

                // Cache it for future use
                _monomorphizedEnums[cacheKey] = monomorphizedEnum;

                return monomorphizedEnum;
            }

            return enumType;
        }

        // Unknown type - report error and return i32 as fallback
        var location = SourceLocationHelper.FromToken(context.typeName().Start, _filePath, _sourceLines);
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
        return _typeInterner.GetArrayType(elementType, size);
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

        return _typeInterner.GetFunctionPointerType(paramTypes, returnType);
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
            "String" => IrStringType.Instance,
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

        // Mutable reference types - must reference compatible types
        if (expected is IrMutReferenceType expectedMutRef && actual is IrMutReferenceType actualMutRef)
        {
            return TypesCompatible(expectedMutRef.PointeeType, actualMutRef.PointeeType);
        }

        // Immutable reference types - must reference compatible types
        if (expected is IrReferenceType expectedRef && actual is IrReferenceType actualRef)
        {
            return TypesCompatible(expectedRef.PointeeType, actualRef.PointeeType);
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
            {
                return false;
            }

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
                    var expData = expVariant.AssociatedData[j];
                    var actData = actVariant.AssociatedData[j];
                    if (!TypesCompatible(expData, actData))
                    {
                        return false;
                    }
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

        // Allow String to i32 conversion for FFI interop
        // Automatically extracts the .ptr field when passing String to functions expecting i32
        if (expected is IrIntType && actual is IrStringType)
        {
            return true;
        }

        // Struct types - allow generic to concrete matching (e.g., Vec<T> can match Vec<i32>)
        if (expected is IrStructType expectedStruct && actual is IrStructType actualStruct)
        {
            // Same struct name
            if (expectedStruct.StructName != actualStruct.StructName)
            {
                return false;
            }

            // If actual type is generic (has generic parameters), allow matching with concrete expected type
            // This handles the case where Vec::new() returns Vec<T>, but we expect Vec<i32>
            if (actualStruct.GenericParameters.Count > 0 && expectedStruct.GenericParameters.Count == 0)
            {
                // Actual is generic (Vec<T>), expected is concrete (Vec<i32>) - compatible!
                return true;
            }

            // If expected type is generic and actual is concrete, also allow
            // This handles the reverse case (less common but possible)
            if (expectedStruct.GenericParameters.Count > 0 && actualStruct.GenericParameters.Count == 0)
            {
                // Expected is generic (Vec<T>), actual is concrete (Vec<i32>) - compatible!
                return true;
            }

            // Both are concrete or both are generic - must match exactly (handled by Equals above)
            return false;
        }

        return false;
    }

    private bool IsNumericType(IrType type)
    {
        return type is IrIntType || type is IrFloatType || type is IrFixedType;
    }

    private bool IsIntegralType(IrType type)
    {
        return type is IrIntType;
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

    private string GetTypeCacheKey(IrType type)
    {
        // Recursively build a cache key for a type, handling nested generics
        if (type is IrEnumType enumType)
        {
            if (enumType.GenericParameters.Count > 0)
            {
                // Still generic - include parameter names
                return $"{enumType.EnumName}<{string.Join(",", enumType.GenericParameters)}>";
            }
            else
            {
                // Monomorphized enum - use stored cache key if available
                if (enumType.CacheKey != null)
                {
                    return enumType.CacheKey;
                }
                // Fallback to just the enum name (shouldn't happen with proper implementation)
                return enumType.EnumName;
            }
        }
        else if (type is IrGenericType gt)
        {
            return gt.ParameterName;
        }
        else
        {
            return type.Name;
        }
    }

    private string TypeToString(IrType type)
    {
        if (type is IrMutReferenceType mutRefType)
        {
            return $"&mut {TypeToString(mutRefType.PointeeType)}";
        }
        if (type is IrReferenceType refType)
        {
            return $"&{TypeToString(refType.PointeeType)}";
        }
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
