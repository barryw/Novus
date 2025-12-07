using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Novus.Frontend.Generics;

namespace Novus.Frontend;

/// <summary>
/// Builds IR from the parsed AST using the visitor pattern.
/// This class is split across multiple partial class files for maintainability.
/// </summary>
public partial class IrBuilder : NovusParserBaseVisitor<object?>
{
    private readonly IrModule _module = new();
    private IrFunction? _currentFunction;
    private IrBasicBlock? _currentBlock;
    private int _tempCounter = 0;
    private int _labelCounter = 0;
    private int _stringCounter = 0;  // Counter for string literal labels

    // Track defer blocks registered in the current scope (for scope-specific cleanup)
    // Each element in the stack represents a scope; the list contains defers registered in that scope
    private Stack<List<IrBasicBlock>> _scopeDeferStack = new();

    // Track emitted defer blocks to prevent double-free bugs
    // A defer block can be registered at both function and scope level, but should only be emitted once
    private readonly HashSet<IrBasicBlock> _emittedDeferBlocks = new();

    // Drop tracking for automatic resource cleanup (RAII)
    // Each scope tracks which local variables need drop calls when the scope exits
    private readonly Stack<List<string>> _scopeDropStack = new(); // Stack of variable names per scope
    private readonly Dictionary<string, bool> _movedVariables = new(); // Track which variables have been moved

    private int _staticVarCounter = 0;  // Counter for auto-generated static variables
    private readonly Stack<string> _loopExitLabels = new(); // Track loop exit labels for break
    private readonly Stack<string> _loopContinueLabels = new(); // Track loop continue labels for continue
    // For labeled loops: maps label name to (exitLabel, continueLabel)
    private readonly Dictionary<string, (string ExitLabel, string ContinueLabel)> _labeledLoops = new();
    private readonly Dictionary<string, IrLocalVariable> _localVariables = new(); // Track local variables in current function

    // Track which temporaries came from IrIndexAccess for optimized member access
    // Key: temp variable name (e.g., "%t59"), Value: (array, index, elementType)
    private readonly Dictionary<string, (IrValue Array, IrValue Index, IrType ElementType)> _indexAccessTemps = new();

    // Unified symbol table for types, functions, and constants
    private readonly SymbolTable _symbols = new();

    // Generic templates are stored here rather than in SymbolTable because:
    // 1. They capture ANTLR parse contexts (NovusParser.FunctionDeclarationContext) which are frontend-specific
    // 2. They capture the constants dictionary at template creation time for proper scoping
    // 3. SymbolTable is meant for resolved types/symbols, not parse-time templates
    // Future consideration: Create a GenericTemplateRegistry abstraction if needed for multi-module support
    //
    // Store generic method templates for later instantiation
    // Key: "TypeName::methodName", Value: (genericParams, context, constants)
    // The constants dictionary captures the constants visible when the template was created
    private readonly Dictionary<string, (List<string> GenericParams, NovusParser.FunctionDeclarationContext Context, Dictionary<string, (IrType Type, object Value)> Constants)> _genericMethodTemplates = new();

    // Store generic function templates for later instantiation (standalone functions, not methods)
    // Key: function name (e.g., "identity"), Value: (genericParams, context, constants)
    private readonly Dictionary<string, (List<string> GenericParams, NovusParser.FunctionDeclarationContext Context, Dictionary<string, (IrType Type, object Value)> Constants)> _genericFunctionTemplates = new();

    // Track which monomorphized methods have been generated
    // Key: "TypeName<ConcreteType>::methodName" (e.g., "Vec<i32>::push")
    private readonly HashSet<string> _instantiatedMethods = new();

    // Track which generic functions have been instantiated with which types
    // Key: "functionName<ConcreteType1,ConcreteType2>" (e.g., "identity<i32>")
    private readonly HashSet<string> _instantiatedGenericFunctions = new();

    // Generic instantiation subsystem - handles monomorphization of generic types and methods
    // This encapsulates all the logic for:
    // - Storing generic templates (methods and functions)
    // - Tracking instantiated methods/functions (cache to avoid duplicates)
    // - Type substitution and inference
    // - Building instantiated function bodies
    private readonly IGenericInstantiator _genericInstantiator;

    private IrType? _expectedType = null; // Expected type for bidirectional type checking
    public readonly List<IrStringLiteral> StringLiterals = new(); // Track all string literals for data section
    private string _stdLibPath = "std"; // Path to standard library
    private string? _inputFilePath = null; // Path to the file being compiled
    private readonly bool _skipAutoImports; // Skip auto-importing core module (for tests)
    private readonly List<string> _importedModulePaths = new(); // Track imported module file paths for linking
    private readonly HashSet<string> _processedModules = new(); // Track which modules we've already fully processed (prevent re-processing)
    private readonly CircularImportDetector _circularImportDetector; // Detect circular import dependencies
    private readonly TypeInterner _typeInterner = new(); // Type interning for efficient type equality

    // Track active type substitutions during generic method instantiation
    // Key: generic param name (e.g., "T"), Value: concrete type (e.g., i32)
    private Dictionary<string, IrType>? _currentTypeSubstitutions = null;

    // Track the implementing type when processing impl blocks for Self type resolution
    // Example: impl From<DosError> for NovusError { ... } -> _currentSelfType = NovusError
    private IrType? _currentSelfType = null;

    // Type parser for unified type parsing logic
    private readonly TypeParser _typeParser;

    // Diagnostic reporting
    private readonly DiagnosticBag _diagnostics = new();
    private string[] _sourceLines = Array.Empty<string>();

    // Statement-level source location tracking for debug symbols
    // Set at the start of each statement and propagated to IR instructions
    private SourceLocation? _currentStatementLocation = null;

    // Track pending function attributes from moduleAttribute that should be applied to the next function.
    // This handles the case where #[export] appears at module level before a function declaration.
    // The grammar parses #[export] as a moduleAttribute when it appears before `pub fn`,
    // so we need to forward these attributes to the appropriate function.
    private readonly List<string> _pendingFunctionAttributes = new();

    /// <summary>
    /// Emit an instruction with the current statement's source location attached.
    /// This enables statement-level debug symbols for precise crash location reporting.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if called outside of a function body (when _currentBlock is null).
    /// </exception>
    private void Emit(IrInstruction instruction)
    {
        if (_currentStatementLocation != null)
        {
            instruction.Location = _currentStatementLocation;
        }
        GetCurrentBlock().AddInstruction(instruction);
    }

