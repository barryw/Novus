using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Novus.Diagnostics;
using Novus.Frontend;
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
    private readonly Dictionary<string, IrTrait> _traits = new();
    private readonly Dictionary<string, ConstantSymbol> _constants = new();
    private readonly Dictionary<string, string> _importedNames = new(); // Maps imported name -> module name
    private readonly HashSet<string> _importedModules = new(); // Track which modules have been imported (by path)
    private FunctionSymbol? _currentFunction;
    private int _loopDepth = 0; // Track loop nesting for break validation
    private readonly string _stdLibPath; // Path to standard library

    // Unsafe block tracking
    private int _unsafeDepth = 0; // Track unsafe block nesting
    private readonly List<UnsafeBlockInfo> _unsafeBlocks = new(); // Collect unsafe blocks for warnings

    public class UnsafeBlockInfo
    {
        public string FilePath { get; set; } = "";
        public int Line { get; set; }
        public int Column { get; set; }
        public int LineCount { get; set; }
        public string Reason { get; set; } = "";
    }

    public IReadOnlyList<UnsafeBlockInfo> UnsafeBlocks => _unsafeBlocks;

    // Generic type parameters in scope (for generic enum/struct definitions)
    private readonly Dictionary<string, IrGenericType> _genericParams = new();

    // Cache for monomorphized generic enums (ensures same instance for same type)
    private readonly Dictionary<string, IrEnumType> _monomorphizedEnums = new();

    // Cache for monomorphized generic structs (ensures same instance for same type)
    private readonly Dictionary<string, IrStructType> _monomorphizedStructs = new();

    // Cache for monomorphized generic functions (ensures same instance for same signature)
    private readonly Dictionary<string, FunctionSymbol> _monomorphizedFunctions = new();

    // Track trait implementations: key = "TypeName::TraitName<TypeArg1,TypeArg2,...>"
    // This allows us to check if a type implements a trait during constraint validation
    private readonly Dictionary<string, TraitImplInfo> _traitImpls = new();

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

        // Pass 1.5: collect all static variable declarations
        foreach (var staticDecl in context.staticDeclaration())
        {
            RegisterStatic(staticDecl);
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

        // 3.5 pass: collect all trait declarations
        foreach (var traitDecl in context.traitDeclaration())
        {
            RegisterTrait(traitDecl);
        }

        // Fourth pass: collect all extern variable declarations
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
            string modulePath = ModuleImportHelper.ResolveModulePath(moduleNamespace, _stdLibPath);
            var (moduleContext, syntaxErrors) = ModuleImportHelper.ParseModuleFile(modulePath);

            if (moduleContext == null || syntaxErrors > 0)
            {
                _diagnostics.ReportError(
                    "E0026",
                    $"module '{moduleNamespace}' not found in reexport",
                    location
                );
                return;
            }

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
        string modulePath = ModuleImportHelper.ResolveModulePath(moduleNamespace, _stdLibPath);

        // Load and parse the module
        var (moduleContext, syntaxErrors) = ModuleImportHelper.ParseModuleFile(modulePath);

        if (moduleContext == null)
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

        if (syntaxErrors > 0)
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

        // Check for circular imports before processing
        // This is different from checking if already imported - circular imports are an error
        if (_importedModules.Contains(modulePath))
        {
            // Module already imported - skip to avoid duplicate processing and circular dependencies
            return;
        }

        // Mark this module as imported
        _importedModules.Add(modulePath);

        // Process the module's imports to make types available for analyzing its declarations
        // This is safe because _importedModules prevents circular dependencies
        // When we import a module, we need to process its imports so that types used in
        // function signatures, struct fields, etc. are available
        foreach (var importDecl in moduleContext.importDeclaration())
        {
            ProcessImport(importDecl);
        }

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
        var namesToImport = ModuleImportHelper.BuildImportNameSet(moduleContext, importAll, importList);

        // Handle import aliases (import Printf as MyPrintf)
        if (!importAll && importList != null)
        {
            foreach (var importNameCtx in importList.importName())
            {
                if (importNameCtx.IDENTIFIER().Length > 1)
                {
                    var alias = importNameCtx.IDENTIFIER(1).GetText();
                    _importedNames[alias] = moduleNamespace;
                }
            }
        }

        // Register imported enums using two-pass approach
        // Pass 1: Register stub enum types for ALL enums in the module (even non-imported)
        // This allows forward references between enums (e.g., NovusError referencing ExecError)
        var enumStubsToCleanup = new List<string>();
        foreach (var enumDecl in moduleContext.enumDeclaration())
        {
            var enumName = enumDecl.IDENTIFIER().GetText();

            // Skip if this enum has already been imported (transitive dependencies)
            if (_enums.ContainsKey(enumName))
            {
                continue;
            }

            // Register a stub enum type with no variants yet
            // This makes the type name resolvable during variant parsing and trait impl type arg parsing
            var stubEnum = new IrEnumType(enumName, new List<IrEnumVariant>(), null);
            _enums[enumName] = stubEnum;

            // Track stubs that aren't in the import list so we can remove them later
            if (!namesToImport.Contains(enumName))
            {
                enumStubsToCleanup.Add(enumName);
            }
        }

        // Pass 2: Fill in enum variants for imported enums only
        foreach (var enumDecl in moduleContext.enumDeclaration())
        {
            var enumName = enumDecl.IDENTIFIER().GetText();

            // Skip if not in the import list
            if (!namesToImport.Contains(enumName))
            {
                continue;
            }

            // Now register the full enum with variants (replacing the stub)
            // At this point, all enum names are resolvable for variant type parsing
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

        // CRITICAL: Register ALL pub structs from the module BEFORE registering impl blocks
        // This is necessary because impl block methods may reference other structs from the same module
        // For example, MemoryBlock::resize returns bool, but other methods might return Allocation<T>
        // Even if we only import MemoryBlock, we need Allocation<T> and Box<T> available for type checking
        foreach (var structDecl in moduleContext.structDeclaration())
        {
            var structName = structDecl.IDENTIFIER().GetText();

            // Skip private structs
            if (!ModuleImportHelper.IsPub(structDecl))
            {
                continue;
            }

            // Skip if this struct has already been imported (transitive dependencies)
            if (_structs.ContainsKey(structName))
            {
                continue;
            }

            // Register ALL pub structs from the module (not just explicitly imported ones)
            // This ensures types are available when parsing impl block method signatures
            RegisterStruct(structDecl);

            // Only mark as imported if it was explicitly requested
            if (namesToImport.Contains(structName))
            {
                _importedNames[structName] = moduleNamespace;
            }
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
            var (isPub, isExtern) = ModuleImportHelper.GetFunctionVisibility(funcDecl);

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

            bool hasVariadic = false;
            if (funcDecl.parameterList() != null)
            {
                var paramList = funcDecl.parameterList();

                foreach (var paramCtx in paramList.parameter())
                {
                    var paramName = paramCtx.IDENTIFIER().GetText();
                    var paramType = ParseType(paramCtx.type());
                    var paramLocation = SourceLocationHelper.FromToken(paramCtx.IDENTIFIER().Symbol, modulePath, new string[] { });
                    parameters.Add(new ParameterSymbol(paramName, paramType, paramLocation));
                }

                // Add variadic parameter if present
                if (paramList.variadicParameter() != null)
                {
                    var variadicCtx = paramList.variadicParameter();
                    var variadicName = variadicCtx.IDENTIFIER().GetText();
                    var variadicLocation = SourceLocationHelper.FromToken(variadicCtx.IDENTIFIER().Symbol, modulePath, new string[] { });
                    // Variadic parameters have void* type for semantic analysis
                    var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                    parameters.Add(new ParameterSymbol(variadicName, variadicType, variadicLocation, IsVariadic: true));
                    hasVariadic = true;
                }
            }

            // Register the function as extern
            var funcLocation = SourceLocationHelper.FromToken(funcDecl.IDENTIFIER().Symbol, modulePath, new string[] { });
            _functions[funcName] = new FunctionSymbol(funcName, returnType, parameters, funcLocation, IsExtern: true, IsVariadic: hasVariadic);
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

        // Register imported traits from the module
        foreach (var traitDecl in moduleContext.traitDeclaration())
        {
            var traitName = traitDecl.IDENTIFIER().GetText();

            // Skip if not in the import list
            if (!namesToImport.Contains(traitName))
            {
                continue;
            }

            // Check if trait is pub
            bool isPub = traitDecl.KW_PUB() != null;

            if (!isPub)
            {
                _diagnostics.ReportError(
                    "E0029",
                    $"cannot import private trait '{traitName}' from module '{moduleNamespace}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "only pub traits can be imported from modules"
                    }
                );
                continue;
            }

            // Register the trait from the imported module
            RegisterTrait(traitDecl);
            _importedNames[traitName] = moduleNamespace;
        }

        // Register all impl blocks from the module (methods are always imported with their types)
        foreach (var implDecl in moduleContext.implDeclaration())
        {
            RegisterImpl(implDecl);
        }

        // Clean up stub enum types that weren't actually imported
        // This happens at the very end, after all parsing (enums, structs, traits, impls) is complete
        foreach (var stubName in enumStubsToCleanup)
        {
            _enums.Remove(stubName);
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

    private void RegisterStatic(NovusParser.StaticDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
        var type = ParseType(context.type());

        // Check for mut keyword
        var isMutable = false;
        for (int i = 0; i < Math.Min(5, context.ChildCount); i++)
        {
            if (context.GetChild(i)?.GetText() == "mut")
            {
                isMutable = true;
                break;
            }
        }

        // Check for duplicate names
        if (_globalVariables.ContainsKey(name))
        {
            var originalLocation = _globalVariables[name].Location;
            _diagnostics.ReportError(
                "E0001",
                $"static variable '{name}' is defined multiple times",
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

        _globalVariables[name] = new VariableSymbol(name, type, IsMutable: isMutable, location);
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

        // Parse attributes
        var attributes = ParseAttributes(context.attribute());

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

        // Handle generic parameters if present (e.g., fn identity<T>(x: T) -> T)
        var genericParams = new List<string>();
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                var paramName = paramId.GetText();
                genericParams.Add(paramName);

                // Add to generic param scope for parameter/return type parsing
                _genericParams[paramName] = new IrGenericType(paramName);
            }
        }

        var returnType = context.type() != null ? ParseType(context.type()) : IrVoidType.Instance;
        var parameters = new List<ParameterSymbol>();

        bool hasVariadic = false;
        if (context.parameterList() != null)
        {
            var paramList = context.parameterList();

            foreach (var paramCtx in paramList.parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                var paramType = ParseType(paramCtx.type());
                var paramLocation = SourceLocationHelper.FromToken(paramCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);
                parameters.Add(new ParameterSymbol(paramName, paramType, paramLocation));
            }

            // Add variadic parameter if present
            if (paramList.variadicParameter() != null)
            {
                var variadicCtx = paramList.variadicParameter();
                var variadicName = variadicCtx.IDENTIFIER().GetText();
                var variadicLocation = SourceLocationHelper.FromToken(variadicCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);
                // Variadic parameters have void* type for semantic analysis
                var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                parameters.Add(new ParameterSymbol(variadicName, variadicType, variadicLocation, IsVariadic: true));
                hasVariadic = true;
            }
        }

        _functions[name] = new FunctionSymbol(name, returnType, parameters, location, isExtern, genericParams.Count > 0 ? genericParams : null, attributes, hasVariadic);

        // Clear generic params from scope after function registration
        foreach (var paramName in genericParams)
        {
            _genericParams.Remove(paramName);
        }
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
        // typeName() returns an array: [Type] for "impl Type" or [Trait, Type] for "impl Trait for Type"
        var typeNames = context.typeName();
        var implTypeName = typeNames[typeNames.Length - 1].IDENTIFIER(0).GetText();

        // Check if this is a trait implementation (has KW_FOR)
        bool isTraitImpl = context.KW_FOR() != null;
        string? traitName = null;
        List<IrType> traitTypeArgs = new();

        if (isTraitImpl)
        {
            // This is "impl Trait for Type"
            traitName = typeNames[0].IDENTIFIER(0).GetText();

            // Parse trait type arguments if present (e.g., Iterator<i32>)
            var traitGenericArgs = context.genericTypeArgs().Length > 0 ? context.genericTypeArgs(0) : null;
            if (traitGenericArgs != null)
            {
                var typeList = traitGenericArgs.typeList();
                foreach (var typeCtx in typeList.type())
                {
                    traitTypeArgs.Add(ParseType(typeCtx));
                }
            }

            // Validate that the trait exists
            if (!_traits.ContainsKey(traitName))
            {
                var location = SourceLocationHelper.FromToken(typeNames[0].IDENTIFIER(0).Symbol, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0032",
                    $"trait '{traitName}' not found",
                    location,
                    helpTexts: new List<string>
                    {
                        $"ensure the trait '{traitName}' is defined or imported"
                    }
                );
                return;
            }

            // Store trait implementation for constraint checking
            // Create a unique key for this trait impl
            // Format: "TypeName::TraitName<Arg1,Arg2,...>"
            var traitArgsStr = traitTypeArgs.Count > 0
                ? $"<{string.Join(",", traitTypeArgs.Select(t => GetTypeCacheKey(t)))}>"
                : "";
            var implKey = $"{implTypeName}::{traitName}{traitArgsStr}";

            // Store the trait impl info
            var implLocation = SourceLocationHelper.FromToken(context.KW_IMPL().Symbol, _filePath, _sourceLines);
            _traitImpls[implKey] = new TraitImplInfo(
                implTypeName,
                traitName,
                traitTypeArgs,
                genericParams,
                implLocation
            );
        }

        // Register each method in the impl block
        foreach (var item in context.implItem())
        {
            if (item.functionDeclaration() != null)
            {
                RegisterImplMethod(item.functionDeclaration(), context, implTypeName, genericParams, traitName, traitTypeArgs);
            }
        }

        // Clear generic params from scope after impl registration
        foreach (var paramName in genericParams)
        {
            _genericParams.Remove(paramName);
        }
    }

    private void RegisterImplMethod(NovusParser.FunctionDeclarationContext context, NovusParser.ImplDeclarationContext implContext, string implTypeName, List<string> genericParams, string? traitName = null, List<IrType>? traitTypeArgs = null)
    {
        var methodName = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Generate mangled name for the method
        // For trait impls: TypeName_TraitName_TypeArg1_TypeArg2_methodName (e.g., Counter_Iterator_i32_next)
        // For inherent impls: TypeName::methodName
        string mangledName;
        if (traitName != null)
        {
            var typeArgsSuffix = (traitTypeArgs != null && traitTypeArgs.Count > 0)
                ? "_" + string.Join("_", traitTypeArgs.Select(t => t.Name.Replace("::", "_")))
                : "";
            mangledName = $"{implTypeName}_{traitName}{typeArgsSuffix}_{methodName}";
        }
        else
        {
            mangledName = $"{implTypeName}::{methodName}";
        }

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
        bool hasVariadic = false;

        // Parse parameters (including self parameter if present)
        if (context.parameterList() != null)
        {
            // Check for self parameter
            if (context.parameterList().selfParameter() != null)
            {
                var selfParam = context.parameterList().selfParameter();
                var selfLocation = SourceLocationHelper.FromToken(selfParam.KW_SELF().Symbol, _filePath, _sourceLines);

                // Determine the base self type - we need to properly handle generic impls
                IrType baseType;

                // Check if this is a generic impl with type arguments (e.g., impl<T> Allocation<T>)
                // In this case, we need to keep the struct as generic with its parameters
                if (_structs.ContainsKey(implTypeName))
                {
                    baseType = _structs[implTypeName];
                }
                else
                {
                    // Fallback to generic type if struct not found
                    baseType = new IrGenericType(implTypeName);
                }

                // Now wrap in pointer if needed based on parameter form
                IrType selfType;
                if (selfParam.GetText().StartsWith("&mut"))
                {
                    // &mut self
                    selfType = new IrPointerType(baseType);
                }
                else if (selfParam.GetText().StartsWith("&"))
                {
                    // &self (immutable reference - treat as pointer for now)
                    selfType = new IrPointerType(baseType);
                }
                else
                {
                    // self (by value)
                    selfType = baseType;
                }

                parameters.Add(new ParameterSymbol("self", selfType, selfLocation));
            }

            // Parse regular parameters
            var paramList = context.parameterList();
            foreach (var paramCtx in paramList.parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                var paramType = ParseType(paramCtx.type());
                var paramLocation = SourceLocationHelper.FromToken(paramCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);
                parameters.Add(new ParameterSymbol(paramName, paramType, paramLocation));
            }

            // Add variadic parameter if present
            if (paramList.variadicParameter() != null)
            {
                var variadicCtx = paramList.variadicParameter();
                var variadicName = variadicCtx.IDENTIFIER().GetText();
                var variadicLocation = SourceLocationHelper.FromToken(variadicCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);
                // Variadic parameters have void* type for semantic analysis
                var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                parameters.Add(new ParameterSymbol(variadicName, variadicType, variadicLocation, IsVariadic: true));
                hasVariadic = true;
            }
        }

        _functions[mangledName] = new FunctionSymbol(mangledName, returnType, parameters, location, false, genericParams.Count > 0 ? genericParams : null, IsVariadic: hasVariadic);
    }

    private void RegisterStruct(NovusParser.StructDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Parse attributes
        var attributes = ParseAttributes(context.attribute());

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

        // Parse where clause
        var whereClause = ParseWhereClause(context.whereClause());

        // Register placeholder struct type FIRST to allow self-referential types
        var placeholderStruct = new IrStructType(name, new List<IrStructField>(), genericParams, null, attributes, whereClause);
        _structs[name] = placeholderStruct;

        // Now parse struct fields (can now reference the struct being defined)
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

        // Replace placeholder with complete struct type
        var structType = new IrStructType(name, fields, genericParams, null, attributes, whereClause);

        // Force offset calculation by accessing SizeInBytes (only for non-generic structs)
        if (genericParams.Count == 0)
        {
            _ = structType.SizeInBytes;
        }

        _structs[name] = structType;
    }

    private void RegisterEnum(NovusParser.EnumDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Parse attributes
        var attributes = ParseAttributes(context.attribute());

        // Check for duplicate enum names (but allow replacing stubs)
        if (_enums.ContainsKey(name))
        {
            var existingEnum = _enums[name];
            // Allow replacing stub enums (which have no variants)
            // This happens during two-pass enum registration in ImportModule
            if (existingEnum.Variants.Count > 0)
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
            // Otherwise, this is a stub being replaced - continue
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

        // Parse where clause
        var whereClause = ParseWhereClause(context.whereClause());

        var enumType = new IrEnumType(name, variants, genericParams.Count > 0 ? genericParams : null, null, attributes, whereClause);

        // Force size calculation
        if (genericParams.Count == 0)
        {
            _ = enumType.SizeInBytes;
        }

        _enums[name] = enumType;

        // Clear generic param scope
        _genericParams.Clear();
    }

    private void RegisterTrait(NovusParser.TraitDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Parse attributes
        var attributes = ParseAttributes(context.attribute());

        // Check for duplicate trait names
        if (_traits.ContainsKey(name))
        {
            _diagnostics.ReportError(
                "E0031",
                $"trait '{name}' is defined multiple times",
                location,
                helpTexts: new List<string>
                {
                    "consider renaming one of the traits"
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

                // Add to generic param scope for method signature parsing
                _genericParams[paramName] = new IrGenericType(paramName);
            }
        }

        // Parse trait method signatures
        var methods = new List<IrTraitMethod>();

        foreach (var itemCtx in context.traitItem())
        {
            var funcSig = itemCtx.functionSignature();
            if (funcSig != null)
            {
                var methodName = funcSig.IDENTIFIER().GetText();

                // Parse method generic parameters (if any)
                var methodGenericParams = new List<string>();
                if (funcSig.genericParams() != null)
                {
                    foreach (var paramId in funcSig.genericParams().IDENTIFIER())
                    {
                        var paramName = paramId.GetText();
                        methodGenericParams.Add(paramName);
                        _genericParams[paramName] = new IrGenericType(paramName);
                    }
                }

                // Parse parameters
                var parameters = new List<IrParameter>();
                if (funcSig.parameterList() != null)
                {
                    var paramList = funcSig.parameterList();

                    foreach (var paramCtx in paramList.parameter())
                    {
                        var paramName = paramCtx.IDENTIFIER().GetText();
                        var paramType = ParseType(paramCtx.type());
                        parameters.Add(new IrParameter(paramName, paramType));
                    }

                    // Handle variadic parameter if present
                    if (paramList.variadicParameter() != null)
                    {
                        var variadicCtx = paramList.variadicParameter();
                        var variadicName = variadicCtx.IDENTIFIER().GetText();
                        var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                        parameters.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
                    }
                }

                // Parse return type
                IrType returnType = IrVoidType.Instance;
                if (funcSig.type() != null)
                {
                    returnType = ParseType(funcSig.type());
                }

                methods.Add(new IrTraitMethod(methodName, parameters, returnType, methodGenericParams.Count > 0 ? methodGenericParams : null));

                // Clear method-level generic params
                foreach (var param in methodGenericParams)
                {
                    _genericParams.Remove(param);
                }
            }
        }

        // Parse visibility
        var visibility = Visibility.Private;
        if (context.KW_PUB() != null)
        {
            visibility = Visibility.Public;
        }
        else if (context.KW_INTERNAL() != null)
        {
            visibility = Visibility.Internal;
        }

        var trait = new IrTrait(name, methods, genericParams.Count > 0 ? genericParams : null, visibility, attributes);
        _traits[name] = trait;

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
        // typeName() returns an array: [Type] for "impl Type" or [Trait, Type] for "impl Trait for Type"
        var typeNames = context.typeName();
        var implTypeName = typeNames[typeNames.Length - 1].IDENTIFIER(0).GetText();

        // Check if this is a trait implementation
        bool isTraitImpl = context.KW_FOR() != null;
        string? traitName = null;
        List<IrType> traitTypeArgs = new();

        if (isTraitImpl)
        {
            traitName = typeNames[0].IDENTIFIER(0).GetText();

            // Parse trait type arguments if present (e.g., Iterator<i32>)
            var traitGenericArgs = context.genericTypeArgs().Length > 0 ? context.genericTypeArgs(0) : null;
            if (traitGenericArgs != null)
            {
                var typeList = traitGenericArgs.typeList();
                foreach (var typeCtx in typeList.type())
                {
                    traitTypeArgs.Add(ParseType(typeCtx));
                }
            }
        }

        // Analyze each method
        foreach (var item in context.implItem())
        {
            if (item.functionDeclaration() != null)
            {
                AnalyzeImplMethod(item.functionDeclaration(), implTypeName, traitName, traitTypeArgs);
            }
        }

        // Clear generic params after analysis
        foreach (var paramName in genericParams)
        {
            _genericParams.Remove(paramName);
        }
    }

    private void AnalyzeImplMethod(NovusParser.FunctionDeclarationContext context, string implTypeName, string? traitName = null, List<IrType>? traitTypeArgs = null)
    {
        var methodName = context.IDENTIFIER().GetText();

        // Use correct mangling for trait impls vs inherent impls
        string mangledName;
        if (traitName != null)
        {
            var typeArgsSuffix = (traitTypeArgs != null && traitTypeArgs.Count > 0)
                ? "_" + string.Join("_", traitTypeArgs.Select(t => t.Name.Replace("::", "_")))
                : "";
            mangledName = $"{implTypeName}_{traitName}{typeArgsSuffix}_{methodName}";
        }
        else
        {
            mangledName = $"{implTypeName}::{methodName}";
        }

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

        // Restore generic parameters to scope for function body analysis
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

        // Add parameters to symbol table (parameters are immutable)
        foreach (var param in _currentFunction.Parameters)
        {
            _variables[param.Name] = new VariableSymbol(param.Name, param.Type, false, param.Location);
        }

        // First, analyze the function body with full semantic analysis (visits all expressions)
        AnalyzeBlock(context.block());

        // Then check if all paths return
        bool allPathsReturn = AnalyzeBlockReturns(context.block());

        // Check if function with non-void return type has all paths returning
        if (_currentFunction.ReturnType is not IrVoidType && !allPathsReturn)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0030",
                $"function '{name}' must return a value on all code paths",
                location,
                helpTexts: new List<string>
                {
                    $"this function is declared to return '{TypeToString(_currentFunction.ReturnType)}'",
                    "ensure every possible execution path ends with a return statement"
                }
            );
        }

        // Clear generic params after function body analysis
        foreach (var paramName in genericParams)
        {
            _genericParams.Remove(paramName);
        }

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
    /// Analyzes a block and returns true if all paths return a value
    /// </summary>
    private bool AnalyzeBlockReturns(NovusParser.BlockContext block)
    {
        var statements = block.statement();
        if (statements.Length == 0)
            return false;

        // Check if any statement guarantees a return on all paths
        for (int i = 0; i < statements.Length; i++)
        {
            var stmt = statements[i];

            // Check if this statement guarantees a return on all paths
            if (StatementAlwaysReturns(stmt))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if an if statement always returns a value on all code paths
    /// </summary>
    private bool IfStatementAlwaysReturns(NovusParser.IfStatementContext ifStmt)
    {
        bool thenReturns = AnalyzeBlockReturns(ifStmt.block(0));

        // Check if there's an else-if statement
        var elseIfStmt = ifStmt.ifStatement();
        if (elseIfStmt != null)
        {
            // Recursively check the else-if chain
            bool elseIfReturns = IfStatementAlwaysReturns(elseIfStmt);
            return thenReturns && elseIfReturns;
        }

        // Check if there's a simple else block
        if (ifStmt.block().Length > 1)
        {
            bool elseReturns = AnalyzeBlockReturns(ifStmt.block(1));
            return thenReturns && elseReturns;
        }

        // No else clause
        return false;
    }

    /// <summary>
    /// Checks if a statement always returns a value on all code paths
    /// </summary>
    private bool StatementAlwaysReturns(NovusParser.StatementContext stmt)
    {
        // Return statement always returns
        if (stmt.returnStatement() != null)
            return true;

        // If statement returns if both branches return
        if (stmt.ifStatement() != null)
        {
            var ifStmt = stmt.ifStatement();
            bool thenReturns = AnalyzeBlockReturns(ifStmt.block(0));

            // Check if there's an else clause
            // Grammar: KW_IF ifCondition block (KW_ELSE (ifStatement | block))?
            // So the else part can be either another ifStatement (else-if) or a block (else)

            // Try to get the else-if statement (nested ifStatement)
            var elseIfStmt = ifStmt.ifStatement();
            if (elseIfStmt != null)
            {
                // This is an else-if chain - recursively check if the else-if returns
                bool elseIfReturns = IfStatementAlwaysReturns(elseIfStmt);
                return thenReturns && elseIfReturns;
            }

            // Check if there's a simple else block
            if (ifStmt.block().Length > 1)
            {
                // Has else block
                bool elseReturns = AnalyzeBlockReturns(ifStmt.block(1));
                return thenReturns && elseReturns;
            }

            // No else clause at all
            return false;
        }

        // Match expression (as statement) returns if it's exhaustive and all arms return
        if (stmt.expressionStatement() != null)
        {
            var matchExpr = FindMatchExpr(stmt.expressionStatement().expression());
            if (matchExpr != null)
            {
                var arms = matchExpr.matchArm();

                if (arms.Length == 0)
                    return false;

                // Note: We can't easily get the matched expression's type here without re-analyzing
                // For now, we'll do a conservative check based on pattern structure
                // The full exhaustiveness check happens in VisitMatchExpr

            // Try to infer if this looks like an exhaustive match based on patterns
            // This is conservative - we might miss some cases, but won't false positive

            // Track which variant names are covered
            var coveredVariants = new HashSet<string>();
            bool hasWildcard = false;

            // Check if all arms that have blocks end with a return
            bool allArmsReturn = true;
            foreach (var arm in arms)
            {
                var pattern = arm.pattern();

                // Track coverage
                if (pattern is NovusParser.WildcardPatternContext)
                {
                    hasWildcard = true;
                }
                else if (pattern is NovusParser.VariantPatternContext variantPattern)
                {
                    var variantNameCtx = variantPattern.variantName();
                    var identifiers = variantNameCtx.IDENTIFIER();
                    var variantName = identifiers[identifiers.Length - 1].GetText();
                    coveredVariants.Add(variantName);
                }
                else if (pattern is NovusParser.SimpleVariantPatternContext simpleVariant)
                {
                    // Pattern like Option::None or Result::Ok
                    var identifiers = simpleVariant.IDENTIFIER();
                    var variantName = identifiers[identifiers.Length - 1].GetText();
                    coveredVariants.Add(variantName);
                }
                else if (pattern is NovusParser.IdentifierPatternContext identPattern)
                {
                    // Bare identifier like "None" or "Ok" - could be a variant
                    var variantName = identPattern.IDENTIFIER().GetText();
                    coveredVariants.Add(variantName);
                }

                // Match arm can have a block, return statement, or just an expression
                if (arm.block() != null)
                {
                    // Block form: check if the last statement is a return
                    var block = arm.block();
                    var stmts = block.statement();
                    if (stmts.Length == 0 || stmts[stmts.Length - 1].returnStatement() == null)
                    {
                        allArmsReturn = false;
                        break;
                    }
                }
                else if (arm.returnStatement() != null)
                {
                    // Return statement form (e.g., Some(x) => return x) - this returns from the function
                    // Continue checking other arms
                }
                else
                {
                    // Expression form (e.g., Some(x) => x) - these don't have explicit returns
                    // They evaluate to a value but don't return from the function
                    allArmsReturn = false;
                    break;
                }
            }

            // Conservative exhaustiveness check for known patterns
            // Full exhaustiveness is checked in VisitMatchStatement
            bool looksExhaustive = hasWildcard ||
                                   (coveredVariants.Contains("Some") && coveredVariants.Contains("None")) ||
                                   (coveredVariants.Contains("Ok") && coveredVariants.Contains("Err")) ||
                                   (coveredVariants.Count >= 3);  // Heuristic: 3+ variants likely means exhaustive enum match

                // Only guarantees return if it looks exhaustive AND all arms return
                return looksExhaustive && allArmsReturn;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds a MatchExprContext within an expression tree (helper for match-as-expression)
    /// </summary>
    private NovusParser.MatchExprContext? FindMatchExpr(NovusParser.ExpressionContext expr)
    {
        // Check if the expression's child is a PrimaryExprContext with MatchExpr
        if (expr is NovusParser.PrimaryExprContext primaryExpr)
        {
            if (primaryExpr.primaryExpression() is NovusParser.MatchExprContext matchExpr)
            {
                return matchExpr;
            }
        }
        return null;
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

        // Check if there's an expression (bare return for void functions)
        var exprContext = context.expression();
        IrType? exprType = null;

        if (exprContext != null)
        {
            // Set expected type for bidirectional type checking (enables type inference)
            var savedExpectedType = _expectedType;
            _expectedType = _currentFunction.ReturnType;

            exprType = Visit(exprContext);

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
                        $"consider using a cast: ({expectedType}){exprContext.GetText()}"
                    }
                );
            }
        }
        else
        {
            // Bare return - only valid for void functions
            if (_currentFunction.ReturnType is not IrVoidType)
            {
                var expectedType = TypeToString(_currentFunction.ReturnType);
                _diagnostics.ReportError(
                    "E0003",
                    $"bare return in non-void function",
                    location,
                    helpTexts: new List<string>
                    {
                        $"this function is declared to return '{expectedType}'",
                        "provide a return value or change the function to return void"
                    }
                );
            }
        }

        return null;
    }

    public override IrType? VisitVariableDeclaration([NotNull] NovusParser.VariableDeclarationContext context)
    {
        // Check if this is a throwaway binding (_)
        var identifierNode = context.IDENTIFIER();
        var name = identifierNode?.GetText() ?? "_";
        var isThrowaway = name == "_";
        var isMutable = context.GetChild(0)?.GetText() == "var" || context.GetChild(1)?.GetText() == "mut";

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
            {
                derefCount++;
            }
            else if (context.GetChild(i) is ITerminalNode terminal && terminal.Symbol.Type == NovusLexer.IDENTIFIER)
            {
                break; // Stop at the first identifier
            }
        }

        var lvalueSuffixes = context.lvalueSuffix();

        // Handle post-increment/decrement statements (no expression)
        if (isPostIncDec)
        {
            // Check if variable exists (local or global)
            if (!_variables.ContainsKey(name) && !_globalVariables.ContainsKey(name))
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

            var incDecVariable = _variables.ContainsKey(name) ? _variables[name] : _globalVariables[name];

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
            if (!_variables.ContainsKey(name) && !_globalVariables.ContainsKey(name))
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

        // Check if variable exists (local or global)
        if (!_variables.ContainsKey(name) && !_globalVariables.ContainsKey(name))
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

        var variable = _variables.ContainsKey(name) ? _variables[name] : _globalVariables[name];

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

        // Check for division by zero or modulo by zero (if right is a constant 0)
        var op = context.GetChild(1).GetText();
        if ((op == "/" || op == "%") && context.expression(1) is NovusParser.PrimaryExprContext primaryExpr)
        {
            var intLiteral = primaryExpr.primaryExpression() as NovusParser.IntegerLiteralContext;
            if (intLiteral?.INTEGER_LITERAL()?.GetText() == "0")
            {
                var location = SourceLocationHelper.FromContext(context.expression(1), _filePath, _sourceLines);
                var errorMessage = op == "/" ? "division by zero" : "modulo by zero";
                _diagnostics.ReportError(
                    "E0005",
                    errorMessage,
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
        // Allow: numeric -> numeric, pointer -> integer, integer -> pointer, pointer -> pointer, &T -> *T
        bool isValidCast = (IsNumericType(targetType) && IsNumericType(exprType)) ||
                           (IsNumericType(targetType) && exprType is IrPointerType) ||
                           (targetType is IrPointerType && IsNumericType(exprType)) ||
                           (targetType is IrPointerType && exprType is IrPointerType) ||
                           (targetType is IrPointerType && exprType is IrReferenceType) ||
                           (targetType is IrPointerType && exprType is IrMutReferenceType);

        if (!isValidCast)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0006",
                $"cannot cast from '{TypeToString(exprType)}' to '{TypeToString(targetType)}'",
                location,
                helpTexts: new List<string>
                {
                    "only numeric types and pointers can be cast"
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

    public override IrType? VisitTryExpr([NotNull] NovusParser.TryExprContext context)
    {
        // The ? operator for Result propagation
        // expr? unwraps Result<T, E> to T or returns early with Err

        var innerExprType = Visit(context.expression());

        if (innerExprType == null)
            return null;

        // Verify it's a Result<T, E> type
        if (innerExprType is not IrEnumType enumType || enumType.EnumName != "Result")
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0040",
                $"? operator requires a Result<T, E> type, got {TypeToString(innerExprType)}",
                location,
                helpTexts: new List<string>
                {
                    "the ? operator can only be used on Result types",
                    "if you have an Option<T>, match on it explicitly"
                }
            );
            return null;
        }

        // Extract the Ok payload type from Result<T, E>
        var okVariant = enumType.Variants.FirstOrDefault(v => v.Name == "Ok");
        if (okVariant == null || okVariant.AssociatedData.Count == 0)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0040",
                "Result type is missing Ok variant or associated data",
                location
            );
            return null;
        }

        // The ? operator evaluates to the Ok payload type
        return okVariant.AssociatedData[0];
    }

    // Override VisitStatement to prevent double traversal via VisitChildren
    // The base implementation calls VisitChildren, which would visit all child nodes
    // after our specific Visit* methods have already processed them
    public override IrType? VisitStatement([NotNull] NovusParser.StatementContext context)
    {
        // Visit only the first child, which is the actual statement
        // This prevents the base VisitChildren from visiting ALL children
        if (context.ChildCount > 0)
        {
            return Visit(context.GetChild(0));
        }
        return null;
    }

    public override IrType? VisitIfStatement([NotNull] NovusParser.IfStatementContext context)
    {
        // Visit the condition (handles expression, if let, if var)
        // This may set _pendingIfLetVariable
        Visit(context.ifCondition());

        // Save current variable scope before adding if let/var binding
        var variablesBeforeIf = new HashSet<string>(_variables.Keys);

        // If we have a pending if let/var variable, declare it in scope for the then block
        if (_pendingIfLetVariable != null)
        {
            var (varName, varType, isMutable) = _pendingIfLetVariable.Value;
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _variables[varName] = new VariableSymbol(varName, varType, isMutable, location);
            _pendingIfLetVariable = null;
        }

        // Analyze then block with unreachable code detection
        AnalyzeBlock(context.block(0));

        // Remove if let/var binding before else block (not in scope there)
        var keysToRemove = _variables.Keys.Where(k => !variablesBeforeIf.Contains(k)).ToList();
        foreach (var key in keysToRemove)
        {
            _variables.Remove(key);
        }

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

    // Helper to pass variable info from condition to then block
    private (string varName, IrType varType, bool isMutable)? _pendingIfLetVariable;

    public override IrType? VisitIfConditionExpression([NotNull] NovusParser.IfConditionExpressionContext context)
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

        return null;
    }

    public override IrType? VisitIfConditionLet([NotNull] NovusParser.IfConditionLetContext context)
    {
        // if let binds a non-null value to an immutable variable
        var exprType = Visit(context.expression());
        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);

        // Check that the expression type is nullable (pointer or integer)
        if (exprType != null && !IsNullableType(exprType))
        {
            _diagnostics.ReportError(
                "E0031",
                "if let requires a nullable type (pointer or integer)",
                location,
                helpTexts: new List<string>
                {
                    $"found type '{TypeToString(exprType)}', expected a pointer or integer type",
                    "if let checks if a value is non-zero/non-null"
                }
            );
        }

        // Store variable info for declaring in then block scope
        if (exprType != null)
        {
            var varName = context.IDENTIFIER().GetText();
            _pendingIfLetVariable = (varName, exprType, false); // false = immutable
        }

        return null;
    }

    public override IrType? VisitIfConditionVar([NotNull] NovusParser.IfConditionVarContext context)
    {
        // if var binds a non-null value to a mutable variable
        var exprType = Visit(context.expression());
        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);

        // Check that the expression type is nullable (pointer or integer)
        if (exprType != null && !IsNullableType(exprType))
        {
            _diagnostics.ReportError(
                "E0031",
                "if var requires a nullable type (pointer or integer)",
                location,
                helpTexts: new List<string>
                {
                    $"found type '{TypeToString(exprType)}', expected a pointer or integer type",
                    "if var checks if a value is non-zero/non-null"
                }
            );
        }

        // Store variable info for declaring in then block scope
        if (exprType != null)
        {
            var varName = context.IDENTIFIER().GetText();
            _pendingIfLetVariable = (varName, exprType, true); // true = mutable
        }

        return null;
    }

    private bool IsNullableType(IrType type)
    {
        return type is IrPointerType || type is IrIntType;
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

    public override IrType? VisitForCStyle([NotNull] NovusParser.ForCStyleContext context)
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

    public override IrType? VisitForInLoop([NotNull] NovusParser.ForInLoopContext context)
    {
        // for item in collection { ... }
        var itemName = context.IDENTIFIER().GetText();
        var collectionType = Visit(context.expression());

        // TODO: Validate that collection implements Iterator trait
        // For now, just add the item variable with a placeholder type
        // The IR builder will properly type it when it unwraps the Option
        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
        var itemSymbol = new VariableSymbol(itemName, IrIntType.I32, false, location!);  // Placeholder type
        _variables[itemName] = itemSymbol;

        // Enter loop context
        _loopDepth++;
        AnalyzeBlock(context.block());
        _loopDepth--;

        // Remove the item variable from scope
        _variables.Remove(itemName);

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

    // Handle: defer { statements }
    public override IrType? VisitDeferBlock([NotNull] NovusParser.DeferBlockContext context)
    {
        // Analyze the deferred block
        // Variables captured in defer have their values at the time defer executes (end of scope)
        // not at the time defer is registered
        AnalyzeBlock(context.block());
        return null;
    }

    // Handle: defer => expression
    public override IrType? VisitDeferExpression([NotNull] NovusParser.DeferExpressionContext context)
    {
        // Analyze the deferred expression
        Visit(context.expression());
        return null;
    }

    // Handle: assert!(condition) or assert!(condition, "message")
    public override IrType? VisitAssertStatement([NotNull] NovusParser.AssertStatementContext context)
    {
        // Analyze the condition expression
        var conditionType = Visit(context.expression());

        // Verify condition is boolean or numeric (C-style truthiness)
        if (conditionType != null && !IsBoolOrNumericType(conditionType))
        {
            var location = SourceLocationHelper.FromToken(context.Start, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0308",
                $"assert condition must be a boolean or numeric expression, found '{TypeToString(conditionType)}'",
                location
            );
        }

        return null;
    }

    // Handle: panic!("message")
    public override IrType? VisitPanicStatement([NotNull] NovusParser.PanicStatementContext context)
    {
        // Panic statement is straightforward - just needs a message string
        // The message is validated by the parser (must be STRING_LITERAL)
        // No additional semantic checks needed
        return null;
    }

    // Handle: unsafe { statements }
    public override IrType? VisitUnsafeBlock([NotNull] NovusParser.UnsafeBlockContext context)
    {
        // Track this unsafe block for warnings
        var startLine = context.Start.Line;
        var startColumn = context.Start.Column;
        var endLine = context.Stop.Line;
        var lineCount = endLine - startLine + 1;

        _unsafeBlocks.Add(new UnsafeBlockInfo
        {
            FilePath = _filePath,
            Line = startLine,
            Column = startColumn,
            LineCount = lineCount,
            Reason = "Manual unsafe block"
        });

        // Enter unsafe context
        _unsafeDepth++;

        try
        {
            // Analyze the block with unsafe operations allowed
            AnalyzeBlock(context.block());
        }
        finally
        {
            // Exit unsafe context
            _unsafeDepth--;
        }

        return null;
    }

    /// <summary>
    /// Check if we're currently in an unsafe context
    /// </summary>
    public bool IsInUnsafeContext()
    {
        return _unsafeDepth > 0;
    }

    /// <summary>
    /// Require that we're in an unsafe context for the given operation
    /// </summary>
    private void RequireUnsafe(ParserRuleContext context, string operation, string reason, List<string>? helpTexts = null)
    {
        if (!IsInUnsafeContext())
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);

            var help = helpTexts ?? new List<string>();
            help.Add($"{operation} is unsafe because {reason}");
            help.Add($"Wrap this code in an unsafe block:");
            help.Add($"");
            help.Add($"    unsafe {{");
            help.Add($"        {operation}");
            help.Add($"    }}");

            _diagnostics.ReportError(
                "E1001",
                $"{operation} requires unsafe block",
                location,
                helpTexts: help
            );
        }
    }

    /// <summary>
    /// List of dangerous FFI functions that require unsafe blocks
    /// </summary>
    private static readonly Dictionary<string, string> UnsafeFunctions = new()
    {
        // Memory management - can leak, double-free, wrong size
        ["AllocMem"] = "it returns raw addresses and can leak memory",
        ["FreeMem"] = "it can double-free or use wrong size",
        ["AllocAbs"] = "it returns raw addresses and can leak memory",
        ["Allocate"] = "it returns raw addresses and can leak memory",
        ["Deallocate"] = "it can double-free or use wrong size",
        ["AllocEntry"] = "it returns raw addresses and can leak memory",
        ["FreeEntry"] = "it can double-free",

        // Library/Device management - can leak handles
        ["OpenLibrary"] = "it can leak library handles if not closed",
        ["OldOpenLibrary"] = "it can leak library handles if not closed",
        ["CloseLibrary"] = "it can close wrong library base",
        ["OpenDevice"] = "it can leak device handles if not closed",
        ["CloseDevice"] = "it can close wrong device",

        // Direct hardware/system access
        ["Supervisor"] = "it executes code in supervisor mode",
        ["SuperState"] = "it switches to supervisor mode",
        ["UserState"] = "it manipulates system stack",
        ["SetSR"] = "it modifies status register",
        ["SetIntVector"] = "it manipulates interrupt vectors",
        ["Disable"] = "it disables interrupts system-wide",
        ["Enable"] = "it enables interrupts system-wide",

        // Raw pointer manipulation
        ["CopyMem"] = "it performs raw memory copies",
        ["CopyMemQuick"] = "it performs raw memory copies",
    };

    /// <summary>
    /// Check if a function call requires an unsafe block
    /// </summary>
    private void CheckUnsafeFunctionCall(ParserRuleContext context, string functionName)
    {
        if (UnsafeFunctions.TryGetValue(functionName, out var reason))
        {
            var help = new List<string>
            {
                $"Use safe alternatives instead:",
                $"  - Allocation::new() for tracked allocations",
                $"  - Box::new() for single heap values",
                $"  - defer block.drop() for RAII cleanup",
                $"",
                $"Or wrap in unsafe block if you need raw control:"
            };

            RequireUnsafe(context, functionName + "()", reason, help);
        }
    }

    // ============================================================================
    // Attribute Parsing
    // ============================================================================

    /// <summary>
    /// Parse attributes from an array of attribute contexts
    /// </summary>
    private AttributeCollection ParseAttributes(NovusParser.AttributeContext[]? attributeContexts)
    {
        var collection = new AttributeCollection();

        if (attributeContexts == null || attributeContexts.Length == 0)
            return collection;

        foreach (var attrCtx in attributeContexts)
        {
            var attrName = attrCtx.IDENTIFIER().GetText();
            var location = SourceLocationHelper.FromToken(attrCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);

            var attr = new AttributeInfo(attrName, location);

            // Parse arguments if present
            if (attrCtx.attributeArgList() != null)
            {
                foreach (var argCtx in attrCtx.attributeArgList().attributeArg())
                {
                    // Named argument: name = value
                    if (argCtx.IDENTIFIER() != null)
                    {
                        var argName = argCtx.IDENTIFIER().GetText();
                        var value = EvaluateConstantExpression(argCtx.expression());
                        attr.NamedArgs[argName] = value ?? "null";
                    }
                    // Positional argument: value
                    else
                    {
                        var value = EvaluateConstantExpression(argCtx.expression());
                        attr.PositionalArgs.Add(value ?? "null");
                    }
                }
            }

            // Validate attribute name
            if (!KnownAttributes.IsKnown(attrName))
            {
                _diagnostics.ReportWarning(
                    "W2001",
                    $"unknown attribute '{attrName}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "This attribute is not recognized and will be ignored",
                        $"Known attributes: {string.Join(", ", KnownAttributes.All.Take(10))}, ..."
                    }
                );
            }

            collection.Add(attr);
        }

        return collection;
    }

    /// <summary>
    /// Evaluate a constant expression for attribute arguments
    /// Currently handles: integers, strings, booleans, identifiers
    /// </summary>
    private object? EvaluateConstantExpression(NovusParser.ExpressionContext expr)
    {
        var text = expr.GetText();

        // Try to parse as integer
        if (int.TryParse(text.TrimStart('-'), out var intValue))
        {
            return text.StartsWith("-") ? -intValue : intValue;
        }

        // String literal
        if (text.StartsWith("\"") && text.EndsWith("\""))
        {
            // Remove quotes and handle escape sequences
            return text.Trim('"').Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        // Boolean literals
        if (text == "true") return true;
        if (text == "false") return false;

        // Default: return as string (identifier or unknown)
        return text;
    }

    public override IrType? VisitMatchExpr([NotNull] NovusParser.MatchExprContext context)
    {
        // Analyze the value being matched
        var matchValueType = Visit(context.expression());
        if (matchValueType == null)
        {
            return null;
        }

        // Ensure we're matching on an enum type or integer type
        bool isEnumMatch = matchValueType is IrEnumType;
        bool isIntegerMatch = matchValueType is IrIntType;

        if (!isEnumMatch && !isIntegerMatch)
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0035",
                $"match expression can only be used with enum or integer types, got '{matchValueType.Name}'",
                location,
                helpTexts: new List<string>
                {
                    "match is used for pattern matching on enum variants or integer literals"
                }
            );
            return null;
        }

        // Track which variants/values are covered
        var coveredVariants = new HashSet<string>();
        var coveredIntegerValues = new HashSet<long>();
        bool hasWildcard = false;

        // Analyze each match arm
        foreach (var armCtx in context.matchArm())
        {
            var pattern = armCtx.pattern();

            // Save current variable scope - store list of variables added by this pattern
            var variablesBeforePattern = new HashSet<string>(_variables.Keys);

            // Analyze pattern and bind variables
            if (isEnumMatch)
            {
                AnalyzePatternAndBind(pattern, (IrEnumType)matchValueType, coveredVariants, ref hasWildcard);
            }
            else // isIntegerMatch
            {
                AnalyzeIntegerPatternAndBind(pattern, (IrIntType)matchValueType, coveredIntegerValues, ref hasWildcard);
            }

            // Analyze the arm body (expression, block, or return statement) with bound variables in scope
            if (armCtx.expression() != null)
            {
                Visit(armCtx.expression());
            }
            else if (armCtx.block() != null)
            {
                AnalyzeBlock(armCtx.block());
            }
            else if (armCtx.returnStatement() != null)
            {
                Visit(armCtx.returnStatement());
            }

            // Remove pattern bindings (they're only valid in this arm)
            var keysToRemove = _variables.Keys.Where(k => !variablesBeforePattern.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _variables.Remove(key);
            }
        }

        // Check exhaustiveness
        if (!hasWildcard)
        {
            if (isEnumMatch)
            {
                var enumType = (IrEnumType)matchValueType;
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
            else // isIntegerMatch
            {
                // For integer matches, exhaustiveness is practically impossible (too many values)
                // So we require a wildcard pattern for integers
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0036",
                    "match on integer type is not exhaustive",
                    location,
                    helpTexts: new List<string>
                    {
                        "integer types have too many values to enumerate",
                        "add a wildcard pattern '_' to handle all other cases"
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

    private void AnalyzeIntegerPatternAndBind(NovusParser.PatternContext pattern, IrIntType intType,
        HashSet<long> coveredValues, ref bool hasWildcard)
    {
        switch (pattern)
        {
            case NovusParser.WildcardPatternContext:
                hasWildcard = true;
                break;

            case NovusParser.LiteralPatternContext literalPattern:
            {
                // Integer literal pattern
                if (literalPattern.INTEGER_LITERAL() != null)
                {
                    var literalText = literalPattern.INTEGER_LITERAL().GetText();
                    if (long.TryParse(literalText, out long value))
                    {
                        // Check if value is already covered
                        if (coveredValues.Contains(value))
                        {
                            var location = SourceLocationHelper.FromToken(literalPattern.Start, _filePath, _sourceLines);
                            _diagnostics.ReportWarning(
                                "W0001",
                                $"duplicate match pattern for value {value}",
                                location,
                                helpTexts: new List<string>
                                {
                                    "this pattern will never be reached because an earlier pattern matches the same value"
                                }
                            );
                        }

                        // Validate that value fits in the integer type
                        bool valueInRange = intType.BitWidth switch
                        {
                            8 when intType.IsSigned => value >= sbyte.MinValue && value <= sbyte.MaxValue,
                            8 when !intType.IsSigned => value >= byte.MinValue && value <= byte.MaxValue,
                            16 when intType.IsSigned => value >= short.MinValue && value <= short.MaxValue,
                            16 when !intType.IsSigned => value >= ushort.MinValue && value <= ushort.MaxValue,
                            32 when intType.IsSigned => value >= int.MinValue && value <= int.MaxValue,
                            32 when !intType.IsSigned => value >= uint.MinValue && value <= uint.MaxValue,
                            64 => true, // long can represent all i64 values, u64 would need ulong but we'll accept it
                            _ => false
                        };

                        if (!valueInRange)
                        {
                            var location = SourceLocationHelper.FromToken(literalPattern.Start, _filePath, _sourceLines);
                            _diagnostics.ReportError(
                                "E0040",
                                $"literal value {value} does not fit in type '{intType.Name}'",
                                location
                            );
                        }

                        coveredValues.Add(value);
                    }
                    else
                    {
                        var location = SourceLocationHelper.FromToken(literalPattern.Start, _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0041",
                            $"invalid integer literal '{literalText}'",
                            location
                        );
                    }
                }
                else if (literalPattern.STRING_LITERAL() != null)
                {
                    var location = SourceLocationHelper.FromToken(literalPattern.Start, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0042",
                        "cannot use string literal in integer match pattern",
                        location,
                        helpTexts: new List<string>
                        {
                            "integer match patterns only accept integer literals or wildcards"
                        }
                    );
                }
                break;
            }

            case NovusParser.BoolLiteralPatternContext boolPattern:
            {
                var location = SourceLocationHelper.FromToken(boolPattern.Start, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0043",
                    "cannot use boolean literal in integer match pattern",
                    location,
                    helpTexts: new List<string>
                    {
                        "integer match patterns only accept integer literals or wildcards"
                    }
                );
                break;
            }

            case NovusParser.IdentifierPatternContext identPattern:
            case NovusParser.VariantPatternContext:
            case NovusParser.SimpleVariantPatternContext:
            {
                var location = SourceLocationHelper.FromToken(pattern.Start, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0044",
                    "invalid pattern for integer match",
                    location,
                    helpTexts: new List<string>
                    {
                        "integer match patterns only accept integer literals or wildcards",
                        $"example: match value {{ 0 => ..., 1 => ..., _ => ... }}"
                    }
                );
                break;
            }
        }
    }

    /// <summary>
    /// Infer generic type parameters from function arguments
    /// </summary>
    /// <param name="genericParams">Generic parameter names (e.g., ["T"])</param>
    /// <param name="paramTypes">Function parameter types (may contain IrGenericType)</param>
    /// <param name="argTypes">Actual argument types provided in the call</param>
    /// <returns>Dictionary mapping generic param names to concrete types, or null if inference fails</returns>
    private Dictionary<string, IrType>? InferGenericTypes(
        List<string> genericParams,
        List<IrType> paramTypes,
        List<IrType> argTypes)
    {
        var substitutions = new Dictionary<string, IrType>();

        for (int i = 0; i < Math.Min(paramTypes.Count, argTypes.Count); i++)
        {
            if (!InferGenericTypeFromPair(paramTypes[i], argTypes[i], substitutions))
            {
                // Inference failed
                return null;
            }
        }

        // Check if all generic parameters were inferred
        foreach (var param in genericParams)
        {
            if (!substitutions.ContainsKey(param))
            {
                // Could not infer all type parameters
                return null;
            }
        }

        return substitutions;
    }

    /// <summary>
    /// Recursively infer generic types by matching a parameter type against an argument type
    /// </summary>
    private bool InferGenericTypeFromPair(IrType paramType, IrType argType, Dictionary<string, IrType> substitutions)
    {
        // If paramType is a generic parameter (e.g., T), bind it to argType
        if (paramType is IrGenericType genericType)
        {
            var paramName = genericType.ParameterName;
            if (substitutions.ContainsKey(paramName))
            {
                // Already inferred - check consistency
                return TypesCompatible(substitutions[paramName], argType);
            }
            else
            {
                // New inference
                substitutions[paramName] = argType;
                return true;
            }
        }

        // If paramType is a pointer (*T), and argType is a pointer (*u8), recursively match
        if (paramType is IrPointerType paramPtr && argType is IrPointerType argPtr)
        {
            return InferGenericTypeFromPair(paramPtr.PointeeType, argPtr.PointeeType, substitutions);
        }

        // If paramType is an enum (Option<T>) and argType is an enum (Option<i32>), match structure
        if (paramType is IrEnumType paramEnum && argType is IrEnumType argEnum)
        {
            // Must be the same enum
            if (paramEnum.EnumName != argEnum.EnumName)
                return false;

            // If paramEnum is generic and argEnum is monomorphized, extract type arguments
            if (paramEnum.GenericParameters.Count > 0 && argEnum.GenericParameters.Count == 0)
            {
                // This is complex - would need to extract from cache key
                // For now, just check if they're compatible
                return TypesCompatible(paramType, argType);
            }

            return true;
        }

        // If paramType is a struct (Vec<T>) and argType is a struct (Vec<i32>), match structure
        if (paramType is IrStructType paramStruct && argType is IrStructType argStruct)
        {
            // Must be the same struct
            if (paramStruct.Name != argStruct.Name)
                return false;

            // Check if paramStruct contains generic types and argStruct is fully concrete
            var paramCacheKey = paramStruct.CacheKey ?? paramStruct.Name;
            var argCacheKey = argStruct.CacheKey ?? argStruct.Name;

            // If both have cache keys with type arguments, try to match them
            if (paramCacheKey.Contains("<") && argCacheKey.Contains("<"))
            {
                // Extract type arguments from both
                var paramStartIdx = paramCacheKey.IndexOf('<');
                var paramEndIdx = paramCacheKey.LastIndexOf('>');
                var paramTypeArgsStr = paramCacheKey.Substring(paramStartIdx + 1, paramEndIdx - paramStartIdx - 1);
                var paramTypeArgKeys = paramTypeArgsStr.Split(',').Select(s => s.Trim()).ToArray();

                var argStartIdx = argCacheKey.IndexOf('<');
                var argEndIdx = argCacheKey.LastIndexOf('>');
                var argTypeArgsStr = argCacheKey.Substring(argStartIdx + 1, argEndIdx - argStartIdx - 1);
                var argTypeArgKeys = argTypeArgsStr.Split(',').Select(s => s.Trim()).ToArray();

                // Match each type argument
                if (paramTypeArgKeys.Length == argTypeArgKeys.Length)
                {
                    for (int i = 0; i < paramTypeArgKeys.Length; i++)
                    {
                        var paramTypeArgKey = paramTypeArgKeys[i];
                        var argTypeArgKey = argTypeArgKeys[i];

                        // Check if paramTypeArgKey is a generic parameter (single capital letter like T, E, etc.)
                        if (paramTypeArgKey.Length == 1 && char.IsUpper(paramTypeArgKey[0]))
                        {
                            // This is a generic parameter - infer it from the argument type
                            var inferredType = ParseTypeFromCacheKey(argTypeArgKey);
                            if (inferredType != null)
                            {
                                if (substitutions.ContainsKey(paramTypeArgKey))
                                {
                                    // Check consistency
                                    if (!TypesCompatible(substitutions[paramTypeArgKey], inferredType))
                                        return false;
                                }
                                else
                                {
                                    substitutions[paramTypeArgKey] = inferredType;
                                }
                            }
                        }
                        else if (paramTypeArgKey != argTypeArgKey)
                        {
                            // Concrete types must match
                            return false;
                        }
                    }
                }
                return true;
            }

            return true;
        }

        // For other types, just check compatibility
        return TypesCompatible(paramType, argType);
    }

    /// <summary>
    /// Parse a type from its cache key representation (reverse of GetTypeCacheKey)
    /// </summary>
    private IrType? ParseTypeFromCacheKey(string key)
    {
        // Handle simple primitive types
        switch (key)
        {
            case "i8": return IrIntType.I8;
            case "i16": return IrIntType.I16;
            case "i32": return IrIntType.I32;
            case "i64": return IrIntType.I64;
            case "u8": return IrIntType.U8;
            case "u16": return IrIntType.U16;
            case "u32": return IrIntType.U32;
            case "u64": return IrIntType.U64;
            case "bool": return IrBoolType.Instance;
            case "void": return IrVoidType.Instance;
            case "String": return IrStringType.Instance;
        }

        // Handle pointer types (ptr_T)
        if (key.StartsWith("ptr_"))
        {
            var pointeeKey = key.Substring(4); // Remove "ptr_"
            var pointeeType = ParseTypeFromCacheKey(pointeeKey);
            if (pointeeType != null)
            {
                return _typeInterner.GetPointerType(pointeeType);
            }
        }

        // Handle struct/enum types (Name or Name<Args>)
        if (key.Contains("<"))
        {
            // Monomorphized type - would need to look up in caches
            // For now, return null and fall back to compatibility check
            return null;
        }

        // Try to find as struct or enum name
        if (_structs.ContainsKey(key))
        {
            return _structs[key];
        }
        if (_enums.ContainsKey(key))
        {
            return _enums[key];
        }

        return null;
    }

    /// <summary>
    /// Check if a type contains any generic type parameters (including nested)
    /// </summary>
    private bool ContainsGenericType(IrType type)
    {
        if (type is IrGenericType)
            return true;

        if (type is IrPointerType ptrType)
            return ContainsGenericType(ptrType.PointeeType);

        if (type is IrEnumType enumType && enumType.GenericParameters.Count > 0)
            return true;

        if (type is IrStructType structType && structType.GenericParameters.Count > 0)
            return true;

        return false;
    }

    /// <summary>
    /// Apply type substitutions to create a concrete type from a potentially generic type
    /// </summary>
    private IrType SubstituteGenericTypes(IrType type, Dictionary<string, IrType> substitutions)
    {

        if (type is IrGenericType gt && substitutions.ContainsKey(gt.ParameterName))
        {
            var result = substitutions[gt.ParameterName];
            return result;
        }

        if (type is IrPointerType ptrType)
        {
            var substitutedPointee = SubstituteGenericTypes(ptrType.PointeeType, substitutions);
            if (substitutedPointee != ptrType.PointeeType)
            {
                var result = _typeInterner.GetPointerType(substitutedPointee);
                return result;
            }
            return ptrType;
        }

        if (type is IrEnumType enumType && enumType.GenericParameters.Count > 0)
        {
            // Substitute generic parameters in enum type (e.g., Option<T> -> Option<u8>, Option<*T> -> Option<*u8>)
            //
            // IMPORTANT: Use the current enum's variants, not the base enum's, because the current enum
            // may already be partially monomorphized (e.g., Option<*T> has variants with *T, not T)
            var baseEnum = enumType;

            // Build type arguments by substituting the variant data types
            // For Option<*T>, the Some variant has data [*T], and we substitute to get [*u8]
            // Extract those to use as type arguments
            var monomorphizedVariants = new List<IrEnumVariant>();
            var substitutedTypeArgs = new List<IrType>();

            foreach (var variant in baseEnum.Variants)
            {
                var monomorphizedData = variant.AssociatedData.Select(d => SubstituteGenericTypes(d, substitutions)).ToList();
                monomorphizedVariants.Add(new IrEnumVariant(variant.Name, variant.Tag, monomorphizedData));

                // Extract type arguments from the first variant with data
                if (substitutedTypeArgs.Count == 0 && monomorphizedData.Count > 0)
                {
                    substitutedTypeArgs.AddRange(monomorphizedData);
                }
            }


            // If all type args are concrete (no generic types, even nested), create/retrieve monomorphized enum
            if (substitutedTypeArgs.All(t => !ContainsGenericType(t)))
            {
                var cacheKey = $"{baseEnum.EnumName}<{string.Join(",", substitutedTypeArgs.Select(GetTypeCacheKey))}>";
                if (_monomorphizedEnums.ContainsKey(cacheKey))
                {
                    return _monomorphizedEnums[cacheKey];
                }

                // Variants already created above
                var monomorphizedEnum = new IrEnumType(baseEnum.EnumName, monomorphizedVariants, null, cacheKey);
                _monomorphizedEnums[cacheKey] = monomorphizedEnum;
                return monomorphizedEnum;
            }
            else
            {
                // Still has generics - return a partially-substituted enum
                return new IrEnumType(baseEnum.EnumName, monomorphizedVariants, baseEnum.GenericParameters, null);
            }
        }

        // Handle struct types (e.g., Vec<T> -> Vec<i32>)
        // Check if the struct contains generic types in its cache key
        if (type is IrStructType structType)
        {
            var cacheKey = structType.CacheKey ?? structType.Name;

            // If cache key contains type arguments with generics, substitute them
            if (cacheKey.Contains("<") && cacheKey.Contains(">"))
            {
                var startIdx = cacheKey.IndexOf('<');
                var endIdx = cacheKey.LastIndexOf('>');
                var typeArgsStr = cacheKey.Substring(startIdx + 1, endIdx - startIdx - 1);
                var typeArgKeys = typeArgsStr.Split(',').Select(s => s.Trim()).ToList();

                // Check if any type argument is a generic parameter
                bool hasGenerics = false;
                var substitutedTypeArgs = new List<IrType>();

                foreach (var typeArgKey in typeArgKeys)
                {
                    // Check if this is a generic parameter (single capital letter)
                    if (typeArgKey.Length == 1 && char.IsUpper(typeArgKey[0]) && substitutions.ContainsKey(typeArgKey))
                    {
                        substitutedTypeArgs.Add(substitutions[typeArgKey]);
                        hasGenerics = true;
                    }
                    else
                    {
                        // Try to parse the type arg key
                        var parsedType = ParseTypeFromCacheKey(typeArgKey);
                        if (parsedType != null)
                        {
                            substitutedTypeArgs.Add(parsedType);
                        }
                        else
                        {
                            // Couldn't parse - return the original type
                            return type;
                        }
                    }
                }

                // If we found and substituted generics, create/lookup monomorphized struct
                if (hasGenerics && substitutedTypeArgs.All(t => !ContainsGenericType(t)))
                {
                    var newCacheKey = $"{structType.Name}<{string.Join(",", substitutedTypeArgs.Select(GetTypeCacheKey))}>";
                    if (_monomorphizedStructs.ContainsKey(newCacheKey))
                    {
                        return _monomorphizedStructs[newCacheKey];
                    }

                    // Create the monomorphized struct by substituting field types
                    var baseStruct = _structs.ContainsKey(structType.Name) ? _structs[structType.Name] : structType;
                    var typeSubstitutions = new Dictionary<string, IrType>();

                    // Build substitution map from generic params to concrete types
                    for (int i = 0; i < Math.Min(baseStruct.GenericParameters.Count, substitutedTypeArgs.Count); i++)
                    {
                        typeSubstitutions[baseStruct.GenericParameters[i]] = substitutedTypeArgs[i];
                    }

                    // Substitute field types
                    var monomorphizedFields = baseStruct.Fields.Select(f =>
                        new IrStructField(f.Name, SubstituteGenericTypes(f.Type, typeSubstitutions))
                    ).ToList();

                    var monomorphizedStruct = new IrStructType(
                        baseStruct.Name,
                        monomorphizedFields,
                        new List<string>(), // No generic parameters - fully concrete
                        newCacheKey
                    );

                    _monomorphizedStructs[newCacheKey] = monomorphizedStruct;
                    return monomorphizedStruct;
                }
            }
        }

        return type;
    }

    /// <summary>
    /// Monomorphize a generic function by applying type substitutions
    /// </summary>
    private FunctionSymbol MonomorphizeFunction(FunctionSymbol genericFunc, Dictionary<string, IrType> substitutions)
    {
        // Create cache key: FunctionName<TypeArg1,TypeArg2,...>
        var typeArgKeys = genericFunc.GenericParameters!.Select(p =>
            substitutions.ContainsKey(p) ? GetTypeCacheKey(substitutions[p]) : p);
        var cacheKey = $"{genericFunc.Name}<{string.Join(",", typeArgKeys)}>";

        // Check cache
        if (_monomorphizedFunctions.ContainsKey(cacheKey))
        {
            return _monomorphizedFunctions[cacheKey];
        }

        // Substitute types in parameters
        var monomorphizedParams = genericFunc.Parameters.Select(p =>
            new ParameterSymbol(p.Name, SubstituteGenericTypes(p.Type, substitutions), p.Location, p.IsVariadic)
        ).ToList();

        // Substitute return type
        var monomorphizedReturnType = SubstituteGenericTypes(genericFunc.ReturnType, substitutions);

        // Create mangled name: OriginalName_TypeArg1_TypeArg2
        var typeArgNames = genericFunc.GenericParameters!.Select(p =>
            substitutions.ContainsKey(p) ? substitutions[p].Name.Replace("<", "_").Replace(">", "_").Replace("*", "ptr_") : p);
        var mangledName = $"{genericFunc.Name}_{string.Join("_", typeArgNames)}";

        // Create monomorphized function
        var monomorphizedFunc = new FunctionSymbol(
            mangledName,
            monomorphizedReturnType,
            monomorphizedParams,
            genericFunc.Location,
            genericFunc.IsExtern,
            null,  // No longer generic
            genericFunc.Attributes,
            genericFunc.IsVariadic
        );

        // Cache it
        _monomorphizedFunctions[cacheKey] = monomorphizedFunc;

        return monomorphizedFunc;
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
                        // Before reporting an error, check if this might be an impl method
                        // (e.g., Option::FromPointer instead of a variant constructor)
                        // The method would be stored as "TypeName::methodName" in _functions
                        if (!_functions.ContainsKey(functionName))
                        {
                            var location = SourceLocationHelper.FromToken(identifierExpr.identifier().Start, _filePath, _sourceLines);
                            _diagnostics.ReportError(
                                "E0037",
                                $"enum '{enumName}' has no variant '{variantName}'",
                                location
                            );
                            return null;
                        }
                        // Not a variant, might be an impl method - fall through to normal function call handling
                    }
                    else
                    {
                        // Validate argument count for variant constructor
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
                    }  // end of else block for variant != null
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

        // Check if this function requires unsafe context
        CheckUnsafeFunctionCall(context, functionName);

        // Check if this is a generic function that needs monomorphization
        if (function.GenericParameters != null && function.GenericParameters.Count > 0)
        {
            // Collect argument types
            var argTypes = new List<IrType>();
            if (context.argumentList() != null)
            {
                foreach (var arg in context.argumentList().expression())
                {
                    var argType = Visit(arg);
                    if (argType == null)
                    {
                        // Type error in argument - abort
                        return function.ReturnType;
                    }
                    argTypes.Add(argType);
                }
            }

            // Infer generic type parameters from arguments
            var paramTypes = function.Parameters.Select(p => p.Type).ToList();
            var substitutions = InferGenericTypes(function.GenericParameters, paramTypes, argTypes);

            // If argument-based inference failed, try inference from expected type
            if (substitutions == null && _expectedType != null)
            {
                substitutions = new Dictionary<string, IrType>();
                if (InferGenericTypeFromPair(function.ReturnType, _expectedType, substitutions))
                {
                    // Check if all generic parameters were inferred
                    var allInferred = true;
                    foreach (var param in function.GenericParameters)
                    {
                        if (!substitutions.ContainsKey(param))
                        {
                            allInferred = false;
                            break;
                        }
                    }

                    if (!allInferred)
                    {
                        substitutions = null; // Failed to infer all parameters
                    }
                }
                else
                {
                    substitutions = null; // Inference failed
                }
            }

            if (substitutions == null)
            {
                // Could not infer generic types - report error
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0043",
                    $"could not infer generic type parameters for function '{functionName}'",
                    location,
                    helpTexts: new List<string>
                    {
                        $"generic parameters: {string.Join(", ", function.GenericParameters)}",
                        "consider providing explicit type annotations"
                    }
                );
                return function.ReturnType;
            }

            // Monomorphize the function
            function = MonomorphizeFunction(function, substitutions);
            // Continue with monomorphized function for validation and return type
        }

        // Validate argument count
        // For variadic functions, we need at least as many args as non-variadic parameters
        var nonVariadicParamCount = function.Parameters.Count(p => !p.IsVariadic);
        var minArgCount = nonVariadicParamCount;
        var maxArgCount = function.IsVariadic ? int.MaxValue : function.Parameters.Count;

        if (argCount < minArgCount || argCount > maxArgCount)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            var expectedMsg = function.IsVariadic
                ? $"at least {minArgCount} argument(s)"
                : $"{function.Parameters.Count} argument(s)";
            _diagnostics.ReportError(
                "E0014",
                $"function '{functionName}' expects {expectedMsg}, but {argCount} were provided",
                location,
                helpTexts: new List<string>
                {
                    function.Parameters.Count == 0
                        ? $"try calling: {functionName}()"
                        : $"expected: {functionName}({string.Join(", ", function.Parameters.Where(p => !p.IsVariadic).Select(p => $"{p.Name}: {TypeToString(p.Type)}"))})"
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

                // For variadic functions, skip type checking for args beyond non-variadic params
                if (function.IsVariadic && i >= nonVariadicParamCount)
                {
                    continue; // Extra args for variadic function - no type checking needed
                }

                var paramType = function.Parameters[i].Type;

                if (argType != null && !TypesCompatible(paramType, argType))
                {
                    var location = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);

                    // Check if this is a function pointer mismatch - give detailed error
                    if (paramType is IrFunctionPointerType expectedFp && argType is IrFunctionPointerType actualFp)
                    {
                        var helpTexts = new List<string>
                        {
                            $"argument {i + 1} ('{function.Parameters[i].Name}'): function pointer signature mismatch"
                        };

                        // Check what's wrong: parameter count, parameter types, or return type
                        if (expectedFp.ParameterTypes.Count != actualFp.ParameterTypes.Count)
                        {
                            helpTexts.Add($"  expected {expectedFp.ParameterTypes.Count} parameter(s), found {actualFp.ParameterTypes.Count}");
                        }
                        else
                        {
                            // Check each parameter type
                            for (int j = 0; j < expectedFp.ParameterTypes.Count; j++)
                            {
                                if (!TypesCompatible(expectedFp.ParameterTypes[j], actualFp.ParameterTypes[j]))
                                {
                                    helpTexts.Add($"  parameter {j + 1}: expected '{TypeToString(expectedFp.ParameterTypes[j])}', found '{TypeToString(actualFp.ParameterTypes[j])}'");
                                }
                            }
                        }

                        // Check return type
                        if (!TypesCompatible(expectedFp.ReturnType, actualFp.ReturnType))
                        {
                            helpTexts.Add($"  return type: expected '{TypeToString(expectedFp.ReturnType)}', found '{TypeToString(actualFp.ReturnType)}'");
                        }

                        helpTexts.Add($"expected signature: {TypeToString(paramType)}");
                        helpTexts.Add($"found signature:    {TypeToString(argType)}");

                        _diagnostics.ReportError(
                            "E0015",
                            $"function pointer signature mismatch",
                            location,
                            helpTexts: helpTexts
                        );
                    }
                    else
                    {
                        // Regular type mismatch
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
            typeName = structType.StructName;  // Use StructName to get base name without generic params
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
                typeName = pointeeStruct.StructName;  // Use StructName to get base name without generic params
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

                // Skip type checking if parameter type is still a generic parameter (will be inferred later)
                // This allows Vec::new() followed by vec.push(42i32) to work with type inference
                if (paramType is not IrGenericType)
                {
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
            var selfType = _variables["self"].Type;
            if (selfType is IrStructType structType)
            {
            }
            else if (selfType is IrPointerType ptrType)
            {
                if (ptrType.PointeeType is IrStructType innerStruct)
                {
                }
            }
            return selfType;
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

                    if (variant != null)
                    {
                        // Found a variant, return the enum type - this will be used when called as a constructor
                        // If we have an expected type that's a monomorphization of this enum, use that
                        if (_expectedType is IrEnumType expectedEnum &&
                            expectedEnum.EnumName == enumType.EnumName &&
                            enumType.GenericParameters.Count > 0)
                        {
                            // Expected type is a specialized/monomorphized version (e.g., Option<*T> or Option<i32>)
                            // of the generic enum (e.g., Option<T>), so use the expected type
                            // to get the correct type arguments
                            return expectedEnum;
                        }
                        return enumType;
                    }

                    // variant == null, so check if this might be an impl method
                    // (e.g., Option::FromPointer instead of a variant)
                    // The method would be stored as "TypeName::methodName" in _functions
                    if (!_functions.ContainsKey(name))
                    {
                        var location = SourceLocationHelper.FromToken(context.identifier().Start, _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0037",
                            $"enum '{enumName}' has no variant '{variantName}'",
                            location
                        );
                        return null;
                    }
                    // Fall through to check _functions below at line 3610
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

        // If it's a function name being used as a value (not being called), return function pointer type
        if (_functions.ContainsKey(name))
        {
            var func = _functions[name];
            // Create a function pointer type
            var funcPtrType = _typeInterner.GetFunctionPointerType(
                func.Parameters.Select(p => p.Type).ToList(),
                func.ReturnType
            );
            return funcPtrType;
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
        var exprText = context.expression().GetText();

        var baseType = Visit(context.expression());
        if (baseType == null)
        {
            return null;
        }

        var memberName = context.IDENTIFIER().GetText();

        if (exprText == "self")
        {
            if (baseType is IrStructType st)
            {
            }
            else if (baseType is IrPointerType pt && pt.PointeeType is IrStructType pts)
            {
            }
        }

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

    public override IrType? VisitIndexExpr([NotNull] NovusParser.IndexExprContext context)
    {
        // Get the base expression type (e.g., pointer, array, slice)
        var baseType = Visit(context.expression(0));
        if (baseType == null)
        {
            return null;
        }

        // Get the index expression type
        var indexType = Visit(context.expression(1));
        if (indexType == null)
        {
            return null;
        }

        // Index must be an integer type
        if (indexType is not IrIntType)
        {
            var location = SourceLocationHelper.FromContext(context.expression(1), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0023",
                $"index must be an integer type, found '{TypeToString(indexType)}'",
                location
            );
            return null;
        }

        // Auto-dereference pointers (like Rust/Swift)
        if (baseType is IrPointerType ptrType)
        {
            // ptr[index] returns the element type
            return ptrType.PointeeType;
        }
        else if (baseType is IrReferenceType refType && refType.PointeeType is IrPointerType refPtrType)
        {
            // &ptr[index] returns the element type
            return refPtrType.PointeeType;
        }
        else if (baseType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrPointerType mutRefPtrType)
        {
            // &mut ptr[index] returns the element type
            return mutRefPtrType.PointeeType;
        }
        else if (baseType is IrArrayType arrayType)
        {
            // array[index] returns the element type
            return arrayType.ElementType;
        }
        else
        {
            var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0024",
                $"cannot index into type '{TypeToString(baseType)}'",
                location,
                helpTexts: new List<string>
                {
                    "indexing is only valid on pointers and arrays"
                }
            );
            return null;
        }
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

    public override IrType? VisitTernaryExpr([NotNull] NovusParser.TernaryExprContext context)
    {
        // Visit all three expressions
        var conditionType = Visit(context.expression(0));
        var trueType = Visit(context.expression(1));
        var falseType = Visit(context.expression(2));

        if (conditionType == null || trueType == null || falseType == null)
            return null;

        // Check that condition is boolean or numeric
        if (!IsBoolOrNumericType(conditionType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0040",
                $"ternary condition must be boolean or numeric type, found '{TypeToString(conditionType)}'",
                location
            );
        }

        // Both branches must have compatible types
        if (!TypesCompatible(trueType, falseType) && !TypesCompatible(falseType, trueType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0041",
                $"ternary branches have incompatible types: '{TypeToString(trueType)}' and '{TypeToString(falseType)}'",
                location
            );
        }

        // Return the type of the true branch (they should be compatible)
        return trueType;
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
        System.Console.WriteLine($"DEBUG: VisitPostIncrementExpr ENTERED, expr text = '{context.expression().GetText()}'");
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

        // Verify it's an lvalue
        bool isLvalue = false;
        if (context.expression() is NovusParser.PrimaryExprContext primaryCtx)
        {
            var primaryExpr = primaryCtx.primaryExpression();
            if (primaryExpr is NovusParser.IdentifierExprContext)
            {
                isLvalue = true;
            }
        }
        else if (context.expression() is NovusParser.MemberAccessExprContext ||
                 context.expression() is NovusParser.IndexExprContext ||
                 context.expression() is NovusParser.DereferenceExprContext)
        {
            isLvalue = true;
        }

        if (!isLvalue)
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0027",
                "operator '++' requires an lvalue",
                location,
                helpTexts: new List<string>
                {
                    "only variables, member accesses, array elements, or dereferences can be incremented"
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

        // Verify it's an lvalue
        bool isLvalue = false;
        if (context.expression() is NovusParser.PrimaryExprContext primaryCtx)
        {
            var primaryExpr = primaryCtx.primaryExpression();
            if (primaryExpr is NovusParser.IdentifierExprContext)
            {
                isLvalue = true;
            }
        }
        else if (context.expression() is NovusParser.MemberAccessExprContext ||
                 context.expression() is NovusParser.IndexExprContext ||
                 context.expression() is NovusParser.DereferenceExprContext)
        {
            isLvalue = true;
        }

        if (!isLvalue)
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0027",
                "operator '--' requires an lvalue",
                location,
                helpTexts: new List<string>
                {
                    "only variables, member accesses, array elements, or dereferences can be decremented"
                }
            );
        }

        return operandType;
    }

    public override IrType? VisitPreIncrementExpr([NotNull] NovusParser.PreIncrementExprContext context)
    {
        System.Console.WriteLine("DEBUG: VisitPreIncrementExpr ENTERED");
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

        // Verify it's an lvalue
        bool isLvalue = false;
        var expr = context.expression();
        System.Console.WriteLine($"DEBUG PreInc: Expression type = {expr.GetType().Name}, Text = '{expr.GetText()}'");
        if (expr is NovusParser.PrimaryExprContext primaryCtx)
        {
            var primaryExpr = primaryCtx.primaryExpression();
            System.Console.WriteLine($"DEBUG PreInc: PrimaryExpression type = {primaryExpr?.GetType().Name}");
            if (primaryExpr is NovusParser.IdentifierExprContext)
            {
                isLvalue = true;
            }
        }
        else if (expr is NovusParser.MemberAccessExprContext ||
                 expr is NovusParser.IndexExprContext ||
                 expr is NovusParser.DereferenceExprContext)
        {
            isLvalue = true;
        }

        if (!isLvalue)
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0027",
                "operator '++' requires an lvalue",
                location,
                helpTexts: new List<string>
                {
                    "only variables, member accesses, array elements, or dereferences can be incremented"
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

        // Verify it's an lvalue
        bool isLvalue = false;
        if (context.expression() is NovusParser.PrimaryExprContext primaryCtx)
        {
            var primaryExpr = primaryCtx.primaryExpression();
            if (primaryExpr is NovusParser.IdentifierExprContext)
            {
                isLvalue = true;
            }
        }
        else if (context.expression() is NovusParser.MemberAccessExprContext ||
                 context.expression() is NovusParser.IndexExprContext ||
                 context.expression() is NovusParser.DereferenceExprContext)
        {
            isLvalue = true;
        }

        if (!isLvalue)
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0027",
                "operator '--' requires an lvalue",
                location,
                helpTexts: new List<string>
                {
                    "only variables, member accesses, array elements, or dereferences can be decremented"
                }
            );
        }

        return operandType;
    }

    public override IrType? VisitParenExpr([NotNull] NovusParser.ParenExprContext context)
    {
        return Visit(context.expression());
    }

    public override IrType? VisitTurboFishExpr([NotNull] NovusParser.TurboFishExprContext context)
    {
        // Turbo-fish expressions (Type::<Args>) are allowed through semantic analysis
        // Type checking will be handled in IR building phase
        return null;
    }

    public override IrType? VisitPathExpr([NotNull] NovusParser.PathExprContext context)
    {
        // Handle path expressions: Type::name or Type::<Args>::name
        // This can be:
        // 1. Enum variants: Option::Some, Result::Ok
        // 2. Associated functions (static methods): Vec::new, Vec::with_capacity
        // 3. Generic associated functions: Vec::<u32>::with_capacity
        var baseExpr = context.expression();
        var memberName = context.IDENTIFIER().GetText();

        // The base expression should be either:
        // 1. A primary expression containing an identifier (Vec::method)
        // 2. A turbo-fish expression (Vec::<u32>::method)
        string? typeName = null;
        List<IrType>? explicitTypeArgs = null;

        if (baseExpr is NovusParser.TurboFishExprContext turboFishCtx)
        {
            // Extract type name from the turbo-fish expression
            var turboBaseExpr = turboFishCtx.expression();
            if (turboBaseExpr is NovusParser.PrimaryExprContext primaryCtx &&
                primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
            {
                typeName = identCtx.identifier().GetText();

                // Parse the explicit type arguments
                explicitTypeArgs = new List<IrType>();
                foreach (var typeCtx in turboFishCtx.genericTypeArgs().typeList().type())
                {
                    var irType = ParseType(typeCtx);
                    explicitTypeArgs.Add(irType);
                }
            }
        }
        else if (baseExpr is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            typeName = identCtx.identifier().GetText();
        }

        if (typeName == null)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0032",
                "path expression base must be a type identifier or turbo-fish expression",
                location,
                helpTexts: new List<string>
                {
                    "expected format: TypeName::member or TypeName::<Args>::member"
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
            // If we have explicit type arguments (turbo-fish), substitute them in the return type
            if (explicitTypeArgs != null && explicitTypeArgs.Count > 0)
            {
                // Get the struct to find its generic parameters
                if (_structs.ContainsKey(typeName))
                {
                    var structType = _structs[typeName];
                    if (structType.GenericParameters.Count != explicitTypeArgs.Count)
                    {
                        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0050",
                            $"wrong number of type arguments for '{typeName}': expected {structType.GenericParameters.Count}, got {explicitTypeArgs.Count}",
                            location
                        );
                        return null;
                    }

                    // Build substitution map: generic param name -> concrete type
                    var substitutions = new Dictionary<string, IrType>();
                    for (int i = 0; i < structType.GenericParameters.Count; i++)
                    {
                        substitutions[structType.GenericParameters[i]] = explicitTypeArgs[i];
                    }

                    // Substitute generic parameters in the return type
                    return SubstituteGenericTypes(funcSymbol.ReturnType, substitutions);
                }
            }

            // No explicit type args - return the function's return type as-is
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

    public override IrType? VisitStructArrayInit([NotNull] NovusParser.StructArrayInitContext context)
    {
        // Handle Vec { {10, 20, 30} } syntax
        var structName = context.typeName().GetText();

        // Check if struct type exists
        if (!_structs.ContainsKey(structName))
        {
            var location = SourceLocationHelper.FromContext(context.typeName(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0023",
                $"unknown struct type '{structName}'",
                location
            );
            return null;
        }

        // Validate the array literal expression
        var arrayType = Visit(context.expression());
        if (arrayType == null)
        {
            return null;  // Error already reported
        }

        // Return the struct type
        return _structs[structName];
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
            NovusParser.ArrayTypeWithSizeContext arrayWithSizeCtx => ParseArrayTypeWithSize(arrayWithSizeCtx),
            NovusParser.ArrayTypeInferredContext arrayInferredCtx => ParseArrayTypeInferred(arrayInferredCtx),
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

                // Validate generic constraints
                var structLocation = SourceLocationHelper.FromToken(context.typeName().Start, _filePath, _sourceLines);
                if (!ValidateGenericConstraints(structType.WhereClause, structType.GenericParameters, typeArgs, structLocation))
                {
                    // Error already reported by ValidateGenericConstraints
                    return IrIntType.I32;
                }

                // NOTE: Even if type arguments contain generics (e.g., *T), we proceed to create a specialized struct
                // This allows Vec<*T> to be distinct from Vec<T>

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

                // Validate generic constraints
                var enumLocation = SourceLocationHelper.FromToken(context.typeName().Start, _filePath, _sourceLines);
                if (!ValidateGenericConstraints(enumType.WhereClause, enumType.GenericParameters, typeArgs, enumLocation))
                {
                    // Error already reported by ValidateGenericConstraints
                    return IrIntType.I32;
                }

                // NOTE: Even if type arguments contain generics (e.g., *T), we proceed to create a specialized enum
                // This allows Option<*T> to be distinct from Option<T>

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

                // Create new enum type - preserve generic parameters if any type args still contain generics
                var hasGenerics = typeArgs.Any(t => ContainsGenericType(t));
                var genericParams = hasGenerics ? enumType.GenericParameters : null;
                var monomorphizedEnum = new IrEnumType(enumType.EnumName, monomorphizedVariants, genericParams, cacheKey);

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

    private IrType ParseArrayTypeWithSize(NovusParser.ArrayTypeWithSizeContext context)
    {
        // Evaluate the size expression as a compile-time constant
        var sizeExpr = context.expression();
        var evaluator = new ConstantExpressionEvaluator(
            _constants.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value),
            errorMsg => _diagnostics.ReportError(
                "E0030",
                $"error evaluating array size: {errorMsg}",
                SourceLocationHelper.FromToken(sizeExpr.Start, _filePath, _sourceLines),
                new List<string>
                {
                    "array sizes must be compile-time constant expressions",
                    "only integer literals, constants, and arithmetic operations are allowed"
                }
            )
        );

        var sizeValue = evaluator.Visit(sizeExpr);

        if (!sizeValue.HasValue)
        {
            _diagnostics.ReportError(
                "E0031",
                "array size must be a compile-time constant expression",
                SourceLocationHelper.FromToken(sizeExpr.Start, _filePath, _sourceLines),
                new List<string>
                {
                    "array sizes must be known at compile time",
                    "use integer literals or const expressions"
                }
            );
            sizeValue = 0; // fallback
        }

        var elementType = ParseType(context.type());
        return _typeInterner.GetArrayType(elementType, sizeValue.Value);
    }

    private IrType ParseArrayTypeInferred(NovusParser.ArrayTypeInferredContext context)
    {
        // For inferred size arrays, we create a placeholder with size -1
        // The actual size will be determined when we parse the array literal initializer
        var elementType = ParseType(context.type());
        // Use size -1 as a sentinel value to indicate "size to be inferred"
        return _typeInterner.GetArrayType(elementType, -1);
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

        // For monomorphized enums, compare by cache key (handles Option<*u8> vs Option<*u8>)
        if (expected is IrEnumType expEnum && actual is IrEnumType actEnum)
        {
            if (expEnum.CacheKey != null && actEnum.CacheKey != null)
            {
                bool match = expEnum.CacheKey == actEnum.CacheKey;
                return match;
            }
            // If either isn't monomorphized, fall through to structural comparison
        }

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

        // Allow integer (especially 0) to be used as null pointer
        // This enables: let ptr: *T = 0
        if (expected is IrPointerType && actual is IrIntType)
        {
            return true;
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
        if (type is IrStringType)
        {
            return "String";
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

    /// <summary>
    /// Parse a where clause from the AST into an IrWhereClause
    /// </summary>
    private IrWhereClause? ParseWhereClause(NovusParser.WhereClauseContext? context)
    {
        if (context == null)
            return null;

        var constraints = new List<IrTypeConstraint>();

        foreach (var boundCtx in context.whereBound())
        {
            var typeParam = boundCtx.IDENTIFIER().GetText();
            var bounds = ParseTraitBound(boundCtx.traitBound());
            constraints.Add(new IrTypeConstraint(typeParam, bounds));
        }

        return new IrWhereClause(constraints);
    }

    /// <summary>
    /// Parse a trait bound (potentially with multiple traits separated by +)
    /// </summary>
    private List<IrTraitBound> ParseTraitBound(NovusParser.TraitBoundContext context)
    {
        var bounds = new List<IrTraitBound>();

        if (context is NovusParser.SingleTraitBoundContext singleBound)
        {
            // Parse trait name and optional type arguments
            var traitName = singleBound.typeName().GetText();
            var typeArgs = new List<IrType>();

            if (singleBound.genericTypeArgs() != null)
            {
                foreach (var typeCtx in singleBound.genericTypeArgs().typeList().type())
                {
                    typeArgs.Add(ParseType(typeCtx));
                }
            }

            bounds.Add(new IrTraitBound(traitName, typeArgs));
        }
        else if (context is NovusParser.MultipleTraitBoundContext multipleBound)
        {
            // Recursively parse both sides of the +
            bounds.AddRange(ParseTraitBound(multipleBound.traitBound(0)));
            bounds.AddRange(ParseTraitBound(multipleBound.traitBound(1)));
        }

        return bounds;
    }

    /// <summary>
    /// Check if a type satisfies all trait bounds
    /// </summary>
    private bool TypeSatisfiesBounds(IrType type, List<IrTraitBound> bounds, SourceLocation location)
    {
        foreach (var bound in bounds)
        {
            if (!TypeImplementsTrait(type, bound.TraitName, bound.TraitTypeArgs))
            {
                _diagnostics.ReportError(
                    "E0100",
                    $"type '{TypeToString(type)}' does not implement trait '{bound}'",
                    location,
                    helpTexts: new List<string>
                    {
                        $"the trait bound '{TypeToString(type)}: {bound}' is not satisfied",
                        $"add an impl block: impl {bound} for {TypeToString(type)}"
                    }
                );
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Check if a type implements a specific trait
    /// </summary>
    private bool TypeImplementsTrait(IrType type, string traitName, List<IrType> traitTypeArgs)
    {
        // Validate that the trait exists
        if (!_traits.ContainsKey(traitName))
        {
            return false; // Unknown trait
        }

        // Extract the base type name from the IR type
        string typeName = GetBaseTypeName(type);

        // Build the lookup key for this specific trait impl
        // Format: "TypeName::TraitName<Arg1,Arg2,...>"
        var traitArgsStr = traitTypeArgs.Count > 0
            ? $"<{string.Join(",", traitTypeArgs.Select(t => GetTypeCacheKey(t)))}>"
            : "";
        var implKey = $"{typeName}::{traitName}{traitArgsStr}";

        // Check if we have an exact match for this trait impl
        if (_traitImpls.ContainsKey(implKey))
        {
            return true;
        }

        // For generic impls, we need to check if there's a generic impl that could satisfy this
        // Example: impl<T> Iterator<T> for Vec<T> should match Vec<i32> with Iterator<i32>
        foreach (var kvp in _traitImpls)
        {
            var implInfo = kvp.Value;

            // Check if this is the right trait
            if (implInfo.TraitName != traitName)
                continue;

            // Check if the type names match
            if (implInfo.TypeName != typeName)
                continue;

            // If the impl has generic parameters, we need to check if the trait type args
            // can be unified with the constraint's trait type args
            if (implInfo.ImplGenericParams.Count > 0)
            {
                // Generic impl exists - assume it can be monomorphized to satisfy this constraint
                // Full unification would require more complex type checking
                return true;
            }

            // Check if trait type arguments match exactly
            if (TraitTypeArgsMatch(implInfo.TraitTypeArgs, traitTypeArgs))
            {
                return true;
            }
        }

        return false; // No impl found
    }

    /// <summary>
    /// Extract the base type name from an IR type
    /// Handles various type wrappers (pointers, arrays, etc.)
    /// </summary>
    private string GetBaseTypeName(IrType type)
    {
        return type switch
        {
            IrStructType structType => structType.StructName,
            IrEnumType enumType => enumType.EnumName,
            IrPointerType ptrType => GetBaseTypeName(ptrType.PointeeType),
            IrArrayType arrayType => GetBaseTypeName(arrayType.ElementType),
            IrIntType intType => intType.IsSigned ? $"i{intType.BitWidth}" : $"u{intType.BitWidth}",
            IrBoolType => "bool",
            IrStringType => "String",
            _ => type.Name
        };
    }

    /// <summary>
    /// Check if two lists of trait type arguments match
    /// </summary>
    private bool TraitTypeArgsMatch(List<IrType> args1, List<IrType> args2)
    {
        if (args1.Count != args2.Count)
            return false;

        for (int i = 0; i < args1.Count; i++)
        {
            // For now, do simple cache key comparison
            // A more sophisticated implementation would need full type unification
            if (GetTypeCacheKey(args1[i]) != GetTypeCacheKey(args2[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Validate generic constraints when monomorphizing a type
    /// </summary>
    private bool ValidateGenericConstraints(
        IrWhereClause? whereClause,
        List<string> genericParams,
        List<IrType> typeArgs,
        SourceLocation location)
    {
        if (whereClause == null || whereClause.Constraints.Count == 0)
            return true;

        // Build substitution map from generic parameters to concrete types
        var substitutions = new Dictionary<string, IrType>();
        for (int i = 0; i < genericParams.Count; i++)
        {
            substitutions[genericParams[i]] = typeArgs[i];
        }

        // Check each constraint
        foreach (var constraint in whereClause.Constraints)
        {
            // Get the concrete type for this constrained parameter
            if (!substitutions.ContainsKey(constraint.TypeParameter))
                continue;

            var concreteType = substitutions[constraint.TypeParameter];

            // Check if the concrete type satisfies all bounds
            if (!TypeSatisfiesBounds(concreteType, constraint.Bounds, location))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Helper record to store trait implementation information for constraint checking
    /// </summary>
    private record TraitImplInfo(
        string TypeName,              // The type implementing the trait (e.g., "Vec", "Counter")
        string TraitName,             // Trait being implemented (e.g., "Iterator")
        List<IrType> TraitTypeArgs,   // Type args for the trait (e.g., [i32] for Iterator<i32>)
        List<string> ImplGenericParams, // Generic params on the impl block itself
        SourceLocation Location       // Where the impl was declared
    );
}

// Symbol table classes
public record FunctionSymbol(
    string Name,
    IrType ReturnType,
    List<ParameterSymbol> Parameters,
    SourceLocation Location,
    bool IsExtern = false,
    List<string>? GenericParameters = null,  // Generic type parameters (e.g., ["T"] for Option::FromPointer)
    AttributeCollection? Attributes = null,  // Function attributes (@inline, @test, etc.)
    bool IsVariadic = false  // true if function accepts variable number of arguments (...)
);
public record ParameterSymbol(string Name, IrType Type, SourceLocation Location, bool IsVariadic = false);
public record VariableSymbol(
    string Name,
    IrType Type,
    bool IsMutable,
    SourceLocation Location,
    AttributeCollection? Attributes = null  // Variable attributes
);
public record ConstantSymbol(
    string Name,
    IrType Type,
    object Value,
    SourceLocation Location,
    AttributeCollection? Attributes = null  // Constant attributes
);
