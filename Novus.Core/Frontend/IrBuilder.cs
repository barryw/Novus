using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// Builds IR from the parsed AST using the visitor pattern.
/// This class is split across multiple partial class files for maintainability.
/// </summary>
public partial class IrBuilder : NovusBaseVisitor<object?>
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

    // TODO: Migrate these to SymbolTable when we standardize on GenericTemplate format
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

    private IrType? _expectedType = null; // Expected type for bidirectional type checking
    public readonly List<IrStringLiteral> StringLiterals = new(); // Track all string literals for data section
    private string _stdLibPath = "std"; // Path to standard library
    private string? _inputFilePath = null; // Path to the file being compiled
    private readonly bool _skipAutoImports; // Skip auto-importing core module (for tests)
    private readonly List<string> _importedModulePaths = new(); // Track imported module file paths for linking
    private readonly HashSet<string> _processedModules = new(); // Track which modules we've already processed for imports (prevent circular imports)
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
    private readonly List<string> _sourceLines = new();

    // Statement-level source location tracking for debug symbols
    // Set at the start of each statement and propagated to IR instructions
    private SourceLocation? _currentStatementLocation = null;

    /// <summary>
    /// Emit an instruction with the current statement's source location attached.
    /// This enables statement-level debug symbols for precise crash location reporting.
    /// </summary>
    private void Emit(IrInstruction instruction)
    {
        if (_currentStatementLocation != null)
        {
            instruction.Location = _currentStatementLocation;
        }
        _currentBlock!.AddInstruction(instruction);
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

            if (type.StructName == "KeyValue")
            {
                Console.WriteLine($"[IrBuilder] FinalizeMonomorphizedStruct called with KeyValue: CacheKey={type.CacheKey}, TypeArgs={type.TypeArguments?.Count ?? 0}");
                if (type.TypeArguments != null)
                {
                    foreach (var arg in type.TypeArguments)
                    {
                        Console.WriteLine($"  TypeArg: {arg.Name} (is generic: {arg is IrGenericType})");
                    }
                }
                // Print stack trace to find caller
                Console.WriteLine($"  Stack trace:\n{Environment.StackTrace}");
            }

            if (type.GenericParameters.Count == 0 && !hasGenericTypeArgs && !_builder._module.Structs.Contains(type))
            {
                Console.WriteLine($"[IrBuilder] Adding monomorphized struct to module: {type.StructName} CacheKey={type.CacheKey}");
                _builder._module.Structs.Add(type);
            }
            else
            {
                Console.WriteLine($"[IrBuilder] NOT adding struct: {type.StructName} CacheKey={type.CacheKey} (GenericParams={type.GenericParameters.Count}, HasGenericTypeArgs={hasGenericTypeArgs}, AlreadyInModule={_builder._module.Structs.Contains(type)})");
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
    /// Constructor for IrBuilder
    /// </summary>
    /// <summary>
    /// Public access to diagnostics collected during IR building
    /// </summary>
    public DiagnosticBag Diagnostics => _diagnostics;

    public IrBuilder(bool skipAutoImports = false)
    {
        _skipAutoImports = skipAutoImports;
        _typeParser = new TypeParser(new IrBuilderTypeContext(this));
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
        _sourceLines.Clear();
        _sourceLines.AddRange(lines);
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
        return SourceLocationHelper.FromContext(context, _inputFilePath ?? "<unknown>", _sourceLines.ToArray());
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
                _genericFunctionTemplates[name] = (genericParams, funcContext, templateConstants);
                continue; // Don't add to _module.Functions yet
            }

            // Non-generic function: register normally
            var returnType = ParseReturnType(funcContext.type());

            // Check for extern, pub, and internal keywords
            var (visibility, isExtern, _) = AstModifierHelper.ParseModifiers(funcContext, 4);

            var function = new IrFunction(name, returnType, visibility, isExtern);
            function.Location = GetLocation(funcContext);  // Store source location for debug info

            // Check for #[export] attribute
            var attributes = ParseAttributesSimple(funcContext.attribute());
            if (attributes.Has("export"))
            {
                function.IsExported = true;
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
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
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

                // Create IrTraitImpl and add to module
                // For generic impls, this is a template that will be instantiated later
                var traitImpl = new IrTraitImpl(fullTraitName, traitTypeArgs, typeName, _currentSelfType, genericParams);
                _module.TraitImpls.Add(traitImpl);
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
            if (_genericFunctionTemplates.ContainsKey(funcName))
            {
                continue;
            }

            _currentFunction = _module.Functions.FirstOrDefault(f => f.Name == funcName);
            if (_currentFunction == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
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

                // Use correct mangling for trait impls vs inherent impls
                string mangledName;
                if (isTraitImpl && traitName != null)
                {
                    var typeArgsSuffix = traitTypeArgs.Count > 0
                        ? "_" + string.Join("_", traitTypeArgs.Select(t => t.Name.Replace("::", "_")))
                        : "";
                    mangledName = $"{typeName}_{traitName}{typeArgsSuffix}_{methodName}";
                }
                else
                {
                    mangledName = $"{typeName}::{methodName}";
                }

                _currentFunction = _module.Functions.FirstOrDefault(f => f.Name == mangledName);
                if (_currentFunction == null)
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
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
            if (namedCtx.typeList() != null)
            {
                foreach (var typeArg in namedCtx.typeList().type())
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