    /// <summary>
    /// Gets the current basic block, throwing a clear error if not inside a function body.
    /// Use this instead of _currentBlock! to get better error messages.
    /// </summary>
    private IrBasicBlock GetCurrentBlock()
    {
        if (_currentBlock == null)
        {
            throw new InvalidOperationException(
                "IR emission attempted outside of a function body. " +
                "Ensure you're inside a function declaration before emitting instructions.");
        }
        return _currentBlock;
    }

    /// <summary>
    /// Gets the current function, throwing a clear error if not inside a function.
    /// Use this instead of _currentFunction! to get better error messages.
    /// </summary>
    private IrFunction GetCurrentFunction()
    {
        if (_currentFunction == null)
        {
            throw new InvalidOperationException(
                "Operation attempted outside of a function. " +
                "Ensure you're inside a function declaration.");
        }
        return _currentFunction;
    }

    /// <summary>
    /// Nested class that implements ITypeParsingContext for IrBuilder
    /// </summary>
    private class IrBuilderTypeContext : ITypeParsingContext
    {
        private readonly IrBuilder _builder;

        public IrBuilderTypeContext(IrBuilder builder)
        {
            _builder = builder;
        }

        // Lookups
        public IrType? LookupGenericParameter(string name) => _builder._symbols.LookupGenericParameter(name);
        public IrStructType? LookupStruct(string name) => _builder._symbols.LookupStruct(name);
        public IrEnumType? LookupEnum(string name) => _builder._symbols.LookupEnum(name);
        public IrStructType? LookupMonomorphizedStruct(string cacheKey) => _builder._symbols.LookupMonomorphizedStruct(cacheKey);
        public IrEnumType? LookupMonomorphizedEnum(string cacheKey) => _builder._symbols.LookupMonomorphizedEnum(cacheKey);

        // Registration
        public void RegisterMonomorphizedStruct(string key, IrStructType type)
        {
            _builder._symbols.RegisterMonomorphizedStruct(key, type);
            // NOTE: We don't add to module here because fields may not be populated yet
            // FinalizeMonomorphizedStruct will be called after fields are fully substituted
        }

        public void RegisterMonomorphizedEnum(string key, IrEnumType type)
        {
            _builder._symbols.RegisterMonomorphizedEnum(key, type);
            // NOTE: We don't add to module here because variants may not be populated yet
            // FinalizeMonomorphizedEnum will be called after variants are fully substituted
        }

        // Finalization (called after fields/variants are fully populated)
        public void FinalizeMonomorphizedStruct(IrStructType type)
        {
            // IMPORTANT: Add the monomorphized struct to the module so it gets emitted in the types header
            // This is critical for nested generic structs like HashMapEntry<u32, u32> which are referenced
            // through pointers but still need their full definition for field access
            // ONLY add fully monomorphized structs (no generic parameters AND no generic type arguments)
            // TypeArguments can contain IrGenericType instances for partially-monomorphized structs like
            // HashMap<K,V>::entries which has type *HashMapEntry<K,V> where K,V are still generic
            bool hasGenericTypeArgs = type.TypeArguments != null &&
                                      type.TypeArguments.Any(arg => arg is IrGenericType);

            if (type.GenericParameters.Count == 0 && !hasGenericTypeArgs && !_builder._module.Structs.Contains(type))
            {
                _builder._module.AddStruct(type);
            }
        }

        public void FinalizeMonomorphizedEnum(IrEnumType type)
        {
            // IMPORTANT: Add the monomorphized enum to the module so it gets emitted in the types header
            // ONLY add fully monomorphized enums (no generic parameters AND no generic type arguments)
            bool hasGenericTypeArgs = type.TypeArguments != null &&
                                      type.TypeArguments.Any(arg => arg is IrGenericType);
            if (type.GenericParameters.Count == 0 && !hasGenericTypeArgs && !_builder._module.Enums.Contains(type))
            {
                _builder._module.Enums.Add(type);
            }
        }

        // Type interning
        public IrType GetReferenceType(IrType pointeeType) => _builder._typeInterner.GetReferenceType(pointeeType);
        public IrType GetMutReferenceType(IrType pointeeType) => _builder._typeInterner.GetMutReferenceType(pointeeType);
        public IrType GetPointerType(IrType pointeeType) => _builder._typeInterner.GetPointerType(pointeeType);
        public IrType GetArrayType(IrType elementType, long length) => _builder._typeInterner.GetArrayType(elementType, (int)length);
        public IrType GetFunctionPointerType(List<IrType> paramTypes, IrType returnType) => _builder._typeInterner.GetFunctionPointerType(paramTypes, returnType);
        public IrType GetTupleType(List<IrType> elementTypes) => _builder._typeInterner.GetTupleType(elementTypes);

        // Current state
        public IrType? CurrentSelfType => _builder._currentSelfType;
        public Dictionary<string, IrType>? CurrentTypeSubstitutions => _builder._currentTypeSubstitutions;

        // Constant values
        public Dictionary<string, (IrType Type, object Value)> GetConstantValues() => _builder.GetConstantsAsTuples();

        // Extern function parsing state (IrBuilder doesn't parse extern functions)
        public bool IsParsingExternFunction => false;

        // Error reporting (null = throw exceptions)
        public Action<string>? ErrorReporter => null;
    }

    /// <summary>
    /// Nested class that implements IInstantiationContext for GenericInstantiator
    /// </summary>
    private class IrBuilderInstantiationContext : IInstantiationContext
    {
        private readonly IrBuilder _builder;

        public IrBuilderInstantiationContext(IrBuilder builder)
        {
            _builder = builder;
        }

        // Type parsing
        public IrType ParseType(NovusParser.TypeContext context) => _builder._typeParser.ParseType(context);
        public IrType? ParseReturnType(NovusParser.TypeContext? context) =>
            context != null ? _builder._typeParser.ParseType(context) : IrVoidType.Instance;

        // Function building
        public void ParseSelfParameter(
            NovusParser.SelfParameterContext? context,
            IrFunction function,
            IrType implementingType)
        {
            _builder.ParseSelfParameter(context, function, implementingType);
        }

