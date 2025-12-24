using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Novus.Assets;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Frontend.Generics;
using Novus.IR;
using Novus.Parser;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Performs semantic analysis on the parsed AST
/// Reports errors and warnings with helpful messages
/// </summary>
public class SemanticAnalyzer : NovusParserBaseVisitor<IrType?>
{
    private readonly DiagnosticBag _diagnostics = new();
    private readonly string _filePath;
    private readonly string[] _sourceLines;

    // Symbol tables
    private readonly SymbolTable _symbols = new();
    private readonly Dictionary<string, FunctionSymbol> _functions = new();
    private readonly Dictionary<string, VariableSymbol> _variables = new();
    private readonly Dictionary<string, VariableSymbol> _globalVariables = new(); // Module-level extern vars
    private readonly Dictionary<string, string> _importedNames = new(); // Maps imported name -> module name
    private readonly HashSet<string> _importedModules = new(); // Track which modules have been imported (by path)
    private FunctionSymbol? _currentFunction;
    private IrWhereClause? _currentStructWhereClause; // Track where clause for current struct/impl block
    private IrWhereClause? _currentFunctionWhereClause; // Track where clause for current function
    private int _loopDepth = 0; // Track loop nesting for break validation
    private readonly string _stdLibPath; // Path to standard library
    private Dictionary<string, bool>? _preprocessorConstants; // Preprocessor constants for imports

    // Location tracking for types (LSP support)
    private readonly Dictionary<string, SourceLocation> _structLocations = new();
    private readonly Dictionary<string, SourceLocation> _enumLocations = new();
    private readonly Dictionary<string, SourceLocation> _traitLocations = new();

    // Documentation comments (LSP support)
    private readonly Dictionary<string, string> _docComments = new();  // key = symbol name, value = doc comment text

    // Unsafe block tracking
    private int _unsafeDepth = 0; // Track unsafe block nesting
    private readonly List<UnsafeBlockInfo> _unsafeBlocks = new(); // Collect unsafe blocks for warnings

    // Warning suppression tracking
    private readonly HashSet<string> _currentFunctionSuppressedWarnings = new(); // Track suppressed warnings for current function

    public class UnsafeBlockInfo
    {
        public string FilePath { get; set; } = "";
        public int Line { get; set; }
        public int Column { get; set; }
        public int LineCount { get; set; }
        public string Reason { get; set; } = "";
    }

    public IReadOnlyList<UnsafeBlockInfo> UnsafeBlocks => _unsafeBlocks;

    /// <summary>
    /// Parsed metadata from an impl block declaration.
    /// Used to consolidate duplicate parsing logic between RegisterImpl and AnalyzeImplBlock.
    /// </summary>
    private record struct ImplBlockInfo(
        List<string> GenericParams,
        bool IsTraitImpl,
        string? TraitName,
        List<IrType> TraitTypeArgs,
        string ImplTypeName,
        bool ParseError
    );

    // Borrow checker for move tracking and memory safety
    private readonly Dictionary<int, DropInfo> _dropInfo = new();  // VariableId -> DropInfo
    private readonly BorrowChecker _borrowChecker;
    private int _nextVariableId = 1;  // Counter for generating unique variable IDs

    // Drop tracking for automatic resource cleanup (RAII)
    private readonly Stack<ScopeDropInfo> _dropScopes = new();

    // Generic type parameters in scope (for generic enum/struct definitions)
    private readonly Dictionary<string, IrGenericType> _genericParams = new();

    // Const generic parameters in scope (for const generics like <const N: u32>)
    private readonly Dictionary<string, IrConstGenericParam> _constGenericParams = new();

    // Note: Monomorphization caches are now managed by SymbolTable to ensure consistency
    // and avoid duplication. Use _symbols.RegisterMonomorphized*() and _symbols.LookupMonomorphized*()
    // instead of maintaining separate caches here.

    // Trait resolver for checking trait implementations and generic constraints
    private readonly TraitResolver _traitResolver;

    // Expected type for bidirectional type checking (flows down from context)
    private IrType? _expectedType = null;

    // Type interning system for efficient type equality
    private readonly TypeInterner _typeInterner = new();

    // Type parser for shared parsing logic
    private readonly TypeParser _typeParser;

    // Track when parsing extern function signatures (skip type validation for FFI)
    private bool _parsingExternFunction = false;

    public DiagnosticBag Diagnostics => _diagnostics;

    // Public read-only access to symbol tables for language server features (go to definition, hover, etc.)
    public IReadOnlyDictionary<string, FunctionSymbol> Functions => _functions;
    public IReadOnlyDictionary<string, VariableSymbol> Variables => _variables;
    public IReadOnlyDictionary<string, VariableSymbol> GlobalVariables => _globalVariables;
    public IReadOnlyDictionary<string, IrStructType> Structs => _symbols.GetLocalStructs();
    public IReadOnlyDictionary<string, IrEnumType> Enums => _symbols.GetLocalEnums();
    public IReadOnlyDictionary<string, IrTrait> Traits => _symbols.GetLocalTraits();
    public IReadOnlyDictionary<string, ConstantSymbol> Constants => _symbols.GetLocalConstants();

    // Public read-only access to type locations (for LSP go-to-definition)
    public IReadOnlyDictionary<string, SourceLocation> StructLocations => _structLocations;
    public IReadOnlyDictionary<string, SourceLocation> EnumLocations => _enumLocations;
    public IReadOnlyDictionary<string, SourceLocation> TraitLocations => _traitLocations;

    // Public read-only access to documentation comments (for LSP hover/completion)
    public IReadOnlyDictionary<string, string> DocComments => _docComments;
    public string SourceText { get; }  // Store source text for doc comment extraction

    /// <summary>
    /// Creates an AnalysisResult containing all data collected during semantic analysis.
    /// This should be called after Analyze() completes and passed to IrBuilder.
    /// </summary>
    public AnalysisResult GetResult()
    {
        return new AnalysisResult(
            success: !_diagnostics.HasErrors,
            diagnostics: _diagnostics,
            filePath: _filePath,
            sourceCode: SourceText,
            functions: _functions,
            variables: _variables,
            globalVariables: _globalVariables,
            structs: _symbols.GetLocalStructs(),
            enums: _symbols.GetLocalEnums(),
            traits: _symbols.GetLocalTraits(),
            constants: _symbols.GetLocalConstants(),
            structLocations: _structLocations,
            enumLocations: _enumLocations,
            traitLocations: _traitLocations,
            docComments: _docComments,
            traitResolver: _traitResolver,
            typeInterner: _typeInterner
        );
    }

    public SemanticAnalyzer(string filePath, string sourceCode, string stdLibPath, Dictionary<string, bool>? preprocessorConstants = null)
    {
        _filePath = filePath;
        _sourceLines = sourceCode.Split('\n');
        _stdLibPath = stdLibPath;
        // Use provided preprocessor constants or defaults
        _preprocessorConstants = preprocessorConstants
            ?? Frontend.IrBuilderConfiguration.GetDefaultPreprocessorConstants();
        SourceText = sourceCode;
        _typeParser = new TypeParser(new SemanticAnalyzerTypeContext(this));
        _traitResolver = new TraitResolver(_symbols)
        {
            GetTypeCacheKeyFn = GetTypeCacheKey
        };
        _borrowChecker = new BorrowChecker(_dropInfo)
        {
            TypeImplementsTraitFn = (type, trait, typeArgs) => _traitResolver.TypeImplementsTrait(type, trait, typeArgs)
        };
    }

    #region Scoped State Accessors

    // These internal methods enable the AnalysisScopes classes to manage state

    /// <summary>Current function being analyzed.</summary>
    internal FunctionSymbol? CurrentFunction => _currentFunction;

    /// <summary>Current where clause for struct/impl block being analyzed.</summary>
    internal IrWhereClause? CurrentStructWhereClause => _currentStructWhereClause;

    /// <summary>Expected type for bidirectional type checking.</summary>
    internal IrType? ExpectedType => _expectedType;

    /// <summary>Warnings suppressed for current function.</summary>
    internal IReadOnlySet<string> CurrentFunctionSuppressedWarnings => _currentFunctionSuppressedWarnings;

    /// <summary>Whether we're inside a loop.</summary>
    internal bool IsInLoop => _loopDepth > 0;

    /// <summary>Whether we're inside an unsafe block.</summary>
    internal bool IsInUnsafe => _unsafeDepth > 0;

    internal void SetCurrentFunction(FunctionSymbol? function) => _currentFunction = function;

    internal void SetCurrentStructWhereClause(IrWhereClause? whereClause) => _currentStructWhereClause = whereClause;

    internal void SetExpectedType(IrType? type) => _expectedType = type;

    internal void ClearFunctionSuppressedWarnings() => _currentFunctionSuppressedWarnings.Clear();

    internal void SetFunctionSuppressedWarnings(IEnumerable<string> warnings)
    {
        _currentFunctionSuppressedWarnings.Clear();
        foreach (var w in warnings)
            _currentFunctionSuppressedWarnings.Add(w);
    }

    internal void IncrementLoopDepth() => _loopDepth++;

    internal void DecrementLoopDepth() => _loopDepth--;

    internal void IncrementUnsafeDepth() => _unsafeDepth++;

    internal void DecrementUnsafeDepth() => _unsafeDepth--;

    internal void PushDropScope() => _dropScopes.Push(new ScopeDropInfo());

    internal void PopDropScope()
    {
        if (_dropScopes.Count > 0)
            _dropScopes.Pop();
    }

    /// <summary>
    /// Creates a scoped context for function analysis.
    /// </summary>
    public FunctionAnalysisScope BeginFunctionAnalysis(FunctionSymbol function)
        => new FunctionAnalysisScope(this, function);

    /// <summary>
    /// Creates a scoped context for loop analysis.
    /// </summary>
    public LoopAnalysisScope BeginLoopAnalysis()
        => new LoopAnalysisScope(this);

    /// <summary>
    /// Creates a scoped context for unsafe block analysis.
    /// </summary>
    public UnsafeAnalysisScope BeginUnsafeAnalysis()
        => new UnsafeAnalysisScope(this);

    /// <summary>
    /// Creates a scoped context for expected type.
    /// </summary>
    public ExpectedTypeScope BeginExpectedType(IrType? type)
        => new ExpectedTypeScope(this, type);

    /// <summary>
    /// Creates a scoped context for struct where clause.
    /// </summary>
    public StructWhereClauseScope BeginStructWhereClause(IrWhereClause? whereClause)
        => new StructWhereClauseScope(this, whereClause);

    #endregion

    #region Reserved Keyword Validation

    /// <summary>
    /// Validates that an identifier is not a reserved keyword (C or Novus).
    /// Reports an error if the identifier conflicts with a reserved word.
    /// </summary>
    /// <param name="name">The identifier name to validate</param>
    /// <param name="location">Source location for error reporting</param>
    /// <param name="context">Context describing where the identifier is used (e.g., "variable", "function", "struct")</param>
    /// <returns>True if the identifier is valid (not reserved), false if it's reserved</returns>
    private bool ValidateNotReservedKeyword(string name, SourceLocation location, string context)
    {
        // Skip validation for underscore (throwaway binding)
        if (name == "_")
            return true;

        // Check for C reserved keywords first (these cause VBCC compilation failures)
        if (ReservedKeywords.IsCKeyword(name))
        {
            var suggestion = ReservedKeywords.GetSuggestedAlternative(name);
            _diagnostics.ReportError(
                ErrorCodes.CReservedKeyword,
                $"'{name}' is a C reserved keyword and cannot be used as a {context} name",
                location,
                helpTexts: new List<string>
                {
                    $"'{name}' is reserved in C and will cause compilation errors when generating code",
                    suggestion != null
                        ? $"consider using '{suggestion}' instead"
                        : $"consider using a different name like '{name}_' or '_{name}'"
                }
            );
            return false;
        }

        // Note: Novus keywords are already caught by the parser/lexer - they can't be
        // parsed as identifiers because they're lexed as keyword tokens (KW_*).
        // This check is here for completeness and future-proofing.
        if (ReservedKeywords.IsNovusKeyword(name))
        {
            _diagnostics.ReportError(
                ErrorCodes.CReservedKeyword, // Reuse same error code for reserved keywords
                $"'{name}' is a Novus reserved keyword and cannot be used as a {context} name",
                location,
                helpTexts: new List<string>
                {
                    $"'{name}' is a keyword in the Novus language",
                    $"consider using a different name like '{name}_' or '_{name}'"
                }
            );
            return false;
        }

        return true;
    }

    #endregion