        public void ParseFunctionParameters(
            NovusParser.FunctionDeclarationContext context,
            IrFunction function)
        {
            _builder.ParseFunctionParameters(context, function);
        }

        public void ParseVariadicParameter(
            NovusParser.ParameterListContext? paramList,
            IrFunction function)
        {
            _builder.ParseVariadicParameter(paramList, function);
        }

        public void ParseVariadicParameter(
            NovusParser.ParameterListContext? paramList,
            List<IrParameter> parameters)
        {
            _builder.ParseVariadicParameter(paramList, parameters);
        }

        // Function body building
        public object? VisitFunctionBody(NovusParser.BlockContext? blockContext)
        {
            return blockContext != null ? _builder.Visit(blockContext) : null;
        }

        // Module access
        public IrModule Module => _builder._module;
        public IrFunction? CurrentFunction
        {
            get => _builder._currentFunction;
            set => _builder._currentFunction = value;
        }
        public IrBasicBlock? CurrentBlock
        {
            get => _builder._currentBlock;
            set => _builder._currentBlock = value;
        }
        public Dictionary<string, IrLocalVariable> LocalVariables => _builder._localVariables;

        // Symbol lookups
        public IrStructType? LookupStruct(string name) => _builder._symbols.LookupStruct(name);
        public IrEnumType? LookupEnum(string name) => _builder._symbols.LookupEnum(name);

        // Generic parameter management
        public void RegisterGenericParameter(string name, IrGenericType genericType) =>
            _builder._symbols.RegisterGenericParameter(name, genericType);
        public IrGenericType? LookupGenericParameter(string name) =>
            _builder._symbols.LookupGenericParameter(name);
        public void ClearGenericParameters() =>
            _builder._symbols.ClearGenericParameters();

        // Type substitution
        public ITypeSubstitutionEngine SubstitutionEngine => _builder._typeParser;

        // Name mangling
        public string GenerateMethodMangledName(
            string typeName,
            string methodName,
            bool isTraitImpl,
            string? traitName,
            List<IrType> traitTypeArgs)
        {
            return _builder.GenerateMethodMangledName(typeName, methodName, isTraitImpl, traitName, traitTypeArgs);
        }

        public string GetTypeCacheKey(IrType type) => _builder._typeParser.GetTypeCacheKey(type);

        // State management
        public Dictionary<string, IrType>? CurrentTypeSubstitutions
        {
            get => _builder._currentTypeSubstitutions;
            set => _builder._currentTypeSubstitutions = value;
        }
        public IrType? CurrentSelfType
        {
            get => _builder._currentSelfType;
            set => _builder._currentSelfType = value;
        }

        // Diagnostic reporting
        public string? InputFilePath => _builder._inputFilePath;
        public void ReportError(string errorCode, string message, SourceLocation location)
        {
            _builder._diagnostics.ReportError(errorCode, message, location);
        }

        // Constants management
        public void RestoreConstantsFromTuples(Dictionary<string, (IrType Type, object Value)> constants)
        {
            _builder.RestoreConstantsFromTuples(constants);
        }
    }

    /// <summary>
    /// Public access to diagnostics collected during IR building
    /// </summary>
    public DiagnosticBag Diagnostics => _diagnostics;

    /// <summary>
    /// Public access to the IR module being built
    /// </summary>
    public IrModule Module => _module;

    /// <summary>
    /// Pre-computed analysis results from SemanticAnalyzer.
    /// When set, IrBuilder will use these instead of re-computing type information.
    /// This ensures type checking happens before IR building.
    /// </summary>
    private readonly AnalysisResult? _analysisResult;

    // Dependency injection factories for interface implementations
    private readonly Func<SymbolTable, Func<string, VariableSymbol?>?, ISymbolResolver>? _symbolResolverFactory;
    private readonly Func<DiagnosticBag, ITypeChecker>? _typeCheckerFactory;
    private readonly Func<IrFunction, IIrEmitter>? _emitterFactory;

    // Lazily-created interface implementations
    private ISymbolResolver? _symbolResolver;
    private ITypeChecker? _typeChecker;

    /// <summary>
    /// Gets the symbol resolver for this builder.
    /// Uses the injected factory or creates a default SymbolTableResolver.
    /// </summary>
    public ISymbolResolver SymbolResolver => _symbolResolver ??=
        _symbolResolverFactory?.Invoke(_symbols, LookupLocalVariable) ??
        new SymbolTableResolver(_symbols, LookupLocalVariable);

    /// <summary>
    /// Gets the type checker for this builder.
    /// Uses the injected factory or creates a default DefaultTypeChecker.
    /// </summary>
    public ITypeChecker TypeChecker => _typeChecker ??=
        _typeCheckerFactory?.Invoke(_diagnostics) ??
        new DefaultTypeChecker(_diagnostics);

    /// <summary>
    /// Creates an IR emitter for the given function.
    /// Uses the injected factory or creates a default DefaultIrEmitter.
    /// </summary>
    public IIrEmitter CreateEmitter(IrFunction function) =>
        _emitterFactory?.Invoke(function) ?? new DefaultIrEmitter(function);

    /// <summary>
    /// Lookup a local variable by name in the current function scope.
    /// Used by SymbolResolver to check local variables before globals.
    /// </summary>
    private VariableSymbol? LookupLocalVariable(string name)
    {
        if (_localVariables.TryGetValue(name, out var local))
        {
            // Convert IrLocalVariable to VariableSymbol for the resolver
            return new VariableSymbol(
                local.Name,
                local.Type,
                local.IsMutable,
                _currentStatementLocation ?? new SourceLocation(_inputFilePath ?? "<unknown>", 0, 0, 0, ""));
        }
        return null;
    }

    public IrBuilder(bool skipAutoImports = false)
    {
        _skipAutoImports = skipAutoImports;
        _typeParser = new TypeParser(new IrBuilderTypeContext(this));
        _circularImportDetector = new CircularImportDetector(_diagnostics);
        _genericInstantiator = new GenericInstantiatorImpl(new IrBuilderInstantiationContext(this));
    }

    /// <summary>
    /// Creates an IrBuilder with the given configuration.
    /// This constructor supports dependency injection for testability.
    /// </summary>
    /// <param name="config">Configuration with optional custom implementations</param>
    public IrBuilder(IrBuilderConfiguration config)
        : this(config.SkipAutoImports)
    {
        _symbolResolverFactory = config.SymbolResolverFactory;
        _typeCheckerFactory = config.TypeCheckerFactory;
        _emitterFactory = config.EmitterFactory;

        if (config.StdLibPath != null)
        {
            _stdLibPath = config.StdLibPath;
        }

        if (config.InputFilePath != null)
        {
            _inputFilePath = config.InputFilePath;
        }

        if (config.SourceLines != null)
        {
            _sourceLines = config.SourceLines;
        }

        if (config.AnalysisResult != null)
        {
            _analysisResult = config.AnalysisResult;
            PopulateFromAnalysisResult(config.AnalysisResult);
        }
    }

    /// <summary>
    /// Populates the symbol table from pre-computed analysis results.
    /// </summary>
    private void PopulateFromAnalysisResult(AnalysisResult analysisResult)
    {
        foreach (var (name, structType) in analysisResult.Structs)
        {
            var location = analysisResult.StructLocations.TryGetValue(name, out var loc) ? loc : null;
            _symbols.RegisterStruct(name, structType, location);
        }
        foreach (var (name, enumType) in analysisResult.Enums)
        {
            var location = analysisResult.EnumLocations.TryGetValue(name, out var loc) ? loc : null;
            _symbols.RegisterEnum(name, enumType, location);
        }
        foreach (var (name, trait) in analysisResult.Traits)
        {
            var location = analysisResult.TraitLocations.TryGetValue(name, out var loc) ? loc : null;
            _symbols.RegisterTrait(name, trait, location);
        }
        foreach (var (name, constant) in analysisResult.Constants)
        {
            _symbols.RegisterConstant(name, constant.Type, constant.Value);
        }
    }

    /// <summary>
    /// Creates an IrBuilder initialized with pre-computed semantic analysis results.
    /// This constructor enforces proper compiler phase ordering:
    /// 1. SemanticAnalyzer.Analyze() runs first (type checking)
    /// 2. IrBuilder.BuildModule() runs second (IR generation)
    /// </summary>
    /// <param name="analysisResult">Results from SemanticAnalyzer.GetResult()</param>
    /// <param name="skipAutoImports">Skip auto-importing core module (for tests)</param>
    public IrBuilder(AnalysisResult analysisResult, bool skipAutoImports = false)
        : this(skipAutoImports)
    {
        _analysisResult = analysisResult;
        PopulateFromAnalysisResult(analysisResult);
    }

    /// <summary>
    /// Set the standard library path
    /// </summary>
    public void SetStdLibPath(string path)
    {
        _stdLibPath = path;
    }

    /// <summary>
    /// Set the input file path being compiled
    /// </summary>
    public void SetInputFilePath(string path)
    {
        _inputFilePath = path;
    }

    /// <summary>
    /// Get list of imported module file paths (for linking)
    /// </summary>
    public List<string> GetImportedModules()
    {
        return _importedModulePaths;
    }

    /// <summary>
    /// Set source lines for error reporting
    /// </summary>
    public void SetSourceLines(string[] lines)
    {
        _sourceLines = lines;
    }

    /// <summary>
    /// Get diagnostics collected during IR building
    /// </summary>
    public DiagnosticBag GetDiagnostics()
    {
        return _diagnostics;
    }

    /// <summary>
    /// Get source location for error reporting from a parser context.
    /// This helper consolidates the repeated pattern that appears 181+ times across IrBuilder
    /// for constructing SourceLocation objects for error reporting.
    /// </summary>
    /// <param name="context">The parser context from which to extract location information</param>
    /// <returns>A SourceLocation object for error reporting</returns>
    private SourceLocation GetLocation(Antlr4.Runtime.ParserRuleContext context)
    {
        return SourceLocationHelper.FromContext(context, _inputFilePath ?? "<unknown>", _sourceLines);
    }

    /// <summary>
    /// Helper to get constants in tuple format for generic templates
    /// </summary>
    private Dictionary<string, (IrType Type, object Value)> GetConstantsAsTuples()
    {
        var result = new Dictionary<string, (IrType Type, object Value)>();
        foreach (var kvp in _symbols.GetLocalConstants())
        {
            result[kvp.Key] = (kvp.Value.Type, kvp.Value.Value);
        }
        return result;
    }

    /// <summary>
    /// Helper to restore constants from tuple format
    /// </summary>
    private void RestoreConstantsFromTuples(Dictionary<string, (IrType Type, object Value)> constants)
    {
        // For now, we need to clear and re-add all constants
        // TODO: Use child scopes instead when SymbolTable supports better scoping
        // Note: We can't clear from SymbolTable directly, so we track which ones we added
        foreach (var kvp in constants)
        {
            _symbols.RegisterConstant(kvp.Key, kvp.Value.Type, kvp.Value.Value);
        }
    }