    public bool Analyze(NovusParser.CompilationUnitContext context)
    {
        // Module-level attributes (stack_size, cpu) are now handled when parsing declaration attributes

        // Pass 0a: Implicitly import all of core module (unless compiling core.novus itself)
        // Don't auto-import std::core when compiling core.novus to prevent circular dependencies
        // But DO import it for other std library modules since they need Option, Result, etc.
        bool isCore = _filePath.EndsWith("core.novus") || _filePath.EndsWith("core" + System.IO.Path.DirectorySeparatorChar);

        if (!isCore)
        {
            ImportModule("std::core", importAll: true);
        }

        // Pass 0b: Process explicit imports
        foreach (var importDecl in context.importDeclaration())
        {
            ProcessImport(importDecl);
        }

        // First pass: register all struct PLACEHOLDERS first (before parsing fields)
        // This allows mutually recursive struct definitions (struct A { b: *B } struct B { a: *A })
        foreach (var structDecl in context.structDeclaration())
        {
            RegisterStructPlaceholder(structDecl);
        }

        // Second pass: register all enum STUBS (names only, no variants)
        // This allows struct fields to reference enums defined later in the file
        foreach (var enumDecl in context.enumDeclaration())
        {
            RegisterEnumStub(enumDecl);
        }

        // Third pass: fill in all struct fields (now all struct and enum names are known)
        foreach (var structDecl in context.structDeclaration())
        {
            FillStructFields(structDecl);
            RegisterDerivedMethods(structDecl);
        }

        // Fourth pass: fill in enum variants (now all struct types are fully defined)
        // CRITICAL: Enum variants may reference structs (e.g., WindowEvent::Refresh(RefreshGuard))
        foreach (var enumDecl in context.enumDeclaration())
        {
            FillEnumVariants(enumDecl);
        }

        // Fifth pass: collect all trait declarations
        foreach (var traitDecl in context.traitDeclaration())
        {
            RegisterTrait(traitDecl);
        }

        // Fourth pass: collect all constant declarations (after types are registered)
        foreach (var constDecl in context.constDeclaration())
        {
            RegisterConstant(constDecl);
        }

        // Fifth pass: collect all static variable declarations
        foreach (var staticDecl in context.staticDeclaration())
        {
            RegisterStatic(staticDecl);
        }

        // Sixth pass: collect all extern variable declarations
        foreach (var globalVarDecl in context.globalVariableDeclaration())
        {
            RegisterGlobalVariable(globalVarDecl);
        }

        // Seventh pass: collect all impl block methods
        foreach (var implDecl in context.implDeclaration())
        {
            RegisterImpl(implDecl);
        }

        // Eighth pass: collect all function declarations
        foreach (var funcDecl in context.functionDeclaration())
        {
            RegisterFunction(funcDecl);
        }

        // Ninth pass: analyze function bodies (including methods from impl blocks)
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
            var (moduleContext, syntaxErrors) = ModuleImportHelper.ParseModuleFile(modulePath, _preprocessorConstants);

            if (moduleContext == null || syntaxErrors > 0)
            {
                _diagnostics.ReportError(
                    "E0026",
                    $"module '{moduleNamespace}' not found in reexport",
                    location ?? new SourceLocation(_filePath, 0, 0, 0, "")
                );
                return;
            }

            // Find and register the specific symbol
            // Check enums
            foreach (var enumDecl in moduleContext.enumDeclaration())
            {
                if (enumDecl.IDENTIFIER().GetText() == symbolName)
                {
                    // Check if enum already exists with variants (fully registered)
                    // This can happen with pub use chains that cause the same enum to be imported multiple times
                    var existingEnum = _symbols.LookupEnum(symbolName);
                    if (existingEnum == null || existingEnum.Variants.Count == 0)
                    {
                        RegisterEnum(enumDecl);
                    }
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
        var (moduleContext, syntaxErrors) = ModuleImportHelper.ParseModuleFile(modulePath, _preprocessorConstants);

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

        // Check if module has already been fully processed
        bool alreadyProcessed = _importedModules.Contains(modulePath);

        if (alreadyProcessed)
        {
            // Even if module is already processed, we still need to handle selective imports
            // This allows: from std::ffi::exec import AllocMem
            //         AND: from std::ffi::exec import FindTask
            // Both imports from the same module
            if (!importAll && importList != null)
            {
                // Build the list of names to import for this specific import statement
                var selectiveImports = ModuleImportHelper.BuildImportNameSet(moduleContext, importAll, importList);

                // Register functions from the already-parsed module
                foreach (var funcDecl in moduleContext.functionDeclaration())
                {
                    var funcName = funcDecl.IDENTIFIER().GetText();
                    if (selectiveImports.Contains(funcName))
                    {
                        // Check if not already imported
                        if (!_functions.ContainsKey(funcName))
                        {
                            RegisterFunction(funcDecl);
                        }
                        _importedNames[funcName] = moduleNamespace;
                    }
                }

                // Register constants
                foreach (var constDecl in moduleContext.constDeclaration())
                {
                    var constName = constDecl.IDENTIFIER().GetText();
                    if (selectiveImports.Contains(constName))
                    {
                        if (!_symbols.HasConstant(constName))
                        {
                            RegisterConstant(constDecl);
                        }
                        _importedNames[constName] = moduleNamespace;
                    }
                }

                // Register types (enums, structs, traits)
                foreach (var enumDecl in moduleContext.enumDeclaration())
                {
                    var enumName = enumDecl.IDENTIFIER().GetText();
                    if (selectiveImports.Contains(enumName))
                    {
                        // Check if enum exists and has variants (fully registered)
                        var existingEnum = _symbols.LookupEnum(enumName);
                        if (existingEnum == null || existingEnum.Variants.Count == 0)
                        {
                            // Enum doesn't exist or is a stub - register the full definition
                            RegisterEnum(enumDecl);
                        }
                        _importedNames[enumName] = moduleNamespace;
                    }
                }

                foreach (var structDecl in moduleContext.structDeclaration())
                {
                    var structName = structDecl.IDENTIFIER().GetText();
                    if (selectiveImports.Contains(structName))
                    {
                        // Check if struct exists and has fields (fully registered)
                        // This mirrors the enum logic above - a struct may exist as a
                        // placeholder stub with empty Fields list
                        var existingStruct = _symbols.LookupStruct(structName);
                        if (existingStruct == null || existingStruct.Fields.Count == 0)
                        {
                            // Struct doesn't exist or is a stub - register the full definition
                            RegisterStruct(structDecl);
                        }
                        _importedNames[structName] = moduleNamespace;
                    }
                }

                foreach (var traitDecl in moduleContext.traitDeclaration())
                {
                    var traitName = traitDecl.IDENTIFIER().GetText();
                    if (selectiveImports.Contains(traitName))
                    {
                        if (!_symbols.HasTrait(traitName))
                        {
                            RegisterTrait(traitDecl);
                        }
                        _importedNames[traitName] = moduleNamespace;
                    }
                }

                // Register global variables (extern var)
                foreach (var globalVarDecl in moduleContext.globalVariableDeclaration())
                {
                    var varName = globalVarDecl.IDENTIFIER().GetText();
                    if (selectiveImports.Contains(varName))
                    {
                        if (!_globalVariables.ContainsKey(varName))
                        {
                            var varType = ParseType(globalVarDecl.type());
                            var varLocation = SourceLocationHelper.FromToken(globalVarDecl.IDENTIFIER().Symbol, modulePath, new string[] { });
                            // extern var is always mutable (the var keyword indicates mutability)
                            _globalVariables[varName] = new VariableSymbol(varName, varType, IsMutable: true, varLocation, Id: _nextVariableId++);
                        }
                        _importedNames[varName] = moduleNamespace;
                    }
                }
            }

            return; // Don't reprocess the entire module
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

            // ALWAYS register stub - even if enum already exists
            // We'll replace it in Pass 2 with the full definition for imported enums
            // This fixes cases where an enum was previously imported with variants,
            // and we're re-importing the same module

            // Register a stub enum type with no variants yet
            // This makes the type name resolvable during variant parsing and trait impl type arg parsing
            // Parse generic parameters for stub so type checking works correctly
            var genericParams = AstParsingHelpers.ParseGenericParameters(enumDecl.genericParams());
            var stubEnum = new IrEnumType(enumName, new List<IrEnumVariant>(), genericParams.Count > 0 ? genericParams : null);
            _symbols.RegisterEnum(enumName, stubEnum);

            // Track stubs that aren't in the import list so we can remove them later
            if (!namesToImport.Contains(enumName))
            {
                enumStubsToCleanup.Add(enumName);
            }
        }

        // CRITICAL: Register ALL struct PLACEHOLDERS from the module FIRST (including private ones)
        // This allows mutually recursive struct definitions (e.g., VSprite -> Bob -> AnimComp -> AnimOb)
        // Private structs must also be registered because public structs may have fields of private types
        // Must happen before filling in fields or enum variants
        foreach (var structDecl in moduleContext.structDeclaration())
        {
            var structName = structDecl.IDENTIFIER().GetText();

            // Skip if this struct has already been imported (transitive dependencies)
            if (_symbols.HasStruct(structName))
            {
                continue;
            }

            // Register placeholder first - fields will be filled in the next pass
            RegisterStructPlaceholder(structDecl);
        }

        // Pass 2: Fill in struct fields now that all struct names are known
        // We must fill in ALL structs (including private ones) because public structs
        // may have fields of private types, and we need the full type information
        foreach (var structDecl in moduleContext.structDeclaration())
        {
            var structName = structDecl.IDENTIFIER().GetText();
            var isPub = ModuleImportHelper.IsPub(structDecl);

            // Check if this struct exists and needs field filling
            var existingStruct = _symbols.LookupStruct(structName);
            if (existingStruct == null)
            {
                continue;
            }

            // Skip if already fully registered (has fields) - from transitive imports
            if (existingStruct.Fields.Count > 0)
            {
                // Mark as imported if it was explicitly requested (only for public structs)
                if (isPub && namesToImport.Contains(structName))
                {
                    _importedNames[structName] = moduleNamespace;
                }
                continue;
            }

            // Fill in the struct fields (placeholder has empty fields)
            FillStructFields(structDecl);
            RegisterDerivedMethods(structDecl);

            // Mark as imported if it was explicitly requested (only for public structs)
            if (isPub && namesToImport.Contains(structName))
            {
                _importedNames[structName] = moduleNamespace;
            }
        }

        // Pass 3: Fill in enum variants for ALL enums in the module
        // This must happen AFTER struct registration so that struct types used in enum variants are available
        // IMPORTANT: We must fill variants for ALL enums (not just imported ones) because:
        // - Imported structs may have fields of non-imported enum types (e.g., HashMapEntry.state: EntryState)
        // - Match expressions on those fields need access to the enum's variants
        // - If we only fill imported enums, non-imported enum stubs remain with 0 variants
        foreach (var enumDecl in moduleContext.enumDeclaration())
        {
            var enumName = enumDecl.IDENTIFIER().GetText();
            var isPub = ModuleImportHelper.IsPub(enumDecl);

            // Check if this enum exists and needs variant filling
            var existingEnum = _symbols.LookupEnum(enumName);
            if (existingEnum == null)
            {
                continue;
            }

            // Skip if already fully registered (has variants) - from transitive imports
            if (existingEnum.Variants.Count > 0)
            {
                // Mark as imported if it was explicitly requested (only for public enums)
                if (isPub && namesToImport.Contains(enumName))
                {
                    _importedNames[enumName] = moduleNamespace;
                }
                continue;
            }

            // Now register the full enum with variants (replacing the stub)
            // At this point, all enum names AND struct names are resolvable for variant type parsing
            RegisterEnum(enumDecl);

            // Mark as imported if it was explicitly requested (only for public enums)
            if (isPub && namesToImport.Contains(enumName))
            {
                _importedNames[enumName] = moduleNamespace;
            }
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
            if (_symbols.HasConstant(constName))
            {
                continue;
            }

            // Register the constant from the imported module
            RegisterConstant(constDecl);
            _importedNames[constName] = moduleNamespace;
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

            // Handle generic parameters if present (e.g., fn channel<T>() -> Result<..., T>)
            // Must register generic params BEFORE parsing return type since it may reference them
            var genericParams = AstParsingHelpers.ParseGenericParameters(funcDecl.genericParams(), _genericParams);

            // Parse function signature
            var returnType = ParseReturnType(funcDecl.type());
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
                    var isConsuming = paramCtx.KW_CONSUMING() != null;
                    parameters.Add(new ParameterSymbol(paramName, paramType, paramLocation, IsConsuming: isConsuming));
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

            // Register the function (may be extern or generic)
            var funcLocation = SourceLocationHelper.FromToken(funcDecl.IDENTIFIER().Symbol, modulePath, new string[] { });
            _functions[funcName] = new FunctionSymbol(
                funcName, returnType, parameters, funcLocation,
                IsExtern: isExtern,
                GenericParameters: genericParams.Count > 0 ? genericParams : null,
                IsVariadic: hasVariadic);
            _importedNames[funcName] = moduleNamespace;

            // Clear generic params from scope after function registration
            foreach (var paramName in genericParams)
            {
                _genericParams.Remove(paramName);
            }
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
            // extern var is always mutable (the var keyword indicates mutability)
            _globalVariables[varName] = new VariableSymbol(varName, varType, IsMutable: true, varLocation, Id: _nextVariableId++);
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
        // NOTE: SymbolTable doesn't support Remove, so stub enums remain in the table.
        // This is acceptable since they're empty enums with no variants and won't affect correctness.
        foreach (var stubName in enumStubsToCleanup)
        {
            // _symbols.Remove(stubName); // Not supported - stubs remain but are harmless
        }
    }

    private void RegisterConstant(NovusParser.ConstDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check for reserved keywords
        if (!ValidateNotReservedKeyword(name, location, "constant"))
            return;

        // Check for duplicate constant names
        var existingConstant = _symbols.LookupConstant(name);
        if (existingConstant != null)
        {
            var originalLocation = existingConstant.Location;
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

        // Evaluate the constant expression
        var valueExpr = context.expression();

        // Check if this is a struct literal constant
        // Struct literals come as: PrimaryExpr -> StructLiteral
        if (valueExpr is NovusParser.PrimaryExprContext primaryExpr &&
            primaryExpr.primaryExpression() is NovusParser.StructLiteralContext structLiteralExpr)
        {
            // For struct literals, we don't evaluate to a single integer value.
            // Instead, we validate that all field values are constant expressions
            // and store a placeholder value (0) to mark it as a valid constant.
            // The actual struct literal will be handled during IR generation.

            if (!IsConstantStructLiteral(structLiteralExpr))
            {
                _diagnostics.ReportError(
                    "E0032",
                    $"struct literal in constant must have all constant field values",
                    location,
                    helpTexts: new List<string>
                    {
                        "all field values must be compile-time constants (integer literals, other constants, or nested struct literals)"
                    }
                );
                return;
            }

            // Store placeholder value (0) - the struct literal itself will be handled in IR generation
            _symbols.RegisterConstant(name, new ConstantSymbol(name, type, 0, location));
            return;
        }

        // For non-struct constants, evaluate using the integer constant evaluator
        // Convert constants dict to use object values for evaluator
        var constantValues = _symbols.GetLocalConstants().ToDictionary(
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
            // Check if the expression contains a deferred function call (const fn)
            // These will be evaluated later during IR building when the const fn bodies are available
            if (evaluator.HasDeferredFunctionCall)
            {
                // At this point, functions haven't been registered yet (they're in a later pass),
                // so we can't validate that the function is a const fn here.
                // The IrBuilder will validate and evaluate this constant later.
                // Use placeholder value (0) - actual evaluation happens in IrBuilder.
                _symbols.RegisterConstant(name, new ConstantSymbol(name, type, 0, location, isDeferredConstFn: true));
                return;
            }

            _diagnostics.ReportError(
                "E0032",
                $"constant value must be a compile-time constant expression",
                location,
                helpTexts: new List<string>
                {
                    "supported: integer/hex/binary literals, constant references, bitwise ops (|, &, ^, <<, >>, ~), arithmetic, struct literals, const fn calls"
                }
            );
            return;
        }

        _symbols.RegisterConstant(name, new ConstantSymbol(name, type, value, location));
    }

    /// <summary>
    /// Checks if a struct literal expression contains only constant values
    /// </summary>
    private bool IsConstantStructLiteral(NovusParser.StructLiteralContext context)
    {
        // Check each field initializer
        foreach (var fieldInit in context.structFieldInit())
        {
            var fieldValue = fieldInit.expression();
            if (!IsConstantExpression(fieldValue))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if an expression is a compile-time constant
    /// </summary>
    private bool IsConstantExpression(NovusParser.ExpressionContext context)
    {
        switch (context)
        {
            // Unary, binary, and arithmetic expressions are constant if operands are constant
            case NovusParser.UnaryExprContext unaryExpr:
                return IsConstantExpression(unaryExpr.expression());

            case NovusParser.BitwiseOrExprContext orExpr:
                return IsConstantExpression(orExpr.expression(0)) && IsConstantExpression(orExpr.expression(1));

            case NovusParser.BitwiseAndExprContext andExpr:
                return IsConstantExpression(andExpr.expression(0)) && IsConstantExpression(andExpr.expression(1));

            case NovusParser.BitwiseXorExprContext xorExpr:
                return IsConstantExpression(xorExpr.expression(0)) && IsConstantExpression(xorExpr.expression(1));

            case NovusParser.ShiftExprContext shiftExpr:
                return IsConstantExpression(shiftExpr.expression(0)) && IsConstantExpression(shiftExpr.expression(1));

            case NovusParser.AdditiveExprContext addExpr:
                return IsConstantExpression(addExpr.expression(0)) && IsConstantExpression(addExpr.expression(1));

            case NovusParser.MultiplicativeExprContext multExpr:
                return IsConstantExpression(multExpr.expression(0)) && IsConstantExpression(multExpr.expression(1));

            // Parenthesized expressions delegate to the inner expression
            case NovusParser.ParenExprContext parenExpr:
                return IsConstantExpression(parenExpr.expression());

            case NovusParser.PrimaryExprContext primaryExpr:
                return IsConstantPrimaryExpression(primaryExpr.primaryExpression());

            // All other expressions are not constant
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks if a primary expression is a compile-time constant
    /// </summary>
    private bool IsConstantPrimaryExpression(NovusParser.PrimaryExpressionContext context)
    {
        switch (context)
        {
            // Literals are always constant
            case NovusParser.IntegerLiteralContext:
            case NovusParser.HexLiteralContext:
            case NovusParser.BinaryLiteralContext:
            case NovusParser.StringLiteralContext:
            case NovusParser.CharLiteralContext:
            case NovusParser.BoolLiteralContext:
                return true;

            // Struct literals are constant if all fields are constant
            case NovusParser.StructLiteralContext structLit:
                return IsConstantStructLiteral(structLit);

            // Identifier references are constant if they refer to a constant
            case NovusParser.IdentifierExprContext identExpr:
                var name = identExpr.identifier().GetText();
                return _symbols.HasConstant(name);

            // All other primary expressions are not constant
            default:
                return false;
        }
    }

    private void RegisterStatic(NovusParser.StaticDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check for reserved keywords
        if (!ValidateNotReservedKeyword(name, location, "static variable"))
            return;

        // Check for var keyword early (used in multiple paths) - indicates static var (mutable)
        var isMutable = context.KW_VAR() != null;

        // Parse attributes to check for special type-altering attributes
        var attributes = ParseAttributes(context.attribute());

        // Check for @embed attribute - unified asset embedding
        var embedAttr = attributes.Get(KnownAttributes.Embed);
        if (embedAttr != null)
        {
            // Get file path to determine asset type
            var filePath = embedAttr.PositionalArgs.Count > 0 ? embedAttr.PositionalArgs[0]?.ToString() : null;

            // Determine if chip RAM is used (defaults to true for DMA assets, false for raw)
            var useChipRam = true;
            if (embedAttr.NamedArgs.TryGetValue("chip", out var chipArg) && chipArg is bool chipBool)
            {
                useChipRam = chipBool;
            }

            // When chip=false, we create an asset struct for automatic chip RAM management
            if (!useChipRam && !string.IsNullOrEmpty(filePath))
            {
                // Detect asset type from extension
                var assetType = Assets.AssetTypeDetector.GetDefaultType(filePath);

                IrType? structType = null;
                switch (assetType)
                {
                    case Assets.AssetType.Mod:
                        structType = _symbols.LookupStruct("ModAsset");
                        if (structType == null)
                        {
                            var fields = new List<IrStructField>
                            {
                                new IrStructField("data", new IrPointerType(IrIntType.U8)),
                                new IrStructField("size", IrIntType.U32)
                            };
                            structType = new IrStructType("ModAsset", fields);
                        }
                        break;

                    case Assets.AssetType.Audio:
                        structType = _symbols.LookupStruct("AudioAsset");
                        if (structType == null)
                        {
                            var fields = new List<IrStructField>
                            {
                                new IrStructField("data", new IrPointerType(IrIntType.U8)),
                                new IrStructField("size", IrIntType.U32),
                                new IrStructField("sample_rate", IrIntType.U32),
                                new IrStructField("period_pal", IrIntType.U16),
                                new IrStructField("period_ntsc", IrIntType.U16)
                            };
                            structType = new IrStructType("AudioAsset", fields);
                        }
                        break;

                    case Assets.AssetType.Raw:
                    default:
                        structType = _symbols.LookupStruct("RawAsset");
                        if (structType == null)
                        {
                            var fields = new List<IrStructField>
                            {
                                new IrStructField("data", new IrPointerType(IrIntType.U8)),
                                new IrStructField("size", IrIntType.U32)
                            };
                            structType = new IrStructType("RawAsset", fields);
                        }
                        break;
                }

                if (structType != null && !_globalVariables.ContainsKey(name))
                {
                    _globalVariables[name] = new VariableSymbol(name, structType, IsMutable: isMutable, location, Id: _nextVariableId++);
                }
                return;
            }
        }

        // Type annotation is optional - if not provided, type will be inferred from initializer
        IrType type;
        if (context.type() != null)
        {
            type = ParseType(context.type());
        }
        else
        {
            // Infer type from initializer expression
            // For array literals, we can infer the type directly
            var initExpr = context.expression();

            // Check if it's a primary expression wrapping an array literal
            if (initExpr is NovusParser.PrimaryExprContext primaryExpr)
            {
                var primaryInner = primaryExpr.primaryExpression();
                if (primaryInner is NovusParser.ArrayLiteralContext arrayLit)
                {
                    // Infer array type from literal
                    type = InferArrayLiteralType(arrayLit);
                }
                else if (primaryInner is NovusParser.ArrayRepeatLiteralContext arrayRepeat)
                {
                    // Infer array type from repeat literal [value; count]
                    var expressions = arrayRepeat.expression();
                    if (expressions.Length == 2)
                    {
                        var elementType = InferExpressionType(expressions[0]);
                        // Try to extract count - for now use placeholder
                        type = new IrArrayType(elementType, 1); // Count will be determined in IR building
                    }
                    else
                    {
                        type = IrVoidType.Instance;
                    }
                }
                else
                {
                    // For other expressions, use a placeholder type
                    type = IrVoidType.Instance;
                }
            }
            else
            {
                // For complex expressions, use a placeholder type
                // The actual type will be determined during IR building
                type = IrVoidType.Instance;
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

        _globalVariables[name] = new VariableSymbol(name, type, IsMutable: isMutable, location, Id: _nextVariableId++);
    }

    private IrType InferArrayLiteralType(NovusParser.ArrayLiteralContext arrayLit)
    {
        var expressions = arrayLit.expression();
        if (expressions.Length == 0)
        {
            // Empty array - return void as placeholder (error will be reported during IR building)
            return IrVoidType.Instance;
        }

        // Infer element type from first element
        var firstExpr = expressions[0];
        var elementType = InferExpressionType(firstExpr);
        var arrayLength = expressions.Length;

        return new IrArrayType(elementType, arrayLength);
    }

    private IrType InferExpressionType(NovusParser.ExpressionContext expr)
    {
        // Simple type inference for literals and basic expressions
        // This is a simplified version that handles common cases

        // Check the text for simple literal patterns
        var text = expr.GetText();

        // Check for boolean literals
        if (text == "true" || text == "false")
        {
            return IrBoolType.Instance;
        }

        // Check for hex literals
        if (text.StartsWith("0x") || text.StartsWith("0X"))
        {
            if (text.EndsWith("u16"))
                return IrIntType.U16;
            if (text.EndsWith("u32"))
                return IrIntType.U32;
            return IrIntType.I32;
        }

        // Check for integer literals (simple digit check)
        if (text.All(char.IsDigit))
        {
            return IrIntType.I32; // Default integer type
        }

        // For complex expressions, use i32 as default
        return IrIntType.I32;
    }

    private void RegisterGlobalVariable(NovusParser.GlobalVariableDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check for reserved keywords
        if (!ValidateNotReservedKeyword(name, location, "global variable"))
            return;

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

        // extern var is always mutable (the var keyword indicates mutability)
        _globalVariables[name] = new VariableSymbol(name, type, IsMutable: true, location, Id: _nextVariableId++);
    }

    private void RegisterFunction(NovusParser.FunctionDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check for reserved keywords
        if (!ValidateNotReservedKeyword(name, location, "function"))
            return;

        // Parse attributes
        var attributes = ParseAttributes(context.attribute());

        // Check if function is extern by looking for 'extern' keyword
        var isExtern = context.KW_EXTERN() != null;

        // Check if function is a const fn
        var isConstFn = Frontend.AstModifierHelper.IsConstFn(context);

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
        var genericParams = AstParsingHelpers.ParseGenericParameters(context.genericParams(), _genericParams);
        // Parse where clause for constraint checking during monomorphization
        var whereClause = AstParsingHelpers.ParseWhereClause(context.whereClause());

        // Set flag to skip type validation for extern functions (FFI types may not be imported)
        if (isExtern)
        {
            _parsingExternFunction = true;
        }

        var returnType = ParseReturnType(context.type());
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
                var isConsuming = paramCtx.KW_CONSUMING() != null;

                // Validate parameter name is not a reserved keyword
                ValidateNotReservedKeyword(paramName, paramLocation, "parameter");

                parameters.Add(new ParameterSymbol(paramName, paramType, paramLocation, IsConsuming: isConsuming));
            }

            // Add variadic parameter if present
            if (paramList.variadicParameter() != null)
            {
                var variadicCtx = paramList.variadicParameter();
                var variadicName = variadicCtx.IDENTIFIER().GetText();
                var variadicLocation = SourceLocationHelper.FromToken(variadicCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);

                // Validate variadic parameter name is not a reserved keyword
                ValidateNotReservedKeyword(variadicName, variadicLocation, "parameter");

                // Variadic parameters have void* type for semantic analysis
                var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                parameters.Add(new ParameterSymbol(variadicName, variadicType, variadicLocation, IsVariadic: true));
                hasVariadic = true;
            }
        }

        // Clear flag after parsing all types
        if (isExtern)
        {
            _parsingExternFunction = false;
        }

        _functions[name] = new FunctionSymbol(name, returnType, parameters, location, isExtern, genericParams.Count > 0 ? genericParams : null, attributes, hasVariadic, null, whereClause, isConstFn);

        // Clear generic params from scope after function registration
        foreach (var paramName in genericParams)
        {
            _genericParams.Remove(paramName);
        }
    }

    /// <summary>
    /// Extract metadata from an impl block declaration.
    /// This consolidates duplicate parsing logic between RegisterImpl and AnalyzeImplBlock.
    /// Also adds generic parameters to the _genericParams scope.
    /// </summary>
    private ImplBlockInfo ParseImplBlockInfo(NovusParser.ImplDeclarationContext context)
    {
        // Handle generic parameters if present (e.g., impl<T> Vec<T> or impl<const N: u32> Buffer<N>)
        var genericParamsResult = AstParsingHelpers.ParseGenericParametersEx(context.genericParams(), ParseType);

        // Register type generic params
        foreach (var paramName in genericParamsResult.TypeParameters)
        {
            _genericParams[paramName] = new IrGenericType(paramName);
        }

        // Register const generic params
        foreach (var (paramName, constType) in genericParamsResult.ConstParameters)
        {
            _constGenericParams[paramName] = new IrConstGenericParam(paramName, constType);
        }

        var genericParams = genericParamsResult.AllParameterNames;

        // Determine if this is a trait impl or inherent impl
        bool isTraitImpl = context.KW_FOR() != null;
        string? traitName = null;
        var traitTypeArgs = new List<IrType>();
        string implTypeName;

        if (isTraitImpl)
        {
            // Format: impl [<GenericParams>] TraitName<TraitArgs> for TargetType
            traitName = context.traitTypeName.IDENTIFIER(0).GetText();

            // Parse trait type arguments if present (e.g., Iterator<i32>)
            if (context.traitTypeArgs != null)
            {
                var typeList = context.traitTypeArgs.typeList();
                foreach (var typeCtx in typeList.type())
                {
                    traitTypeArgs.Add(ParseType(typeCtx));
                }
            }

            // implTargetType is the type receiving the implementation
            var targetTypeCtx = context.implTargetType();

            if (targetTypeCtx is NovusParser.PrimitiveImplTargetContext primitiveCtx)
            {
                implTypeName = primitiveCtx.primitiveTypeName().GetText().ToLowerInvariant();
            }
            else if (targetTypeCtx is NovusParser.NamedImplTargetContext namedCtx)
            {
                implTypeName = namedCtx.typeName().IDENTIFIER(0).GetText();
            }
            else
            {
                _diagnostics.ReportError(
                    "E0001",
                    $"Unknown impl target type",
                    SourceLocationHelper.FromToken(context.KW_IMPL().Symbol, _filePath, _sourceLines)
                );
                return new ImplBlockInfo(genericParams, isTraitImpl, traitName, traitTypeArgs, "", ParseError: true);
            }
        }
        else
        {
            // Format: impl [<GenericParams>] TargetType
            implTypeName = context.targetTypeName.IDENTIFIER(0).GetText();
        }

        return new ImplBlockInfo(genericParams, isTraitImpl, traitName, traitTypeArgs, implTypeName, ParseError: false);
    }

    /// <summary>
    /// Clear generic params from scope (typically called after processing an impl block).
    /// Clears both type generic params and const generic params.
    /// </summary>
    private void ClearImplGenericParams(List<string> genericParams)
    {
        foreach (var paramName in genericParams)
        {
            _genericParams.Remove(paramName);
            _constGenericParams.Remove(paramName);
        }
    }

    private void RegisterImpl(NovusParser.ImplDeclarationContext context)
    {
        var info = ParseImplBlockInfo(context);
        if (info.ParseError)
        {
            ClearImplGenericParams(info.GenericParams);
            return;
        }

        // For trait impls, validate that the trait exists
        if (info.IsTraitImpl && !_symbols.HasTrait(info.TraitName!))
        {
            // Clear generic params and return - no error for unimported traits
            ClearImplGenericParams(info.GenericParams);
            return;
        }

        if (info.IsTraitImpl)
        {
            // Store trait implementation for constraint checking
            var implLocation = SourceLocationHelper.FromToken(context.KW_IMPL().Symbol, _filePath, _sourceLines);
            _traitResolver.RegisterTraitImpl(
                info.ImplTypeName,
                info.TraitName!,
                info.TraitTypeArgs,
                info.GenericParams,
                implLocation
            );
        }

        // Register each method in the impl block
        foreach (var item in context.implItem())
        {
            if (item.functionDeclaration() != null)
            {
                RegisterImplMethod(item.functionDeclaration(), context, info.ImplTypeName, info.GenericParams, info.TraitName, info.TraitTypeArgs);
            }
        }

        // Clear generic params from scope after impl registration
        ClearImplGenericParams(info.GenericParams);
    }

    private void RegisterImplMethod(NovusParser.FunctionDeclarationContext context, NovusParser.ImplDeclarationContext implContext, string implTypeName, List<string> genericParams, string? traitName = null, List<IrType>? traitTypeArgs = null)
    {
        var methodName = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Parse method-level generic parameters (e.g., <E> in fn ok_or<E>)
        // These need to be registered BEFORE parsing parameter types and return type
        var methodGenericParams = AstParsingHelpers.ParseGenericParameters(context.genericParams(), _genericParams);

        // Parse method-level where clause (e.g., where T: Eq)
        var whereClause = AstParsingHelpers.ParseWhereClause(context.whereClause());

        // Parse attributes (including @suppress for warnings)
        var attributes = ParseAttributes(context.attribute());

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
            // Clear method-level generic params before returning
            foreach (var param in methodGenericParams)
            {
                _genericParams.Remove(param);
            }
            return;
        }

        var returnType = ParseReturnType(context.type());
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
                var lookupStruct = _symbols.LookupStruct(implTypeName);
                if (lookupStruct != null)
                {
                    baseType = lookupStruct;
                }
                else
                {
                    // Check for enum types
                    var lookupEnum = _symbols.LookupEnum(implTypeName);
                    if (lookupEnum != null)
                    {
                        baseType = lookupEnum;
                    }
                    else
                    {
                        // Check for primitive types (bool, i8, u8, etc.)
                        var primitiveType = GetPrimitiveType(implTypeName);
                        if (primitiveType != null)
                        {
                            baseType = primitiveType;
                        }
                        else
                        {
                            // Fallback to generic type if neither struct, enum, nor primitive found
                            baseType = new IrGenericType(implTypeName);
                        }
                    }
                }

                // Now wrap in reference/pointer if needed based on parameter form
                IrType selfType;
                bool isConsumingSelf = false;
                if (selfParam.GetText().StartsWith("&var"))
                {
                    // &var self - mutable reference
                    selfType = _typeInterner.GetMutReferenceType(baseType);
                }
                else if (selfParam.GetText().StartsWith("&"))
                {
                    // &self - immutable reference
                    selfType = _typeInterner.GetReferenceType(baseType);
                }
                else
                {
                    // self (by value) - check if it's consuming
                    selfType = baseType;
                    isConsumingSelf = selfParam.KW_CONSUMING() != null;
                }

                parameters.Add(new ParameterSymbol("self", selfType, selfLocation, IsConsuming: isConsumingSelf));
            }

            // Parse regular parameters
            var paramList = context.parameterList();
            foreach (var paramCtx in paramList.parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                var paramType = ParseType(paramCtx.type());
                var paramLocation = SourceLocationHelper.FromToken(paramCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);
                var isConsuming = paramCtx.KW_CONSUMING() != null;

                // Validate parameter name is not a reserved keyword
                ValidateNotReservedKeyword(paramName, paramLocation, "parameter");

                parameters.Add(new ParameterSymbol(paramName, paramType, paramLocation, IsConsuming: isConsuming));
            }

            // Add variadic parameter if present
            if (paramList.variadicParameter() != null)
            {
                var variadicCtx = paramList.variadicParameter();
                var variadicName = variadicCtx.IDENTIFIER().GetText();
                var variadicLocation = SourceLocationHelper.FromToken(variadicCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);

                // Validate variadic parameter name is not a reserved keyword
                ValidateNotReservedKeyword(variadicName, variadicLocation, "parameter");

                // Variadic parameters have void* type for semantic analysis
                var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                parameters.Add(new ParameterSymbol(variadicName, variadicType, variadicLocation, IsVariadic: true));
                hasVariadic = true;
            }
        }

        // Handle #[chain] attribute - makes method return &var Self and adds implicit return self
        if (attributes != null && attributes.Has(KnownAttributes.Chain))
        {
            // Validate that the method has &var self parameter
            bool hasVarSelf = false;
            if (context.parameterList()?.selfParameter() != null)
            {
                var selfParam = context.parameterList().selfParameter();
                hasVarSelf = selfParam.GetText().StartsWith("&var");
            }

            if (!hasVarSelf)
            {
                _diagnostics.ReportError(
                    "E0098",
                    $"#[chain] attribute requires method to have '&var self' parameter",
                    location,
                    helpTexts: new List<string>
                    {
                        "#[chain] automatically adds 'return self' for method chaining",
                        "change the self parameter to '&var self' or remove the #[chain] attribute"
                    }
                );
            }
            else if (returnType is not IrVoidType)
            {
                _diagnostics.ReportError(
                    "E0098",
                    $"#[chain] attribute requires method to have no explicit return type",
                    location,
                    helpTexts: new List<string>
                    {
                        "#[chain] automatically sets the return type to '&var Self'",
                        "remove the explicit return type or remove the #[chain] attribute"
                    }
                );
            }
            else
            {
                // Set return type to pointer to the impl type (same as &var self type)
                if (parameters.Count > 0 && parameters[0].Name == "self")
                {
                    returnType = parameters[0].Type;
                }
            }
        }

        // Store both impl-level and method-level generic params in the symbol
        var allGenericParams = genericParams.Count > 0 ? genericParams : null;
        _functions[mangledName] = new FunctionSymbol(mangledName, returnType, parameters, location, false, allGenericParams, attributes, hasVariadic, methodGenericParams.Count > 0 ? methodGenericParams : null, whereClause);

        // Clear method-level generic params from scope
        foreach (var param in methodGenericParams)
        {
            _genericParams.Remove(param);
        }
    }

    /// <summary>
    /// Register a struct in a single pass (placeholder + fill fields).
    /// Used for individual imports where mutual recursion within the import is not needed.
    /// For files with mutually recursive structs, use RegisterStructPlaceholder + FillStructFields.
    /// </summary>
    private void RegisterStruct(NovusParser.StructDeclarationContext context)
    {
        RegisterStructPlaceholder(context);
        FillStructFields(context);
        RegisterDerivedMethods(context);
    }

    /// <summary>
    /// Phase 1: Register a placeholder struct with just its name and generic parameters.
    /// This allows mutually recursive struct definitions where struct A references struct B and vice versa.
    /// </summary>
    private void RegisterStructPlaceholder(NovusParser.StructDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check for reserved keywords
        if (!ValidateNotReservedKeyword(name, location, "struct"))
            return;

        // Parse attributes
        var attributes = ParseAttributes(context.attribute());

        // Check for duplicate struct names
        if (_symbols.HasStruct(name))
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
        var genericParams = AstParsingHelpers.ParseGenericParameters(context.genericParams());

        // Parse where clause (can be done without fields)
        var whereClause = ParseWhereClause(context.whereClause());

        // Register placeholder struct type - fields will be filled in by FillStructFields
        var placeholderStruct = new IrStructType(name, new List<IrStructField>(), genericParams.Count > 0 ? genericParams : null, null, attributes, whereClause);
        _symbols.RegisterStruct(name, placeholderStruct, location);
    }

    /// <summary>
    /// Phase 2: Fill in the struct fields now that all struct names are known.
    /// This resolves forward references to other structs defined later in the file.
    /// </summary>
    private void FillStructFields(NovusParser.StructDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Skip if struct wasn't registered (error already reported)
        if (!_symbols.HasStruct(name))
        {
            return;
        }

        // Get the placeholder - we will MUTATE it to add fields
        // This is critical because pointer types (*StructName) that were already created
        // hold references to this placeholder instance. If we create a new struct and
        // replace it in the symbol table, those pointer types will still reference the
        // empty placeholder and won't see the fields.
        var placeholder = _symbols.LookupStruct(name);
        if (placeholder == null)
        {
            return; // Should never happen if HasStruct returned true
        }

        // Handle generic parameters if present - add to scope for field parsing
        AstParsingHelpers.ParseGenericParameters(context.genericParams(), _genericParams);

        // Parse struct fields and add them to the EXISTING placeholder
        // (now all struct names are known, including those defined later)
        foreach (var fieldCtx in context.structField())
        {
            var fieldName = fieldCtx.IDENTIFIER().GetText();
            var fieldType = ParseType(fieldCtx.type());
            placeholder.Fields.Add(new IrStructField(fieldName, fieldType));
        }

        // Clear generic params from scope after struct registration
        AstParsingHelpers.ClearGenericParameters(context.genericParams(), _genericParams);

        // Force offset calculation by accessing SizeInBytes (only for non-generic structs)
        // The placeholder already has its GenericParameters set from RegisterStructPlaceholder
        if (placeholder.GenericParameters.Count == 0)
        {
            _ = placeholder.SizeInBytes;
        }

        // No need to re-register - we mutated the existing placeholder in place
    }

    /// <summary>
    /// Phase 3: Register synthetic methods for #[derive(...)] attributes.
    /// This must happen after FillStructFields so the struct type is complete,
    /// and must register FunctionSymbols so method lookup in semantic analysis works.
    /// </summary>
    private void RegisterDerivedMethods(NovusParser.StructDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Get the struct we just registered
        var structType = _symbols.LookupStruct(name);
        if (structType == null) return;

        // Check for derive attribute
        if (structType.Attributes == null) return;
        var deriveAttr = structType.Attributes.Get(KnownAttributes.Derive);
        if (deriveAttr == null) return;

        // Skip generic structs - they're handled during monomorphization
        if (structType.GenericParameters.Count > 0) return;

        // Get the pointer type for self parameters
        var selfPtrType = new IrPointerType(structType);

        // Parse derive traits and register synthetic methods
        foreach (var arg in deriveAttr.PositionalArgs)
        {
            if (arg is not string traitName) continue;

            switch (traitName)
            {
                case "Eq":
                    RegisterDerivedEq(name, selfPtrType, location);
                    break;
                case "Hash":
                    RegisterDerivedHash(name, selfPtrType, location);
                    break;
                default:
                    _diagnostics.ReportError(
                        ErrorCodes.UnknownDeriveTrait,
                        $"Unknown derive trait '{traitName}'. Supported traits: Eq, Hash",
                        deriveAttr.Location
                    );
                    break;
            }
        }
    }

    /// <summary>
    /// Register the eq(&self, other: &Self) -> bool method for #[derive(Eq)]
    /// </summary>
    private void RegisterDerivedEq(string typeName, IrPointerType selfPtrType, SourceLocation location)
    {
        var mangledName = $"{typeName}::eq";

        // Don't register if already exists (user-defined impl takes precedence)
        if (_functions.ContainsKey(mangledName)) return;

        var boolType = IrBoolType.Instance;
        var parameters = new List<ParameterSymbol>
        {
            new ParameterSymbol("self", selfPtrType, location),
            new ParameterSymbol("other", selfPtrType, location)
        };

        _functions[mangledName] = new FunctionSymbol(mangledName, boolType, parameters, location);
    }

    /// <summary>
    /// Register the hash(&self) -> u32 method for #[derive(Hash)]
    /// </summary>
    private void RegisterDerivedHash(string typeName, IrPointerType selfPtrType, SourceLocation location)
    {
        var mangledName = $"{typeName}::hash";

        // Don't register if already exists (user-defined impl takes precedence)
        if (_functions.ContainsKey(mangledName)) return;

        var u32Type = IrIntType.U32;
        var parameters = new List<ParameterSymbol>
        {
            new ParameterSymbol("self", selfPtrType, location)
        };

        _functions[mangledName] = new FunctionSymbol(mangledName, u32Type, parameters, location);
    }

    private void RegisterEnum(NovusParser.EnumDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Parse attributes
        var attributes = ParseAttributes(context.attribute());

        // Check for duplicate enum names (but allow replacing stubs)
        var existingEnum = _symbols.LookupEnum(name);
        if (existingEnum != null)
        {
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

        // Handle generic parameters if present - add to scope for variant parsing
        var genericParams = AstParsingHelpers.ParseGenericParameters(context.genericParams(), _genericParams);

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

        _symbols.RegisterEnum(name, enumType, location);

        // Clear generic param scope
        _genericParams.Clear();
    }

    /// <summary>
    /// Phase 1: Register an enum stub with just its name and generic parameters.
    /// This allows struct fields to reference enums defined later in the file.
    /// </summary>
    private void RegisterEnumStub(NovusParser.EnumDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check for reserved keywords
        if (!ValidateNotReservedKeyword(name, location, "enum"))
            return;

        // Check for duplicate enum names
        if (_symbols.HasEnum(name))
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
        var genericParams = AstParsingHelpers.ParseGenericParameters(context.genericParams());

        // Parse attributes
        var attributes = ParseAttributes(context.attribute());

        // Register stub enum with empty variants - variants will be filled in by FillEnumVariants
        var stubEnum = new IrEnumType(name, new List<IrEnumVariant>(), genericParams.Count > 0 ? genericParams : null, null, attributes);
        _symbols.RegisterEnum(name, stubEnum, location);
    }

    /// <summary>
    /// Phase 2: Fill in the enum variants now that all struct types are known.
    /// This resolves forward references to structs defined later in the file.
    /// IMPORTANT: We mutate the stub in-place to preserve references from struct fields.
    /// </summary>
    private void FillEnumVariants(NovusParser.EnumDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Skip if enum wasn't registered (error already reported)
        if (!_symbols.HasEnum(name))
        {
            return;
        }

        // Get the stub - we'll mutate it in-place
        var stub = _symbols.LookupEnum(name);
        if (stub == null)
        {
            return;
        }

        // Skip if already fully registered (has variants) - from imports
        if (stub.Variants.Count > 0)
        {
            return;
        }

        // Handle generic parameters if present - add to scope for variant parsing
        AstParsingHelpers.ParseGenericParameters(context.genericParams(), _genericParams);

        // Parse enum variants and add to the stub's Variants list in-place
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

            stub.Variants.Add(new IrEnumVariant(variantName, tag++, associatedData));
        }

        // Parse and set where clause
        var whereClause = ParseWhereClause(context.whereClause());
        stub.WhereClause = whereClause;

        // Force size calculation (only for non-generic enums)
        if (stub.GenericParameters.Count == 0)
        {
            _ = stub.SizeInBytes;
        }

        // Clear generic param scope
        _genericParams.Clear();
    }

    private void RegisterTrait(NovusParser.TraitDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);

        // Check for reserved keywords
        if (!ValidateNotReservedKeyword(name, location, "trait"))
            return;

        // Parse attributes
        var attributes = ParseAttributes(context.attribute());

        // Check for duplicate trait names
        if (_symbols.HasTrait(name))
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

        // Handle generic parameters if present - add to scope for method signature parsing
        var genericParams = AstParsingHelpers.ParseGenericParameters(context.genericParams(), _genericParams);

        // Parse trait method signatures (and optional default implementations)
        var methods = new List<IrTraitMethod>();

        foreach (var itemCtx in context.traitItem())
        {
            // After grammar change, we use traitMethodDeclaration instead of functionSignature
            var methodDecl = itemCtx.traitMethodDeclaration();
            if (methodDecl != null)
            {
                var methodName = methodDecl.IDENTIFIER().GetText();

                // Parse method generic parameters (if any)
                var methodGenericParams = AstParsingHelpers.ParseGenericParameters(methodDecl.genericParams(), _genericParams);

                // Parse parameters
                var parameters = new List<IrParameter>();
                if (methodDecl.parameterList() != null)
                {
                    var paramList = methodDecl.parameterList();

                    foreach (var paramCtx in paramList.parameter())
                    {
                        var paramName = paramCtx.IDENTIFIER().GetText();
                        var paramType = ParseType(paramCtx.type());
                        var paramLocation = SourceLocationHelper.FromToken(paramCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);

                        // Validate parameter name is not a reserved keyword
                        ValidateNotReservedKeyword(paramName, paramLocation, "parameter");

                        parameters.Add(new IrParameter(paramName, paramType));
                    }

                    // Handle variadic parameter if present
                    if (paramList.variadicParameter() != null)
                    {
                        var variadicCtx = paramList.variadicParameter();
                        var variadicName = variadicCtx.IDENTIFIER().GetText();
                        var variadicLocation = SourceLocationHelper.FromToken(variadicCtx.IDENTIFIER().Symbol, _filePath, _sourceLines);

                        // Validate variadic parameter name is not a reserved keyword
                        ValidateNotReservedKeyword(variadicName, variadicLocation, "parameter");

                        var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                        parameters.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
                    }
                }

                // Parse return type
                IrType returnType = IrVoidType.Instance;
                if (methodDecl.type() != null)
                {
                    returnType = ParseType(methodDecl.type());
                }

                var traitMethod = new IrTraitMethod(methodName, parameters, returnType, methodGenericParams.Count > 0 ? methodGenericParams : null);

                // Check if there's a default implementation (body block)
                if (methodDecl.block() != null)
                {
                    // Store the AST context for the default implementation body
                    traitMethod.DefaultBodyContext = methodDecl.block();
                }

                methods.Add(traitMethod);

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
        _symbols.RegisterTrait(name, trait, location);

        // Clear generic param scope
        _genericParams.Clear();
    }

    private void AnalyzeImplBlock(NovusParser.ImplDeclarationContext context)
    {
        var info = ParseImplBlockInfo(context);
        if (info.ParseError)
        {
            ClearImplGenericParams(info.GenericParams);
            return;
        }

        // Parse the where clause for this impl block (if any)
        var implWhereClause = ParseWhereClause(context.whereClause());

        // Look up the struct type to get its where clause constraints
        var structType = _symbols.LookupStruct(info.ImplTypeName);
        IrWhereClause? combinedWhereClause = null;

        if (structType?.WhereClause != null && implWhereClause != null)
        {
            // Combine struct where clause with impl where clause
            var combinedConstraints = new List<IrTypeConstraint>();
            combinedConstraints.AddRange(structType.WhereClause.Constraints);
            combinedConstraints.AddRange(implWhereClause.Constraints);
            combinedWhereClause = new IrWhereClause(combinedConstraints);
        }
        else
        {
            combinedWhereClause = structType?.WhereClause ?? implWhereClause;
        }

        // Set the current struct where clause for the duration of method analysis
        var savedStructWhereClause = _currentStructWhereClause;
        _currentStructWhereClause = combinedWhereClause;

        // Analyze each method
        foreach (var item in context.implItem())
        {
            if (item.functionDeclaration() != null)
            {
                AnalyzeImplMethod(item.functionDeclaration(), info.ImplTypeName, info.TraitName, info.TraitTypeArgs);
            }
        }

        // Restore previous where clause
        _currentStructWhereClause = savedStructWhereClause;

        // Clear generic params after analysis
        ClearImplGenericParams(info.GenericParams);
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
        _currentFunctionWhereClause = _currentFunction.WhereClause; // Track method-level where clause
        _variables.Clear();
        _borrowChecker.Reset(); // Reset move tracking for new method

        // Parse @suppress attributes to track which warnings to suppress
        _currentFunctionSuppressedWarnings.Clear();
        if (_currentFunction.Attributes != null)
        {
            var suppressAttrs = _currentFunction.Attributes.GetAll(KnownAttributes.Suppress);
            foreach (var suppressAttr in suppressAttrs)
            {
                // First positional arg is the warning code, second is the reason (optional)
                var warningCode = suppressAttr.GetPositionalArg<string>(0);
                if (warningCode != null)
                {
                    // Validate that only warnings (W-codes) can be suppressed, not errors (E-codes)
                    if (warningCode.StartsWith("E", StringComparison.Ordinal))
                    {
                        _diagnostics.ReportError(
                            "E0099",
                            $"@suppress cannot suppress error code '{warningCode}' - only warning codes (starting with 'W') can be suppressed",
                            suppressAttr.Location,
                            helpTexts: new List<string>
                            {
                                "errors indicate serious problems that must be fixed",
                                "only warnings can be suppressed with @suppress",
                                $"if you believe this error is incorrect, fix the underlying issue instead of suppressing it"
                            }
                        );
                    }
                    else
                    {
                        _currentFunctionSuppressedWarnings.Add(warningCode);
                    }
                }
            }
        }

        // Add parameters to symbol table (including self if present)
        foreach (var param in _currentFunction.Parameters)
        {
            _variables[param.Name] = new VariableSymbol(param.Name, param.Type, false, param.Location, Id: _nextVariableId++);
        }

        // Analyze function body with unreachable code detection
        if (context.block() != null)
        {
            AnalyzeBlock(context.block());
        }

        _currentFunction = null;
        _currentFunctionWhereClause = null;
        _currentFunctionSuppressedWarnings.Clear();
    }

    public override IrType? VisitFunctionDeclaration([NotNull] NovusParser.FunctionDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        _currentFunction = _functions[name];
        _currentFunctionWhereClause = _currentFunction.WhereClause; // Track method-level where clause
        _variables.Clear();
        _borrowChecker.Reset(); // Reset move tracking for new function
        _dropScopes.Clear(); // Reset drop tracking for new function
        _dropInfo.Clear();

        // Parse @suppress attributes to track which warnings to suppress
        _currentFunctionSuppressedWarnings.Clear();
        if (_currentFunction.Attributes != null)
        {
            var suppressAttrs = _currentFunction.Attributes.GetAll(KnownAttributes.Suppress);
            foreach (var suppressAttr in suppressAttrs)
            {
                // First positional arg is the warning code, second is the reason (optional)
                var warningCode = suppressAttr.GetPositionalArg<string>(0);
                if (warningCode != null)
                {
                    // Validate that only warnings (W-codes) can be suppressed, not errors (E-codes)
                    if (warningCode.StartsWith("E", StringComparison.Ordinal))
                    {
                        _diagnostics.ReportError(
                            "E0099",
                            $"@suppress cannot suppress error code '{warningCode}' - only warning codes (starting with 'W') can be suppressed",
                            suppressAttr.Location,
                            helpTexts: new List<string>
                            {
                                "errors indicate serious problems that must be fixed",
                                "only warnings can be suppressed with @suppress",
                                $"if you believe this error is incorrect, fix the underlying issue instead of suppressing it"
                            }
                        );
                    }
                    else
                    {
                        _currentFunctionSuppressedWarnings.Add(warningCode);
                    }
                }
            }
        }

        // Skip body analysis for extern functions
        if (_currentFunction.IsExtern)
        {
            _currentFunction = null;
            _currentFunctionWhereClause = null;
            _currentFunctionSuppressedWarnings.Clear();
            return null;
        }

        // Restore generic parameters to scope for function body analysis
        var genericParams = AstParsingHelpers.ParseGenericParameters(context.genericParams(), _genericParams);

        // Add parameters to symbol table (parameters are immutable)
        foreach (var param in _currentFunction.Parameters)
        {
            _variables[param.Name] = new VariableSymbol(param.Name, param.Type, false, param.Location, Id: _nextVariableId++);
        }

        // First, analyze the function body with full semantic analysis (visits all expressions)
        AnalyzeBlock(context.block());

        // Then check if all paths return
        bool allPathsReturn = AnalyzeBlockReturns(context.block());

        // Check if function with non-void return type has all paths returning
        // Skip this check for #[chain] methods - they get implicit return self
        bool isChainMethod = _currentFunction.Attributes?.Has(KnownAttributes.Chain) ?? false;
        if (_currentFunction.ReturnType is not IrVoidType && !allPathsReturn && !isChainMethod)
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

        // Validate const fn purity (check for forbidden operations)
        if (_currentFunction.IsConstFn)
        {
            ValidateConstFnPurity(context.block(), name);
        }

        _currentFunction = null;
        _currentFunctionWhereClause = null;
        _currentFunctionSuppressedWarnings.Clear();
        return null;
    }

    /// <summary>
    /// Validates that a const fn body doesn't contain forbidden operations.
    /// </summary>
    private void ValidateConstFnPurity(NovusParser.BlockContext block, string functionName)
    {
        var visitor = new ConstFnPurityVisitor(this, functionName);
        visitor.Visit(block);
    }

    /// <summary>
    /// AST visitor that checks for const fn purity violations.
    /// </summary>
    private class ConstFnPurityVisitor : NovusParserBaseVisitor<object?>
    {
        private readonly SemanticAnalyzer _analyzer;
        private readonly string _functionName;

        public ConstFnPurityVisitor(SemanticAnalyzer analyzer, string functionName)
        {
            _analyzer = analyzer;
            _functionName = functionName;
        }

        public override object? VisitDeferExpression(NovusParser.DeferExpressionContext context)
        {
            var location = SourceLocationHelper.FromContext(context, _analyzer._filePath, _analyzer._sourceLines);
            _analyzer._diagnostics.ReportError(
                ErrorCodes.ConstFnCannotUseDefer,
                $"const fn '{_functionName}': defer statements are not allowed in const fn",
                location
            );
            return base.VisitDeferExpression(context);
        }

        public override object? VisitDeferBlock(NovusParser.DeferBlockContext context)
        {
            var location = SourceLocationHelper.FromContext(context, _analyzer._filePath, _analyzer._sourceLines);
            _analyzer._diagnostics.ReportError(
                ErrorCodes.ConstFnCannotUseDefer,
                $"const fn '{_functionName}': defer blocks are not allowed in const fn",
                location
            );
            return base.VisitDeferBlock(context);
        }

        public override object? VisitPanicStatement(NovusParser.PanicStatementContext context)
        {
            var location = SourceLocationHelper.FromContext(context, _analyzer._filePath, _analyzer._sourceLines);
            _analyzer._diagnostics.ReportError(
                ErrorCodes.ConstFnCannotUsePanic,
                $"const fn '{_functionName}': panic! is not allowed in const fn",
                location
            );
            return base.VisitPanicStatement(context);
        }

        public override object? VisitAsmBlock(NovusParser.AsmBlockContext context)
        {
            var location = SourceLocationHelper.FromContext(context, _analyzer._filePath, _analyzer._sourceLines);
            _analyzer._diagnostics.ReportError(
                ErrorCodes.ConstFnCannotUseInlineAsm,
                $"const fn '{_functionName}': inline assembly is not allowed in const fn",
                location
            );
            return base.VisitAsmBlock(context);
        }

        public override object? VisitIdentifierExpr(NovusParser.IdentifierExprContext context)
        {
            // Check for global variable access
            // IdentifierExprContext has identifier() which returns IdentifierContext
            var identifier = context.identifier();
            if (identifier != null)
            {
                // Get the first IDENTIFIER token (simple variable name, not paths like Foo::Bar)
                var name = identifier.IDENTIFIER(0)?.GetText();
                if (name != null && _analyzer._globalVariables.ContainsKey(name))
                {
                    var location = SourceLocationHelper.FromContext(context, _analyzer._filePath, _analyzer._sourceLines);
                    _analyzer._diagnostics.ReportError(
                        ErrorCodes.ConstFnCannotAccessGlobal,
                        $"const fn '{_functionName}': cannot access global variable '{name}'",
                        location
                    );
                }
            }
            return base.VisitIdentifierExpr(context);
        }

        public override object? VisitAssignmentStatement(NovusParser.AssignmentStatementContext context)
        {
            // Check for assignment to global variable
            // AssignmentStatementContext has IDENTIFIER() directly
            var identifier = context.IDENTIFIER();
            if (identifier != null)
            {
                var name = identifier.GetText();
                if (_analyzer._globalVariables.ContainsKey(name))
                {
                    var location = SourceLocationHelper.FromContext(context, _analyzer._filePath, _analyzer._sourceLines);
                    _analyzer._diagnostics.ReportError(
                        ErrorCodes.ConstFnCannotAccessGlobal,
                        $"const fn '{_functionName}': cannot write to global variable '{name}'",
                        location
                    );
                }
            }
            return base.VisitAssignmentStatement(context);
        }

        public override object? VisitCallExpr(NovusParser.CallExprContext context)
        {
            // Check for non-const function calls
            // CallExpr has: expression '(' argumentList? ')'
            // We need to get the function name from the expression
            var expr = context.expression();
            if (expr is NovusParser.PrimaryExprContext primary)
            {
                var primaryExpr = primary.primaryExpression();
                if (primaryExpr is NovusParser.IdentifierExprContext identExpr)
                {
                    var identifier = identExpr.identifier();
                    var funcName = identifier?.IDENTIFIER(0)?.GetText();
                    if (funcName != null && _analyzer._functions.TryGetValue(funcName, out var func))
                    {
                        if (!func.IsConstFn && !func.IsExtern)
                        {
                            var location = SourceLocationHelper.FromContext(context, _analyzer._filePath, _analyzer._sourceLines);
                            _analyzer._diagnostics.ReportError(
                                ErrorCodes.ConstFnCannotCallNonConst,
                                $"const fn '{_functionName}': cannot call non-const function '{funcName}'",
                                location
                            );
                        }
                    }
                }
            }
            return base.VisitCallExpr(context);
        }
    }

    /// <summary>
    /// Analyzes a block and detects unreachable code after return/break statements
    /// </summary>
    private void AnalyzeBlock(NovusParser.BlockContext block)
    {
        // Push a new drop scope for this block
        var scopeLocation = SourceLocationHelper.FromContext(block, _filePath, _sourceLines);
        _dropScopes.Push(new ScopeDropInfo());

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

        // Pop drop scope and emit drop calls for variables in this scope
        if (_dropScopes.Count > 0)
        {
            var scopeInfo = _dropScopes.Pop();
            EmitDropCallsForScope(scopeInfo);
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

        // Check if the last statement is an asm statement with a return type (implicit return)
        var lastStmt = statements[statements.Length - 1];
        if (lastStmt.asmStatement() != null)
        {
            var asmStmt = lastStmt.asmStatement();
            // If the asm statement has a return spec, it implicitly returns a value
            if (asmStmt.asmReturnSpec() != null)
            {
                return true;
            }
        }

        // Check if the last statement is an expression statement (implicit return)
        if (lastStmt.expressionStatement() != null)
        {
            // Expression statements at the end of a block serve as implicit returns
            // in functions with non-void return types
            return true;
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

        // Panic statement always diverges (never returns)
        if (stmt.panicStatement() != null)
            return true;

        // Unsafe block returns if its inner block returns
        if (stmt.unsafeBlock() != null)
        {
            var unsafeBlock = stmt.unsafeBlock();
            return AnalyzeBlockReturns(unsafeBlock.block());
        }

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

                    // Track coverage (recursively handle pipe patterns)
                    CollectCoveredVariants(pattern, coveredVariants, ref hasWildcard);

                    // Match arm can have a block, return statement, or just an expression
                    if (arm.block() != null)
                    {
                        // Block form: check if the block returns on all paths
                        if (!AnalyzeBlockReturns(arm.block()))
                        {
                            allArmsReturn = false;
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
                    }
                }

                if (!allArmsReturn)
                {
                    // Not all arms return - can't guarantee function returns
                    return false;
                }

                // Conservative exhaustiveness check for known patterns
                // Full exhaustiveness is checked in VisitMatchExpr
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

            // Track return moves (return x marks x as moved if non-Copy type)
            var returnedVarName = ExtractVariableName(exprContext);
            if (returnedVarName != null && _variables.TryGetValue(returnedVarName, out var returnedVar))
            {
                // Only track moves for non-Copy types
                if (!IsCopyType(returnedVar.Type))
                {
                    var moveLocation = SourceLocationHelper.FromContext(exprContext, _filePath, _sourceLines);
                    RecordMove(returnedVar.Id, new MoveInfo
                    {
                        VariableName = returnedVarName,
                        VariableId = returnedVar.Id,
                        MoveLocation = moveLocation,
                        Reason = "value moved by return statement"
                    });
                }
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

        // Before return, drop all variables in all scopes (in reverse scope order)
        // This handles early returns where variables need to be cleaned up
        foreach (var scopeInfo in _dropScopes.Reverse())
        {
            EmitDropCallsForScope(scopeInfo);
        }

        return null;
    }

    public override IrType? VisitVariableDeclaration([NotNull] NovusParser.VariableDeclarationContext context)
    {
        // Variable is mutable if declared with 'var', immutable if declared with 'let'
        var isMutable = context.KW_VAR() != null;

        // Check if this is tuple destructuring
        var tuplePattern = context.tuplePattern();
        if (tuplePattern != null)
        {
            // Handle tuple destructuring: let (a, b, c) = expr
            return HandleTupleDestructuring(tuplePattern, context.expression(), context.type(), isMutable, context);
        }

        // Regular single-variable declaration
        var identifierNode = context.IDENTIFIER();
        var name = identifierNode?.GetText() ?? "_";
        var isThrowaway = name == "_";

        // For location, use identifier if present, otherwise use the first token (let/var)
        var location = identifierNode != null
            ? SourceLocationHelper.FromToken(identifierNode.Symbol, _filePath, _sourceLines)
            : SourceLocationHelper.FromToken(context.Start, _filePath, _sourceLines);

        // Check for reserved keywords (skip for throwaway bindings)
        if (!isThrowaway && !ValidateNotReservedKeyword(name, location, "variable"))
            return null;

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
            // Check for redundant array size specification - this is error-prone
            // E.g., var buffer: [u8; 16] = [0u8; 16] - the size appears twice
            var typeCtx = context.type();
            var exprCtx = context.expression();
            if (typeCtx is NovusParser.ArrayTypeWithSizeContext &&
                exprCtx is NovusParser.PrimaryExprContext primaryExpr &&
                primaryExpr.primaryExpression() is NovusParser.ArrayRepeatLiteralContext)
            {
                _diagnostics.ReportError(
                    "E0045",
                    "redundant array size specification",
                    location,
                    helpTexts: new List<string>
                    {
                        "the array size is specified both in the type annotation and the initializer",
                        "remove the size from the type annotation: use '[T]' instead of '[T; N]'",
                        "alternatively, omit the type annotation entirely and let the compiler infer it"
                    }
                );
            }

            // Parse the type annotation (explicit array sizes are allowed and validated against initializer)
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

        // Check for assignment moves (let y = x marks x as moved if non-Copy type)
        var sourceVarName = ExtractVariableName(context.expression());
        var sourceFieldName = ExtractFieldName(context.expression());

        if (sourceVarName != null && _variables.TryGetValue(sourceVarName, out var sourceVar))
        {
            // Check if this is a field access (e.g., let x = obj.field)
            if (sourceFieldName != null)
            {
                // Get the field type to check if it's Copy
                IrType? fieldType = null;
                if (sourceVar.Type is IrStructType structType)
                {
                    var field = structType.GetField(sourceFieldName);
                    fieldType = field?.Type;
                }

                // Only track field move if the field type is non-Copy
                if (fieldType != null && !IsCopyType(fieldType))
                {
                    // Moving a specific field via assignment
                    var moveLocation = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                    RecordFieldMove(sourceVar.Id, sourceVarName, sourceFieldName, moveLocation,
                        $"field '{sourceFieldName}' moved by assignment to '{name}'");
                }
                // If field is Copy, no move tracking needed
            }
            else if (!IsCopyType(sourceVar.Type))
            {
                // Moving the entire value via assignment (simple identifier, not field access)
                var moveLocation = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                RecordMove(sourceVar.Id, new MoveInfo
                {
                    VariableName = sourceVarName,
                    VariableId = sourceVar.Id,
                    MoveLocation = moveLocation,
                    Reason = $"value moved by assignment to '{name}'"
                });
            }
        }

        // Add variable to symbol table (skip for throwaway bindings)
        if (!isThrowaway)
        {
            var variableSymbol = new VariableSymbol(name, varType, isMutable, location, Id: _nextVariableId++);
            _variables[name] = variableSymbol;

            // Track variable for automatic drop if it is not a Copy type
            // Note: We can't check TypeImplementsDrop here since we don't have the IrModule yet.
            // The actual drop call insertion will be done in IrBuilder after we know which types implement Drop.
            if (!IsCopyType(varType))
            {
                var dropInfo = new DropInfo
                {
                    VariableId = variableSymbol.Id,
                    VariableName = name,
                    VariableType = varType,
                    DeclLocation = location,
                    WasMoved = false,
                    MovedFields = null
                };

                _dropInfo[variableSymbol.Id] = dropInfo;

                // Add to current scope's drop list (if we have a scope)
                if (_dropScopes.Count > 0)
                {
                    _dropScopes.Peek().VariablesToDrop.Add(dropInfo);
                }
            }
        }

        return null;
    }

    private IrType? HandleTupleDestructuring(
        NovusParser.TuplePatternContext tuplePattern,
        NovusParser.ExpressionContext expression,
        NovusParser.TypeContext? typeAnnotation,
        bool isMutable,
        ParserRuleContext context)
    {
        // First, analyze the expression to get its type
        var exprType = Visit(expression);
        if (exprType == null)
            return null;

        // Expression must be a tuple type
        if (exprType is not IrTupleType tupleType)
        {
            var location = SourceLocationHelper.FromContext(expression, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0017",
                $"cannot destructure non-tuple type '{TypeToString(exprType)}'",
                location,
                helpTexts: new List<string>
                {
                    "tuple destructuring requires a tuple value on the right-hand side",
                    $"consider using a regular variable binding instead"
                }
            );
            return null;
        }

        // Extract the identifiers from the tuple pattern
        var identifiers = new List<string>();
        foreach (var child in tuplePattern.children)
        {
            if (child is ITerminalNode terminal && terminal.Symbol.Type == NovusParser.IDENTIFIER)
            {
                identifiers.Add(terminal.GetText());
            }
            else if (child.GetText() == "_")
            {
                identifiers.Add("_");
            }
        }

        // Validate that the number of bindings matches the tuple arity
        if (identifiers.Count != tupleType.ElementTypes.Count)
        {
            var location = SourceLocationHelper.FromContext(tuplePattern, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0017",
                $"tuple destructuring pattern has {identifiers.Count} bindings but tuple has {tupleType.ElementTypes.Count} elements",
                location,
                helpTexts: new List<string>
                {
                    $"expected {tupleType.ElementTypes.Count} bindings to match tuple type '{TypeToString(tupleType)}'"
                }
            );
            return null;
        }

        // Register each binding
        for (int i = 0; i < identifiers.Count; i++)
        {
            var bindingName = identifiers[i];
            var bindingType = tupleType.ElementTypes[i];

            // Skip throwaway bindings (_)
            if (bindingName == "_")
                continue;

            // Get location for this binding
            var bindingLocation = SourceLocationHelper.FromContext(tuplePattern, _filePath, _sourceLines);

            // Check for duplicate variable names
            if (_variables.ContainsKey(bindingName))
            {
                var originalLocation = _variables[bindingName].Location;
                _diagnostics.ReportError(
                    "E0016",
                    $"variable '{bindingName}' is already defined in this scope",
                    bindingLocation,
                    relatedLocations: new List<(SourceLocation, string)>
                    {
                        (originalLocation, $"previous definition of '{bindingName}' here")
                    }
                );
                continue;
            }

            // Add variable to symbol table
            var variableSymbol = new VariableSymbol(bindingName, bindingType, isMutable, bindingLocation, Id: _nextVariableId++);
            _variables[bindingName] = variableSymbol;

            // Track variable for automatic drop if it is not a Copy type
            if (!IsCopyType(bindingType))
            {
                var dropInfo = new DropInfo
                {
                    VariableId = variableSymbol.Id,
                    VariableName = bindingName,
                    VariableType = bindingType,
                    DeclLocation = bindingLocation,
                    WasMoved = false,
                    MovedFields = null
                };

                _dropInfo[variableSymbol.Id] = dropInfo;

                // Add to current scope's drop list (if we have a scope)
                if (_dropScopes.Count > 0)
                {
                    _dropScopes.Peek().VariablesToDrop.Add(dropInfo);
                }
            }
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
            // Internal error - assignment statement AST is malformed
            // Report diagnostic and continue with error recovery
            var errorLocation = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                ErrorCodes.InternalCompilerError,
                "Assignment statement must have either IDENTIFIER or KW_SELF (internal parser error)",
                errorLocation
            );
            // Return early - can't continue analyzing this statement
            return IrIntType.I32;
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

            // Only check mutability if it's a simple variable (no member/index access)
            // For member/index access (e.g., self.len++), we're modifying the field, not the variable
            if (lvalueSuffixes.Length == 0)
            {
                // Simple variable increment/decrement: check if variable is mutable
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
            }
            // For member/index access, the type checking is already done in VisitPostIncrementExpr/VisitPostDecrementExpr

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
            // Lvalue suffix chain validation is complex and requires handling:
            // - IrStructType (direct field access)
            // - IrPointerType (auto-dereference for field access)
            // - IrReferenceType (auto-dereference for field access)
            // - IrArrayType and pointer indexing
            // The existing code in IrBuilder handles this during code generation.
            // For now, we just visit the value expression to check its type.
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
                            "consider using a mutable reference (&var) if you need to modify the value"
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

        // Check if left operand supports the operator (either built-in or via trait)
        if (!TypeSupportsOperator(leftType, op, out var traitName, out _))
        {
            var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
            string traitHint = op == "+" ? "Add" : "Sub";
            _diagnostics.ReportError(
                "E0004",
                $"cannot apply operator '{op}' to type '{TypeToString(leftType)}' - type does not implement {traitHint}",
                location,
                helpTexts: new List<string>
                {
                    $"implement the {traitHint} trait for '{TypeToString(leftType)}' to enable this operator"
                }
            );
            return null;
        }

        // For trait-based operators, both operands must have the same type
        if (traitName != null)
        {
            if (TypeToString(leftType) != TypeToString(rightType))
            {
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0004",
                    $"mismatched types in operator '{op}': '{TypeToString(leftType)}' and '{TypeToString(rightType)}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "both operands must have the same type for trait-based operators"
                    }
                );
                return null;
            }
            // Trait-based operators return Self (same type)
            return leftType;
        }

        // For built-in numeric operators, check right operand
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
            if (!_currentFunctionSuppressedWarnings.Contains("W0001"))
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

        var op = context.GetChild(1).GetText();

        // Check if left operand supports the operator (either built-in or via trait)
        if (!TypeSupportsOperator(leftType, op, out var traitName, out _))
        {
            var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
            string traitHint = op switch { "*" => "Mul", "/" => "Div", "%" => "Rem", _ => "operator" };
            _diagnostics.ReportError(
                "E0004",
                $"cannot apply operator '{op}' to type '{TypeToString(leftType)}' - type does not implement {traitHint}",
                location,
                helpTexts: new List<string>
                {
                    $"implement the {traitHint} trait for '{TypeToString(leftType)}' to enable this operator"
                }
            );
            return null;
        }

        // For trait-based operators, both operands must have the same type
        if (traitName != null)
        {
            if (TypeToString(leftType) != TypeToString(rightType))
            {
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0004",
                    $"mismatched types in operator '{op}': '{TypeToString(leftType)}' and '{TypeToString(rightType)}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "both operands must have the same type for trait-based operators"
                    }
                );
                return null;
            }
            // Trait-based operators return Self (same type)
            return leftType;
        }

        // For built-in numeric operators, check right operand
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

        // Check for division by zero or modulo by zero (if right is a constant 0)
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
            if (!_currentFunctionSuppressedWarnings.Contains("W0001"))
            {
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportWarning(
                    "W0001",
                    $"mixing signed and unsigned types in arithmetic operation",
                    location
                );
            }
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
        // Allow: numeric -> numeric, bool -> numeric, numeric -> bool, pointer -> integer, integer -> pointer, pointer -> pointer, &T -> *T
        // Also allow: fn(...) -> numeric (function pointer to address), numeric -> fn(...) (address to function pointer)
        bool isValidCast = (IsNumericType(targetType) && IsNumericType(exprType)) ||
                           (IsNumericType(targetType) && exprType is IrBoolType) ||  // bool -> numeric
                           (targetType is IrBoolType && IsNumericType(exprType)) ||  // numeric -> bool
                           (IsNumericType(targetType) && exprType is IrPointerType) ||
                           (targetType is IrPointerType && IsNumericType(exprType)) ||
                           (targetType is IrPointerType && exprType is IrPointerType) ||
                           (targetType is IrPointerType && exprType is IrReferenceType) ||
                           (targetType is IrPointerType && exprType is IrMutReferenceType) ||
                           (IsNumericType(targetType) && exprType is IrFunctionPointerType) ||  // fn(...) -> u32
                           (targetType is IrFunctionPointerType && IsNumericType(exprType));    // u32 -> fn(...)

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
            if (!_currentFunctionSuppressedWarnings.Contains("W0002"))
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
        }

        return targetType;
    }

    public override IrType? VisitAsCastExpr([NotNull] NovusParser.AsCastExprContext context)
    {
        var exprType = Visit(context.expression());
        var targetType = ParseType(context.type());

        if (exprType == null)
            return targetType;

        // Check if cast is valid (same rules as C-style cast)
        // Allow: numeric -> numeric, bool -> numeric, numeric -> bool, pointer -> integer, integer -> pointer, pointer -> pointer, &T -> *T
        // Also allow: fn(...) -> numeric (function pointer to address), numeric -> fn(...) (address to function pointer)
        bool isValidCast = (IsNumericType(targetType) && IsNumericType(exprType)) ||
                           (IsNumericType(targetType) && exprType is IrBoolType) ||  // bool -> numeric
                           (targetType is IrBoolType && IsNumericType(exprType)) ||  // numeric -> bool
                           (IsNumericType(targetType) && exprType is IrPointerType) ||
                           (targetType is IrPointerType && IsNumericType(exprType)) ||
                           (targetType is IrPointerType && exprType is IrPointerType) ||
                           (targetType is IrPointerType && exprType is IrReferenceType) ||
                           (targetType is IrPointerType && exprType is IrMutReferenceType) ||
                           (IsNumericType(targetType) && exprType is IrFunctionPointerType) ||  // fn(...) -> u32
                           (targetType is IrFunctionPointerType && IsNumericType(exprType));    // u32 -> fn(...)

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
            if (!_currentFunctionSuppressedWarnings.Contains("W0002"))
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

        // Track moves in then branch
        EnterBranch(ControlFlowKind.If);
        AnalyzeBlock(context.block(0));
        var thenMoves = ExitBranch();

        // Remove if let/var binding before else block (not in scope there)
        var keysToRemove = _variables.Keys.Where(k => !variablesBeforeIf.Contains(k)).ToList();
        foreach (var key in keysToRemove)
        {
            _variables.Remove(key);
        }

        // Track moves in else branch (if present)
        Dictionary<int, MoveInfo>? elseMoves = null;
        if (context.ifStatement() != null)
        {
            EnterBranch(ControlFlowKind.If);
            Visit(context.ifStatement());
            elseMoves = ExitBranch();
        }
        else if (context.block().Length > 1)
        {
            EnterBranch(ControlFlowKind.If);
            AnalyzeBlock(context.block(1));
            elseMoves = ExitBranch();
        }

        // Merge: variable moved if moved in ANY branch
        MergeBranchMoves(thenMoves, elseMoves);

        return null;
    }

    // Helper to pass variable info from condition to then block
    private (string varName, IrType varType, bool isMutable)? _pendingIfLetVariable;

    public override IrType? VisitIfConditionExpression([NotNull] NovusParser.IfConditionExpressionContext context)
    {
        var conditionType = Visit(context.expression());
        var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);

        // Check that condition is a valid boolean expression
        // Accept bool, any numeric type (0 = false, non-zero = true), or pointer type (null = false, non-null = true)
        if (conditionType != null && !IsBoolOrNumericOrPointerType(conditionType))
        {
            _diagnostics.ReportError(
                "E0010",
                "if condition must be a boolean, numeric, or pointer type",
                location,
                helpTexts: new List<string>
                {
                    $"found type '{TypeToString(conditionType)}', expected a boolean, numeric, or pointer type",
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

    public override IrType? VisitWhileExpr([NotNull] NovusParser.WhileExprContext context)
    {
        var conditionType = Visit(context.expression());
        var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);

        // Check that condition is a valid boolean expression
        if (conditionType != null && !IsBoolOrNumericOrPointerType(conditionType))
        {
            _diagnostics.ReportError(
                "E0010",
                "while condition must be a boolean, numeric, or pointer type",
                location,
                helpTexts: new List<string>
                {
                    $"found type '{TypeToString(conditionType)}', expected a boolean, numeric, or pointer type"
                }
            );
        }

        // Enter loop context and track moves in loop body
        _loopDepth++;
        EnterBranch(ControlFlowKind.While);
        AnalyzeBlock(context.block());
        var loopMoves = ExitBranch();
        _loopDepth--;

        // Conservative: any move in loop body makes variable moved
        foreach (var (varId, moveInfo) in loopMoves)
        {
            _borrowChecker.RecordLoopMove(varId, new MoveInfo
            {
                VariableId = varId,
                VariableName = moveInfo.VariableName,
                MoveLocation = moveInfo.MoveLocation,
                Reason = $"value moved in loop body: {moveInfo.Reason}"
            });
        }

        return null;
    }

    public override IrType? VisitWhileVar([NotNull] NovusParser.WhileVarContext context)
    {
        // Analyze the RHS expression to get its type
        var rhsType = Visit(context.expression());
        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);

        if (rhsType == null)
        {
            _diagnostics.ReportError(
                "E0010",
                "while var condition RHS must have a valid type",
                location
            );
            return null;
        }

        // Determine variable type: explicit or inferred from RHS
        IrType varType;
        if (context.type() != null)
        {
            varType = _typeParser.ParseType(context.type());
        }
        else
        {
            varType = rhsType;
        }

        // Verify the variable type is valid for comparison (numeric or pointer)
        if (!IsNumericType(varType) && varType is not IrPointerType)
        {
            _diagnostics.ReportError(
                "E0010",
                $"while var loop counter must be a numeric type, found '{TypeToString(varType)}'",
                location
            );
        }

        // Declare the loop variable in a new scope for the while block
        var varName = context.IDENTIFIER().GetText();

        // Check for shadowing in current scope
        if (_variables.ContainsKey(varName))
        {
            _diagnostics.ReportWarning(
                "W0005",
                $"variable '{varName}' shadows an existing variable",
                location
            );
        }

        // Declare the mutable loop variable
        _variables[varName] = new VariableSymbol(varName, varType, IsMutable: true, location, Id: _nextVariableId++);

        // Enter loop context and track moves in loop body
        _loopDepth++;
        EnterBranch(ControlFlowKind.While);
        AnalyzeBlock(context.block());
        var loopMoves = ExitBranch();
        _loopDepth--;

        // Conservative: any move in loop body makes variable moved
        foreach (var (varId, moveInfo) in loopMoves)
        {
            _borrowChecker.RecordLoopMove(varId, new MoveInfo
            {
                VariableId = varId,
                VariableName = moveInfo.VariableName,
                MoveLocation = moveInfo.MoveLocation,
                Reason = $"value moved in loop body: {moveInfo.Reason}"
            });
        }

        return null;
    }

    public override IrType? VisitForCStyle([NotNull] NovusParser.ForCStyleContext context)
    {
        // Visit initialization - check both possible alternatives
        var varDecl = context.variableDeclaration();
        if (varDecl != null)
        {
            Visit(varDecl);
        }
        else
        {
            var assignmentStmts = context.assignmentStatement();
            if (assignmentStmts.Length > 0 && assignmentStmts[0] != null)
            {
                Visit(assignmentStmts[0]);
            }
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
        var assignmentStmts2 = context.assignmentStatement();
        if (assignmentStmts2.Length > 1 && assignmentStmts2[1] != null)
        {
            // Note: This is validated in the loop context during IR building
            // We don't validate it here separately
        }

        return null;
    }

    public override IrType? VisitForInLoop([NotNull] NovusParser.ForInLoopContext context)
    {
        // for [var] item in collection { ... }
        var itemName = context.IDENTIFIER().GetText();
        var collectionType = Visit(context.expression());
        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);

        // Check if the binding is mutable
        var isMutable = context.KW_VAR() != null;

        // Determine the item type from the collection type
        IrType itemType = IrIntType.I32; // Default fallback

        if (collectionType != null)
        {
            // Check if this is a range expression (handled specially - ranges yield their element type)
            var exprCtx = context.expression();
            if (exprCtx is NovusParser.RangeExprContext || exprCtx is NovusParser.RangeInclusiveExprContext)
            {
                // Range expressions yield their element type (the integer type)
                itemType = collectionType;
            }
            else
            {
                // For collections, validate that the type implements Iterable<T> and extract T
                var elementType = GetIterableElementType(collectionType);
                if (elementType != null)
                {
                    itemType = elementType;
                }
                else
                {
                    // Type doesn't implement Iterable<T> - report error
                    var typeName = GetBaseTypeName(collectionType);
                    _diagnostics.ReportError(
                        "E0050",
                        $"Type '{typeName}' cannot be used in a for-in loop",
                        location,
                        helpTexts: new List<string>
                        {
                            $"For-in loops require the collection type to implement Iterable<T>",
                            $"Implement 'impl<T> Iterable<T> for {typeName}' with get() and len() methods"
                        }
                    );
                }
            }
        }

        var itemSymbol = new VariableSymbol(itemName, itemType, isMutable, location!);
        _variables[itemName] = itemSymbol;

        // Enter loop context
        _loopDepth++;
        AnalyzeBlock(context.block());
        _loopDepth--;

        // Remove the item variable from scope
        _variables.Remove(itemName);

        return null;
    }

    /// <summary>
    /// Get the element type T for a type that implements Iterable<T>
    /// Returns null if the type doesn't implement Iterable
    /// </summary>
    private IrType? GetIterableElementType(IrType collectionType)
    {
        string typeName = GetBaseTypeName(collectionType);

        // Search through all trait impls to find Iterable<T> for this type
        foreach (var kvp in _traitResolver.GetAllImpls())
        {
            var implInfo = kvp.Value;

            // Check if this is an Iterable impl for our type
            if (implInfo.TraitName != "Iterable")
                continue;

            if (implInfo.TypeName != typeName)
                continue;

            // Found an Iterable impl for this type!
            // The element type is the first (and only) trait type argument
            if (implInfo.TraitTypeArgs.Count > 0)
            {
                var elementType = implInfo.TraitTypeArgs[0];

                // If the element type is still a generic parameter, try to substitute from the collection type's CacheKey
                if (elementType is IrGenericType genericElement && collectionType is IrStructType structType)
                {
                    // Find the index of this generic parameter in the impl's generic params
                    var paramIndex = implInfo.ImplGenericParams.IndexOf(genericElement.ParameterName);
                    if (paramIndex >= 0 && structType.CacheKey != null)
                    {
                        // Parse type arguments from CacheKey like "Vec<i32>" -> ["i32"]
                        var typeArgs = ParseTypeArgsFromCacheKey(structType.CacheKey);
                        if (paramIndex < typeArgs.Count)
                        {
                            // Resolve the type argument name to an actual type
                            var resolvedType = ResolveTypeByName(typeArgs[paramIndex]);
                            if (resolvedType != null)
                            {
                                return resolvedType;
                            }
                        }
                    }
                }

                return elementType;
            }

            // Iterable impl exists but no type argument - shouldn't happen for valid impls
            return null;
        }

        // Also check for array types - arrays are implicitly iterable
        if (collectionType is IrArrayType arrayType)
        {
            return arrayType.ElementType;
        }

        return null; // No Iterable impl found
    }

    /// <summary>
    /// Parse type arguments from a CacheKey like "Vec<i32>" or "HashMap<string, i32>"
    /// </summary>
    private List<string> ParseTypeArgsFromCacheKey(string cacheKey)
    {
        var result = new List<string>();
        var startIdx = cacheKey.IndexOf('<');
        if (startIdx < 0) return result;

        var endIdx = cacheKey.LastIndexOf('>');
        if (endIdx <= startIdx) return result;

        var argsStr = cacheKey.Substring(startIdx + 1, endIdx - startIdx - 1);

        // Split by comma, but respect nested angle brackets
        int depth = 0;
        int lastStart = 0;
        for (int i = 0; i < argsStr.Length; i++)
        {
            char c = argsStr[i];
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(argsStr.Substring(lastStart, i - lastStart).Trim());
                lastStart = i + 1;
            }
        }
        // Add the last argument
        if (lastStart < argsStr.Length)
        {
            result.Add(argsStr.Substring(lastStart).Trim());
        }

        return result;
    }

    /// <summary>
    /// Resolve a type name to an IrType
    /// </summary>
    private IrType? ResolveTypeByName(string typeName)
    {
        // Handle primitive types
        return typeName switch
        {
            "i8" => IrIntType.I8,
            "i16" => IrIntType.I16,
            "i32" => IrIntType.I32,
            "i64" => IrIntType.I64,
            "u8" => IrIntType.U8,
            "u16" => IrIntType.U16,
            "u32" => IrIntType.U32,
            "u64" => IrIntType.U64,
            "bool" => IrBoolType.Instance,
            "f32" => IrFloatType.F32,
            "f64" => IrFloatType.F64,
            _ => TryResolveStructOrEnumType(typeName)
        };
    }

    private IrType? TryResolveStructOrEnumType(string typeName)
    {
        // During semantic analysis, we don't have access to the IrModule yet.
        // For struct/enum types referenced in generic parameters, we rely on
        // the type already being correctly resolved in TraitTypeArgs.
        // This method is a fallback for primitive types which are handled above.
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

        // Before break, drop variables in current scope
        // Note: This drops the innermost scope only. Loop scopes are handled separately.
        if (_dropScopes.Count > 0)
        {
            EmitDropCallsForScope(_dropScopes.Peek());
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

    // Handle: using expression { statements }
    public override IrType? VisitUsingStatement([NotNull] NovusParser.UsingStatementContext context)
    {
        // Evaluate the expression (should be a value with Drop trait)
        var exprType = Visit(context.expression());

        if (exprType == null)
        {
            return null; // Error already reported
        }

        var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);

        // Check if the type implements Drop trait
        if (!TypeImplementsTrait(exprType, "Drop", new List<IrType>()))
        {
            _diagnostics.ReportError(
                "E0379",
                $"type '{TypeToString(exprType)}' does not implement the 'Drop' trait",
                location,
                helpTexts: new List<string>
                {
                    $"'using' requires a type that implements Drop for automatic cleanup",
                    $"add an impl block: impl Drop for {TypeToString(exprType)}",
                    $"or use a 'defer' statement for manual cleanup"
                }
            );
        }

        // The expression must be a value, not a complex expression
        // We need to be able to call drop() on it at the end of scope
        // For now, we'll analyze the block - the IR builder will handle creating the implicit drop call

        // Analyze the block body
        AnalyzeBlock(context.block());

        return null;
    }

    // Handle: assert!(condition) or assert!(condition, "message")
    public override IrType? VisitAssertStatement([NotNull] NovusParser.AssertStatementContext context)
    {
        // Analyze the condition expression
        var conditionType = Visit(context.expression());

        // Verify condition is boolean (asserts require explicit bool, not C-style truthiness)
        if (conditionType != null && conditionType is not IrBoolType)
        {
            var location = SourceLocationHelper.FromToken(context.Start, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0308",
                $"assert condition must be a boolean expression, found '{TypeToString(conditionType)}'",
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

    /// <summary>
    /// Handle let...else statement: let pattern = expr else { diverging block }
    /// The else block must diverge (return, break, continue, or panic).
    /// </summary>
    public override IrType? VisitLetElseStatement([NotNull] NovusParser.LetElseStatementContext context)
    {
        var location = SourceLocationHelper.FromToken(context.Start, _filePath, _sourceLines);

        // Type-check the expression
        var exprType = Visit(context.expression());
        if (exprType == null)
        {
            return null;
        }

        // Analyze the pattern to extract bindings
        var pattern = context.pattern();

        // For now, we support simple patterns that introduce bindings
        // The bindings will be available after the let...else statement
        AnalyzeLetElsePattern(pattern, exprType, location);

        // Visit the else block - it must diverge
        Visit(context.block());

        // Verify that the else block actually diverges
        var elseBlock = context.block();
        if (!AnalyzeBlockReturns(elseBlock))
        {
            var elseLocation = SourceLocationHelper.FromContext(elseBlock, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0021",
                "else block in let...else must diverge (return, break, continue, or panic)",
                elseLocation
            );
        }

        return null;
    }

    /// <summary>
    /// Analyze a pattern in let...else and register any bindings it introduces.
    /// </summary>
    private void AnalyzeLetElsePattern(NovusParser.PatternContext pattern, IrType exprType, SourceLocation location)
    {
        // Handle different pattern types
        if (pattern is NovusParser.IdentifierPatternContext idPattern)
        {
            // Simple binding: let x = expr else { ... }
            var name = idPattern.IDENTIFIER().GetText();
            if (!_variables.ContainsKey(name))
            {
                _variables[name] = new VariableSymbol(name, exprType, false, location);
            }
        }
        else if (pattern is NovusParser.VarIdentifierPatternContext mutIdPattern)
        {
            // Mutable binding: let mut x = expr else { ... }
            var name = mutIdPattern.IDENTIFIER().GetText();
            if (!_variables.ContainsKey(name))
            {
                _variables[name] = new VariableSymbol(name, exprType, true, location);
            }
        }
        else if (pattern is NovusParser.VariantPatternContext variantPattern)
        {
            // Enum variant pattern: let Some(value) = expr else { ... }
            // Extract bindings from the pattern
            var patternList = variantPattern.patternList();
            if (patternList != null)
            {
                // Get the inner types from the enum variant
                var innerTypes = GetVariantInnerTypes(exprType, variantPattern.variantName());

                var subPatterns = patternList.pattern();
                for (int i = 0; i < subPatterns.Length; i++)
                {
                    var innerType = i < innerTypes.Count ? innerTypes[i] : exprType;
                    AnalyzeLetElsePattern(subPatterns[i], innerType, location);
                }
            }
        }
        else if (pattern is NovusParser.WildcardPatternContext)
        {
            // Wildcard pattern: let _ = expr else { ... }
            // No bindings introduced
        }
        // Other pattern types can be added as needed
    }

    /// <summary>
    /// Get the inner types of an enum variant for pattern matching.
    /// </summary>
    private List<IrType> GetVariantInnerTypes(IrType enumType, NovusParser.VariantNameContext variantName)
    {
        var result = new List<IrType>();

        // Handle Option<T>::Some(value) -> T
        // Handle Result<T,E>::Ok(value) -> T
        // Handle Result<T,E>::Err(e) -> E

        if (enumType is IrEnumType irEnumType)
        {
            var variantStr = variantName.GetText();
            var variantNameOnly = variantStr.Contains("::") ? variantStr.Split("::").Last() : variantStr;

            // Find the variant in the enum
            foreach (var variant in irEnumType.Variants)
            {
                if (variant.Name == variantNameOnly)
                {
                    // Return the associated data types of this variant
                    result.AddRange(variant.AssociatedData);
                    break;
                }
            }
        }

        return result;
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
    /// Handle unsafe block as an expression: unsafe { ... }
    /// Returns the type of the last expression in the block.
    /// </summary>
    public override IrType? VisitUnsafeExpr([NotNull] NovusParser.UnsafeExprContext context)
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
            Reason = "Unsafe expression"
        });

        // Enter unsafe context
        _unsafeDepth++;

        IrType? resultType = null;
        try
        {
            // Analyze the block and get the type of the last expression
            resultType = AnalyzeBlockAsExpression(context.block());
        }
        finally
        {
            // Exit unsafe context
            _unsafeDepth--;
        }

        // Return the type of the last expression, or unit if no expression
        return resultType ?? IrTupleType.Unit;
    }

    /// <summary>
    /// Handle Copper DSL expression: copper { wait(0, 100); move(COLOR00, $F00); }
    /// Returns a pointer type that points to the generated copper list data.
    /// </summary>
    public override IrType? VisitCopperExpr([NotNull] NovusParser.CopperExprContext context)
    {
        // Copper list is inherently unsafe (direct hardware access)
        RequireUnsafe(context, "copper list", "it directly programs the Copper coprocessor hardware");

        // Visit all copper operations for validation
        var copperList = context.copperList();
        foreach (var operation in copperList.copperOperation())
        {
            ValidateCopperOperation(operation);
        }

        // Copper list evaluates to a pointer to the copper list data (chip RAM)
        // This pointer can then be stored in COP1LC/COP2LC registers
        return new IrPointerType(IrIntType.U16);
    }

    /// <summary>
    /// Validate a copper operation for semantic correctness
    /// </summary>
    private void ValidateCopperOperation(NovusParser.CopperOperationContext operation)
    {
        // Get the operation name from copperOpName rule (IDENTIFIER)
        var opName = operation.copperOpName().IDENTIFIER().GetText().ToLower();
        var expr0 = operation.expression(0);
        var expr1 = operation.expression(1);

        // Validate operation name is a valid copper instruction
        if (opName != "wait" && opName != "move" && opName != "skip")
        {
            var location = SourceLocationHelper.FromContext(operation.copperOpName(), _filePath, _sourceLines);
            _diagnostics.ReportError("E1056", $"Unknown copper operation '{opName}'. Valid operations are: wait, move, skip", location);
            return;
        }

        // Validate expression types based on operation
        var type0 = Visit(expr0) as IrType;
        var type1 = Visit(expr1) as IrType;

        if (opName == "wait" || opName == "skip")
        {
            // wait(x, y) / skip(x, y) - both must be integers
            if (type0 != null && !IsIntegralType(type0))
            {
                var location = SourceLocationHelper.FromContext(expr0, _filePath, _sourceLines);
                _diagnostics.ReportError("E1050", $"Copper {opName.ToUpper()} horizontal position must be an integer, got {type0.Name}", location);
            }
            if (type1 != null && !IsIntegralType(type1))
            {
                var location = SourceLocationHelper.FromContext(expr1, _filePath, _sourceLines);
                _diagnostics.ReportError("E1051", $"Copper {opName.ToUpper()} vertical position must be an integer, got {type1.Name}", location);
            }
        }
        else if (opName == "move")
        {
            // move(register, value) - both must be integers
            if (type0 != null && !IsIntegralType(type0))
            {
                var location = SourceLocationHelper.FromContext(expr0, _filePath, _sourceLines);
                _diagnostics.ReportError("E1052", $"Copper MOVE register must be an integer, got {type0.Name}", location);
            }
            if (type1 != null && !IsIntegralType(type1))
            {
                var location = SourceLocationHelper.FromContext(expr1, _filePath, _sourceLines);
                _diagnostics.ReportError("E1053", $"Copper MOVE value must be an integer, got {type1.Name}", location);
            }
        }
    }

    /// <summary>
    /// Handle Blitter DSL expression: blitter { source: ptr, dest: screen, width: 16, height: 16 }
    /// Returns unit type (blitter jobs execute synchronously by default).
    /// </summary>
    public override IrType? VisitBlitterExpr([NotNull] NovusParser.BlitterExprContext context)
    {
        // Blitter job is inherently unsafe (direct hardware access)
        RequireUnsafe(context, "blitter job", "it directly programs the Blitter hardware");

        // Track which required fields are present
        bool hasDestination = false;
        bool hasMinterm = false;

        // Validate blitter fields
        var blitterJob = context.blitterJob();
        foreach (var field in blitterJob.blitterField())
        {
            var fieldName = field.IDENTIFIER().GetText();
            var fieldExpr = field.expression();
            var fieldType = Visit(fieldExpr) as IrType;

            // Validate based on field name
            switch (fieldName.ToLower())
            {
                case "source":
                case "sourcea":
                case "sourceb":
                case "sourcec":
                    // Should be a pointer type
                    if (fieldType != null && fieldType is not IrPointerType)
                    {
                        var location = SourceLocationHelper.FromContext(fieldExpr, _filePath, _sourceLines);
                        _diagnostics.ReportError("E1060", $"Blitter {fieldName} must be a pointer, got {fieldType.Name}", location);
                    }
                    break;

                case "dest":
                case "destination":
                    hasDestination = true;
                    // Should be a pointer type
                    if (fieldType != null && fieldType is not IrPointerType)
                    {
                        var location = SourceLocationHelper.FromContext(fieldExpr, _filePath, _sourceLines);
                        _diagnostics.ReportError("E1060", $"Blitter {fieldName} must be a pointer, got {fieldType.Name}", location);
                    }
                    break;

                case "width":
                case "height":
                case "modulo":
                case "modulo_a":
                case "modulo_b":
                case "modulo_c":
                case "modulo_d":
                case "shift":
                case "shift_a":
                case "shift_b":
                    // Should be an integer type
                    if (fieldType != null && !IsIntegralType(fieldType))
                    {
                        var location = SourceLocationHelper.FromContext(fieldExpr, _filePath, _sourceLines);
                        _diagnostics.ReportError("E1061", $"Blitter {fieldName} must be an integer, got {fieldType.Name}", location);
                    }
                    break;

                case "minterm":
                    hasMinterm = true;
                    // Should be an integer type
                    if (fieldType != null && !IsIntegralType(fieldType))
                    {
                        var location = SourceLocationHelper.FromContext(fieldExpr, _filePath, _sourceLines);
                        _diagnostics.ReportError("E1061", $"Blitter {fieldName} must be an integer, got {fieldType.Name}", location);
                    }
                    break;

                case "wait":
                case "async":
                case "fill":
                case "descending":
                    // Should be a boolean
                    if (fieldType != null && fieldType is not IrBoolType)
                    {
                        var location = SourceLocationHelper.FromContext(fieldExpr, _filePath, _sourceLines);
                        _diagnostics.ReportError("E1062", $"Blitter {fieldName} must be a boolean, got {fieldType.Name}", location);
                    }
                    break;

                default:
                    // Unknown field - report warning
                    var loc = SourceLocationHelper.FromContext(field, _filePath, _sourceLines);
                    _diagnostics.ReportWarning("W1060", $"Unknown blitter field '{fieldName}'", loc);
                    break;
            }
        }

        // Validate required fields
        if (!hasDestination)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError("E1063", "Blitter job requires a 'dest' field specifying the destination pointer", location);
        }
        if (!hasMinterm)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError("E1064", "Blitter job requires a 'minterm' field specifying the boolean operation (e.g., $F0 for copy)", location);
        }

        // Blitter job returns unit (no value)
        return IrTupleType.Unit;
    }

    // ===========================
    // Inline Assembly Expression
    // ===========================

    /// <summary>
    /// Visit an inline assembly expression wrapper (from primaryExpression).
    /// Delegates to VisitAsmExpression.
    /// </summary>
    public override IrType? VisitAsmExpr([NotNull] NovusParser.AsmExprContext context)
    {
        return Visit(context.asmExpression()) as IrType;
    }

    /// <summary>
    /// Visit the actual inline assembly expression and return its type.
    /// The type is determined by the asmReturnSpec (-> type in register).
    /// </summary>
    public override IrType? VisitAsmExpression([NotNull] NovusParser.AsmExpressionContext context)
    {
        // Inline assembly is inherently unsafe
        // Note: The grammar already requires 'unsafe' keyword, so we don't need to check here

        // Validate inputs if present
        var inputList = context.asmInputList();
        if (inputList != null)
        {
            foreach (var input in inputList.asmInput())
            {
                // Validate the expression for each input
                var expr = input.expression();
                if (expr != null)
                {
                    Visit(expr);
                }
            }
        }

        // Determine return type from asmReturnSpec
        var returnSpec = context.asmReturnSpec();
        if (returnSpec == null)
        {
            // No return spec means no value returned (unit type)
            return IrTupleType.Unit;
        }

        // Check for single return type: -> type in register
        var typeCtx = returnSpec.type();
        if (typeCtx != null)
        {
            return ParseType(typeCtx);
        }

        // Check for multi-return: -> (type1 in reg1, type2 in reg2, ...)
        var multiReturn = returnSpec.asmMultiReturn();
        if (multiReturn != null)
        {
            // asmMultiReturn is: type KW_IN asmRegister (COMMA type KW_IN asmRegister)+
            var returnTypes = new List<IrType>();
            foreach (var typeCtx2 in multiReturn.type())
            {
                var itemType = ParseType(typeCtx2);
                returnTypes.Add(itemType);
            }

            // Return a tuple type for multiple returns
            if (returnTypes.Count == 1)
            {
                return returnTypes[0];
            }
            return new IrTupleType(returnTypes);
        }

        // Fallback to unit type
        return IrTupleType.Unit;
    }

    /// <summary>
    /// Analyze a block and return the type of the last expression.
    /// Used for block expressions like unsafe { } and potentially future block expressions.
    /// </summary>
    private IrType? AnalyzeBlockAsExpression(NovusParser.BlockContext block)
    {
        var statements = block.statement();
        IrType? lastType = null;

        foreach (var stmt in statements)
        {
            // Visit the statement for semantic analysis
            var stmtType = Visit(stmt);

            // Check if this is an expression statement that has a type
            if (stmt.expressionStatement() != null)
            {
                // Get the type from visiting the expression
                var exprStmt = stmt.expressionStatement();
                var expr = exprStmt.expression();
                if (expr != null)
                {
                    lastType = Visit(expr) as IrType;
                }
            }
            else
            {
                // Non-expression statements don't contribute a value
                lastType = null;
            }
        }

        return lastType;
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

        // Auto-dereference pointer and reference types for matching
        var actualMatchType = matchValueType;
        if (matchValueType is IrPointerType ptrType)
        {
            actualMatchType = ptrType.PointeeType;
        }
        else if (matchValueType is IrReferenceType refType)
        {
            actualMatchType = refType.PointeeType;
        }
        else if (matchValueType is IrMutReferenceType mutRefType)
        {
            actualMatchType = mutRefType.PointeeType;
        }

        // Ensure we're matching on an enum type, integer type, or bool type
        bool isEnumMatch = actualMatchType is IrEnumType;
        bool isIntegerMatch = actualMatchType is IrIntType || actualMatchType is IrBoolType;

        // For pattern analysis, we need the enum type to look up variants
        // If actualMatchType is a generic type parameter that refers to an enum,
        // we need to get the actual enum type for validation
        IrEnumType? enumTypeForValidation = null;
        if (isEnumMatch)
        {
            var enumFromType = (IrEnumType)actualMatchType;
            // When matching on a function return type, the IrEnumType stored in the function
            // might be a stale stub reference (created before variants were filled in during import).
            // Try to get the best available enum type with variants:
            // 1. If it's a monomorphized type (has CacheKey), look up from cache first (has concrete types)
            // 2. Otherwise look up by name (may be generic like Option<T>)
            // 3. Fall back to the type from the expression
            if (!string.IsNullOrEmpty(enumFromType.CacheKey))
            {
                // It's a monomorphized type like Option<MemoryBlock>
                enumTypeForValidation = _symbols.LookupMonomorphizedEnum(enumFromType.CacheKey);

                // If not in cache but we have a CacheKey, create monomorphized version on the fly
                if (enumTypeForValidation == null)
                {
                    // Get the base generic enum definition
                    var baseEnum = _symbols.LookupEnum(enumFromType.EnumName);
                    if (baseEnum != null && baseEnum.GenericParameters.Count > 0)
                    {
                        // Parse type arguments from CacheKey (format: "EnumName<TypeArg1,TypeArg2,...>")
                        var typeArgs = ParseTypeArgsFromCacheKey(enumFromType.CacheKey, enumFromType.EnumName, baseEnum.GenericParameters.Count);
                        if (typeArgs != null && typeArgs.Count == baseEnum.GenericParameters.Count)
                        {
                            // Build substitution map
                            var typeSubstitutions = new Dictionary<string, IrType>();
                            for (int i = 0; i < baseEnum.GenericParameters.Count; i++)
                            {
                                typeSubstitutions[baseEnum.GenericParameters[i]] = typeArgs[i];
                            }

                            // Create monomorphized variants
                            var monomorphizedVariants = new List<IrEnumVariant>();
                            foreach (var origVariant in baseEnum.Variants)
                            {
                                var monomorphizedData = new List<IrType>();
                                foreach (var dataType in origVariant.AssociatedData)
                                {
                                    monomorphizedData.Add(_typeParser.SubstituteGenericTypes(dataType, typeSubstitutions));
                                }
                                monomorphizedVariants.Add(new IrEnumVariant(origVariant.Name, origVariant.Tag, monomorphizedData));
                            }

                            // Create and cache the monomorphized enum
                            enumTypeForValidation = new IrEnumType(baseEnum.EnumName, monomorphizedVariants, null, enumFromType.CacheKey);
                            _symbols.RegisterMonomorphizedEnum(enumFromType.CacheKey, enumTypeForValidation);
                        }
                    }

                    // Fall back to enumFromType if we couldn't monomorphize
                    enumTypeForValidation ??= enumFromType;
                }
            }
            else if (enumFromType.Variants.Count > 0)
            {
                // Already has variants, use it directly
                enumTypeForValidation = enumFromType;
            }
            else
            {
                // Stale stub, look up by name
                enumTypeForValidation = _symbols.LookupEnum(enumFromType.EnumName) ?? enumFromType;
            }
        }
        else if (!isIntegerMatch && actualMatchType is IrGenericType genericType)
        {
            // Check if this generic type name refers to an enum
            // This handles cases like match on 'self' in impl<T> Option<T>
            // where 'self' has type IrGenericType("Option")
            // Also handles cases where dereferencing a pointer/reference yields IrGenericType
            if (_symbols.HasEnum(genericType.ParameterName))
            {
                isEnumMatch = true;
                enumTypeForValidation = _symbols.LookupEnum(genericType.ParameterName)!;
            }
        }

        if (!isEnumMatch && !isIntegerMatch)
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0035",
                $"match expression can only be used with enum or integer types, got '{actualMatchType.Name}'",
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

        // Track moves across all match arms
        var allArmMoves = new List<Dictionary<int, MoveInfo>>();

        // Collect arm types to determine the match expression's result type
        var armTypes = new List<IrType?>();

        // Analyze each match arm
        foreach (var armCtx in context.matchArm())
        {
            var pattern = armCtx.pattern();

            // Save current variable scope - store list of variables added by this pattern
            var variablesBeforePattern = new HashSet<string>(_variables.Keys);

            // Check if this arm has a guard - if so, allow binding patterns
            bool hasGuard = armCtx.KW_IF() != null;

            // Analyze pattern and bind variables
            if (isEnumMatch)
            {
                AnalyzePatternAndBind(pattern, enumTypeForValidation!, coveredVariants, ref hasWildcard);
            }
            else if (actualMatchType is IrIntType intType)
            {
                AnalyzeIntegerPatternAndBind(pattern, intType, coveredIntegerValues, ref hasWildcard, allowBinding: hasGuard);
            }
            else if (actualMatchType is IrBoolType)
            {
                // For bool matches, treat it as integer match with values 0/1
                // Bool patterns can be `true` or `false` which map to 1 and 0
                AnalyzeIntegerPatternAndBind(pattern, IrIntType.I32, coveredIntegerValues, ref hasWildcard, allowBinding: hasGuard);
            }

            // Track moves in this arm
            EnterBranch(ControlFlowKind.MatchArm);

            // Analyze guard expression if present (first expression in array)
            var expressions = armCtx.expression();
            int valueExprIndex = 0;
            if (armCtx.KW_IF() != null && expressions != null && expressions.Length > 0)
            {
                // First expression is the guard - must be boolean
                var guardType = Visit(expressions[0]);
                if (guardType != null && guardType is not IrBoolType)
                {
                    var location = SourceLocationHelper.FromContext(expressions[0], _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0308",
                        $"match guard must be a boolean expression, found '{TypeToString(guardType)}'",
                        location
                    );
                }
                valueExprIndex = 1;
            }

            // Analyze the arm body (expression, block, or return statement) with bound variables in scope
            IrType? armType = null;
            if (expressions != null && expressions.Length > valueExprIndex)
            {
                // Value expression (either the only expression, or the second if there's a guard)
                armType = Visit(expressions[valueExprIndex]);
            }
            else if (armCtx.block() != null)
            {
                AnalyzeBlock(armCtx.block());
                // Blocks in match arms don't currently support trailing expressions for type inference
                // The actual type will be determined by the IrBuilder which handles block values
                armType = _expectedType;
            }
            else if (armCtx.returnStatement() != null)
            {
                Visit(armCtx.returnStatement());
                // Return statements don't contribute to match type
                armType = null;
            }

            armTypes.Add(armType);

            // Collect moves from this arm
            allArmMoves.Add(ExitBranch());

            // Remove pattern bindings (they're only valid in this arm)
            var keysToRemove = _variables.Keys.Where(k => !variablesBeforePattern.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _variables.Remove(key);
            }
        }

        // Merge all arms - moved if moved in ANY arm
        MergeBranchMoves(allArmMoves);

        // Check exhaustiveness
        if (!hasWildcard)
        {
            if (isEnumMatch)
            {
                var uncoveredVariants = enumTypeForValidation!.Variants
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
            else if (actualMatchType is IrBoolType)
            {
                // For bool matches, check if both true (1) and false (0) are covered
                bool hasFalse = coveredIntegerValues.Contains(0);
                bool hasTrue = coveredIntegerValues.Contains(1);

                if (!hasFalse || !hasTrue)
                {
                    var missing = new List<string>();
                    if (!hasTrue) missing.Add("true");
                    if (!hasFalse) missing.Add("false");

                    var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0036",
                        "match on bool is not exhaustive",
                        location,
                        helpTexts: new List<string>
                        {
                            $"missing patterns: {string.Join(", ", missing)}",
                            "add missing patterns or use a wildcard pattern '_'"
                        }
                    );
                }
            }
            else // isIntegerMatch (but not bool)
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

        // Determine the result type of the match expression
        // Filter out nulls (return statements) and find the first non-null type
        var nonNullArmTypes = armTypes.Where(t => t != null).ToList();
        if (nonNullArmTypes.Count > 0)
        {
            // All arms should have the same type - use the first one
            // (Type checking between arms will be done in IrBuilder)
            return nonNullArmTypes[0];
        }

        // If all arms are return statements or have no value, match has no type
        return null;
    }

    // Helper method to collect covered variants from a pattern (handles pipe patterns recursively)
    private void CollectCoveredVariants(NovusParser.PatternContext pattern,
        HashSet<string> coveredVariants, ref bool hasWildcard)
    {
        if (pattern is NovusParser.WildcardPatternContext)
        {
            hasWildcard = true;
        }
        else if (pattern is NovusParser.PipePatternContext pipePattern)
        {
            // Recursively collect from both sides
            CollectCoveredVariants(pipePattern.pattern(0), coveredVariants, ref hasWildcard);
            CollectCoveredVariants(pipePattern.pattern(1), coveredVariants, ref hasWildcard);
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
    }

    private void AnalyzePatternAndBind(NovusParser.PatternContext pattern, IrEnumType enumType,
        HashSet<string> coveredVariants, ref bool hasWildcard)
    {
        switch (pattern)
        {
            case NovusParser.WildcardPatternContext:
                hasWildcard = true;
                break;

            case NovusParser.PipePatternContext pipePattern:
            {
                // Recursively analyze both sides of the pipe
                AnalyzePatternAndBind(pipePattern.pattern(0), enumType, coveredVariants, ref hasWildcard);
                AnalyzePatternAndBind(pipePattern.pattern(1), enumType, coveredVariants, ref hasWildcard);
                break;
            }

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

                        // Handle identifier patterns (e.g., Some(x) binds x as immutable)
                        if (subPattern is NovusParser.IdentifierPatternContext idPattern)
                        {
                            var bindingName = idPattern.IDENTIFIER().GetText();
                            var bindingType = variant.AssociatedData[i];
                            var location = SourceLocationHelper.FromToken(idPattern.IDENTIFIER().Symbol, _filePath, _sourceLines);

                            // Register this variable as immutable
                            _variables[bindingName] = new VariableSymbol(bindingName, bindingType!, false, location);
                        }
                        // Handle mut identifier patterns (e.g., Some(mut x) binds x as mutable)
                        else if (subPattern is NovusParser.VarIdentifierPatternContext mutIdPattern)
                        {
                            var bindingName = mutIdPattern.IDENTIFIER().GetText();
                            var bindingType = variant.AssociatedData[i];
                            var location = SourceLocationHelper.FromToken(mutIdPattern.IDENTIFIER().Symbol, _filePath, _sourceLines);

                            // Register this variable as mutable
                            _variables[bindingName] = new VariableSymbol(bindingName, bindingType!, true, location);
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
        HashSet<long> coveredValues, ref bool hasWildcard, bool allowBinding = false)
    {
        switch (pattern)
        {
            case NovusParser.WildcardPatternContext:
                hasWildcard = true;
                break;

            case NovusParser.PipePatternContext pipePattern:
            {
                // Recursively analyze both sides of the pipe
                AnalyzeIntegerPatternAndBind(pipePattern.pattern(0), intType, coveredValues, ref hasWildcard, allowBinding);
                AnalyzeIntegerPatternAndBind(pipePattern.pattern(1), intType, coveredValues, ref hasWildcard, allowBinding);
                break;
            }

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
                            if (!_currentFunctionSuppressedWarnings.Contains("W0001"))
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
                // Allow bool literals in patterns (they map to 0/1)
                // This is valid when matching on bool type
                var isTrue = boolPattern.KW_TRUE() != null;
                long value = isTrue ? 1 : 0;

                if (coveredValues.Contains(value))
                {
                    if (!_currentFunctionSuppressedWarnings.Contains("W0001"))
                    {
                        var location = SourceLocationHelper.FromToken(boolPattern.Start, _filePath, _sourceLines);
                        _diagnostics.ReportWarning(
                            "W0001",
                            $"duplicate match pattern for value {(isTrue ? "true" : "false")}",
                            location,
                            helpTexts: new List<string>
                            {
                                "this pattern will never be reached because an earlier pattern matches the same value"
                            }
                        );
                    }
                }

                coveredValues.Add(value);
                break;
            }

            case NovusParser.IdentifierPatternContext identPattern:
            {
                // Check if this identifier refers to a constant
                var identName = identPattern.IDENTIFIER().GetText();
                var constantSymbol = _symbols.LookupConstant(identName);

                if (constantSymbol != null && constantSymbol.Type is IrIntType)
                {
                    // Treat this as a literal value - extract the integer value
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
                    {
                        var location = SourceLocationHelper.FromToken(pattern.Start, _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0041",
                            $"constant '{identName}' has non-integer value",
                            location
                        );
                        break;
                    }

                    // Check for duplicate patterns
                    if (coveredValues.Contains(value))
                    {
                        var location = SourceLocationHelper.FromToken(pattern.Start, _filePath, _sourceLines);
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
                        64 => true,
                        _ => false
                    };

                    if (!valueInRange)
                    {
                        var location = SourceLocationHelper.FromToken(pattern.Start, _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0040",
                            $"constant value {value} does not fit in type '{intType.Name}'",
                            location
                        );
                    }

                    coveredValues.Add(value);
                }
                else if (allowBinding)
                {
                    // This is a binding pattern (e.g., `n if n > 0`)
                    // Bind the identifier to the matched value
                    var location = SourceLocationHelper.FromToken(identPattern.Start, _filePath, _sourceLines);
                    _variables[identName] = new VariableSymbol(identName, intType, false, location);
                    // Binding patterns don't contribute to exhaustiveness checking
                    // They match any value (like wildcards) but bind it to a name
                }
                else
                {
                    // Not a constant or not an integer constant - report error
                    var location = SourceLocationHelper.FromToken(pattern.Start, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0044",
                        "invalid pattern for integer match",
                        location,
                        helpTexts: new List<string>
                        {
                            "integer match patterns only accept integer literals, integer constants, or wildcards",
                            $"example: match value {{ 0 => ..., 1 => ..., CONSTANT => ..., _ => ... }}",
                            "use a guard with binding patterns: match value { n if n > 0 => ... }"
                        }
                    );
                }
                break;
            }

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
                        "integer match patterns only accept integer literals, integer constants, or wildcards",
                        $"example: match value {{ 0 => ..., 1 => ..., CONSTANT => ..., _ => ... }}"
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
            // Must be the same struct (compare StructName, not Name, because Name includes generic params)
            if (paramStruct.StructName != argStruct.StructName)
                return false;

            // Check if paramStruct contains generic types and argStruct is fully concrete
            var paramCacheKey = paramStruct.CacheKey ?? paramStruct.Name;
            var argCacheKey = argStruct.CacheKey ?? argStruct.Name;

            // Case 1: paramStruct has generic type parameters (either in GenericParameters or in CacheKey like "Vec<T>")
            // and argStruct is monomorphized (has CacheKey with concrete type args like "Vec<Str>")
            // This handles Vec::new() -> Vec<T> being matched against expected type Vec<Str>
            if (argCacheKey.Contains("<") && paramCacheKey.Contains("<"))
            {
                // Extract type arguments from param cache key
                var paramStartIdx = paramCacheKey.IndexOf('<');
                var paramEndIdx = paramCacheKey.LastIndexOf('>');
                var paramTypeArgsStr = paramCacheKey.Substring(paramStartIdx + 1, paramEndIdx - paramStartIdx - 1);
                var paramTypeArgKeys = paramTypeArgsStr.Split(',').Select(s => s.Trim()).ToArray();

                // Use TypeArguments directly if available, otherwise fall back to parsing from cache key
                IrType?[] argTypeArgs;
                if (argStruct.TypeArguments != null && argStruct.TypeArguments.Count == paramTypeArgKeys.Length)
                {
                    argTypeArgs = argStruct.TypeArguments.ToArray();
                }
                else
                {
                    // Fall back to parsing from cache key
                    var argStartIdx = argCacheKey.IndexOf('<');
                    var argEndIdx = argCacheKey.LastIndexOf('>');
                    var argTypeArgsStr = argCacheKey.Substring(argStartIdx + 1, argEndIdx - argStartIdx - 1);
                    var argTypeArgKeys = argTypeArgsStr.Split(',').Select(s => s.Trim()).ToArray();
                    argTypeArgs = argTypeArgKeys.Select(k => ParseTypeFromCacheKey(k)).ToArray();

                    if (argTypeArgKeys.Length != paramTypeArgKeys.Length)
                        return false;
                }

                // Match each type argument
                for (int i = 0; i < paramTypeArgKeys.Length; i++)
                {
                    var paramTypeArgKey = paramTypeArgKeys[i];
                    var inferredType = argTypeArgs[i];

                    // Check if paramTypeArgKey is a generic parameter (typically single capital letter like T, E, etc.)
                    // Note: could also be multi-char like "Item" but single cap letters are most common
                    if (IsGenericParameterName(paramTypeArgKey))
                    {
                        // This is a generic parameter - infer it from the argument type
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
                    else
                    {
                        // Concrete types must match - compare types directly if available
                        if (inferredType != null)
                        {
                            var expectedType = ParseTypeFromCacheKey(paramTypeArgKey);
                            if (expectedType != null && !TypesCompatible(expectedType, inferredType))
                                return false;
                        }
                    }
                }
                return true;
            }

            // Case 1b: paramStruct has GenericParameters list (the generic template itself)
            if (paramStruct.GenericParameters.Count > 0 && (argStruct.TypeArguments != null || argCacheKey.Contains("<")))
            {
                // Use TypeArguments directly if available, otherwise fall back to parsing from cache key
                IrType?[] argTypeArgs;
                if (argStruct.TypeArguments != null && argStruct.TypeArguments.Count == paramStruct.GenericParameters.Count)
                {
                    argTypeArgs = argStruct.TypeArguments.ToArray();
                }
                else if (argCacheKey.Contains("<"))
                {
                    // Fall back to parsing from cache key
                    var argStartIdx = argCacheKey.IndexOf('<');
                    var argEndIdx = argCacheKey.LastIndexOf('>');
                    var argTypeArgsStr = argCacheKey.Substring(argStartIdx + 1, argEndIdx - argStartIdx - 1);
                    var argTypeArgKeys = argTypeArgsStr.Split(',').Select(s => s.Trim()).ToArray();

                    if (paramStruct.GenericParameters.Count != argTypeArgKeys.Length)
                        return false;

                    argTypeArgs = argTypeArgKeys.Select(k => ParseTypeFromCacheKey(k)).ToArray();
                }
                else
                {
                    return false;
                }

                // Match each generic parameter to its corresponding type argument
                for (int i = 0; i < paramStruct.GenericParameters.Count; i++)
                {
                    var paramName = paramStruct.GenericParameters[i];
                    var inferredType = argTypeArgs[i];

                    // Infer the generic parameter from the argument type
                    if (inferredType != null)
                    {
                        if (substitutions.ContainsKey(paramName))
                        {
                            // Check consistency
                            if (!TypesCompatible(substitutions[paramName], inferredType))
                                return false;
                        }
                        else
                        {
                            substitutions[paramName] = inferredType;
                        }
                    }
                }
                return true;
            }

            // Case 2: Both have cache keys with type arguments, try to match them
            if (paramCacheKey.Contains("<") && (argStruct.TypeArguments != null || argCacheKey.Contains("<")))
            {
                // Extract type arguments from param cache key
                var paramStartIdx = paramCacheKey.IndexOf('<');
                var paramEndIdx = paramCacheKey.LastIndexOf('>');
                var paramTypeArgsStr = paramCacheKey.Substring(paramStartIdx + 1, paramEndIdx - paramStartIdx - 1);
                var paramTypeArgKeys = paramTypeArgsStr.Split(',').Select(s => s.Trim()).ToArray();

                // Use TypeArguments directly if available, otherwise fall back to parsing from cache key
                IrType?[] argTypeArgs;
                if (argStruct.TypeArguments != null && argStruct.TypeArguments.Count == paramTypeArgKeys.Length)
                {
                    argTypeArgs = argStruct.TypeArguments.ToArray();
                }
                else if (argCacheKey.Contains("<"))
                {
                    var argStartIdx = argCacheKey.IndexOf('<');
                    var argEndIdx = argCacheKey.LastIndexOf('>');
                    var argTypeArgsStr = argCacheKey.Substring(argStartIdx + 1, argEndIdx - argStartIdx - 1);
                    var argTypeArgKeys = argTypeArgsStr.Split(',').Select(s => s.Trim()).ToArray();

                    if (paramTypeArgKeys.Length != argTypeArgKeys.Length)
                        return false;

                    argTypeArgs = argTypeArgKeys.Select(k => ParseTypeFromCacheKey(k)).ToArray();
                }
                else
                {
                    return false;
                }

                // Match each type argument
                for (int i = 0; i < paramTypeArgKeys.Length; i++)
                {
                    var paramTypeArgKey = paramTypeArgKeys[i];
                    var inferredType = argTypeArgs[i];

                    // Check if paramTypeArgKey is a generic parameter (single capital letter like T, E, etc.)
                    if (paramTypeArgKey.Length == 1 && char.IsUpper(paramTypeArgKey[0]))
                    {
                        // This is a generic parameter - infer it from the argument type
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
                    else
                    {
                        // Concrete types must match - compare types directly if available
                        if (inferredType != null)
                        {
                            var expectedType = ParseTypeFromCacheKey(paramTypeArgKey);
                            if (expectedType != null && !TypesCompatible(expectedType, inferredType))
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
        var structLookup = _symbols.LookupStruct(key);
        if (structLookup != null)
        {
            return structLookup;
        }
        var enumLookup = _symbols.LookupEnum(key);
        if (enumLookup != null)
        {
            return enumLookup;
        }

        return null;
    }

    /// <summary>
    /// Parse type arguments from a cache key string like "Option&lt;Vec&lt;u8&gt;&gt;"
    /// Returns list of IrType or null if parsing fails.
    /// </summary>
    private List<IrType>? ParseTypeArgsFromCacheKey(string cacheKey, string enumName, int expectedCount)
    {
        // CacheKey format: "EnumName<TypeArg1,TypeArg2,...>"
        // Need to handle nested generics like "Option<Vec<u8>>"

        var prefix = enumName + "<";
        if (!cacheKey.StartsWith(prefix) || !cacheKey.EndsWith(">"))
            return null;

        // Extract the type arguments part: "Vec<u8>" from "Option<Vec<u8>>"
        var argsString = cacheKey.Substring(prefix.Length, cacheKey.Length - prefix.Length - 1);

        // Split by comma, respecting nested angle brackets
        var typeArgStrings = SplitTypeArgs(argsString);
        if (typeArgStrings.Count != expectedCount)
            return null;

        // Parse each type argument string into IrType
        var result = new List<IrType>();
        foreach (var typeStr in typeArgStrings)
        {
            var irType = ParseTypeFromCacheKeyRecursive(typeStr);
            if (irType == null)
                return null;
            result.Add(irType);
        }

        return result;
    }

    /// <summary>
    /// Split type argument string by comma, respecting nested angle brackets.
    /// E.g., "Vec&lt;u8&gt;,i32" -> ["Vec&lt;u8&gt;", "i32"]
    /// </summary>
    private List<string> SplitTypeArgs(string argsString)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (int i = 0; i < argsString.Length; i++)
        {
            var c = argsString[i];
            if (c == '<')
                depth++;
            else if (c == '>')
                depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(argsString.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        // Add the last argument
        if (start < argsString.Length)
            result.Add(argsString.Substring(start).Trim());

        return result;
    }

    /// <summary>
    /// Recursively parse a type from cache key format (handles nested generics)
    /// </summary>
    private IrType? ParseTypeFromCacheKeyRecursive(string key)
    {
        key = key.Trim();

        // Handle primitive types
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
        }

        // Handle pointer types (ptr_...)
        if (key.StartsWith("ptr_"))
        {
            var pointeeType = ParseTypeFromCacheKeyRecursive(key.Substring(4));
            return pointeeType != null ? _typeInterner.GetPointerType(pointeeType) : null;
        }

        // Handle tuple types: (Type1, Type2, ...)
        if (key.StartsWith("(") && key.EndsWith(")"))
        {
            // Extract the inner part: "Type1, Type2, ..."
            var innerPart = key.Substring(1, key.Length - 2);
            var elementStrings = SplitTypeArgs(innerPart);

            var elementTypes = new List<IrType>();
            foreach (var elemStr in elementStrings)
            {
                var elemType = ParseTypeFromCacheKeyRecursive(elemStr);
                if (elemType == null) return null;
                elementTypes.Add(elemType);
            }

            // Create a tuple struct type (uses synthesized struct name)
            return _typeInterner.GetTupleType(elementTypes);
        }

        // Handle generic types (Name<Args>)
        if (key.Contains("<"))
        {
            var openBracket = key.IndexOf('<');
            var baseName = key.Substring(0, openBracket);
            var argsString = key.Substring(openBracket + 1, key.Length - openBracket - 2);
            var typeArgStrings = SplitTypeArgs(argsString);

            // Try to look up in monomorphized cache first
            var cached = _symbols.LookupMonomorphizedEnum(key);
            if (cached != null)
                return cached;

            var cachedStruct = _symbols.LookupMonomorphizedStruct(key);
            if (cachedStruct != null)
                return cachedStruct;

            // Try to build it from base type
            var baseEnum = _symbols.LookupEnum(baseName);
            if (baseEnum != null && baseEnum.GenericParameters.Count == typeArgStrings.Count)
            {
                var typeArgs = new List<IrType>();
                foreach (var argStr in typeArgStrings)
                {
                    var argType = ParseTypeFromCacheKeyRecursive(argStr);
                    if (argType == null) return null;
                    typeArgs.Add(argType);
                }

                // Build substitutions and create monomorphized enum
                var typeSubstitutions = new Dictionary<string, IrType>();
                for (int i = 0; i < baseEnum.GenericParameters.Count; i++)
                {
                    typeSubstitutions[baseEnum.GenericParameters[i]] = typeArgs[i];
                }

                var monomorphizedVariants = new List<IrEnumVariant>();
                foreach (var origVariant in baseEnum.Variants)
                {
                    var monomorphizedData = new List<IrType>();
                    foreach (var dataType in origVariant.AssociatedData)
                    {
                        monomorphizedData.Add(_typeParser.SubstituteGenericTypes(dataType, typeSubstitutions));
                    }
                    monomorphizedVariants.Add(new IrEnumVariant(origVariant.Name, origVariant.Tag, monomorphizedData));
                }

                // IMPORTANT: Pass typeArgs as typeArguments so method calls can substitute generic params
                var monomorphizedEnum = new IrEnumType(baseName, monomorphizedVariants, null, key, typeArguments: typeArgs);
                _symbols.RegisterMonomorphizedEnum(key, monomorphizedEnum);
                return monomorphizedEnum;
            }

            var baseStruct = _symbols.LookupStruct(baseName);
            if (baseStruct != null && baseStruct.GenericParameters.Count == typeArgStrings.Count)
            {
                var typeArgs = new List<IrType>();
                foreach (var argStr in typeArgStrings)
                {
                    var argType = ParseTypeFromCacheKeyRecursive(argStr);
                    if (argType == null) return null;
                    typeArgs.Add(argType);
                }

                // Build substitutions and create monomorphized struct
                var typeSubstitutions = new Dictionary<string, IrType>();
                for (int i = 0; i < baseStruct.GenericParameters.Count; i++)
                {
                    typeSubstitutions[baseStruct.GenericParameters[i]] = typeArgs[i];
                }

                var monomorphizedFields = new List<IrStructField>();
                foreach (var field in baseStruct.Fields)
                {
                    var substitutedType = _typeParser.SubstituteGenericTypes(field.Type, typeSubstitutions);
                    monomorphizedFields.Add(new IrStructField(field.Name, substitutedType));
                }

                // IMPORTANT: Pass typeArgs as typeArguments so method calls can substitute generic params
                var monomorphizedStruct = new IrStructType(baseName, monomorphizedFields, null, key, typeArguments: typeArgs);
                _symbols.RegisterMonomorphizedStruct(key, monomorphizedStruct);
                return monomorphizedStruct;
            }

            return null;
        }

        // Try as simple struct/enum name
        var structLookup = _symbols.LookupStruct(key);
        if (structLookup != null)
            return structLookup;

        var enumLookup = _symbols.LookupEnum(key);
        if (enumLookup != null)
            return enumLookup;

        return null;
    }

    /// <summary>
    /// Recursively determines if a type contains any unresolved generic type parameters.
    ///
    /// This predicate is crucial for the monomorphization system to determine when a type
    /// is "fully concrete" and ready to be code-generated vs. "still generic" and requiring
    /// further substitution.
    ///
    /// The check is recursive to handle nested generic types:
    /// - Option&lt;T&gt; contains generic (has type parameter)
    /// - Option&lt;i32&gt; does NOT contain generic (fully concrete)
    /// - Option&lt;*T&gt; contains generic (pointer to generic)
    /// - Vec&lt;Option&lt;T&gt;&gt; contains generic (nested generic)
    ///
    /// Used to decide whether to:
    /// 1. Cache a monomorphized type (only if fully concrete)
    /// 2. Continue type inference (if still has generics)
    /// 3. Emit code generation (only for concrete types)
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>true if type contains any generic parameters, false if fully concrete</returns>
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
    /// Monomorphizes a generic function by creating a concrete version with type parameters replaced.
    ///
    /// This is the core of the compiler's generic specialization system. When a generic function like
    /// `fn identity&lt;T&gt;(x: T) -> T` is called with concrete types like `identity(42)`, this method
    /// creates a specialized version `identity_i32` with all T replaced by i32.
    ///
    /// The algorithm:
    /// 1. Generate a cache key from the function name and concrete type arguments (e.g., "identity&lt;i32&gt;")
    /// 2. Check the SymbolTable cache - if this exact instantiation exists, reuse it
    /// 3. Otherwise, create a new specialized function:
    ///    a. Substitute all type parameters in parameter types (T → i32)
    ///    b. Substitute type parameters in return type
    ///    c. Generate a unique mangled name for code generation (e.g., "identity_i32")
    ///    d. Create a new FunctionSymbol with no generic parameters (fully concrete)
    /// 4. Cache the result in SymbolTable for future lookups
    /// 5. Return the monomorphized function
    ///
    /// Caching is critical for:
    /// - Preventing duplicate instantiations (calling identity(42) twice shouldn't create two versions)
    /// - Ensuring type equality (all references to Vec&lt;i32&gt; refer to the same type)
    /// - Compilation performance (avoid redundant work)
    ///
    /// Example:
    ///   Generic: fn identity&lt;T&gt;(x: T) -> T
    ///   Substitutions: { "T" → IrIntType.I32 }
    ///   Result: FunctionSymbol("identity_i32", returns i32, params [(x, i32)])
    /// </summary>
    /// <param name="genericFunc">The generic function template to specialize</param>
    /// <param name="substitutions">Mapping from type parameter names to concrete types</param>
    /// <returns>A fully concrete (non-generic) function symbol, or null if constraint validation failed</returns>
    private FunctionSymbol? MonomorphizeFunction(FunctionSymbol genericFunc, Dictionary<string, IrType> substitutions)
    {
        // Validate generic constraints before monomorphization
        if (!ValidateGenericConstraints(genericFunc.WhereClause, genericFunc.GenericParameters!,
            substitutions.Values.ToList(), genericFunc.Location))
        {
            // Error already reported by ValidateGenericConstraints
            return null;
        }

        // Create cache key: FunctionName<TypeArg1,TypeArg2,...>
        var typeArgKeys = genericFunc.GenericParameters!.Select(p =>
            substitutions.ContainsKey(p) ? GetTypeCacheKey(substitutions[p]) : p);
        var cacheKey = $"{genericFunc.Name}<{string.Join(",", typeArgKeys)}>";

        // Check cache
        var cached = _symbols.LookupMonomorphizedFunction(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        // Substitute types in parameters
        var monomorphizedParams = genericFunc.Parameters.Select(p =>
            new ParameterSymbol(p.Name, _typeParser.SubstituteGenericTypes(p.Type, substitutions), p.Location, p.IsVariadic, p.IsConsuming)
        ).ToList();

        // Substitute return type
        var monomorphizedReturnType = _typeParser.SubstituteGenericTypes(genericFunc.ReturnType, substitutions);

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
        _symbols.RegisterMonomorphizedFunction(cacheKey, monomorphizedFunc);

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
                                        // Before reporting error, check if From<ArgType> trait exists for the expected type
                                        // This allows: Result::Err(DosError) when Result<T, NovusError> is expected
                                        if (!CanConvertViaFromTrait(argType, typeSubstitutions[paramName]))
                                        {
                                            var location = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);
                                            _diagnostics.ReportError(
                                                "E0042",
                                                $"type parameter '{paramName}' inferred as both '{TypeToString(typeSubstitutions[paramName])}' and '{TypeToString(argType)}'",
                                                location,
                                                helpTexts: new List<string>
                                                {
                                                    $"consider implementing From<{TypeToString(argType)}> for {TypeToString(typeSubstitutions[paramName])}"
                                                }
                                            );
                                        }
                                        // else: conversion is possible via From trait, allow it
                                    }
                                }
                            }
                            else
                            {
                                // Concrete type - validate compatibility
                                if (argType != null && !TypesCompatible(expectedParamType, argType))
                                {
                                    // Before reporting error, check if From<ArgType> trait exists for ExpectedType
                                    // This enables automatic conversion: Result::Err(DosError) -> Result<T, NovusError>
                                    if (!CanConvertViaFromTrait(argType, expectedParamType))
                                    {
                                        var location = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);
                                        _diagnostics.ReportError(
                                            "E0041",
                                            $"argument {i + 1} type mismatch",
                                            location,
                                            helpTexts: new List<string>
                                            {
                                                $"expected '{TypeToString(expectedParamType)}', got '{TypeToString(argType)}'",
                                                $"consider implementing From<{TypeToString(argType)}> for {TypeToString(expectedParamType)}"
                                            }
                                        );
                                    }
                                    // else: conversion is possible, allow it - IrBuilder will generate the conversion
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
                    var cached = _symbols.LookupMonomorphizedEnum(cacheKey);
                    if (cached != null)
                    {
                        return cached;
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
                    _symbols.RegisterMonomorphizedEnum(cacheKey, monomorphizedEnum);

                    return monomorphizedEnum;
                }
            }

            return enumType;
            }
            else
            {
                // Handle associated function calls (e.g., Vec::new())
                // resultType is the function's return type from the path expression

                // Check if this is a generic function that needs type inference
                var pathParts = pathCtx.GetText().Split("::");
                if (pathParts.Length == 2)
                {
                    var associatedFuncName = $"{pathParts[0]}::{pathParts[1]}";
                    if (_functions.ContainsKey(associatedFuncName))
                    {
                        var funcSymbol = _functions[associatedFuncName];

                        // If the function has generic parameters and we have an expected type, try to infer
                        if (funcSymbol.GenericParameters != null && funcSymbol.GenericParameters.Count > 0 && _expectedType != null)
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
                                        return resultType;
                                    }
                                    argTypes.Add(argType);
                                }
                            }

                            // Try to infer generic types from arguments first
                            var paramTypes = funcSymbol.Parameters.Select(p => p.Type).ToList();
                            var substitutions = InferGenericTypes(funcSymbol.GenericParameters, paramTypes, argTypes);

                            // If argument-based inference failed, try inference from expected type
                            if (substitutions == null)
                            {
                                substitutions = new Dictionary<string, IrType>();
                                if (funcSymbol.ReturnType != null && _expectedType != null && InferGenericTypeFromPair(funcSymbol.ReturnType, _expectedType, substitutions))
                                {
                                    // Check if all generic parameters were inferred
                                    var allInferred = true;
                                    foreach (var param in funcSymbol.GenericParameters)
                                    {
                                        if (!substitutions.ContainsKey(param))
                                        {
                                            allInferred = false;
                                            break;
                                        }
                                    }

                                    if (allInferred)
                                    {
                                        // Successfully inferred all type parameters from expected type
                                        return _expectedType;
                                    }
                                }
                                substitutions = null; // Inference failed
                            }

                            if (substitutions != null && funcSymbol.ReturnType != null)
                            {
                                // Successfully inferred - return the substituted return type
                                return _typeParser.SubstituteGenericTypes(funcSymbol.ReturnType, substitutions);
                            }

                            // Could not infer - error will be reported below
                        }
                    }
                }

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

        // Handle turbofish syntax: func::<T>() for standalone generic functions
        if (funcExpr is NovusParser.TurboFishExprContext turboFishCtx)
        {
            // Extract the function name from the inner expression
            var innerExpr = turboFishCtx.expression();
            if (innerExpr is NovusParser.PrimaryExprContext innerPrimaryCtx &&
                innerPrimaryCtx.primaryExpression() is NovusParser.IdentifierExprContext innerIdentExpr)
            {
                var turboFishFuncName = innerIdentExpr.identifier().GetText();

                // Parse the explicit type arguments
                var explicitTypeArgs = new List<IrType>();
                var typeArgsCtx = turboFishCtx.genericTypeArgs();
                if (typeArgsCtx != null)
                {
                    foreach (var typeCtx in typeArgsCtx.typeList().type())
                    {
                        var parsedType = _typeParser.ParseType(typeCtx);
                        explicitTypeArgs.Add(parsedType);
                    }
                }

                // Look up the generic function
                if (_functions.TryGetValue(turboFishFuncName, out var funcSymbol) &&
                    funcSymbol.GenericParameters != null && funcSymbol.GenericParameters.Count > 0)
                {
                    // Validate type argument count
                    if (explicitTypeArgs.Count != funcSymbol.GenericParameters.Count)
                    {
                        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0012",
                            $"wrong number of type arguments for '{turboFishFuncName}': expected {funcSymbol.GenericParameters.Count}, got {explicitTypeArgs.Count}",
                            location
                        );
                        return null;
                    }

                    // Build type substitutions from explicit type args
                    var substitutions = new Dictionary<string, IrType>();
                    for (int i = 0; i < funcSymbol.GenericParameters.Count; i++)
                    {
                        substitutions[funcSymbol.GenericParameters[i]] = explicitTypeArgs[i];
                    }

                    // Validate arguments if present
                    if (context.argumentList() != null)
                    {
                        var paramIndex = 0;
                        foreach (var argCtx in context.argumentList().expression())
                        {
                            if (paramIndex < funcSymbol.Parameters.Count)
                            {
                                var expectedParamType = _typeParser.SubstituteGenericTypes(
                                    funcSymbol.Parameters[paramIndex].Type, substitutions);
                                var savedExpected = _expectedType;
                                _expectedType = expectedParamType;
                                Visit(argCtx);
                                _expectedType = savedExpected;
                            }
                            else
                            {
                                Visit(argCtx);
                            }
                            paramIndex++;
                        }
                    }

                    // Monomorphize and return the substituted return type
                    var monomorphizedFunc = MonomorphizeFunction(funcSymbol, substitutions);
                    if (monomorphizedFunc == null)
                    {
                        return null; // Constraint validation failed, error already reported
                    }
                    return monomorphizedFunc.ReturnType;
                }

                // Not a generic function - report error
                var errLocation = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0012",
                    $"'{turboFishFuncName}' is not a generic function",
                    errLocation
                );
                return null;
            }

            // Turbofish on something other than an identifier
            var loc = SourceLocationHelper.FromContext(funcExpr, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0012",
                "turbofish syntax requires a function identifier",
                loc
            );
            return null;
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

                var enumType = _symbols.LookupEnum(enumName);
                if (enumType != null)
                {
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
                        // Note: We check for both fully monomorphized (GenericParameters.Count == 0)
                        // AND partially monomorphized (has CacheKey) types. This handles cases like
                        // Result<T, ExecError> in a generic function where T is still generic but E is concrete.
                        if (_expectedType is IrEnumType expectedEnumType &&
                            expectedEnumType.EnumName == enumType.EnumName &&
                            (expectedEnumType.GenericParameters.Count == 0 || expectedEnumType.CacheKey != null))
                        {
                            // Build a mapping from generic parameters to concrete types
                            // We need to extract ALL generic parameters from the expected type,
                            // not just those that appear in the current variant being constructed.
                            // For example: Result::Ok(value) with expected type Result<T, ExecError>
                            // should extract both T and E=ExecError, even though E doesn't appear in Ok's data.

                            // Strategy: Look through ALL variants to build the complete substitution map
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
                                            // Before reporting error, check if From<ArgType> trait exists for the expected type
                                            if (!CanConvertViaFromTrait(argType, typeSubstitutions[paramName]))
                                            {
                                                var location = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);
                                                _diagnostics.ReportError(
                                                    "E0042",
                                                    $"type parameter '{paramName}' inferred as both '{TypeToString(typeSubstitutions[paramName])}' and '{TypeToString(argType)}'",
                                                    location,
                                                    helpTexts: new List<string>
                                                    {
                                                        $"consider implementing From<{TypeToString(argType)}> for {TypeToString(typeSubstitutions[paramName])}"
                                                    }
                                                );
                                            }
                                            // else: conversion is possible via From trait, allow it
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
                                var expectedType = variant.AssociatedData[i];

                                // Set expected type for bidirectional type checking (enables null inference)
                                var savedExpectedType = _expectedType;
                                _expectedType = expectedType;

                                var argType = Visit(arguments[i]);

                                // Restore previous expected type
                                _expectedType = savedExpectedType;

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
                        var cached = _symbols.LookupMonomorphizedEnum(cacheKey);
                        if (cached != null)
                        {
                            return cached;
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
                        _symbols.RegisterMonomorphizedEnum(cacheKey, monomorphizedEnum);

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
                        var paramType = fpType.ParameterTypes[i];

                        // Set expected type for bidirectional type checking (enables null inference)
                        var savedExpectedType = _expectedType;
                        _expectedType = paramType;

                        var argType = Visit(arguments[i]);

                        // Restore previous expected type
                        _expectedType = savedExpectedType;

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
            else if (variable.Type is IrClosureType closureType)
            {
                // Validate argument count for closure call
                if (argCount != closureType.ParameterTypes.Count)
                {
                    var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0014",
                        $"closure expects {closureType.ParameterTypes.Count} argument(s), but {argCount} were provided",
                        location
                    );
                    return closureType.ReturnType;
                }

                // Validate argument types
                if (context.argumentList() != null)
                {
                    var arguments = context.argumentList().expression();
                    for (int i = 0; i < arguments.Length; i++)
                    {
                        var paramType = closureType.ParameterTypes[i];

                        // Set expected type for bidirectional type checking (enables null inference)
                        var savedExpectedType = _expectedType;
                        _expectedType = paramType;

                        var argType = Visit(arguments[i]);

                        // Restore previous expected type
                        _expectedType = savedExpectedType;

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

                return closureType.ReturnType;
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
            var expectedType = _expectedType;  // Capture to local to help nullability flow analysis
            var returnType = function.ReturnType;  // Capture to local to help nullability flow analysis
            if (substitutions == null && expectedType != null && returnType != null)
            {
                substitutions = new Dictionary<string, IrType>();
                if (InferGenericTypeFromPair(returnType, expectedType, substitutions))
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
            var monomorphized = MonomorphizeFunction(function, substitutions);
            if (monomorphized == null)
            {
                return null; // Constraint validation failed, error already reported
            }
            function = monomorphized;
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
                // For variadic functions, skip type checking for args beyond non-variadic params
                if (function.IsVariadic && i >= nonVariadicParamCount)
                {
                    Visit(arguments[i]); // Still visit, just don't type check
                    continue; // Extra args for variadic function - no type checking needed
                }

                var param = function.Parameters[i];
                var paramType = param.Type;

                // Set expected type for bidirectional type checking (enables null inference)
                var savedExpectedType = _expectedType;
                _expectedType = paramType;

                var argType = Visit(arguments[i]);

                // Restore previous expected type
                _expectedType = savedExpectedType;

                // Check if this parameter is consuming and mark the argument as moved
                if (param.IsConsuming)
                {
                    var argVarName = ExtractVariableName(arguments[i]);
                    var argFieldName = ExtractFieldName(arguments[i]);

                    if (argVarName != null && _variables.TryGetValue(argVarName, out var argVar))
                    {
                        var moveLocation = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);

                        // Check if this is a field move (e.g., consume(obj.field))
                        if (argFieldName != null)
                        {
                            // Get the field type to check if it's Copy
                            IrType? fieldType = null;
                            if (argVar.Type is IrStructType structType)
                            {
                                var field = structType.GetField(argFieldName);
                                fieldType = field?.Type;
                            }

                            // Only track field move if field type is non-Copy
                            if (fieldType != null && !IsCopyType(fieldType))
                            {
                                // Moving a specific field of a struct
                                RecordFieldMove(argVar.Id, argVarName, argFieldName, moveLocation,
                                    $"field '{argFieldName}' moved into consuming parameter '{param.Name}' of function '{functionName}'");
                            }
                        }
                        else
                        {
                            // Only track move for non-Copy types
                            // Copy types (primitives, pointers) are implicitly copied when passed to functions
                            if (!IsCopyType(argVar.Type))
                            {
                                RecordMove(argVar.Id, new MoveInfo
                                {
                                    VariableName = argVarName,
                                    VariableId = argVar.Id,
                                    MoveLocation = moveLocation,
                                    Reason = $"value moved into consuming parameter '{param.Name}' of function '{functionName}'"
                                });
                            }
                        }
                    }
                }

                if (argType != null && !TypesCompatible(paramType, argType))
                {
                    // Check if we can coerce Str to *u8
                    if (CanCoerceStrToU8Ptr(paramType, argType))
                    {
                        // Allow this coercion - IrBuilder will handle field extraction
                        continue;
                    }

                    // Check if we can coerce &[T; N] to Slice<T>
                    if (CanCoerceArrayToSlice(paramType, argType))
                    {
                        // Allow this coercion - IrBuilder will handle Slice construction
                        continue;
                    }

                    // Check if we can coerce &[T; N] to &[T] (sized to unsized slice)
                    if (CanCoerceSizedArrayRefToUnsizedSliceRef(paramType, argType))
                    {
                        // Allow this coercion - IrBuilder will handle unsizing
                        continue;
                    }

                    // Check if we can coerce Str to &Str
                    if (CanCoerceStrToStrRef(paramType, argType))
                    {
                        // Allow this coercion - IrBuilder will handle reference creation
                        continue;
                    }

                    // Check if we can coerce &T to *T (for extern functions)
                    if (CanCoerceReferenceToPointer(paramType, argType))
                    {
                        // Allow this coercion - IrBuilder will handle reference-to-pointer conversion
                        continue;
                    }

                    var location = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);

                    // Check if this is a function pointer mismatch - give detailed error
                    if (paramType is IrFunctionPointerType expectedFp && argType is IrFunctionPointerType actualFp)
                    {
                        var helpTexts = new List<string>
                        {
                            $"argument {i + 1} ('{param.Name}'): function pointer signature mismatch"
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
                                $"argument {i + 1} ('{param.Name}'): expected '{TypeToString(paramType)}', found '{TypeToString(argType)}'",
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
        else if (receiverType is IrReferenceType refType)
        {
            // Auto-dereference immutable references
            if (refType.PointeeType is IrStructType refStruct)
            {
                typeName = refStruct.StructName;
            }
            else if (refType.PointeeType is IrEnumType refEnum)
            {
                typeName = refEnum.EnumName;
            }
            else if (refType.PointeeType is IrGenericType refGenericType)
            {
                // The receiver is a reference to a generic type parameter (e.g., &K in HashMap<K, V>)
                // We need to check if the type parameter has trait bounds that include this method
                return HandleTraitMethodCallOnGenericType(callCtx, memberAccessCtx, refGenericType, methodName);
            }
            else if (refType.PointeeType is IrArrayType arrayType)
            {
                // Handle slice methods (arrays with length -1 are slices)
                if (arrayType.Length == -1)
                {
                    // Built-in slice methods
                    if (methodName == "len")
                    {
                        // .len() returns u32
                        return new IrIntType(32, false);
                    }
                    // Add more slice methods here as needed
                }
                var location = SourceLocationHelper.FromContext(memberAccessCtx, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0052",
                    $"cannot call method on type '{receiverType.Name}'",
                    location,
                    helpTexts: new List<string>
                    {
                        arrayType.Length == -1
                            ? "slices support methods: len()"
                            : "arrays do not have methods, use slice references instead"
                    }
                );
                return null;
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
        else if (receiverType is IrMutReferenceType mutRefType)
        {
            // Auto-dereference mutable references
            if (mutRefType.PointeeType is IrStructType mutRefStruct)
            {
                typeName = mutRefStruct.StructName;
            }
            else if (mutRefType.PointeeType is IrEnumType mutRefEnum)
            {
                typeName = mutRefEnum.EnumName;
            }
            else if (mutRefType.PointeeType is IrGenericType mutRefGenericType)
            {
                // The receiver is a mutable reference to a generic type parameter (e.g., &var K in HashMap<K, V>)
                // We need to check if the type parameter has trait bounds that include this method
                return HandleTraitMethodCallOnGenericType(callCtx, memberAccessCtx, mutRefGenericType, methodName);
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
        else if (receiverType is IrGenericType genericType)
        {
            // The receiver is a generic type parameter (e.g., K in HashMap<K, V>)
            // We need to check if the type parameter has trait bounds that include this method
            return HandleTraitMethodCallOnGenericType(callCtx, memberAccessCtx, genericType, methodName);
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
        var mangledMethodName = InstantiationKeyBuilder.BuildInherentMethodName(typeName, methodName);

        if (!_functions.ContainsKey(mangledMethodName))
        {
            // Inherent method not found - try trait implementations
            // Example: Point implements Clone trait, so p.clone() should find Point_Clone_clone
            var traitMethodName = _traitResolver.FindTraitMethod(typeName, methodName);
            if (traitMethodName != null && _functions.ContainsKey(traitMethodName))
            {
                mangledMethodName = traitMethodName;
            }
            else
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
        }

        var method = _functions[mangledMethodName];

        // Build type substitution map for generic methods
        var typeSubstitutions = new Dictionary<string, IrType>();
        if (receiverType is IrStructType receiverStruct && receiverStruct.CacheKey != null)
        {
            // Receiver is a monomorphized struct (e.g., Vec<i32>, HashMap<u32, u32>)
            // Get the base generic struct to find generic parameter names
            var baseStruct = _symbols.LookupStruct(receiverStruct.StructName);
            if (baseStruct != null && baseStruct.GenericParameters.Count > 0)
            {
                // First, try to use TypeArguments directly if available (most reliable method)
                if (receiverStruct.TypeArguments != null && receiverStruct.TypeArguments.Count == baseStruct.GenericParameters.Count)
                {
                    for (int i = 0; i < baseStruct.GenericParameters.Count; i++)
                    {
                        var genericParam = baseStruct.GenericParameters[i];
                        typeSubstitutions[genericParam] = receiverStruct.TypeArguments[i];
                    }
                }
                else
                {
                    // Fallback: Extract type arguments from the monomorphized struct fields
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
        }
        else if (receiverType is IrEnumType receiverEnum && receiverEnum.CacheKey != null)
        {
            // Receiver is a monomorphized enum (e.g., Option<i32>)
            // Get the base generic enum to find generic parameter names
            var baseEnum = _symbols.LookupEnum(receiverEnum.EnumName);
            if (baseEnum != null && baseEnum.GenericParameters.Count > 0)
            {
                // First, try to use TypeArguments directly if available (most reliable method)
                if (receiverEnum.TypeArguments != null && receiverEnum.TypeArguments.Count == baseEnum.GenericParameters.Count)
                {
                    for (int i = 0; i < baseEnum.GenericParameters.Count; i++)
                    {
                        var genericParam = baseEnum.GenericParameters[i];
                        typeSubstitutions[genericParam] = receiverEnum.TypeArguments[i];
                    }
                }
                else
                {
                    // Fallback: Extract type mappings by comparing base enum variants with monomorphized enum variants
                    for (int varIdx = 0; varIdx < baseEnum.Variants.Count && varIdx < receiverEnum.Variants.Count; varIdx++)
                    {
                        var baseVariant = baseEnum.Variants[varIdx];
                        var monoVariant = receiverEnum.Variants[varIdx];

                        if (baseVariant.Name == monoVariant.Name &&
                            baseVariant.AssociatedData.Count == monoVariant.AssociatedData.Count)
                        {
                            for (int dataIdx = 0; dataIdx < baseVariant.AssociatedData.Count; dataIdx++)
                            {
                                var baseDataType = baseVariant.AssociatedData[dataIdx];
                                var monoDataType = monoVariant.AssociatedData[dataIdx];

                                // If base variant data is generic type T, map T to the monomorphized type
                                if (baseDataType is IrGenericType gt)
                                {
                                    if (!typeSubstitutions.ContainsKey(gt.ParameterName))
                                    {
                                        typeSubstitutions[gt.ParameterName] = monoDataType;
                                    }
                                }
                                // If base variant data is *T, extract T from monomorphized *i32
                                else if (baseDataType is IrPointerType basePtrType && basePtrType.PointeeType is IrGenericType ptrGt)
                                {
                                    if (monoDataType is IrPointerType monoPtrType)
                                    {
                                        if (!typeSubstitutions.ContainsKey(ptrGt.ParameterName))
                                        {
                                            typeSubstitutions[ptrGt.ParameterName] = monoPtrType.PointeeType;
                                        }
                                    }
                                }
                            }
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

        // Check if self parameter is consuming and mark receiver as moved
        if (hasSelfParam && method.Parameters[0].IsConsuming)
        {
            // Extract variable name from receiver expression
            var receiverVarName = ExtractVariableName(receiverExpr);
            if (receiverVarName != null && _variables.TryGetValue(receiverVarName, out var receiverVar))
            {
                var moveLocation = SourceLocationHelper.FromContext(memberAccessCtx, _filePath, _sourceLines);
                RecordMove(receiverVar.Id, new MoveInfo
                {
                    VariableName = receiverVarName,
                    VariableId = receiverVar.Id,
                    MoveLocation = moveLocation,
                    Reason = $"value moved into consuming method '{methodName}'"
                });
            }
        }

        // Validate argument types (skip self parameter)
        if (callCtx.argumentList() != null)
        {
            var arguments = callCtx.argumentList().expression();
            var paramStartIndex = hasSelfParam ? 1 : 0;

            for (int i = 0; i < arguments.Length; i++)
            {
                var paramType = method.Parameters[paramStartIndex + i].Type;
                var savedExpectedType = _expectedType;
                _expectedType = paramType;
                var argType = Visit(arguments[i]);
                _expectedType = savedExpectedType;
                var param = method.Parameters[paramStartIndex + i];

                // Check if this parameter is consuming and mark the argument as moved
                if (param.IsConsuming)
                {
                    var argVarName = ExtractVariableName(arguments[i]);
                    var argFieldName = ExtractFieldName(arguments[i]);

                    if (argVarName != null && _variables.TryGetValue(argVarName, out var argVar))
                    {
                        var moveLocation = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);

                        // Check if this is a field move (e.g., obj.method(obj.field))
                        if (argFieldName != null)
                        {
                            // Get the field type to check if it's Copy
                            IrType? fieldType = null;
                            if (argVar.Type is IrStructType argStructType)
                            {
                                var field = argStructType.GetField(argFieldName);
                                fieldType = field?.Type;
                            }

                            // Only track field move if field type is non-Copy
                            if (fieldType != null && !IsCopyType(fieldType))
                            {
                                // Moving a specific field of a struct
                                RecordFieldMove(argVar.Id, argVarName, argFieldName, moveLocation,
                                    $"field '{argFieldName}' moved into consuming parameter '{param.Name}'");
                            }
                        }
                        else
                        {
                            // Only track move for non-Copy types
                            // Copy types (primitives, pointers) are implicitly copied when passed to functions
                            if (!IsCopyType(argVar.Type))
                            {
                                RecordMove(argVar.Id, new MoveInfo
                                {
                                    VariableName = argVarName,
                                    VariableId = argVar.Id,
                                    MoveLocation = moveLocation,
                                    Reason = $"value moved into consuming parameter '{param.Name}'"
                                });
                            }
                        }
                    }
                }

                // Substitute generic types in parameter type
                // Use SubstituteGenericTypes to handle all cases including nested generics like *T, Vec<T>, etc.
                paramType = _typeParser.SubstituteGenericTypes(paramType, typeSubstitutions);

                // Skip type checking if parameter type is still a generic parameter (will be inferred later)
                // This allows Vec::new() followed by vec.push(42i32) to work with type inference
                if (paramType is not IrGenericType)
                {
                    if (argType != null && !TypesCompatible(paramType, argType))
                    {
                        // Check if we can coerce Str to *u8
                        if (CanCoerceStrToU8Ptr(paramType, argType))
                        {
                            // Allow this coercion - IrBuilder will handle field extraction
                            continue;
                        }

                        // Check if we can coerce &[T; N] to Slice<T>
                        if (CanCoerceArrayToSlice(paramType, argType))
                        {
                            // Allow this coercion - IrBuilder will handle Slice construction
                            continue;
                        }

                        // Check if we can coerce &[T; N] to &[T] (sized to unsized slice)
                        if (CanCoerceSizedArrayRefToUnsizedSliceRef(paramType, argType))
                        {
                            // Allow this coercion - IrBuilder will handle unsizing
                            continue;
                        }

                        // Check if we can coerce Str to &Str
                        if (CanCoerceStrToStrRef(paramType, argType))
                        {
                            // Allow this coercion - IrBuilder will handle reference creation
                            continue;
                        }

                        // Check if we can coerce &T to *T (for extern functions)
                        if (CanCoerceReferenceToPointer(paramType, argType))
                        {
                            // Allow this coercion - IrBuilder will handle reference-to-pointer conversion
                            continue;
                        }

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

        // Apply type substitutions to the return type
        // Use SubstituteGenericTypes to handle all cases including nested generics like *T, Vec<T>, etc.
        var returnType = _typeParser.SubstituteGenericTypes(method.ReturnType, typeSubstitutions);

        return returnType;
    }

    /// <summary>
    /// Handle method calls on generic type parameters (e.g., key.hash() where key: K and K: Hash)
    /// This enables calling trait methods on generic types that have trait bounds.
    /// </summary>
    private IrType? HandleTraitMethodCallOnGenericType(
        NovusParser.CallExprContext callCtx,
        NovusParser.MemberAccessExprContext memberAccessCtx,
        IrGenericType genericType,
        string methodName)
    {
        var location = SourceLocationHelper.FromContext(memberAccessCtx, _filePath, _sourceLines);

        // Find the where clause constraints for this generic parameter
        // We need to search through enclosing contexts (function, impl, struct) for constraints
        var bounds = GetBoundsForGenericParameter(genericType.ParameterName);

        if (bounds == null || bounds.Count == 0)
        {
            _diagnostics.ReportError(
                "E0100",
                $"type '{genericType.ParameterName}' does not implement trait '{methodName}'",
                location,
                helpTexts: new List<string>
                {
                    $"the trait bound '{genericType.ParameterName}: {methodName}' is not satisfied",
                    $"add an impl block: impl {methodName} for {genericType.ParameterName}"
                }
            );
            return null;
        }

        // Find which trait defines this method
        foreach (var bound in bounds)
        {
            var trait = _symbols.LookupTrait(bound.TraitName);
            if (trait == null)
                continue;

            // Check if this trait has a method with the given name
            var traitMethod = trait.Methods.FirstOrDefault(m => m.Name == methodName);
            if (traitMethod != null)
            {
                // Found the trait method!
                // Validate argument count (excluding self)
                var providedArgCount = callCtx.argumentList()?.expression().Length ?? 0;
                var hasSelfParam = traitMethod.Parameters.Count > 0 && traitMethod.Parameters[0].Name == "self";
                var expectedArgCount = hasSelfParam ? traitMethod.Parameters.Count - 1 : traitMethod.Parameters.Count;

                if (providedArgCount != expectedArgCount)
                {
                    _diagnostics.ReportError(
                        "E0014",
                        $"trait method '{methodName}' expects {expectedArgCount} argument(s), but {providedArgCount} were provided",
                        location
                    );
                }

                // Validate argument types (skip self parameter)
                // Note: We need to substitute Self with the receiver's generic type
                if (callCtx.argumentList() != null)
                {
                    var arguments = callCtx.argumentList().expression();
                    var paramStartIndex = hasSelfParam ? 1 : 0;

                    for (int i = 0; i < arguments.Length && paramStartIndex + i < traitMethod.Parameters.Count; i++)
                    {
                        var paramType = traitMethod.Parameters[paramStartIndex + i].Type;
                        var argType = Visit(arguments[i]);

                        // Substitute Self with the generic type in parameter types
                        // e.g., for Eq::eq(&self, other: &Self), when called on K, &Self becomes &K
                        paramType = SubstituteSelfType(paramType, genericType);

                        if (argType != null && paramType != null && !TypesCompatible(paramType, argType))
                        {
                            var argLocation = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);
                            _diagnostics.ReportError(
                                "E0015",
                                $"mismatched types in trait method call",
                                argLocation,
                                helpTexts: new List<string>
                                {
                                    $"argument {i + 1}: expected '{TypeToString(paramType)}', found '{TypeToString(argType)}'"
                                }
                            );
                        }
                    }
                }

                // Return the method's return type
                // Note: The return type might be Self, which should be substituted with the generic type
                var returnType = traitMethod.ReturnType;
                if (returnType is IrSelfType)
                {
                    return genericType;
                }
                return returnType;
            }
        }

        // No matching method found in any of the trait bounds
        var traitNames = string.Join(", ", bounds.Select(b => b.TraitName));
        _diagnostics.ReportError(
            "E0053",
            $"no method named '{methodName}' found in traits {traitNames}",
            location,
            helpTexts: new List<string>
            {
                $"type parameter '{genericType.ParameterName}' is bounded by: {traitNames}",
                $"none of these traits define a method named '{methodName}'"
            }
        );
        return null;
    }

    /// <summary>
    /// Substitute Self type with the given concrete type in a type expression.
    /// Used when validating trait method calls on generic types.
    /// </summary>
    /// <param name="type">The type that may contain Self</param>
    /// <param name="selfType">The concrete type to substitute for Self</param>
    /// <returns>The type with Self replaced by selfType</returns>
    private IrType SubstituteSelfType(IrType type, IrType selfType)
    {
        if (type is IrSelfType)
        {
            return selfType;
        }

        if (type is IrReferenceType refType)
        {
            var substitutedPointee = SubstituteSelfType(refType.PointeeType, selfType);
            if (substitutedPointee != refType.PointeeType)
            {
                return new IrReferenceType(substitutedPointee);
            }
        }

        if (type is IrMutReferenceType mutRefType)
        {
            var substitutedPointee = SubstituteSelfType(mutRefType.PointeeType, selfType);
            if (substitutedPointee != mutRefType.PointeeType)
            {
                return new IrMutReferenceType(substitutedPointee);
            }
        }

        if (type is IrPointerType ptrType)
        {
            var substitutedPointee = SubstituteSelfType(ptrType.PointeeType, selfType);
            if (substitutedPointee != ptrType.PointeeType)
            {
                return new IrPointerType(substitutedPointee);
            }
        }

        // Handle generic types like Option<Self>, Result<Self, E>, Vec<Self>
        if (type is IrEnumType enumType && enumType.TypeArguments != null && enumType.TypeArguments.Count > 0)
        {
            bool anyChanged = false;
            var newTypeArgs = new List<IrType>();
            foreach (var typeArg in enumType.TypeArguments)
            {
                var substituted = SubstituteSelfType(typeArg, selfType);
                newTypeArgs.Add(substituted);
                if (substituted != typeArg)
                    anyChanged = true;
            }

            if (anyChanged)
            {
                // Create a new enum type with substituted type arguments
                // We need to create a new monomorphized version with the concrete types
                var newVariants = new List<IrEnumVariant>();
                foreach (var variant in enumType.Variants)
                {
                    var newData = new List<IrType>();
                    foreach (var dataType in variant.AssociatedData)
                    {
                        newData.Add(SubstituteSelfType(dataType, selfType));
                    }
                    newVariants.Add(new IrEnumVariant(variant.Name, variant.Tag, newData));
                }

                // Build new cache key based on substituted type arguments
                var typeArgNames = newTypeArgs.Select(t => GetTypeName(t));
                var newCacheKey = $"{enumType.EnumName}<{string.Join(", ", typeArgNames)}>";

                var newEnum = new IrEnumType(
                    enumType.EnumName,
                    newVariants,
                    null,  // No generic parameters on monomorphized type
                    newCacheKey,
                    enumType.Attributes,
                    enumType.WhereClause,
                    newTypeArgs
                );

                return newEnum;
            }
        }

        // Handle generic struct types like Vec<Self>
        if (type is IrStructType structType && structType.TypeArguments != null && structType.TypeArguments.Count > 0)
        {
            bool anyChanged = false;
            var newTypeArgs = new List<IrType>();
            foreach (var typeArg in structType.TypeArguments)
            {
                var substituted = SubstituteSelfType(typeArg, selfType);
                newTypeArgs.Add(substituted);
                if (substituted != typeArg)
                    anyChanged = true;
            }

            if (anyChanged)
            {
                // Create a new struct type with substituted type arguments
                var newFields = new List<IrStructField>();
                foreach (var field in structType.Fields)
                {
                    var newFieldType = SubstituteSelfType(field.Type, selfType);
                    newFields.Add(new IrStructField(field.Name, newFieldType));
                }

                // Build new cache key based on substituted type arguments
                var typeArgNames = newTypeArgs.Select(t => GetTypeName(t));
                var newCacheKey = $"{structType.StructName}<{string.Join(", ", typeArgNames)}>";

                var newStruct = new IrStructType(
                    structType.StructName,
                    newFields,
                    null,  // No generic parameters on monomorphized type
                    newCacheKey,
                    structType.Attributes,
                    structType.WhereClause,
                    newTypeArgs
                );

                return newStruct;
            }
        }

        return type;
    }

    /// <summary>
    /// Helper to get a type name for cache key generation
    /// </summary>
    private string GetTypeName(IrType type)
    {
        return type switch
        {
            IrEnumType et => et.CacheKey ?? et.Name,
            IrStructType st => st.CacheKey ?? st.Name,
            _ => type.Name
        };
    }

    /// <summary>
    /// Get the trait bounds for a generic type parameter from the current context.
    /// Checks both function-level and struct/impl-level where clauses.
    /// </summary>
    private List<IrTraitBound>? GetBoundsForGenericParameter(string paramName)
    {
        // Check the current function's where clause first (method-level bounds)
        if (_currentFunctionWhereClause != null)
        {
            var bounds = _currentFunctionWhereClause.GetBoundsFor(paramName);
            if (bounds.Count > 0)
                return bounds;
        }

        // Check the current struct/impl being analyzed (if any)
        if (_currentStructWhereClause != null)
        {
            var bounds = _currentStructWhereClause.GetBoundsFor(paramName);
            if (bounds.Count > 0)
                return bounds;
        }

        return null;
    }

    public override IrType? VisitBoolLiteral([NotNull] NovusParser.BoolLiteralContext context)
    {
        return IrBoolType.Instance;
    }

    public override IrType? VisitCharLiteral([NotNull] NovusParser.CharLiteralContext context)
    {
        // Character literals are always u8
        return IrIntType.U8;
    }

    public override IrType? VisitNullLiteral([NotNull] NovusParser.NullLiteralContext context)
    {
        // null is compatible with any pointer type
        // If we have an expected type from bidirectional type checking, use it
        if (_expectedType is IrPointerType)
        {
            return _expectedType;
        }
        // Otherwise, return a generic "null pointer" type (*u8) which is compatible with all pointers
        return new IrPointerType(IrIntType.U8);
    }

    public override IrType? VisitStringLiteral([NotNull] NovusParser.StringLiteralContext context)
    {
        var text = context.STRING_LITERAL().GetText();
        var stringValue = text.Substring(1, text.Length - 2);  // Remove quotes

        // Check if string contains interpolation (unescaped curly braces)
        bool hasInterpolation = false;
        for (int i = 0; i < stringValue.Length; i++)
        {
            if (stringValue[i] == '\\')
            {
                // Skip escaped character
                i++;
                continue;
            }
            if (stringValue[i] == '{')
            {
                hasInterpolation = true;
                break;
            }
        }

        if (hasInterpolation)
        {
            // Handle as interpolated string - returns String type
            return GetInterpolatedStringType(context);
        }

        // String literals create Str struct instances from std::strings
        // Str { ptr: *u8, len: u32 }
        var strType = _symbols.LookupStruct("Str");
        if (strType == null)
        {
            // When Str type is not available, fall back to *u8 (C-style string pointer)
            // This allows string literals to work in modules that can't import std::strings
            return new IrPointerType(IrIntType.U8);
        }

        return strType;
    }

    private IrType? GetInterpolatedStringType(ParserRuleContext context)
    {
        // Interpolated strings return String struct instances from std::strings
        var stringType = _symbols.LookupStruct("String");
        if (stringType == null)
        {
            // When String type is not available, fall back to *u8 (C-style string pointer)
            // This allows interpolated strings to work in minimal contexts without full std lib
            return new IrPointerType(IrIntType.U8);
        }

        // Check for Formatter type (used internally)
        var formatterType = _symbols.LookupStruct("Formatter");
        if (formatterType == null)
        {
            // Formatter not available, fall back to *u8
            return new IrPointerType(IrIntType.U8);
        }

        return stringType;
    }

    public override IrType? VisitInterpolatedStringLiteral([NotNull] NovusParser.InterpolatedStringLiteralContext context)
    {
        // Get type validation (String and Formatter must be available)
        var stringType = GetInterpolatedStringType(context);
        if (stringType == null)
        {
            return null;
        }

        // Parse the f-string content and validate expressions
        var fstring = context.GetChild(0)?.GetText();
        if (fstring == null)
        {
            return stringType;
        }

        var content = fstring.StartsWith("f\"")
            ? fstring.Substring(2, fstring.Length - 3)  // f"..." -> ...
            : fstring.Substring(1, fstring.Length - 2);  // "..." -> ...

        // Parse interpolation segments and validate each expression
        var segments = ParseInterpolatedStringSegments(content);
        foreach (var segment in segments)
        {
            // Check for parsing errors from the segment parser
            if (segment.HasError)
            {
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    ErrorCodes.UnmatchedBracesInFString,
                    segment.ErrorMessage,
                    location
                );
                continue;
            }

            if (!segment.IsStringSegment)
            {
                // Parse and visit the expression to validate it
                try
                {
                    var inputStream = new Antlr4.Runtime.AntlrInputStream(segment.Expression);
                    var lexer = new NovusLexer(inputStream);
                    var tokens = new Antlr4.Runtime.CommonTokenStream(lexer);
                    var parser = new NovusParser(tokens);
                    var exprContext = parser.expression();

                    // Visit the expression to validate it exists and has the right type
                    var exprType = Visit(exprContext);
                    if (exprType == null)
                    {
                        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0999",
                            $"invalid expression in f-string: {segment.Expression}",
                            location
                        );
                        return null;
                    }

                    // Note: We should verify the type implements Display trait here,
                    // but that check is complex and will be done in the IR builder
                }
                catch (Exception ex)
                {
                    var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                    _diagnostics.ReportError(
                        "E0999",
                        $"failed to parse expression in f-string: {segment.Expression} - {ex.Message}",
                        location
                    );
                    return null;
                }
            }
        }

        return stringType;
    }

    private List<InterpolationSegment> ParseInterpolatedStringSegments(string content)
    {
        var segments = new List<InterpolationSegment>();
        var i = 0;
        var currentString = new System.Text.StringBuilder();

        while (i < content.Length)
        {
            if (i < content.Length - 1 && content[i] == '{' && content[i + 1] == '{')
            {
                // Escaped brace {{ -> single {
                currentString.Append('{');
                i += 2;
            }
            else if (i < content.Length - 1 && content[i] == '}' && content[i + 1] == '}')
            {
                // Escaped brace }} -> single }
                currentString.Append('}');
                i += 2;
            }
            else if (content[i] == '{')
            {
                // Start of interpolation
                if (currentString.Length > 0)
                {
                    segments.Add(new InterpolationSegment { IsStringSegment = true, StringContent = currentString.ToString() });
                    currentString.Clear();
                }

                // Find matching }
                var braceDepth = 1;
                var expressionStart = i + 1;
                i++;
                while (i < content.Length && braceDepth > 0)
                {
                    if (content[i] == '{') braceDepth++;
                    else if (content[i] == '}') braceDepth--;
                    if (braceDepth > 0) i++;
                }

                if (braceDepth != 0)
                {
                    // Mismatched braces - return a special error segment instead of throwing
                    // The caller can check for this and report the error with proper location
                    segments.Add(new InterpolationSegment
                    {
                        IsStringSegment = false,
                        Expression = "",
                        HasError = true,
                        ErrorMessage = "Mismatched braces in f-string"
                    });
                    // Skip to end of content to prevent further errors
                    break;
                }

                var expression = content.Substring(expressionStart, i - expressionStart);
                segments.Add(new InterpolationSegment { IsStringSegment = false, Expression = expression });
                i++; // Skip closing }
            }
            else
            {
                currentString.Append(content[i]);
                i++;
            }
        }

        if (currentString.Length > 0)
        {
            segments.Add(new InterpolationSegment { IsStringSegment = true, StringContent = currentString.ToString() });
        }

        return segments;
    }

    private class InterpolationSegment
    {
        public bool IsStringSegment { get; set; }
        public string StringContent { get; set; } = "";
        public string Expression { get; set; } = "";
        // Error recovery fields - set when f-string parsing fails
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = "";
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

        // Check for use-after-move before any other processing
        // Only check simple identifiers (not qualified names like Result::Ok)
        if (!name.Contains("::") && _variables.TryGetValue(name, out var variable) && _borrowChecker.IsFullyMoved(variable.Id))
        {
            var moveInfo = _borrowChecker.GetMoveInfo(variable.Id)!;

            // Report error since entire value was moved (not partial move)
            {
                var useLocation = SourceLocationHelper.FromToken(context.identifier().Start, _filePath, _sourceLines);

                // Determine context-specific help text
                var helpTexts = new List<string> { moveInfo.Reason };

                if (moveInfo.Reason.Contains("conditional branch"))
                {
                    helpTexts.Add("value may have been moved in a conditional branch");
                    helpTexts.Add("help: if you need to use the value after the conditional, clone it before moving");
                }
                else if (moveInfo.Reason.Contains("loop body"))
                {
                    helpTexts.Add("value was moved inside a loop");
                    helpTexts.Add("help: consider restructuring to avoid moving in loops, or clone the value");
                }
                else
                {
                    helpTexts.Add("help: if you need to use the value after moving, consider cloning it first");
                }

                _diagnostics.ReportError(
                    "E0382",
                    $"use of moved value: `{name}`",
                    useLocation,
                    helpTexts: helpTexts,
                    relatedLocations: new List<(SourceLocation, string)>
                    {
                        (moveInfo.MoveLocation, "value moved here")
                    }
                );
                // Return the type anyway so we can continue analysis
                if (_variables.ContainsKey(name))
                    return _variables[name].Type;
                if (_globalVariables.ContainsKey(name))
                    return _globalVariables[name].Type;
                return null;
            }
            // If only some fields were moved, allow access to the variable itself
            // Field-specific checks will happen in VisitMemberAccessExpr
        }

        // Check if this is a qualified name (e.g., Result::Ok, Option::Some)
        if (name.Contains("::"))
        {
            var parts = name.Split("::");
            if (parts.Length == 2)
            {
                var enumName = parts[0];
                var variantName = parts[1];

                // Check if the enum exists
                var enumType = _symbols.LookupEnum(enumName);
                if (enumType != null)
                {
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

        // Check if this is a const generic parameter (e.g., N in Buffer<const N: u32>)
        if (_constGenericParams.TryGetValue(name, out var constGenericParam))
        {
            // Return the const generic parameter's type (e.g., u32 for `const N: u32`)
            // The actual value substitution happens during monomorphization in IrBuilder
            return constGenericParam.ConstType;
        }

        if (!_variables.ContainsKey(name) && !_globalVariables.ContainsKey(name) && !_functions.ContainsKey(name) && !_symbols.HasConstant(name))
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
        var constant = _symbols.LookupConstant(name);
        if (constant != null)
        {
            return constant.Type;
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

        // Check if this specific field has been moved (partial move tracking)
        var baseVarName = ExtractVariableName(context.expression());
        if (baseVarName != null && _variables.TryGetValue(baseVarName, out var baseVar))
        {
            var moveInfo = _borrowChecker.GetMoveInfo(baseVar.Id);
            if (moveInfo != null)
            {
                // Check if the entire struct was moved
                if (moveInfo.MovedFields == null)
                {
                    // Entire struct moved - this will be caught by VisitIdentifierExpr
                    // Don't report duplicate error here
                }
                else if (moveInfo.MovedFields.Contains(memberName))
                {
                    // This specific field was moved
                    var location = SourceLocationHelper.FromToken(context.IDENTIFIER().Symbol, _filePath, _sourceLines);
                    var otherFields = structType.Fields
                        .Where(f => !moveInfo.MovedFields.Contains(f.Name))
                        .Select(f => f.Name)
                        .ToList();

                    var helpTexts = new List<string>
                    {
                        $"field '{memberName}' was previously moved {moveInfo.Reason}"
                    };

                    if (otherFields.Any())
                    {
                        helpTexts.Add($"other fields of `{baseVarName}` are still valid: {string.Join(", ", otherFields)}");
                    }

                    _diagnostics.ReportError(
                        "E0382",
                        $"use of moved field: `{baseVarName}.{memberName}`",
                        location,
                        helpTexts: helpTexts
                    );
                }
            }
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
            // &var ptr[index] returns the element type
            return mutRefPtrType.PointeeType;
        }
        else if (baseType is IrArrayType arrayType)
        {
            // array[index] returns the element type
            return arrayType.ElementType;
        }
        else if (baseType is IrReferenceType indexRefType && indexRefType.PointeeType is IrArrayType refArrayType)
        {
            // &array[index] or &[T][index] (slice indexing) returns the element type
            return refArrayType.ElementType;
        }
        else if (baseType is IrMutReferenceType indexMutRefType && indexMutRefType.PointeeType is IrArrayType mutRefArrayType)
        {
            // &var array[index] or &var [T][index] (slice indexing) returns the element type
            return mutRefArrayType.ElementType;
        }
        else
        {
            // Check if the type implements the Index trait
            var indexReturnType = TypeSupportsIndexOperator(baseType, indexType);
            if (indexReturnType != null)
            {
                // Type implements Index<I, T> - return the T type
                return indexReturnType;
            }

            var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0024",
                $"cannot index into type '{TypeToString(baseType)}'",
                location,
                helpTexts: new List<string>
                {
                    "indexing is only valid on pointers, arrays, slices, or types implementing Index<I, T>"
                }
            );
            return null;
        }
    }

    public override IrType? VisitBorrowExpr([NotNull] NovusParser.BorrowExprContext context)
    {
        var exprContext = context.expression();
        bool isMutable = context.KW_VAR() != null;

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

        // Return a reference type: &T for immutable, &var T for mutable
        if (isMutable)
        {
            return _typeInterner.GetMutReferenceType(valueType);
        }
        else
        {
            return _typeInterner.GetReferenceType(valueType);
        }
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

        // Bool types can be compared with == and !=
        if (leftType is IrBoolType && rightType is IrBoolType)
        {
            if (op != "==" && op != "!=")
            {
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0004",
                    $"bool types can only be compared with == and !=, not '{op}'",
                    location
                );
            }
            return IrBoolType.Instance;
        }

        // Pointer types can be compared with == and != (for null checks)
        if (leftType is IrPointerType || rightType is IrPointerType)
        {
            if (op != "==" && op != "!=")
            {
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0004",
                    $"pointer types can only be compared with == and !=, not '{op}'",
                    location
                );
                return IrBoolType.Instance;
            }

            // Both sides must be pointer types (for ptr == ptr or ptr == null)
            if (!(leftType is IrPointerType) && !IsNumericType(leftType))
            {
                var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0004",
                    $"cannot compare '{TypeToString(leftType)}' with pointer type",
                    location
                );
            }
            if (!(rightType is IrPointerType) && !IsNumericType(rightType))
            {
                var location = SourceLocationHelper.FromContext(context.expression(1), _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0004",
                    $"cannot compare pointer type with '{TypeToString(rightType)}'",
                    location
                );
            }

            return IrBoolType.Instance;
        }

        // Check if left operand supports the comparison operator (either built-in or via trait)
        if (!TypeSupportsOperator(leftType, op, out var traitName, out _))
        {
            var location = SourceLocationHelper.FromContext(context.expression(0), _filePath, _sourceLines);
            string traitHint = (op == "==" || op == "!=") ? "Eq" : "PartialOrd";
            _diagnostics.ReportError(
                "E0004",
                $"cannot apply operator '{op}' to type '{TypeToString(leftType)}' - type does not implement {traitHint}",
                location,
                helpTexts: new List<string>
                {
                    $"implement the {traitHint} trait for '{TypeToString(leftType)}' to enable this operator"
                }
            );
            return IrBoolType.Instance;
        }

        // For trait-based operators, both operands must have the same type
        if (traitName != null)
        {
            if (TypeToString(leftType) != TypeToString(rightType))
            {
                var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0004",
                    $"mismatched types in operator '{op}': '{TypeToString(leftType)}' and '{TypeToString(rightType)}'",
                    location,
                    helpTexts: new List<string>
                    {
                        "both operands must have the same type for trait-based comparison operators"
                    }
                );
            }
            // Trait-based comparison operators always return bool
            return IrBoolType.Instance;
        }

        // For built-in numeric comparisons, check right operand
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

    public override IrType? VisitIfExpr([NotNull] NovusParser.IfExprContext context)
    {
        // Visit condition and both branches
        var conditionType = Visit(context.expression());

        // Get the type from the true block
        var trueType = VisitBlockAsExpressionType(context.block(0));

        // Get the type from the else part
        IrType? falseType;
        if (context.ifElseChain() != null)
        {
            falseType = Visit(context.ifElseChain());
        }
        else
        {
            falseType = VisitBlockAsExpressionType(context.block(1));
        }

        if (conditionType == null || trueType == null || falseType == null)
            return null;

        // Check that condition is boolean or numeric
        if (!IsBoolOrNumericType(conditionType) && !IsPointerType(conditionType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0040",
                $"if-expression condition must be boolean, numeric, or pointer type, found '{TypeToString(conditionType)}'",
                location
            );
        }

        // Both branches must have compatible types
        if (!TypesCompatible(trueType, falseType) && !TypesCompatible(falseType, trueType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0041",
                $"if-expression branches have incompatible types: '{TypeToString(trueType)}' and '{TypeToString(falseType)}'",
                location
            );
        }

        // Return the type of the true branch (they should be compatible)
        return trueType;
    }

    public override IrType? VisitIfElseChain([NotNull] NovusParser.IfElseChainContext context)
    {
        // Visit condition and both branches
        var conditionType = Visit(context.expression());

        // Get the type from the true block
        var trueType = VisitBlockAsExpressionType(context.block(0));

        // Get the type from the else part
        IrType? falseType;
        if (context.ifElseChain() != null)
        {
            falseType = Visit(context.ifElseChain());
        }
        else
        {
            falseType = VisitBlockAsExpressionType(context.block(1));
        }

        if (conditionType == null || trueType == null || falseType == null)
            return null;

        // Check that condition is boolean or numeric
        if (!IsBoolOrNumericType(conditionType) && !IsPointerType(conditionType))
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0040",
                $"if-expression condition must be boolean, numeric, or pointer type, found '{TypeToString(conditionType)}'",
                location
            );
        }

        // Both branches must have compatible types
        if (!TypesCompatible(trueType, falseType) && !TypesCompatible(falseType, trueType))
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0041",
                $"if-expression branches have incompatible types: '{TypeToString(trueType)}' and '{TypeToString(falseType)}'",
                location
            );
        }

        // Return the type of the true branch (they should be compatible)
        return trueType;
    }

    /// <summary>
    /// Get the type of a block when used as an expression (the type of the last expression)
    /// </summary>
    private IrType? VisitBlockAsExpressionType(NovusParser.BlockContext block)
    {
        var statements = block.statement();
        if (statements == null || statements.Length == 0)
        {
            // Empty block - return i32 as placeholder (could be unit/void in the future)
            return IrIntType.I32;
        }

        // Visit all statements except the last (for their side effects and variable declarations)
        for (int i = 0; i < statements.Length - 1; i++)
        {
            Visit(statements[i]);
        }

        // The last statement determines the block's type
        var lastStmt = statements[statements.Length - 1];

        if (lastStmt.expressionStatement() != null)
        {
            return Visit(lastStmt.expressionStatement().expression());
        }
        else if (lastStmt.returnStatement() != null)
        {
            var retStmt = lastStmt.returnStatement();
            if (retStmt.expression() != null)
            {
                return Visit(retStmt.expression());
            }
            return IrVoidType.Instance;
        }
        else
        {
            // Other statement types - visit for side effects and return a default
            Visit(lastStmt);
            return IrIntType.I32;
        }
    }

    private bool IsPointerType(IrType type)
    {
        return type is IrPointerType or IrReferenceType or IrMutReferenceType;
    }

    public override IrType? VisitDereferenceExpr([NotNull] NovusParser.DereferenceExprContext context)
    {
        var operandType = Visit(context.expression());
        if (operandType == null)
            return IrIntType.I32;

        // Check if it's a pointer or reference type
        if (operandType is IrPointerType ptrType)
        {
            return ptrType.PointeeType;
        }
        else if (operandType is IrReferenceType refType)
        {
            return refType.PointeeType;
        }
        else if (operandType is IrMutReferenceType mutRefType)
        {
            return mutRefType.PointeeType;
        }
        else
        {
            var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
            _diagnostics.ReportError(
                "E0025",
                $"cannot dereference non-pointer/reference type '{TypeToString(operandType)}'",
                location,
                helpTexts: new List<string>
                {
                    "only pointers (*T) and references (&T, &var T) can be dereferenced"
                }
            );
            return IrIntType.I32; // Fallback
        }
    }

    public override IrType? VisitUnaryExpr([NotNull] NovusParser.UnaryExprContext context)
    {
        var op = context.GetChild(0).GetText();

        // Visit operand first
        var operandType = Visit(context.expression());
        if (operandType == null)
            return IrIntType.I32;

        if (op == "!")
        {
            // Logical NOT: requires boolean, numeric, or pointer type
            if (!IsBoolOrNumericOrPointerType(operandType))
            {
                var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0024",
                    $"logical operator '!' requires boolean, numeric, or pointer type, found '{TypeToString(operandType)}'",
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

        // Unknown operator - report error and return operand type as fallback
        var unknownOpLocation = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
        _diagnostics.ReportError(
            ErrorCodes.UnknownOperator,
            $"Unknown unary operator: {op}",
            unknownOpLocation
        );
        return operandType ?? IrIntType.I32;
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

        // Verify it's an lvalue and check mutability
        bool isLvalue = false;
        if (context.expression() is NovusParser.PrimaryExprContext primaryCtx)
        {
            var primaryExpr = primaryCtx.primaryExpression();
            if (primaryExpr is NovusParser.IdentifierExprContext identCtx)
            {
                isLvalue = true;

                // Check if the variable is mutable
                var varName = identCtx.identifier().GetText();
                if (_variables.TryGetValue(varName, out var variable))
                {
                    if (!variable.IsMutable)
                    {
                        var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0384",
                            $"cannot increment immutable variable `{varName}`",
                            location,
                            helpTexts: new List<string>
                            {
                                "help: declare the variable as mutable with 'var mut'"
                            }
                        );
                    }
                }
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

        // Verify it's an lvalue and check mutability
        bool isLvalue = false;
        if (context.expression() is NovusParser.PrimaryExprContext primaryCtx)
        {
            var primaryExpr = primaryCtx.primaryExpression();
            if (primaryExpr is NovusParser.IdentifierExprContext identCtx)
            {
                isLvalue = true;

                // Check if the variable is mutable
                var varName = identCtx.identifier().GetText();
                if (_variables.TryGetValue(varName, out var variable))
                {
                    if (!variable.IsMutable)
                    {
                        var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0384",
                            $"cannot decrement immutable variable `{varName}`",
                            location,
                            helpTexts: new List<string>
                            {
                                "help: declare the variable as mutable with 'var mut'"
                            }
                        );
                    }
                }
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

        // Verify it's an lvalue and check mutability
        bool isLvalue = false;
        var expr = context.expression();
        if (expr is NovusParser.PrimaryExprContext primaryCtx)
        {
            var primaryExpr = primaryCtx.primaryExpression();
            if (primaryExpr is NovusParser.IdentifierExprContext identCtx)
            {
                isLvalue = true;

                // Check if the variable is mutable
                var varName = identCtx.identifier().GetText();
                if (_variables.TryGetValue(varName, out var variable))
                {
                    if (!variable.IsMutable)
                    {
                        var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0384",
                            $"cannot increment immutable variable `{varName}`",
                            location,
                            helpTexts: new List<string>
                            {
                                "help: declare the variable as mutable with 'var mut'"
                            }
                        );
                    }
                }
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

        // Verify it's an lvalue and check mutability
        bool isLvalue = false;
        if (context.expression() is NovusParser.PrimaryExprContext primaryCtx)
        {
            var primaryExpr = primaryCtx.primaryExpression();
            if (primaryExpr is NovusParser.IdentifierExprContext identCtx)
            {
                isLvalue = true;

                // Check if the variable is mutable
                var varName = identCtx.identifier().GetText();
                if (_variables.TryGetValue(varName, out var variable))
                {
                    if (!variable.IsMutable)
                    {
                        var location = SourceLocationHelper.FromContext(context.expression(), _filePath, _sourceLines);
                        _diagnostics.ReportError(
                            "E0384",
                            $"cannot decrement immutable variable `{varName}`",
                            location,
                            helpTexts: new List<string>
                            {
                                "help: declare the variable as mutable with 'var mut'"
                            }
                        );
                    }
                }
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
        var enumType = _symbols.LookupEnum(typeName);
        if (enumType != null)
        {
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
                var structType = _symbols.LookupStruct(typeName);
                if (structType != null)
                {
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
                    var result = funcSymbol.ReturnType != null ? _typeParser.SubstituteGenericTypes(funcSymbol.ReturnType, substitutions) : null;
                    return result;
                }
            }

            // No explicit type args - return the function's return type as-is
            return funcSymbol.ReturnType;
        }

        // Type not found or member doesn't exist
        var errorLocation = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
        if (_symbols.HasStruct(typeName))
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
        if (firstType == null)
        {
            return null;
        }

        int index = 1;
        foreach (var expr in expressions.Skip(1))
        {
            var exprType = Visit(expr);
            if (exprType != null && !TypesCompatible(firstType, exprType))
            {
                _diagnostics.ReportError(
                    "E0029",
                    $"array element type mismatch: expected '{TypeToString(firstType)}' (from first element), found '{TypeToString(exprType!)}'  at index {index}",
                    SourceLocationHelper.FromContext(expr, _filePath, _sourceLines)
                );
            }
            index++;
        }

        return _typeInterner.GetArrayType(firstType, expressions.Length);
    }

    public override IrType? VisitArrayRepeatLiteral([NotNull] NovusParser.ArrayRepeatLiteralContext context)
    {
        // Array repeat literals: [value; count]
        var expressions = context.expression();
        if (expressions.Length != 2)
        {
            var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);
            _diagnostics.ReportError("E0999", "array repeat literal must have exactly 2 expressions", location);
            return null;
        }

        // First expression is the value
        var valueType = Visit(expressions[0]);
        if (valueType == null)
        {
            return null;
        }

        // Second expression is the count (must be compile-time constant integer)
        var countType = Visit(expressions[1]);
        if (countType == null)
        {
            return null;
        }

        // Validate that count is an integer type
        if (countType is not IrIntType)
        {
            var location = SourceLocationHelper.FromContext(expressions[1], _filePath, _sourceLines);
            _diagnostics.ReportError("E0999", "array repeat count must be an integer", location);
            return null;
        }

        // Try to evaluate the count expression if it's a compile-time constant
        if (!TryEvaluateIntegerLiteral(expressions[1], out int arraySize) || arraySize < 0)
        {
            var loc = SourceLocationHelper.FromContext(expressions[1], _filePath, _sourceLines);
            _diagnostics.ReportError("E0999", "array repeat count must be a compile-time constant integer literal (non-negative)", loc);
            return null;
        }

        return _typeInterner.GetArrayType(valueType, arraySize);
    }

    public override IrType? VisitStructLiteral([NotNull] NovusParser.StructLiteralContext context)
    {
        var structName = context.typeName().GetText();

        // Check if struct type exists
        var structTypeLookup = _symbols.LookupStruct(structName);
        if (structTypeLookup == null)
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
            structType = structTypeLookup;
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

            // Validate field value type (set expected type for bidirectional type checking)
            var previousExpectedType = _expectedType;
            _expectedType = field.Type;
            var fieldValueType = Visit(fieldInit.expression());
            _expectedType = previousExpectedType;

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

        // Check for struct update syntax (..base)
        // The grammar now captures this: (COMMA NEWLINE* DOTDOT expression)?
        var spreadExpr = context.expression();
        IrType? spreadType = null;
        if (spreadExpr != null)
        {
            // There's a spread expression (..base)
            spreadType = Visit(spreadExpr);

            if (spreadType != null && !TypesCompatible(structType, spreadType))
            {
                var location = SourceLocationHelper.FromContext(spreadExpr, _filePath, _sourceLines);
                _diagnostics.ReportError(
                    "E0027",
                    $"type mismatch in struct update: expected '{structType.Name}', got '{spreadType.Name}'",
                    location,
                    helpTexts: new List<string>
                    {
                        $"the base expression must be of type '{structType.Name}'"
                    }
                );
            }
        }

        // Check that all fields are initialized (either explicitly or via spread)
        if (spreadType == null)
        {
            // No spread - all fields must be explicitly initialized
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
        }
        // With spread, missing fields are filled from the base expression

        return structType;
    }

    public override IrType? VisitStructArrayInit([NotNull] NovusParser.StructArrayInitContext context)
    {
        // Handle Vec { {10, 20, 30} } syntax
        var structName = context.typeName().GetText();

        // Check if struct type exists
        var structTypeLookup = _symbols.LookupStruct(structName);
        if (structTypeLookup == null)
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
        var arrayLiteralInit = context.arrayLiteralInit();
        foreach (var expr in arrayLiteralInit.expression())
        {
            var elemType = Visit(expr);
            if (elemType == null)
            {
                return null;  // Error already reported
            }
        }

        // Return the struct type
        return structTypeLookup;
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
        // Remove hex prefix ('$' or '0x'/'0X') and underscores
        if (text.StartsWith("0x") || text.StartsWith("0X"))
        {
            text = text[2..].Replace("_", "");
        }
        else
        {
            // Must be '$' prefix
            text = text[1..].Replace("_", "");
        }
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
            // For named types, use our own implementation that includes validation
            NovusParser.NamedTypeContext namedCtx => ParseNamedType(namedCtx),
            // For all other types, delegate to TypeParser for shared parsing logic
            _ => _typeParser.ParseType(context)
        };
    }


    private IrType ParseNamedType(NovusParser.NamedTypeContext context)
    {
        var typeName = context.typeName().GetText();

        // Check if it's a generic type parameter (T, E, etc.)
        if (_genericParams.ContainsKey(typeName))
        {
            return _genericParams[typeName];
        }

        // Check if it's a const generic parameter (N where const N: u32)
        if (_constGenericParams.ContainsKey(typeName))
        {
            return _constGenericParams[typeName];
        }

        // Check if it's a struct type
        var structType = _symbols.LookupStruct(typeName);
        if (structType != null)
        {

            // Handle generic instantiation (e.g., Vec<i32>)
            if (context.genericTypeArgs()?.typeList() != null)
            {
                var typeArgs = new List<IrType>();
                foreach (var typeCtx in context.genericTypeArgs().typeList().type())
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

                // Validate generic constraints only for concrete types
                // Skip validation if type args contain generic type parameters (e.g., HashMap<K, V> in a generic context)
                // Constraints will be validated when the type is fully instantiated with concrete types
                bool hasGenericTypeArgs = typeArgs.Any(t => ContainsGenericType(t));
                if (!hasGenericTypeArgs)
                {
                    var structLocation = SourceLocationHelper.FromToken(context.typeName().Start, _filePath, _sourceLines);
                    if (!ValidateGenericConstraints(structType.WhereClause, structType.GenericParameters, typeArgs, structLocation))
                    {
                        // Error already reported by ValidateGenericConstraints
                        return IrIntType.I32;
                    }
                }

                // NOTE: Even if type arguments contain generics (e.g., *T), we proceed to create a specialized struct
                // This allows Vec<*T> to be distinct from Vec<T>

                // Create cache key: StructName<TypeArg1CacheKey,TypeArg2CacheKey,...>
                var cacheKey = $"{structType.StructName}<{string.Join(",", typeArgs.Select(t => GetTypeCacheKey(t)))}>";

                // Check cache first
                var cached = _symbols.LookupMonomorphizedStruct(cacheKey);
                if (cached != null)
                {
                    return cached;
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

                    // Use SubstituteGenericTypes to handle all cases including nested generics
                    fieldType = _typeParser.SubstituteGenericTypes(fieldType, typeSubstitutions);

                    monomorphizedFields.Add(new IrStructField(origField.Name, fieldType));
                }

                // Create new struct type - this is a monomorphized instance (GenericParameters should be empty)
                // Even if type args contain generics (e.g., Vec<T>), we've instantiated this specific type
                var monomorphizedStruct = new IrStructType(structType.StructName, monomorphizedFields, null, cacheKey, typeArguments: typeArgs);

                // Cache it for future use
                _symbols.RegisterMonomorphizedStruct(cacheKey, monomorphizedStruct);

                return monomorphizedStruct;
            }

            return structType;
        }

        // Check if it's an enum type
        var enumType = _symbols.LookupEnum(typeName);
        if (enumType != null)
        {

            // Handle generic instantiation (e.g., Option<i32>)
            if (context.genericTypeArgs()?.typeList() != null)
            {
                var typeArgs = new List<IrType>();
                foreach (var typeCtx in context.genericTypeArgs().typeList().type())
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

                // Validate generic constraints only for concrete types
                // Skip validation if type args contain generic type parameters (e.g., Option<K> in a generic context)
                // Constraints will be validated when the type is fully instantiated with concrete types
                bool hasGenericTypeArgs = typeArgs.Any(t => ContainsGenericType(t));
                if (!hasGenericTypeArgs)
                {
                    var enumLocation = SourceLocationHelper.FromToken(context.typeName().Start, _filePath, _sourceLines);
                    if (!ValidateGenericConstraints(enumType.WhereClause, enumType.GenericParameters, typeArgs, enumLocation))
                    {
                        // Error already reported by ValidateGenericConstraints
                        return IrIntType.I32;
                    }
                }

                // NOTE: Even if type arguments contain generics (e.g., *T), we proceed to create a specialized enum
                // This allows Option<*T> to be distinct from Option<T>

                // Create cache key: EnumName<TypeArg1CacheKey,TypeArg2CacheKey,...>
                // Use GetTypeCacheKey to handle nested types correctly
                var cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgs.Select(t => GetTypeCacheKey(t)))}>";
                // Check cache first
                var cached = _symbols.LookupMonomorphizedEnum(cacheKey);
                if (cached != null)
                {
                    return cached;
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
                        // Use SubstituteGenericTypes to handle nested generic types (e.g., Vec<T> in Option<Vec<T>>)
                        var substituted = _typeParser.SubstituteGenericTypes(dataType, typeSubstitutions);
                        monomorphizedData.Add(substituted);
                    }
                    monomorphizedVariants.Add(new IrEnumVariant(origVariant.Name, origVariant.Tag, monomorphizedData));
                }

                // Create new enum type - this is a monomorphized instance (GenericParameters should be empty)
                // Even if type args contain generics (e.g., Option<T>), we've instantiated this specific type
                var monomorphizedEnum = new IrEnumType(enumType.EnumName, monomorphizedVariants, null, cacheKey, typeArguments: typeArgs);

                // Cache it for future use
                _symbols.RegisterMonomorphizedEnum(cacheKey, monomorphizedEnum);

                return monomorphizedEnum;
            }

            return enumType;
        }

        // Check if it's a primitive type (bool, i8, u8, void, etc.)
        var primitiveType = GetPrimitiveType(typeName);
        if (primitiveType != null)
        {
            return primitiveType;
        }

        // Unknown type - for extern functions, treat as opaque FFI type (no error)
        // For regular functions, report error
        if (!_parsingExternFunction)
        {
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
        }
        return IrIntType.I32;
    }


    private bool TypesCompatible(IrType expected, IrType actual)
    {
        // Exact match
        if (expected.Equals(actual))
            return true;

        // Automatic coercion: Str/String → *u8
        // This allows string literals in contexts like Option::Some("string") when Option<*u8> is expected
        if (expected is IrPointerType ptrType &&
            ptrType.PointeeType.Equals(IrIntType.U8) &&
            actual is IrStructType structType &&
            (structType.StructName == "Str" || structType.StructName == "String"))
        {
            return true;  // Allow coercion - will be handled by IR builder
        }

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

        // Reference to pointer coercion (allow &T or &var T where *T is expected)
        // This is safe since references are essentially pointers at runtime
        if (expected is IrPointerType expectedPtrForRef)
        {
            if (actual is IrReferenceType refForPtrCoerce &&
                TypesCompatible(expectedPtrForRef.PointeeType, refForPtrCoerce.PointeeType))
            {
                return true;
            }
            if (actual is IrMutReferenceType mutRefForPtrCoerce &&
                TypesCompatible(expectedPtrForRef.PointeeType, mutRefForPtrCoerce.PointeeType))
            {
                return true;
            }
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

            // For non-generic enums with matching names, consider them compatible
            // This handles the case where multiple IrEnumType instances exist for the same enum
            // (e.g., one from import stub creation, another from full registration)
            var expectedGenericCount = expectedEnum.GenericParameters?.Count ?? 0;
            var actualGenericCount = actualEnum.GenericParameters?.Count ?? 0;
            if (expectedGenericCount == 0 && actualGenericCount == 0)
            {
                // Non-generic enum with same name - they're the same type
                return true;
            }

            // For generic enums, check if both are instantiated (have CacheKeys)
            // If both have CacheKeys, compare them directly (handles Option<i32> vs Option<i32>)
            if (expectedEnum.CacheKey != null && actualEnum.CacheKey != null)
            {
                return expectedEnum.CacheKey == actualEnum.CacheKey;
            }

            // If generic param counts match and both are uninstantiated templates, they're the same
            if (expectedGenericCount == actualGenericCount)
            {
                // Both are generic templates with same parameter count - consider compatible
                return true;
            }

            return false;
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

        // Allow float literals to be assigned to fixed-point types
        // This enables clean syntax: var pos: fixed16 = 100.0 instead of 100.0fixed16
        if (expected is IrFixedType && actual is IrFloatType)
        {
            return true;  // Conversion will be handled by IR builder
        }

        // Struct types - allow generic to concrete matching (e.g., Vec<T> can match Vec<i32>)
        if (expected is IrStructType expectedStruct && actual is IrStructType actualStruct)
        {
            // Same struct name
            if (expectedStruct.StructName != actualStruct.StructName)
            {
                return false;
            }

            // For monomorphized structs, compare by cache key (handles Vec<u8> vs Vec<u8>)
            // This is essential because two different IrStructType instances representing the same
            // monomorphized type (e.g., Vec<u8>) won't be reference-equal, but should be considered compatible
            if (expectedStruct.CacheKey != null && actualStruct.CacheKey != null)
            {
                return expectedStruct.CacheKey == actualStruct.CacheKey;
            }

            // Debug: Check if one has CacheKey and the other doesn't
            if (expectedStruct.CacheKey != null || actualStruct.CacheKey != null)
            {

                // Special case: if both are generic (same number of generic parameters),
                // and one has a cache key while the other doesn't, consider them compatible.
                // This handles the case where Allocation<T> in one context has a cache key
                // but in another context doesn't, but they're the same generic template.
                if (expectedStruct.GenericParameters.Count > 0 &&
                    expectedStruct.GenericParameters.Count == actualStruct.GenericParameters.Count)
                {
                    return true;
                }
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

            // Both are non-monomorphized generics - must match exactly (handled by Equals above)
            return false;
        }

        // Allow integer (especially 0) to be used as null pointer
        // This enables: let ptr: *T = 0
        if (expected is IrPointerType && actual is IrIntType)
        {
            return true;
        }

        // Allow array-to-pointer decay: [T; N] → *T
        // This allows passing arrays directly to functions expecting pointers
        // Example: OpenWindowTagList(0, tags) where tags is [TagItem; 8]
        if (expected is IrPointerType expectedPtrForArrayDecay && actual is IrArrayType actualArrayForDecay)
        {
            // Check if element types match
            if (expectedPtrForArrayDecay.PointeeType.Name == actualArrayForDecay.ElementType.Name)
            {
                return true;
            }
        }

        // Allow Str to be implicitly converted to u32 (extracts .ptr field and casts to u32)
        // This is used for TagItem.ti_Data and similar AmigaOS APIs that store pointers as u32
        if (expected is IrIntType expectedIntForPtr && expectedIntForPtr == IrIntType.U32 &&
            actual is IrStructType actualStructForPtr && actualStructForPtr.StructName == "Str")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if we can automatically coerce Str to *u8
    /// This is safe because Str has ptr: *u8 as its first field
    /// </summary>
    private bool CanCoerceStrToU8Ptr(IrType expectedType, IrType actualType)
    {
        // Check if expected is *u8 and actual is Str or String
        if (expectedType is IrPointerType ptrType &&
            ptrType.PointeeType is IrIntType intType &&
            intType == IrIntType.U8 &&
            actualType is IrStructType structType &&
            (structType.StructName == "Str" || structType.StructName == "String"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if we can coerce &[T; N] → Slice<T>
    /// </summary>
    private bool CanCoerceArrayToSlice(IrType expectedType, IrType actualType)
    {
        // Check if expected is Slice<T> and actual is &[T; N]
        if (expectedType is IrStructType sliceStruct &&
            sliceStruct.StructName == "Slice" &&
            actualType is IrReferenceType refType &&
            refType.PointeeType is IrArrayType arrayType)
        {
            // Get the 'ptr' field to extract the element type T from Slice<T>
            var ptrField = sliceStruct.GetField("ptr");
            if (ptrField?.Type is IrPointerType slicePtrType)
            {
                // Verify array element type matches Slice element type
                return TypesCompatible(slicePtrType.PointeeType, arrayType.ElementType);
            }
        }

        return false;
    }

    /// <summary>
    /// Check if we can coerce &[T; N] → &[T] (sized array ref to unsized slice ref)
    /// This allows passing array references to functions expecting slice references
    /// </summary>
    private bool CanCoerceSizedArrayRefToUnsizedSliceRef(IrType expectedType, IrType actualType)
    {
        // Check if expected is &[T] (unsized, length -1) and actual is &[T; N] (sized)
        if (expectedType is IrReferenceType expectedRef &&
            expectedRef.PointeeType is IrArrayType expectedArray &&
            expectedArray.Length == -1 &&
            actualType is IrReferenceType actualRef &&
            actualRef.PointeeType is IrArrayType actualArray &&
            actualArray.Length >= 0)
        {
            // Verify element types match
            return TypesCompatible(expectedArray.ElementType, actualArray.ElementType);
        }

        return false;
    }

    /// <summary>
    /// Check if we can coerce Str → &Str
    /// This allows string literals to be passed directly to functions expecting &Str
    /// </summary>
    private bool CanCoerceStrToStrRef(IrType expectedType, IrType actualType)
    {
        // Check if expected is &Str and actual is Str
        if (expectedType is IrReferenceType refType &&
            refType.PointeeType is IrStructType expectedStruct &&
            expectedStruct.StructName == "Str" &&
            actualType is IrStructType actualStruct &&
            actualStruct.StructName == "Str")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if we can coerce &T → *T
    /// This allows passing references to extern functions that expect pointers
    /// </summary>
    private bool CanCoerceReferenceToPointer(IrType expectedType, IrType actualType)
    {
        // Check if expected is *T and actual is &T (or &var T)
        if (expectedType is IrPointerType ptrType &&
            (actualType is IrReferenceType refType || actualType is IrMutReferenceType mutRefType))
        {
            var actualPointeeType = actualType is IrReferenceType r ? r.PointeeType :
                                   actualType is IrMutReferenceType m ? m.PointeeType : null;

            if (actualPointeeType != null)
            {
                // Check if the pointee types are compatible
                return TypesCompatible(ptrType.PointeeType, actualPointeeType);
            }
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

    private bool IsBoolOrNumericOrPointerType(IrType type)
    {
        return type is IrBoolType || IsNumericType(type) || type is IrPointerType || type is IrReferenceType || type is IrMutReferenceType;
    }

    /// <summary>
    /// Check if a type supports a binary operator, either through built-in support
    /// (for primitive types) or through trait implementation (for struct/enum types).
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <param name="operatorSymbol">The operator symbol (e.g., "+", "-", "==", "&lt;")</param>
    /// <param name="traitName">Output: the trait name if trait-based, null otherwise</param>
    /// <param name="methodName">Output: the method name if trait-based, null otherwise</param>
    /// <returns>True if the type supports the operator</returns>
    private bool TypeSupportsOperator(IrType type, string operatorSymbol, out string? traitName, out string? methodName)
    {
        traitName = null;
        methodName = null;

        // Primitive numeric types use built-in operators for arithmetic and comparison
        if (IsNumericType(type))
            return true;

        // Bool uses built-in operators for == and !=
        if (type is IrBoolType && (operatorSymbol == "==" || operatorSymbol == "!="))
            return true;

        // Pointers use built-in operators for arithmetic and comparison
        if (type is IrPointerType)
            return true;

        // Map operator symbol to trait and method names
        (traitName, methodName) = operatorSymbol switch
        {
            "+" => ("Add", "add"),
            "-" => ("Sub", "sub"),
            "*" => ("Mul", "mul"),
            "/" => ("Div", "div"),
            "%" => ("Rem", "rem"),
            "==" or "!=" => ("Eq", "eq"),
            "<" => ("PartialOrd", "lt"),
            "<=" => ("PartialOrd", "le"),
            ">" => ("PartialOrd", "gt"),
            ">=" => ("PartialOrd", "ge"),
            _ => (null, null)
        };

        if (traitName == null)
            return false;

        // For generic type parameters, check if they have a trait bound from the where clause
        if (type is IrGenericType genericType)
        {
            var bounds = GetBoundsForGenericParameter(genericType.ParameterName);
            var requiredTrait = traitName; // Copy to local for lambda capture
            if (bounds != null && bounds.Any(b => b.TraitName == requiredTrait))
                return true;
        }

        // Get the base type name for trait lookup
        string typeName = GetBaseTypeNameForTraitLookup(type);
        if (string.IsNullOrEmpty(typeName))
            return false;

        // Check if type implements the trait
        return _traitResolver.HasTraitImpl(typeName, traitName);
    }

    /// <summary>
    /// Get the base type name for trait implementation lookup.
    /// Handles struct types, enum types, and generic types.
    /// </summary>
    private string GetBaseTypeNameForTraitLookup(IrType type)
    {
        return type switch
        {
            IrStructType st => st.StructName,
            IrEnumType et => et.EnumName,
            _ => type.Name
        };
    }

    /// <summary>
    /// Check if a type supports the index operator via Index trait implementation.
    /// Returns the return type (T in Index&lt;I, T&gt;) if the type implements Index,
    /// or null if no implementation is found.
    /// </summary>
    private IrType? TypeSupportsIndexOperator(IrType baseType, IrType indexType)
    {
        // Get the base type name for trait lookup
        string typeName = GetBaseTypeNameForTraitLookup(baseType);
        if (string.IsNullOrEmpty(typeName))
            return null;

        // Search for Index trait implementation
        // The key format is "TypeName::Index" (without type args in the key)
        foreach (var kvp in _traitResolver.GetAllImpls())
        {
            var implInfo = kvp.Value;
            if (implInfo.TypeName == typeName && implInfo.TraitName == "Index")
            {
                // Found Index impl - check if the index type matches
                // TraitTypeArgs should be [I, T] where I is index type and T is return type
                if (implInfo.TraitTypeArgs.Count >= 2)
                {
                    var expectedIndexType = implInfo.TraitTypeArgs[0];
                    var returnType = implInfo.TraitTypeArgs[1];

                    // Check if the index type is compatible
                    if (TypeToString(indexType) == TypeToString(expectedIndexType))
                    {
                        return returnType;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Check if a type supports the index mutation operator via IndexMut trait implementation.
    /// Returns the value type (T in IndexMut&lt;I, T&gt;) if the type implements IndexMut,
    /// or null if no implementation is found.
    /// </summary>
    private IrType? TypeSupportsIndexMutOperator(IrType baseType, IrType indexType)
    {
        // Get the base type name for trait lookup
        string typeName = GetBaseTypeNameForTraitLookup(baseType);
        if (string.IsNullOrEmpty(typeName))
            return null;

        // Search for IndexMut trait implementation
        foreach (var kvp in _traitResolver.GetAllImpls())
        {
            var implInfo = kvp.Value;
            if (implInfo.TypeName == typeName && implInfo.TraitName == "IndexMut")
            {
                // Found IndexMut impl - check if the index type matches
                // TraitTypeArgs should be [I, T] where I is index type and T is value type
                if (implInfo.TraitTypeArgs.Count >= 2)
                {
                    var expectedIndexType = implInfo.TraitTypeArgs[0];
                    var valueType = implInfo.TraitTypeArgs[1];

                    // Check if the index type is compatible
                    if (TypeToString(indexType) == TypeToString(expectedIndexType))
                    {
                        return valueType;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Check if a string looks like a generic type parameter name (e.g., T, E, K, V, Item)
    /// Generic parameters are typically single uppercase letters, but can be longer like "Item"
    /// They are NOT primitive types, struct names, or enum names
    /// </summary>
    private bool IsGenericParameterName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // If it's a primitive type name, it's not a generic parameter
        if (GetPrimitiveType(name) != null)
            return false;

        // If it's a known struct or enum, it's not a generic parameter
        if (_symbols.LookupStruct(name) != null || _symbols.LookupEnum(name) != null)
            return false;

        // Common generic parameter names: single uppercase letters like T, E, K, V
        // or longer names like Item, Self (though Self is special)
        // Typically start with uppercase
        return char.IsUpper(name[0]) && !name.Contains("::") && !name.Contains("<");
    }

    private IrType? GetPrimitiveType(string typeName)
    {
        return typeName switch
        {
            "bool" => IrBoolType.Instance,
            "void" => IrVoidType.Instance,
            "i8" => IrIntType.I8,
            "i16" => IrIntType.I16,
            "i32" => IrIntType.I32,
            "i64" => IrIntType.I64,
            "u8" => IrIntType.U8,
            "u16" => IrIntType.U16,
            "u32" => IrIntType.U32,
            "u64" => IrIntType.U64,
            _ => null
        };
    }

    // Control flow tracking methods for move analysis - delegate to BorrowChecker
    private void EnterBranch(ControlFlowKind kind) => _borrowChecker.EnterBranch(kind);
    private Dictionary<int, MoveInfo> ExitBranch() => _borrowChecker.ExitBranch();
    private void RecordMove(int variableId, MoveInfo moveInfo) => _borrowChecker.RecordMove(variableId, moveInfo);

    /// <summary>
    /// Records that a specific field of a struct has been moved.
    /// Supports partial moves where only some fields are moved.
    /// </summary>
    private void RecordFieldMove(int variableId, string variableName, string fieldName,
                                 SourceLocation moveLocation, string reason)
        => _borrowChecker.RecordFieldMove(variableId, variableName, fieldName, moveLocation, reason);

    private void MergeBranchMoves(params Dictionary<int, MoveInfo>?[] branchMoves)
        => _borrowChecker.MergeBranchMoves(branchMoves);

    private void MergeBranchMoves(List<Dictionary<int, MoveInfo>> branchMoves)
        => _borrowChecker.MergeBranchMoves(branchMoves);

    /// <summary>
    /// Emit drop calls for all variables in the given scope that need dropping.
    /// Variables are dropped in reverse order of declaration (LIFO).
    /// </summary>
    private void EmitDropCallsForScope(ScopeDropInfo scopeInfo)
    {
        // Drop in reverse order of declaration (LIFO)
        foreach (var dropInfo in scopeInfo.VariablesToDrop.AsEnumerable().Reverse())
        {
            if (!dropInfo.WasMoved)
            {
                // Variable was not moved - emit full drop call
                EmitDropCall(dropInfo);
            }
            else if (dropInfo.MovedFields != null && dropInfo.MovedFields.Count > 0)
            {
                // Partial move - drop non-moved fields
                EmitPartialDrop(dropInfo);
            }
            // If WasMoved is true and MovedFields is null, the entire value was moved - no drop needed
        }
    }

    /// <summary>
    /// Emit a drop call for a variable that implements the Drop trait.
    /// NOTE: This is a placeholder - actual drop call insertion happens in IrBuilder.
    /// </summary>
    private void EmitDropCall(DropInfo dropInfo)
    {
        // Drop call emission is handled by IrBuilder.GenerateDropCall().
        // SemanticAnalyzer tracks drop info for ownership analysis;
        // IrBuilder uses this to generate actual Drop trait method calls.
    }

    /// <summary>
    /// Emit drop calls for non-moved fields in a partially moved struct.
    /// NOTE: This is a placeholder - actual drop call insertion happens in IrBuilder.
    /// </summary>
    private void EmitPartialDrop(DropInfo dropInfo)
    {
        // Partial drop handling is performed by IrBuilder.
        // SemanticAnalyzer tracks MovedFields in DropInfo;
        // IrBuilder iterates non-moved fields and generates individual Drop calls.
    }

    /// <summary>
    /// Determines if a type implements Copy semantics (can be copied instead of moved).
    /// Delegates to BorrowChecker.
    /// </summary>
    private bool IsCopyType(IrType type) => _borrowChecker.IsCopyType(type);

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
        else if (type is IrStructType structType)
        {
            if (structType.GenericParameters.Count > 0)
            {
                // Still generic - include parameter names
                return $"{structType.StructName}<{string.Join(",", structType.GenericParameters)}>";
            }
            else if (structType.CacheKey != null)
            {
                // Monomorphized struct - use stored cache key (e.g., "Vec<u8>")
                return structType.CacheKey;
            }
            else
            {
                // Non-generic struct - just use the name
                return structType.StructName;
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
            return $"&var {TypeToString(mutRefType.PointeeType)}";
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
    /// Check if a type implements a specific trait.
    /// Delegates to TraitResolver.
    /// </summary>
    private bool TypeImplementsTrait(IrType type, string traitName, List<IrType> traitTypeArgs)
        => _traitResolver.TypeImplementsTrait(type, traitName, traitTypeArgs);

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
            _ => type.Name
        };
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
    /// Checks if a From<SourceType> trait implementation exists for the target type.
    /// This is used to enable automatic type conversion in Result::Err and similar contexts.
    /// </summary>
    /// <param name="sourceType">The type being converted from</param>
    /// <param name="targetType">The type being converted to</param>
    /// <returns>True if From<SourceType> is implemented for targetType</returns>
    private bool CanConvertViaFromTrait(IrType sourceType, IrType targetType)
        => _traitResolver.CanConvertViaFromTrait(sourceType, targetType);

    /// <summary>
    /// ITypeParsingContext implementation for SemanticAnalyzer.
    /// Provides access to symbol lookups and error reporting.
    /// </summary>
    private class SemanticAnalyzerTypeContext : ITypeParsingContext
    {
        private readonly SemanticAnalyzer _analyzer;

        public SemanticAnalyzerTypeContext(SemanticAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        // Lookups
        public IrType? LookupGenericParameter(string name)
        {
            return _analyzer._genericParams.ContainsKey(name) ? _analyzer._genericParams[name] : null;
        }

        public IrConstGenericParam? LookupConstGenericParameter(string name)
        {
            return _analyzer._symbols.LookupConstGenericParameter(name);
        }

        public IrStructType? LookupStruct(string name)
        {
            return _analyzer._symbols.LookupStruct(name);
        }

        public IrEnumType? LookupEnum(string name)
        {
            return _analyzer._symbols.LookupEnum(name);
        }

        public IrStructType? LookupMonomorphizedStruct(string cacheKey)
        {
            return _analyzer._symbols.LookupMonomorphizedStruct(cacheKey);
        }

        public IrEnumType? LookupMonomorphizedEnum(string cacheKey)
        {
            return _analyzer._symbols.LookupMonomorphizedEnum(cacheKey);
        }

        // Registration
        public void RegisterMonomorphizedStruct(string key, IrStructType type)
        {
            _analyzer._symbols.RegisterMonomorphizedStruct(key, type);
        }

        public void RegisterMonomorphizedEnum(string key, IrEnumType type)
        {
            _analyzer._symbols.RegisterMonomorphizedEnum(key, type);
        }

        // Finalization (SemanticAnalyzer doesn't need to add to module, just no-op)
        public void FinalizeMonomorphizedStruct(IrStructType type)
        {
            // SemanticAnalyzer doesn't build a module, so nothing to do here
        }

        public void FinalizeMonomorphizedEnum(IrEnumType type)
        {
            // SemanticAnalyzer doesn't build a module, so nothing to do here
        }

        // Type interning
        public IrType GetReferenceType(IrType pointeeType)
        {
            return _analyzer._typeInterner.GetReferenceType(pointeeType);
        }

        public IrType GetMutReferenceType(IrType pointeeType)
        {
            return _analyzer._typeInterner.GetMutReferenceType(pointeeType);
        }

        public IrType GetPointerType(IrType pointeeType)
        {
            return _analyzer._typeInterner.GetPointerType(pointeeType);
        }

        public IrType GetArrayType(IrType elementType, long length)
        {
            return _analyzer._typeInterner.GetArrayType(elementType, (int)length);
        }

        public IrType GetFunctionPointerType(List<IrType> paramTypes, IrType returnType)
        {
            return _analyzer._typeInterner.GetFunctionPointerType(paramTypes, returnType);
        }

        public IrType GetTupleType(List<IrType> elementTypes)
        {
            return _analyzer._typeInterner.GetTupleType(elementTypes);
        }

        public IrType GetClosureType(List<IrType> paramTypes, IrType returnType)
        {
            return _analyzer._typeInterner.GetClosureType(paramTypes, returnType);
        }

        // Current state (SemanticAnalyzer doesn't track these)
        public IrType? CurrentSelfType => null;
        public Dictionary<string, IrType>? CurrentTypeSubstitutions => null;

        // Constant values
        public Dictionary<string, (IrType Type, object Value)> GetConstantValues()
        {
            // Convert ConstantSymbol dictionary to the expected format
            return _analyzer._symbols.GetLocalConstants()
                .ToDictionary(kvp => kvp.Key, kvp => (kvp.Value.Type, kvp.Value.Value));
        }

        // Extern function parsing state
        public bool IsParsingExternFunction => _analyzer._parsingExternFunction;

        // Error reporting
        public Action<string>? ErrorReporter => (msg) =>
        {
            // SemanticAnalyzer uses diagnostics, but we need a location
            // For now, report with a generic error code and empty location
            // The TypeParser will typically be called from contexts where we have proper location
            _analyzer._diagnostics.ReportError(
                "E0020",
                msg,
                new SourceLocation(_analyzer._filePath, 0, 0, 0, "")
            );
        };
    }

    // TraitImplInfo is now defined in TraitResolver.cs

    /// <summary>
    /// Attempts to evaluate a compile-time integer literal from an expression context.
    /// Supports decimal, hexadecimal ($), and binary (%) literals with optional type suffixes.
    /// </summary>
    /// <param name="exprCtx">The expression context to evaluate</param>
    /// <param name="value">The evaluated integer value</param>
    /// <returns>True if evaluation succeeded, false otherwise</returns>
    private bool TryEvaluateIntegerLiteral(NovusParser.ExpressionContext exprCtx, out int value)
    {
        value = 0;

        // Navigate through the expression hierarchy
        // ExpressionContext -> PrimaryExprContext -> PrimaryExpressionContext
        if (exprCtx is not NovusParser.PrimaryExprContext primaryExpr)
        {
            return false;
        }

        var innerExpr = primaryExpr.primaryExpression();
        if (innerExpr == null)
        {
            return false;
        }

        // Get the full text of the literal (includes optional minus sign from grammar: '-'? INTEGER_LITERAL)
        string text = innerExpr.GetText();
        bool isNegative = text.StartsWith('-');
        if (isNegative)
        {
            text = text[1..]; // Remove the minus sign for parsing
        }

        // Remove underscores (used as digit separators)
        text = text.Replace("_", "");

        // Remove type suffix if present
        if (text.EndsWith("u8") || text.EndsWith("i8"))
        {
            text = text[..^2];
        }
        else if (text.EndsWith("u16") || text.EndsWith("i16") ||
                 text.EndsWith("u32") || text.EndsWith("i32") ||
                 text.EndsWith("u64") || text.EndsWith("i64"))
        {
            text = text[..^3];
        }

        int parsedValue;
        bool success;

        // Handle different literal types based on prefix
        if (innerExpr is NovusParser.HexLiteralContext)
        {
            // Hexadecimal literal: $FF, $DEADBEEF
            if (!text.StartsWith('$'))
            {
                return false;
            }
            text = text[1..]; // Remove $ prefix
            success = int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out parsedValue);
        }
        else if (innerExpr is NovusParser.BinaryLiteralContext)
        {
            // Binary literal: %1010, %11110000
            if (!text.StartsWith('%'))
            {
                return false;
            }
            text = text[1..]; // Remove % prefix
            try
            {
                parsedValue = Convert.ToInt32(text, 2);
                success = true;
            }
            catch
            {
                return false;
            }
        }
        else if (innerExpr is NovusParser.IntegerLiteralContext)
        {
            // Decimal literal: 42, 1000
            success = int.TryParse(text, out parsedValue);
        }
        else
        {
            // Not a supported literal type
            return false;
        }

        if (!success)
        {
            return false;
        }

        value = isNegative ? -parsedValue : parsedValue;
        return true;
    }

    /// <summary>
    /// Parses return type from a function declaration context.
    /// This helper consolidates the repeated ternary pattern that appears 3+ times in SemanticAnalyzer.
    ///
    /// If the context has a type annotation, parses it. Otherwise returns void.
    /// </summary>
    /// <param name="typeContext">The type context from the parse tree (may be null)</param>
    /// <returns>The parsed return type, or IrVoidType.Instance if no type specified</returns>
    private IrType ParseReturnType(NovusParser.TypeContext? typeContext)
    {
        return typeContext != null ? ParseType(typeContext) : IrVoidType.Instance;
    }

    /// <summary>
    /// Extracts a variable name from an expression if it's a simple identifier reference
    /// Returns null for complex expressions like field access, array indexing, etc.
    /// </summary>
    private string? ExtractVariableName(ParserRuleContext expr)
    {
        if (expr is NovusParser.IdentifierExprContext identExpr)
        {
            // Simple identifier like "x" or "formatter"
            var identifierCtx = identExpr.identifier();
            if (identifierCtx.IDENTIFIER().Length == 1)
            {
                return identifierCtx.IDENTIFIER(0).GetText();
            }
        }
        else if (expr is NovusParser.PrimaryExprContext primaryCtx)
        {
            // Unwrap primary expressions
            return ExtractVariableName((primaryCtx.GetChild(0) as ParserRuleContext)!);
        }
        else if (expr is NovusParser.MemberAccessExprContext memberCtx)
        {
            // For member access like "obj.field", extract the base object name
            return ExtractVariableName(memberCtx.expression());
        }

        return null;
    }

    /// <summary>
    /// Extracts field name from member access expression like "obj.field"
    /// Returns null if not a member access or if it's a method call
    /// </summary>
    private string? ExtractFieldName(ParserRuleContext expr)
    {
        if (expr is NovusParser.MemberAccessExprContext memberCtx)
        {
            // Check if this is a field access (not a method call)
            // Member access has the form: expression '.' IDENTIFIER
            var fieldName = memberCtx.IDENTIFIER()?.GetText();
            return fieldName;
        }
        else if (expr is NovusParser.PrimaryExprContext primaryCtx)
        {
            // Unwrap primary expressions
            return ExtractFieldName((primaryCtx.GetChild(0) as ParserRuleContext)!);
        }

        return null;
    }

    // ========================================
    // Macro-like Expression Visitors
    // ========================================

    /// <summary>
    /// Type-check matches!(expr, pattern) - evaluates to bool
    /// </summary>
    public override IrType? VisitMatchesExpr([NotNull] NovusParser.MatchesExprContext context)
    {
        var location = SourceLocationHelper.FromContext(context, _filePath, _sourceLines);

        // Analyze the expression being matched
        var exprType = Visit(context.expression());
        if (exprType == null)
        {
            return null; // Error already reported
        }

        // Auto-dereference pointer and reference types for matching
        var actualExprType = exprType;
        if (exprType is IrPointerType ptrType)
        {
            actualExprType = ptrType.PointeeType;
        }
        else if (exprType is IrReferenceType refType)
        {
            actualExprType = refType.PointeeType;
        }
        else if (exprType is IrMutReferenceType mutRefType)
        {
            actualExprType = mutRefType.PointeeType;
        }

        // Validate pattern is compatible with expression type
        var pattern = context.pattern();
        if (pattern != null)
        {
            ValidateMatchesPattern(pattern, actualExprType, location);
        }

        // matches! always returns bool
        return IrBoolType.Instance;
    }

    /// <summary>
    /// Validate that a pattern in matches!(expr, pattern) is compatible with the expression type.
    /// </summary>
    private void ValidateMatchesPattern(NovusParser.PatternContext pattern, IrType exprType, SourceLocation location)
    {
        // Handle variant patterns like Some(x) or None
        if (pattern is NovusParser.VariantPatternContext variantPattern)
        {
            var variantName = variantPattern.variantName()?.GetText();

            if (variantName != null)
            {
                // Check if expression type is an enum
                if (exprType is not IrEnumType enumType)
                {
                    _diagnostics.ReportError(
                        "E0022",
                        $"cannot use variant pattern '{variantName}' on non-enum type '{exprType}'",
                        location
                    );
                    return;
                }

                // Look up the enum and check if the variant exists
                var actualEnum = !string.IsNullOrEmpty(enumType.CacheKey)
                    ? _symbols.LookupMonomorphizedEnum(enumType.CacheKey) ?? enumType
                    : _symbols.LookupEnum(enumType.EnumName) ?? enumType;

                // Extract just the variant name (e.g., "Some" from "Option::Some")
                var simpleVariantName = variantName.Contains("::")
                    ? variantName.Split("::").Last()
                    : variantName;

                if (!actualEnum.Variants.Any(v => v.Name == simpleVariantName))
                {
                    _diagnostics.ReportError(
                        "E0023",
                        $"enum '{enumType.EnumName}' has no variant named '{simpleVariantName}'",
                        location
                    );
                }
            }
        }
        // Wildcard patterns are always valid
        // Literal patterns (integers, strings) would need type checking against exprType
        // For now, we trust the pattern is valid for other pattern types
    }

    /// <summary>
    /// Type-check dbg!(expr) - returns the same type as expr (passes through)
    /// </summary>
    public override IrType? VisitDbgExpr([NotNull] NovusParser.DbgExprContext context)
    {
        // Analyze the expression
        var exprType = Visit(context.expression());

        // dbg! returns the same type as the input expression (it's pass-through)
        return exprType;
    }

    /// <summary>
    /// Type-check unreachable!() - returns Never type (diverges)
    /// </summary>
    public override IrType? VisitUnreachableExpr([NotNull] NovusParser.UnreachableExprContext context)
    {
        // unreachable! diverges - it never returns
        // Return IrNeverType which is the "never" type (!)
        return IrNeverType.Instance;
    }

    /// <summary>
    /// Type-check closure expressions |x: i32| -> i32 { x + 1 }
    /// Closures create a new scope for their parameters and analyze the body in that scope.
    /// </summary>
    public override IrType? VisitClosureExpr([NotNull] NovusParser.ClosureExprContext context)
    {
        var closureExpr = context.closureExpression();
        if (closureExpr == null)
        {
            return null;
        }

        // Save current variables scope - closures introduce a new scope for parameters
        var savedVariables = new Dictionary<string, VariableSymbol>(_variables);

        // Parse closure parameters and add them to scope
        var paramTypes = new List<IrType>();
        if (closureExpr.closureParameterList() != null)
        {
            foreach (var paramCtx in closureExpr.closureParameterList().closureParameter())
            {
                if (paramCtx is NovusParser.TypedClosureParamContext typedParam)
                {
                    var name = typedParam.IDENTIFIER().GetText();
                    var type = ParseType(typedParam.type());
                    var location = SourceLocationHelper.FromToken(typedParam.IDENTIFIER().Symbol, _filePath, _sourceLines);

                    // Add parameter to variables scope
                    _variables[name] = new VariableSymbol(name, type, IsMutable: false, location, Id: _nextVariableId++);
                    paramTypes.Add(type);
                }
                else if (paramCtx is NovusParser.MutableCaptureParamContext)
                {
                    // Mutable captures |mut x| - these are capture specifications, not new parameters
                    // They reference existing variables from the outer scope
                }
                else if (paramCtx is NovusParser.ReferenceCaptureParamContext)
                {
                    // Reference captures |&x| - these are capture specifications, not new parameters
                    // They reference existing variables from the outer scope
                }
            }
        }

        // Parse return type (default to void if not specified)
        IrType returnType = IrVoidType.Instance;
        if (closureExpr.type() != null)
        {
            returnType = ParseType(closureExpr.type());
        }

        // Analyze the closure body
        Visit(closureExpr.block());

        // Restore variables scope
        _variables.Clear();
        foreach (var kv in savedVariables)
        {
            _variables[kv.Key] = kv.Value;
        }

        // Return the closure type
        return _typeInterner.GetClosureType(paramTypes, returnType);
    }
}

// Symbol table classes
public record FunctionSymbol(
    string Name,
    IrType ReturnType,
    List<ParameterSymbol> Parameters,
    SourceLocation Location,
    bool IsExtern = false,
    List<string>? GenericParameters = null,  // Type-level generic parameters (e.g., ["T"] for impl<T> Option<T>)
    AttributeCollection? Attributes = null,  // Function attributes (@inline, @test, etc.)
    bool IsVariadic = false,  // true if function accepts variable number of arguments (...)
    List<string>? MethodGenericParameters = null,  // Method-level generic parameters (e.g., ["E"] for fn ok_or<E>)
    IrWhereClause? WhereClause = null,  // Generic type constraints (e.g., where T: Sortable)
    bool IsConstFn = false  // true if function is declared with 'const fn'
);
public record ParameterSymbol(string Name, IrType Type, SourceLocation Location, bool IsVariadic = false, bool IsConsuming = false);
public record VariableSymbol(
    string Name,
    IrType Type,
    bool IsMutable,
    SourceLocation Location,
    AttributeCollection? Attributes = null,  // Variable attributes
    int Id = 0  // Unique ID to distinguish shadowed variables
);

// DropInfo and ScopeDropInfo are now defined in BorrowChecker.cs

public record ConstantSymbol(
    string Name,
    IrType Type,
    object Value,
    SourceLocation Location,
    AttributeCollection? Attributes = null,  // Constant attributes
    bool isDeferredConstFn = false  // true if this constant contains const fn calls that need deferred evaluation
);