    /// <summary>
    /// Helper to get constant values (without types) for expression evaluator
    /// </summary>
    private Dictionary<string, object> GetConstantValues()
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in _symbols.GetLocalConstants())
        {
            result[kvp.Key] = kvp.Value.Value;
        }
        return result;
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

    /// <summary>
    /// Add an implicit return at the end of a function if it doesn't already have a terminator.
    /// This helper consolidates the repeated implicit return logic that appears in Pass 5 and Pass 6.
    /// </summary>
    /// <param name="lastValue">The last expression value from the function body (may be null)</param>
    private void AddImplicitReturn(IrValue? lastValue)
    {
        if (!CurrentBlockHasTerminator())
        {
            if (_currentFunction!.ReturnType is not IrVoidType && lastValue != null)
            {
                // Non-void function with expression: return the value
                _currentBlock!.AddInstruction(new IrReturn(lastValue));
            }
            else
            {
                // Void function or no return value: add void return
                _currentBlock!.AddInstruction(new IrReturn(null));
            }
        }
    }

    /// <summary>
    /// Handle postfix conditionals (if/unless) by wrapping statement execution in a conditional branch
    /// For example: `return true if x == 1` becomes `if (x == 1) { return true }`
    /// </summary>
    private void HandlePostfixCondition(NovusParser.PostfixConditionContext? postfixContext, Action statementAction)
    {
        if (postfixContext == null)
        {
            // No postfix condition, execute statement directly
            statementAction();
            return;
        }

        // Generate labels for the conditional
        var thenLabel = $"postfix_then_{_labelCounter}";
        var endLabel = $"postfix_end_{_labelCounter}";
        _labelCounter++;

        // Evaluate the condition
        var conditionExpr = (IrValue?)Visit(postfixContext.expression());
        if (conditionExpr == null)
        {
            return; // Error already reported
        }

        // Check if this is an 'unless' condition (invert the condition)
        bool isUnless = postfixContext.KW_UNLESS() != null;

        // Generate conditional branch
        // For 'if': branch to thenLabel if true, endLabel if false
        // For 'unless': branch to thenLabel if false, endLabel if true (invert)
        if (isUnless)
        {
            // unless condition: execute if condition is false
            _currentBlock!.AddInstruction(new IrConditionalBranch(conditionExpr, endLabel, thenLabel));
        }
        else
        {
            // if condition: execute if condition is true
            _currentBlock!.AddInstruction(new IrConditionalBranch(conditionExpr, thenLabel, endLabel));
        }

        // Then block: execute the statement
        _currentBlock!.AddInstruction(new IrLabel(thenLabel));
        statementAction();

        // Always emit the end label, even if the statement terminated
        // The conditional branch references this label, so it must exist
        // If the statement terminated (e.g., return), this label is unreachable but still valid
        _currentBlock!.AddInstruction(new IrLabel(endLabel));
    }

    /// <summary>
    /// Build IR from a parsed compilation unit using a multi-pass approach.
    /// </summary>
    /// <remarks>
    /// COMPILATION PASS DEPENDENCIES AND ORDERING:
    /// =============================================
    ///
    /// The passes are ordered to satisfy these dependencies:
    ///
    /// Pass 0a/0b: Imports
    ///   - No dependencies. Sets up external symbols.
    ///
    /// Pass 1: Constants
    ///   - Depends on: Imports (constants may reference imported types)
    ///   - Must complete before: Enum variants (may use constants for discriminants)
    ///
    /// Pass 2a: Enum Stubs
    ///   - Depends on: Nothing
    ///   - Produces: Enum type names are resolvable (but no variants yet)
    ///   - Reason: Allows forward references between enums
    ///
    /// Pass 2a.5: Struct Stubs
    ///   - Depends on: Enum stubs (struct fields may reference enums)
    ///   - Produces: Struct type names are resolvable (but no fields yet)
    ///   - Reason: Enum variants may contain struct associated data
    ///
    /// Pass 2b: Enum Variants
    ///   - Depends on: Enum stubs, Struct stubs, Constants
    ///   - Produces: Complete enum types with variants and associated data
    ///   - Reason: Variant associated data may be struct types
    ///
    /// Pass 3: Struct Fields
    ///   - Depends on: Enum variants (fields may be enum types), Struct stubs (for self-references)
    ///   - Produces: Complete struct types with field definitions
    ///
    /// Pass 3.1: Static Variables
    ///   - Depends on: Struct fields (static initializers may be struct literals)
    ///   - Reason: Static initializers need complete type information
    ///
    /// Pass 3.25: Trait Types
    ///   - Depends on: Struct/enum types (trait bounds may reference them)
    ///
    /// Pass 3.5: External Variables
    ///   - Depends on: All types registered
    ///
    /// Pass 4: Function Signatures
    ///   - Depends on: All types, constants
    ///   - Generic functions stored as templates (not instantiated yet)
    ///
    /// Pass 4.5: Impl Method Signatures
    ///   - Depends on: Struct types, trait types, function signatures
    ///   - Must store generic method templates for later instantiation
    ///
    /// Pass 5: Function Bodies
    ///   - Depends on: All signatures, types, constants
    ///   - May trigger generic instantiation
    ///
    /// Pass 6: Impl Method Bodies
    ///   - Depends on: Function bodies (methods may call functions)
    ///   - Only for non-generic impl blocks (generic ones instantiated on demand)
    ///
    /// CRITICAL INVARIANTS:
    /// - Type names must be registered as stubs before being referenced
    /// - Complete type information (fields/variants) needed before function bodies
    /// - Generic templates stored in Pass 4/4.5, instantiated in Pass 5/6 on demand
    /// </remarks>
    public IrModule BuildModule(NovusParser.CompilationUnitContext context)
    {
        // Process module-level attributes first (e.g., #[stack_size(65536)])
        ProcessModuleAttributes(context.moduleAttribute());

        // Multi-pass approach to handle forward references:
        // Pass 0a: Implicitly import all of core module (unless testing or compiling a std library module)
        // Don't auto-import std::core when compiling std library modules to prevent circular dependencies
        bool isStdLibraryModule = _inputFilePath != null && _inputFilePath.Contains(System.IO.Path.DirectorySeparatorChar + "std" + System.IO.Path.DirectorySeparatorChar);

        if (!_skipAutoImports && !isStdLibraryModule)
        {
            ImportModule("std::core", importAll: true);
        }

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

        // NOTE: Static variables are registered later (Pass 3.1) after struct types are defined,
        // because static initializers may contain struct literals that need type resolution.

        // Pass 2: Register all enum types using two-pass approach
        // Pass 2a: Register stub enum types for ALL enums in the module
        // This allows forward references between enums (e.g., NovusError referencing ExecError)
        foreach (var enumContext in context.enumDeclaration())
        {
            var enumName = enumContext.IDENTIFIER().GetText();

            // Skip if this enum has already been imported (transitive dependencies)
            if (_symbols.HasEnum(enumName))
            {
                continue;
            }

            // Register a stub enum type with no variants yet
            // This makes the type name resolvable during variant parsing
            // Parse generic parameters for stub so type checking works correctly
            var genericParams = ParseGenericParameters(enumContext.genericParams());
            var stubEnum = new IrEnumType(enumName, new List<IrEnumVariant>(), genericParams.Count > 0 ? genericParams : null);
            _symbols.RegisterEnum(enumName, stubEnum);
        }

        // Pass 2a.5: Register stub struct types for ALL structs in the module
        // CRITICAL: This must happen BEFORE Pass 2b (filling enum variants) because
        // enum variants may have structs as associated data (e.g., WindowEvent::Refresh(RefreshGuard))
        // Without this pass, the compiler can't resolve struct types used in enum variants
        foreach (var structContext in context.structDeclaration())
        {
            var structName = structContext.IDENTIFIER().GetText();

            // Skip if this struct has already been imported (transitive dependencies)
            if (_symbols.HasStruct(structName))
            {
                continue;
            }

            // Register a placeholder struct with empty fields
            // Parse generic parameters for stub so type checking works correctly
            var genericParams = ParseGenericParameters(structContext.genericParams());
            var placeholderStruct = new IrStructType(structName, new List<IrStructField>(), genericParams.Count > 0 ? genericParams : null);
            _symbols.RegisterStruct(structName, placeholderStruct);
        }

        // Pass 2b: Fill in enum variants for all enums
        foreach (var enumContext in context.enumDeclaration())
        {
            // Now register the full enum with variants (replacing the stub)
            // At this point, all enum names AND struct names are resolvable for variant type parsing
            RegisterEnum(enumContext);
        }

        // Pass 3: Register all struct types (fill in fields, replacing placeholders from Pass 2a.5)
        foreach (var structContext in context.structDeclaration())
        {
            RegisterStruct(structContext);
        }

        // Pass 3.1: Register all static variables (after struct types are registered)
        // Static initializers may contain struct literals that require type resolution
        foreach (var staticContext in context.staticDeclaration())
        {
            RegisterStatic(staticContext);
        }

        // Pass 3.25: Register all trait types
        foreach (var traitContext in context.traitDeclaration())
        {
            RegisterTrait(traitContext);
        }

        // Pass 3.5: Register all external variables (after types are registered)
        foreach (var externVarContext in context.globalVariableDeclaration())
        {
            RegisterExternalVariable(externVarContext);
        }

        // Pass 4: Collect all function signatures (including impl methods)
        foreach (var funcContext in context.functionDeclaration())
        {
            var name = funcContext.IDENTIFIER().GetText();

            // Check if this is a generic function
            var genericParams = ParseGenericParameters(funcContext.genericParams());

            // If generic, store as template for later instantiation
            if (genericParams.Count > 0)
            {
                var templateConstants = GetConstantsAsTuples();
                var template = new Generics.GenericTemplate(genericParams, funcContext, templateConstants);
                _genericInstantiator.RegisterFunctionTemplate(name, template);
                continue; // Don't add to _module.Functions yet
            }

            // Non-generic function: register normally
            var returnType = ParseReturnType(funcContext.type());

            // Check for extern, pub, and internal keywords
            var (visibility, isExtern, _) = AstModifierHelper.ParseModifiers(funcContext, 4);

            var function = new IrFunction(name, returnType, visibility, isExtern);
            function.Location = GetLocation(funcContext);  // Store source location for debug info

            // Parse and store function attributes (for @test, @export, etc.)
            var attributes = ParseAttributesSimple(funcContext.attribute());
            function.Attributes = attributes;

            // Check for #[export] attribute (from function's own attributes)
            if (attributes.Has("export"))
            {
                function.IsExported = true;
            }

            // Also check pending function attributes from moduleAttribute
            // This handles the case where #[export] appears at module level before the function.
            // The grammar parses #[export] as moduleAttribute when it appears before `pub fn`.
            if (_pendingFunctionAttributes.Contains("export"))
            {
                function.IsExported = true;
                _pendingFunctionAttributes.Remove("export");
            }

            // Parse parameters
            ParseFunctionParameters(funcContext, function);

            _module.AddFunction(function);
        }

        // Pass 4.5: Collect impl block method signatures
        foreach (var implContext in context.implDeclaration())
        {
            // IMPORTANT: Extract generic parameters FIRST before parsing trait type args
            // This ensures that 'T' is in scope when parsing 'Iterable<T>'
            var genericParams = ParseGenericParameters(implContext.genericParams(), registerInSymbolTable: true);

            // Determine if this is a trait impl or inherent impl
            bool isTraitImpl = implContext.KW_FOR() != null;
            string? traitName = null;
            List<IrType> traitTypeArgs = new();

            // Extract implementing type name and type
            string? typeName;
            IrType? implementingType;

            if (isTraitImpl)
            {
                // Format: impl [<GenericParams>] TraitName<TraitArgs> for TargetType<TypeArgs>
                // traitTypeName is the trait being implemented
                traitName = implContext.traitTypeName.IDENTIFIER(0).GetText();

                // Parse trait type arguments if present (e.g., From<DosError>)
                traitTypeArgs = ParseTypeArguments(implContext.traitTypeArgs);

                // Parse the impl target type (primitive or named)
                (typeName, implementingType) = ParseImplTargetType(implContext.implTargetType(), null, implContext);
                if (implementingType == null || typeName == null)
                {
                    continue;
                }
            }
            else
            {
                // Format: impl [<GenericParams>] TargetType<TypeArgs>
                // Parse the impl target type (inherent impl)
                (typeName, implementingType) = ParseImplTargetType(null, implContext.targetTypeName, implContext);
                if (implementingType == null || typeName == null)
                {
                    continue;
                }
            }

            // Set the current Self type for this impl block
            _currentSelfType = implementingType;

            // Process each method in the impl block
            foreach (var implItem in implContext.implItem())
            {
                var funcDecl = implItem.functionDeclaration();
                if (funcDecl == null) continue;

                var methodName = funcDecl.IDENTIFIER().GetText();

                // For generic impl blocks, store methods as templates for later instantiation
                if (genericParams.Count > 0)
                {
                    StoreGenericMethodTemplate(typeName!, methodName, genericParams, funcDecl);
                    // Don't create function yet - it will be instantiated when called with concrete types
                    continue;
                }

                // Non-generic impl blocks: create function signatures now
                var returnType = ParseReturnType(funcDecl.type());

                // Check for extern, pub, and internal keywords
                var (visibility, isExtern, _) = AstModifierHelper.ParseModifiers(funcDecl, 4);

                // Methods are registered with mangled names
                var mangledName = GenerateMethodMangledName(typeName!, methodName, isTraitImpl, traitName, traitTypeArgs);

                var function = new IrFunction(mangledName, returnType, visibility, isExtern);
                function.Location = GetLocation(funcDecl);  // Store source location for debug info

                // Parse parameters (including self)
                if (funcDecl.parameterList() != null)
                {
                    var paramList = funcDecl.parameterList();

                    // Handle self parameter if present
                    ParseSelfParameter(paramList.selfParameter(), function, implementingType);

                    // Add regular and variadic parameters
                    ParseFunctionParameters(funcDecl, function);
                }

                _module.AddFunction(function);
            }

            // Register trait implementation if this is a trait impl
            // Note: We register even generic trait impls (e.g., impl<T> Drop for Vec<T>)
            // so that TypeImplementsDrop can detect them for monomorphized types
            if (isTraitImpl && traitName != null)
            {
                // _currentSelfType already contains the implementing type (set earlier)
                if (_currentSelfType == null)
                {
                    var errorLocation = GetLocation(implContext);
                    _diagnostics.ReportError(
                        ErrorCodes.TypeNotFound,
                        $"Type '{typeName}' not found for trait implementation",
                        errorLocation
                    );
                    continue;
                }

                // Construct full trait name with type arguments (e.g., "From<DosError>")
                var fullTraitName = traitName;
                if (traitTypeArgs.Count > 0)
                {
                    fullTraitName = $"{traitName}<{string.Join(", ", traitTypeArgs.Select(t => t.Name))}>";
                }

                // Create IrTraitImpl and add to module using AddTraitImpl to maintain indices
                // For generic impls, this is a template that will be instantiated later
                var traitImpl = new IrTraitImpl(fullTraitName, traitTypeArgs, typeName, _currentSelfType, genericParams);
                _module.AddTraitImpl(traitImpl);
            }

            // Clear generic parameters and Self type after processing impl block
            _symbols.ClearGenericParameters();
            _currentSelfType = null;
        }

        // Pass 5: Build function bodies
        foreach (var funcContext in context.functionDeclaration())
        {
            var funcName = funcContext.IDENTIFIER().GetText();

            // Skip generic function templates - they'll be instantiated on-demand
            if (_genericInstantiator.HasFunctionTemplate(funcName))
            {
                continue;
            }

            _currentFunction = _module.GetFunction(funcName);
            if (_currentFunction == null)
            {
                var errorLocation = GetLocation(funcContext);
                _diagnostics.ReportError(
                    ErrorCodes.FunctionNotFound,
                    $"Function '{funcName}' not found in module. This indicates a compiler bug in an earlier pass.",
                    errorLocation
                );
                continue;
            }

            // Skip extern functions - they have no body
            if (_currentFunction.IsExtern || funcContext.block() == null)
            {
                continue;
            }

            _currentBlock = _currentFunction.CreateBasicBlock("entry");
            _localVariables.Clear(); // Clear local variables for new function

            // Visit function body and get the last expression value
            var lastValue = Visit(funcContext.block()) as IrValue;

            // Add implicit return if block doesn't already have a terminator
            AddImplicitReturn(lastValue);
        }

        // Pass 6: Build impl method bodies (only for non-generic impl blocks)
        foreach (var implContext in context.implDeclaration())
        {
            // Check if this is a generic impl block and skip early
            // Skip generic impl blocks - they will be instantiated on demand
            var isGeneric = implContext.genericParams() != null;
            if (isGeneric)
            {
                continue;
            }

            // Determine if this is a trait impl or inherent impl
            bool isTraitImpl = implContext.KW_FOR() != null;
            string? traitName = null;
            List<IrType> traitTypeArgs = new();

            // Extract implementing type name and type
            string? typeName;
            IrType? implementingType;

            if (isTraitImpl)
            {
                // Format: impl TraitName<TraitArgs> for TargetType
                // traitTypeName is the trait being implemented
                traitName = implContext.traitTypeName.IDENTIFIER(0).GetText();

                // Parse trait type arguments if present (e.g., From<DosError>)
                traitTypeArgs = ParseTypeArguments(implContext.traitTypeArgs);

                // Parse the impl target type (primitive or named)
                (typeName, implementingType) = ParseImplTargetType(implContext.implTargetType(), null, implContext);
                if (implementingType == null)
                {
                    continue; // Skip this impl block if type parsing failed
                }
            }
            else
            {
                // Format: impl TargetType
                // Parse the impl target type (inherent impl)
                (typeName, implementingType) = ParseImplTargetType(null, implContext.targetTypeName, implContext);
                if (implementingType == null)
                {
                    continue; // Skip this impl block if type parsing failed
                }
            }

            _currentSelfType = implementingType;

            // Non-generic impl blocks: build method bodies
            foreach (var implItem in implContext.implItem())
            {
                var funcDecl = implItem.functionDeclaration();
                if (funcDecl == null) continue;

                var methodName = funcDecl.IDENTIFIER().GetText();

                // Use centralized name mangling for trait impls vs inherent impls
                var mangledName = Generics.InstantiationKeyBuilder.BuildMethodMangledName(
                    typeName!, methodName, isTraitImpl, traitName, traitTypeArgs);

                _currentFunction = _module.GetFunction(mangledName);
                if (_currentFunction == null)
                {
                    var errorLocation = GetLocation(funcDecl);
                    _diagnostics.ReportError(
                        ErrorCodes.MethodNotFound,
                        $"Method '{mangledName}' not found in module. This indicates a compiler bug in an earlier pass.",
                        errorLocation
                    );
                    continue; // Skip this method if it wasn't found
                }

                // Skip extern functions or methods with no body
                if (_currentFunction.IsExtern || funcDecl.block() == null)
                {
                    continue;
                }

                _currentBlock = _currentFunction.CreateBasicBlock("entry");
                _localVariables.Clear();

                // Add self parameter to local variables if present
                if (_currentFunction.Parameters.Any(p => p.Name == "self"))
                {
                    var selfParam = _currentFunction.Parameters.First(p => p.Name == "self");
                    // Parameters are not mutable by default (they're immutable copies)
                    _localVariables["self"] = new IrLocalVariable("self", selfParam.Type, isMutable: false);
                }

                // Add other parameters to local variables
                foreach (var param in _currentFunction.Parameters.Where(p => p.Name != "self"))
                {
                    _localVariables[param.Name] = new IrLocalVariable(param.Name, param.Type, isMutable: false);
                }

                // Set expected type for implicit returns (so match expressions know their result type)
                var savedExpectedType = _expectedType;
                if (_currentFunction.ReturnType is not IrVoidType)
                {
                    _expectedType = _currentFunction.ReturnType;
                }

                // Visit method body
                var lastValue = Visit(funcDecl.block()) as IrValue;

                // Restore previous expected type
                _expectedType = savedExpectedType;

                // Add implicit return if block doesn't already have a terminator
                AddImplicitReturn(lastValue);
            }

            // Clear Self type after processing impl block
            _currentSelfType = null;
        }

        return _module;
    }


    /// <summary>
    /// Store a generic method template for later instantiation.
    /// </summary>

    private void ExtractGenericTypeMapping(IrType baseType, IrType monomorphizedType, Dictionary<string, IrType> substitutions)
    {
        switch (baseType)
        {
            case IrGenericType gt:
                // Direct generic type - map it to the concrete type
                if (!substitutions.ContainsKey(gt.ParameterName))
                {
                    substitutions[gt.ParameterName] = monomorphizedType;
                }
                break;

            case IrPointerType basePtrType when monomorphizedType is IrPointerType monoPtrType:
                // Recurse into pointer pointee types
                ExtractGenericTypeMapping(basePtrType.PointeeType, monoPtrType.PointeeType, substitutions);
                break;

            case IrMutReferenceType baseRefType when monomorphizedType is IrMutReferenceType monoRefType:
                // Recurse into mutable reference types
                ExtractGenericTypeMapping(baseRefType.PointeeType, monoRefType.PointeeType, substitutions);
                break;

            case IrReferenceType baseRefType when monomorphizedType is IrReferenceType monoRefType:
                // Recurse into immutable reference types
                ExtractGenericTypeMapping(baseRefType.PointeeType, monoRefType.PointeeType, substitutions);
                break;

            case IrArrayType baseArrayType when monomorphizedType is IrArrayType monoArrayType:
                // Recurse into array element types
                if (baseArrayType.Length == monoArrayType.Length)
                {
                    ExtractGenericTypeMapping(baseArrayType.ElementType, monoArrayType.ElementType, substitutions);
                }
                break;

            case IrStructType baseStructType when monomorphizedType is IrStructType monoStructType:
                // Recurse into struct field types to extract generic mappings
                // For example: Box<T> matched with Box<i32> should extract T -> i32
                if (baseStructType.StructName == monoStructType.StructName &&
                    baseStructType.Fields.Count == monoStructType.Fields.Count)
                {
                    for (int i = 0; i < baseStructType.Fields.Count; i++)
                    {
                        ExtractGenericTypeMapping(baseStructType.Fields[i].Type, monoStructType.Fields[i].Type, substitutions);
                    }
                }
                break;

            case IrEnumType baseEnumType when monomorphizedType is IrEnumType monoEnumType:
                // Recurse into enum variant types to extract generic mappings
                // For example: Option<T> matched with Option<i32> should extract T -> i32
                if (baseEnumType.EnumName == monoEnumType.EnumName &&
                    baseEnumType.Variants.Count == monoEnumType.Variants.Count)
                {
                    for (int i = 0; i < baseEnumType.Variants.Count; i++)
                    {
                        var baseVariant = baseEnumType.Variants[i];
                        var monoVariant = monoEnumType.Variants[i];

                        if (baseVariant.Name == monoVariant.Name &&
                            baseVariant.AssociatedData.Count == monoVariant.AssociatedData.Count)
                        {
                            for (int j = 0; j < baseVariant.AssociatedData.Count; j++)
                            {
                                ExtractGenericTypeMapping(baseVariant.AssociatedData[j], monoVariant.AssociatedData[j], substitutions);
                            }
                        }
                    }
                }
                break;

            // For non-generic types (primitives, concrete structs), no mapping needed
            default:
                break;
        }
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
    /// Extract all struct/enum type names referenced in a type expression
    /// This is used to find dependencies when importing structs
    /// Example: *TextAttr returns ["TextAttr"], [*BitMap; 8] returns ["BitMap"]
    /// </summary>
    private HashSet<string> ExtractTypeNameDependencies(NovusParser.TypeContext typeContext)
    {
        var dependencies = new HashSet<string>();

        if (typeContext is NovusParser.PointerTypeContext ptrCtx)
        {
            // *T - extract from T
            dependencies.UnionWith(ExtractTypeNameDependencies(ptrCtx.type()));
        }
        else if (typeContext is NovusParser.ReferenceTypeContext refCtx)
        {
            // &T or &mut T - extract from T
            dependencies.UnionWith(ExtractTypeNameDependencies(refCtx.type()));
        }
        else if (typeContext is NovusParser.ArrayTypeWithSizeContext arrayCtx)
        {
            // [T; N] - extract from T
            dependencies.UnionWith(ExtractTypeNameDependencies(arrayCtx.type()));
        }
        else if (typeContext is NovusParser.ArrayTypeInferredContext arrayInferredCtx)
        {
            // [T] - extract from T
            dependencies.UnionWith(ExtractTypeNameDependencies(arrayInferredCtx.type()));
        }
        else if (typeContext is NovusParser.NamedTypeContext namedCtx)
        {
            // Type name like TextAttr, Vec<i32>, etc.
            var typeName = namedCtx.typeName().GetText();

            // Only add if it's not a primitive type
            if (!IsPrimitiveTypeName(typeName))
            {
                dependencies.Add(typeName);
            }

            // If it has generic arguments, extract from those too
            if (namedCtx.genericTypeArgs()?.typeList() != null)
            {
                foreach (var typeArg in namedCtx.genericTypeArgs().typeList().type())
                {
                    dependencies.UnionWith(ExtractTypeNameDependencies(typeArg));
                }
            }
        }
        else if (typeContext is NovusParser.FunctionPointerTypeContext fpCtx)
        {
            // fn(T1, T2) -> R - extract from parameters and return type
            if (fpCtx.typeList() != null)
            {
                foreach (var paramType in fpCtx.typeList().type())
                {
                    dependencies.UnionWith(ExtractTypeNameDependencies(paramType));
                }
            }
            if (fpCtx.type() != null)
            {
                dependencies.UnionWith(ExtractTypeNameDependencies(fpCtx.type()));
            }
        }

        return dependencies;
    }

    /// <summary>
    /// Check if a type name is a primitive type (not a struct/enum)
    /// </summary>
}
