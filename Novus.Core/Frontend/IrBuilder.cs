using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

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

    // Track defer blocks registered in the current scope (for scope-specific cleanup)
    // Each element in the stack represents a scope; the list contains defers registered in that scope
    private Stack<List<IrBasicBlock>> _scopeDeferStack = new();

    // Drop tracking for automatic resource cleanup (RAII)
    // Each scope tracks which local variables need drop calls when the scope exits
    private readonly Stack<List<string>> _scopeDropStack = new(); // Stack of variable names per scope
    private readonly Dictionary<string, bool> _movedVariables = new(); // Track which variables have been moved

    private int _staticVarCounter = 0;  // Counter for auto-generated static variables
    private readonly Stack<string> _loopExitLabels = new(); // Track loop exit labels for break
    private readonly Dictionary<string, IrLocalVariable> _localVariables = new(); // Track local variables in current function

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
        public void RegisterMonomorphizedStruct(string key, IrStructType type) => _builder._symbols.RegisterMonomorphizedStruct(key, type);
        public void RegisterMonomorphizedEnum(string key, IrEnumType type) => _builder._symbols.RegisterMonomorphizedEnum(key, type);

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

    public IrModule BuildModule(NovusParser.CompilationUnitContext context)
    {
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

        // Pass 1.5: Register all static variables
        foreach (var staticContext in context.staticDeclaration())
        {
            RegisterStatic(staticContext);
        }

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
            List<string>? genericParams = null;
            if (enumContext.genericParams() != null)
            {
                genericParams = new List<string>();
                foreach (var paramId in enumContext.genericParams().IDENTIFIER())
                {
                    genericParams.Add(paramId.GetText());
                }
            }
            var stubEnum = new IrEnumType(enumName, new List<IrEnumVariant>(), genericParams);
            _symbols.RegisterEnum(enumName, stubEnum);
        }

        // Pass 2b: Fill in enum variants for all enums
        foreach (var enumContext in context.enumDeclaration())
        {
            // Now register the full enum with variants (replacing the stub)
            // At this point, all enum names are resolvable for variant type parsing
            RegisterEnum(enumContext);
        }

        // Pass 3: Register all struct types
        foreach (var structContext in context.structDeclaration())
        {
            RegisterStruct(structContext);
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
            var genericParams = new List<string>();
            if (funcContext.genericParams() != null)
            {
                foreach (var paramId in funcContext.genericParams().IDENTIFIER())
                {
                    genericParams.Add(paramId.GetText());
                }
            }

            // If generic, store as template for later instantiation
            if (genericParams.Count > 0)
            {
                var templateConstants = GetConstantsAsTuples();
                _genericFunctionTemplates[name] = (genericParams, funcContext, templateConstants);
                continue; // Don't add to _module.Functions yet
            }

            // Non-generic function: register normally
            var returnType = funcContext.type() != null ? ParseType(funcContext.type()) : IrVoidType.Instance;

            // Check for extern, pub, and internal keywords
            var (visibility, isExtern, _) = AstModifierHelper.ParseModifiers(funcContext, 4);

            var function = new IrFunction(name, returnType, visibility, isExtern);

            // Check for #[export] attribute
            var attributes = ParseAttributesSimple(funcContext.attribute());
            if (attributes.Has("export"))
            {
                function.IsExported = true;
            }

            // Parse parameters
            if (funcContext.parameterList() != null)
            {
                var paramList = funcContext.parameterList();
                foreach (var paramCtx in paramList.parameter())
                {
                    var paramName = paramCtx.IDENTIFIER().GetText();
                    var paramType = ParseType(paramCtx.type());
                    function.Parameters.Add(new IrParameter(paramName, paramType));
                }

                // Add variadic parameter if present
                if (paramList.variadicParameter() != null)
                {
                    var variadicCtx = paramList.variadicParameter();
                    var variadicName = variadicCtx.IDENTIFIER().GetText();
                    // Variadic parameters have opaque type for now (we'll handle type checking later)
                    var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                    function.Parameters.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
                    function.IsVariadic = true;
                }
            }

            _module.AddFunction(function);
        }

        // Pass 4.5: Collect impl block method signatures
        foreach (var implContext in context.implDeclaration())
        {
            // IMPORTANT: Extract generic parameters FIRST before parsing trait type args
            // This ensures that 'T' is in scope when parsing 'Iterable<T>'
            var genericParams = new List<string>();
            if (implContext.genericParams() != null)
            {
                foreach (var paramId in implContext.genericParams().IDENTIFIER())
                {
                    var paramName = paramId.GetText();
                    genericParams.Add(paramName);
                    _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
                }
            }

            // Determine if this is a trait impl or inherent impl
            bool isTraitImpl = implContext.KW_FOR() != null;
            string? traitName = null;
            List<IrType> traitTypeArgs = new();

            // Extract implementing type name and type
            string typeName;
            IrType? implementingType = null;

            if (isTraitImpl)
            {
                // Format: impl [<GenericParams>] TraitName<TraitArgs> for TargetType<TypeArgs>
                // traitTypeName is the trait being implemented
                traitName = implContext.traitTypeName.IDENTIFIER(0).GetText();

                // Parse trait type arguments if present (e.g., From<DosError>)
                if (implContext.traitTypeArgs != null)
                {
                    var typeList = implContext.traitTypeArgs.typeList();
                    foreach (var typeCtx in typeList.type())
                    {
                        traitTypeArgs.Add(ParseType(typeCtx));
                    }
                }

                // implTargetType is the type receiving the implementation
                var targetTypeCtx = implContext.implTargetType();

                if (targetTypeCtx is NovusParser.PrimitiveImplTargetContext primitiveCtx)
                {
                    // impl Trait for i32, bool, etc.
                    var primitiveTypeNameCtx = primitiveCtx.primitiveTypeName();
                    typeName = primitiveTypeNameCtx.GetText().ToLowerInvariant();
                    implementingType = MapPrimitiveTypeName(typeName);
                }
                else if (targetTypeCtx is NovusParser.NamedImplTargetContext namedCtx)
                {
                    // impl Trait for MyType<T>
                    typeName = namedCtx.typeName().IDENTIFIER(0).GetText();

                    // Look up the implementing type (could be struct or enum)
                    var structType = _symbols.LookupStruct(typeName);
                    var enumType = _symbols.LookupEnum(typeName);

                    if (structType != null)
                    {
                        implementingType = structType;
                    }
                    else if (enumType != null)
                    {
                        implementingType = enumType;
                    }
                    else
                    {
                        var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.TypeNotFound,
                            $"Type '{typeName}' not found for impl block",
                            errorLocation
                        );
                        return null;
                    }
                }
                else
                {
                    throw new CompilerBugException(
                        $"Unknown impl target type context: {targetTypeCtx?.GetType().Name}",
                        "ProcessModuleDeclarations - impl block processing",
                        _inputFilePath,
                        null
                    );
                }
            }
            else
            {
                // Format: impl [<GenericParams>] TargetType<TypeArgs>
                // targetTypeName is the type receiving inherent methods
                typeName = implContext.targetTypeName.IDENTIFIER(0).GetText();

                // Look up the implementing type (could be struct or enum)
                var structType = _symbols.LookupStruct(typeName);
                var enumType = _symbols.LookupEnum(typeName);

                if (structType != null)
                {
                    implementingType = structType;
                }
                else if (enumType != null)
                {
                    implementingType = enumType;
                }
                else
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.TypeNotFound,
                        $"Type '{typeName}' not found for impl block",
                        errorLocation
                    );
                    return null;
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
                    StoreGenericMethodTemplate(typeName, methodName, genericParams, funcDecl);
                    // Don't create function yet - it will be instantiated when called with concrete types
                    continue;
                }

                // Non-generic impl blocks: create function signatures now
                var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;

                // Check for extern, pub, and internal keywords
                var (visibility, isExtern, _) = AstModifierHelper.ParseModifiers(funcDecl, 4);

                // Methods are registered with mangled names
                var mangledName = GenerateMethodMangledName(typeName, methodName, isTraitImpl, traitName, traitTypeArgs);

                var function = new IrFunction(mangledName, returnType, visibility, isExtern);

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
                    return null;
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
                return null;
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
            if (!CurrentBlockHasTerminator())
            {
                if (_currentFunction.ReturnType is not IrVoidType && lastValue != null)
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
            string typeName;
            IrType? implementingType = null;

            if (isTraitImpl)
            {
                // Format: impl TraitName<TraitArgs> for TargetType
                // traitTypeName is the trait being implemented
                traitName = implContext.traitTypeName.IDENTIFIER(0).GetText();

                // Parse trait type arguments if present (e.g., From<DosError>)
                if (implContext.traitTypeArgs != null)
                {
                    var typeList = implContext.traitTypeArgs.typeList();
                    foreach (var typeCtx in typeList.type())
                    {
                        traitTypeArgs.Add(ParseType(typeCtx));
                    }
                }

                // implTargetType is the type receiving the implementation
                var targetTypeCtx = implContext.implTargetType();

                if (targetTypeCtx is NovusParser.PrimitiveImplTargetContext primitiveCtx)
                {
                    // impl Trait for i32, bool, etc.
                    var primitiveTypeNameCtx = primitiveCtx.primitiveTypeName();
                    typeName = primitiveTypeNameCtx.GetText().ToLowerInvariant();
                    implementingType = MapPrimitiveTypeName(typeName);
                }
                else if (targetTypeCtx is NovusParser.NamedImplTargetContext namedCtx)
                {
                    // impl Trait for MyType
                    typeName = namedCtx.typeName().IDENTIFIER(0).GetText();

                    // Look up the implementing type (could be struct or enum)
                    var structType = _symbols.LookupStruct(typeName);
                    var enumType = _symbols.LookupEnum(typeName);

                    if (structType != null)
                    {
                        implementingType = structType;
                    }
                    else if (enumType != null)
                    {
                        implementingType = enumType;
                    }
                    else
                    {
                        var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.TypeNotFound,
                            $"Type '{typeName}' not found for impl block",
                            errorLocation
                        );
                        return null;
                    }
                }
                else
                {
                    throw new CompilerBugException(
                        $"Unknown impl target type context: {targetTypeCtx?.GetType().Name}",
                        "ProcessModuleDeclarations Pass 6 - impl block processing",
                        _inputFilePath,
                        null
                    );
                }
            }
            else
            {
                // Format: impl TargetType
                // targetTypeName is the type receiving inherent methods
                typeName = implContext.targetTypeName.IDENTIFIER(0).GetText();

                // Look up the implementing type (could be struct or enum)
                var structType = _symbols.LookupStruct(typeName);
                var enumType = _symbols.LookupEnum(typeName);

                if (structType != null)
                {
                    implementingType = structType;
                }
                else if (enumType != null)
                {
                    implementingType = enumType;
                }
                else
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.TypeNotFound,
                        $"Type '{typeName}' not found for impl block",
                        errorLocation
                    );
                    return null;
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
                    return null;
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

                // Visit method body
                var lastValue = Visit(funcDecl.block()) as IrValue;

                // Add implicit return if block doesn't already have a terminator
                if (!CurrentBlockHasTerminator())
                {
                    if (_currentFunction.ReturnType is not IrVoidType && lastValue != null)
                    {
                        _currentBlock!.AddInstruction(new IrReturn(lastValue));
                    }
                    else
                    {
                        _currentBlock!.AddInstruction(new IrReturn(null));
                    }
                }
            }

            // Clear Self type after processing impl block
            _currentSelfType = null;
        }

        return _module;
    }

    private void ProcessImport(NovusParser.ImportDeclarationContext context)
    {
        // Get the module path (e.g., "std::dos" or "std::ffi::exec")
        var modulePath = context.modulePath().GetText();

        // Get the list of names to import
        var importList = context.importList();
        bool importAll = importList.GetText() == "*";

        ImportModule(modulePath, importAll, importList);
    }

    private void ImportModuleSpecificSymbols(string moduleNamespace, List<string> symbolNames)
    {
        // Build a pseudo import list that contains the specific symbols
        // We can't create a real ImportListContext without the parser, so we'll
        // pass the symbol names another way
        // For now, recursively call ImportModule for each symbol individually
        foreach (var symbolName in symbolNames)
        {
            // Parse the module to get the symbols
            string modulePath = ModuleImportHelper.ResolveModulePath(moduleNamespace, _stdLibPath);
            var (moduleContext, syntaxErrors) = ModuleImportHelper.ParseModuleFile(modulePath);

            if (moduleContext == null || syntaxErrors > 0)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.ModuleNotFound,
                    $"Module '{moduleNamespace}' not found or has syntax errors",
                    errorLocation
                );
                return;
            }

            // IMPORTANT: Process the module's own reexports first
            // This ensures that types used by the symbol we're importing are available
            foreach (var reexportDecl in moduleContext.reexportDeclaration())
            {
                var reexportPath = reexportDecl.modulePath().GetText();
                var reexportText = reexportDecl.GetText();
                if (reexportText.EndsWith("::*"))
                {
                    ImportModule(reexportPath, importAll: true, importList: null);
                }
                else
                {
                    var reexportList = reexportDecl.reexportList();
                    if (reexportList != null)
                    {
                        var reexportSymbols = new List<string>();
                        foreach (var id in reexportList.IDENTIFIER())
                        {
                            reexportSymbols.Add(id.GetText());
                        }
                        ImportModuleSpecificSymbols(reexportPath, reexportSymbols);
                    }
                }
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
            // Check traits
            foreach (var traitDecl in moduleContext.traitDeclaration())
            {
                if (traitDecl.IDENTIFIER().GetText() == symbolName)
                {
                    RegisterTrait(traitDecl);
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

    private void ImportModule(string moduleNamespace, bool importAll, NovusParser.ImportListContext? importList = null)
    {
        // Convert namespace path to file path
        string modulePath = ModuleImportHelper.ResolveModulePath(moduleNamespace, _stdLibPath);

        // Load and parse the module first to check if it needs compilation
        var (moduleContext, syntaxErrors) = ModuleImportHelper.ParseModuleFile(modulePath);

        if (moduleContext == null || syntaxErrors > 0)
        {
            var errorLocation = importList != null
                ? SourceLocationHelper.FromContext(importList, _inputFilePath, _sourceLines.ToArray())
                : new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.ModuleNotFound,
                $"Module '{moduleNamespace}' not found at {modulePath} or has syntax errors",
                errorLocation
            );
            return;
        }

        // Check if module has already been fully processed
        bool alreadyProcessed = _processedModules.Contains(moduleNamespace);


        if (alreadyProcessed)
        {
            // Even if module is already processed, we still need to handle selective imports
            // This allows: from std::ffi::dos import SystemTagList
            //         AND: from std::ffi::dos import IoErr
            // Both imports from the same module
            if (!importAll && importList != null)
            {
                // Build the list of names to import for this specific import statement
                var selectiveImports = ModuleImportHelper.BuildImportNameSet(moduleContext, importAll, importList);

                // CRITICAL: Register ALL type stubs FIRST before parsing any signatures
                // Function signatures, struct fields, and impl blocks all need types to be registered

                // Step 1: Register ALL enum stubs (not just selective imports)
                RegisterAllEnumStubsForImport(moduleContext);

                // Step 2: Register ALL struct placeholders (not just selective imports)
                RegisterAllStructPlaceholdersForImport(moduleContext);

                // Step 3: Fill in enum variants for selective imports only
                FillEnumVariantsForImport(moduleContext, selectiveImports);

                // Register functions from the already-parsed module
                // At this point, all type stubs are registered so function signatures can reference any type
                foreach (var funcDecl in moduleContext.functionDeclaration())
                {
                    var funcName = funcDecl.IDENTIFIER().GetText();
                    if (selectiveImports.Contains(funcName))
                    {
                        // Check if not already imported
                        if (!_module.Functions.Any(f => f.Name == funcName))
                        {
                            // Parse and add the function
                            var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;

                            var (visibility, isExtern, _) = AstModifierHelper.ParseModifiers(funcDecl, 4);

                            var function = new IrFunction(funcName, returnType, visibility, isExtern);

                            // Parse parameters
                            ParseFunctionParameters(funcDecl, function);

                            _module.AddFunction(function);
                        }
                    }
                }

                // Register constants
                RegisterConstantsForImport(moduleContext, selectiveImports);

                // Register structs (with dependency expansion)
                // First, expand selective imports to include struct dependencies
                var expandedStructImports = ExpandStructDependencies(moduleContext, selectiveImports);

                // Register placeholder structs
                RegisterStructPlaceholdersForImport(moduleContext, expandedStructImports);

                // Fill in struct fields
                // At this point, enum stubs are registered so struct fields can reference enums
                FillStructFieldsForImport(moduleContext, expandedStructImports);

                // Register traits
                RegisterTraitsForImport(moduleContext, selectiveImports);
            }

            return; // Don't reprocess the entire module
        }

        // Mark this module as being processed
        _processedModules.Add(moduleNamespace);

        // Check if this module has any pub (non-extern) functions that need compilation
        // FFI modules (only extern functions) don't need to be compiled separately
        bool hasImplementation = ModuleImportHelper.CheckHasImplementation(moduleContext);

        // Track this module for compilation only if it has real implementations
        // (avoid duplicates)
        if (hasImplementation && !_importedModulePaths.Contains(modulePath))
        {
            _importedModulePaths.Add(modulePath);
        }

        // Note: We need to process the module's imports to make constants available for generic templates
        // This is safe because _processedModules prevents circular dependencies
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
                ImportModule(reexportPath, importAll: true, importList: null);
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
                    // Create a fake import list context with these names
                    // We need to import these symbols so they're available when parsing function signatures
                    ImportModuleSpecificSymbols(reexportPath, symbolNames);
                }
            }
        }

        // Build the list of names to import
        var namesToImport = ModuleImportHelper.BuildImportNameSet(moduleContext, importAll, importList);


        // CRITICAL PHASE 1: Register ALL type stubs BEFORE parsing any signatures
        // This is essential because impl block method signatures may reference ANY type from the module,
        // even types not being explicitly imported. For example:
        //   - User imports Str (struct) from std::strings
        //   - Str has methods that return Result<Str, StringError>
        //   - StringError enum must be resolvable during impl block processing
        //   - Similarly, String struct may be referenced even if not imported

        // Step 1a: Register ALL enum stubs from the module (not just imported ones)
        RegisterAllEnumStubsForImport(moduleContext);

        // Step 1b: Register ALL struct placeholders from the module (not just imported ones)
        RegisterAllStructPlaceholdersForImport(moduleContext);

        // CRITICAL PHASE 2: Fill in type details for explicitly imported types only

        // Step 2a: Fill in enum variants for imported enums only
        FillEnumVariantsForImport(moduleContext, namesToImport);

        // Step 2b: Register imported constants
        RegisterConstantsForImport(moduleContext, namesToImport);

        // Step 2c: Expand struct import list to include dependencies and fill in fields
        // When importing NewScreen, we also need to import TextAttr and BitMap that it references
        var expandedStructNames = ExpandStructDependencies(moduleContext, namesToImport);

        // Fill in struct fields for expanded struct list
        // At this point, all type names (enums + structs) are resolvable for field type parsing
        FillStructFieldsForImport(moduleContext, expandedStructNames);

        // Register imported traits in the module
        RegisterTraitsForImport(moduleContext, namesToImport);

        // Register imported functions in the module
        RegisterFunctionsForImport(moduleContext, namesToImport, moduleNamespace);

        // Register imported impl block methods in the module
        foreach (var implDecl in moduleContext.implDeclaration())
        {
            // Handle generic parameters if present (e.g., impl<T> Vec<T>)
            var genericParams = new List<string>();
            if (implDecl.genericParams() != null)
            {
                foreach (var paramId in implDecl.genericParams().IDENTIFIER())
                {
                    var paramName = paramId.GetText();
                    genericParams.Add(paramName);
                    _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
                }
            }

            // Determine if this is a trait impl or inherent impl
            bool isTraitImpl = implDecl.KW_FOR() != null;
            string? traitName = null;
            List<IrType> traitTypeArgs = new();

            // Extract implementing type name
            string typeName;
            IrType? implementingType = null;

            if (isTraitImpl)
            {
                // Format: impl [<GenericParams>] TraitName<TraitArgs> for TargetType
                // traitTypeName is the trait being implemented
                traitName = implDecl.traitTypeName.IDENTIFIER(0).GetText();

                // Parse trait type arguments if present (e.g., From<DosError>)
                if (implDecl.traitTypeArgs != null)
                {
                    var typeList = implDecl.traitTypeArgs.typeList();
                    foreach (var typeCtx in typeList.type())
                    {
                        traitTypeArgs.Add(ParseType(typeCtx));
                    }
                }

                // implTargetType is the type receiving the implementation
                var targetTypeCtx = implDecl.implTargetType();

                if (targetTypeCtx is NovusParser.PrimitiveImplTargetContext primitiveCtx)
                {
                    // impl Trait for i32, bool, etc.
                    var primitiveTypeNameCtx = primitiveCtx.primitiveTypeName();
                    typeName = primitiveTypeNameCtx.GetText().ToLowerInvariant();
                    implementingType = MapPrimitiveTypeName(typeName);
                }
                else if (targetTypeCtx is NovusParser.NamedImplTargetContext namedCtx)
                {
                    // impl Trait for MyType
                    typeName = namedCtx.typeName().IDENTIFIER(0).GetText();

                    // Look up the implementing type (could be struct or enum)
                    var structType = _symbols.LookupStruct(typeName);
                    var enumType = _symbols.LookupEnum(typeName);

                    if (structType != null)
                    {
                        implementingType = structType;
                    }
                    else if (enumType != null)
                    {
                        implementingType = enumType;
                    }
                    // Will check for null below
                }
                else
                {
                    throw new CompilerBugException(
                        $"Unknown impl target type context: {targetTypeCtx?.GetType().Name}",
                        "ImportModule Pass 7 - impl block processing",
                        _inputFilePath,
                        null
                    );
                }
            }
            else
            {
                // Format: impl [<GenericParams>] TargetType
                // targetTypeName is the type receiving inherent methods
                typeName = implDecl.targetTypeName.IDENTIFIER(0).GetText();

                // Look up the implementing type (could be struct or enum)
                var structType = _symbols.LookupStruct(typeName);
                var enumType = _symbols.LookupEnum(typeName);

                if (structType != null)
                {
                    implementingType = structType;
                }
                else if (enumType != null)
                {
                    implementingType = enumType;
                }
                // Will check for null below
            }

            // Skip if the type this impl is for is not in the import list
            // This prevents importing methods for types we don't have access to
            // However, ALWAYS allow impl blocks for primitive types (i8, i16, i32, u8, etc.)
            // because primitives are universally available and their trait impls should be imported
            bool isPrimitiveType = typeName is "i8" or "i16" or "i32" or "i64" or
                                                "u8" or "u16" or "u32" or "u64" or
                                                "bool" or "f32" or "f64";

            if (!isPrimitiveType && !namesToImport.Contains(typeName))
            {
                _symbols.ClearGenericParameters();
                continue;
            }

            // Skip if implementing type not found (type not imported or not registered yet)
            if (implementingType == null)
            {
                // Clear generic params before skipping
                _symbols.ClearGenericParameters();
                continue;
            }

            _currentSelfType = implementingType;

            foreach (var implItem in implDecl.implItem())
            {
                var funcDecl = implItem.functionDeclaration();
                if (funcDecl == null) continue;

                var methodName = funcDecl.IDENTIFIER().GetText();

                // Check if method is pub
                var isPub = AstModifierHelper.HasModifier(funcDecl, "pub", 3);

                // For trait implementations, methods are implicitly public since they implement
                // a public trait method, even if not explicitly marked `pub`
                if (isTraitImpl)
                {
                    isPub = true;
                }

                // For generic impl blocks, store ALL methods as templates (pub and private)
                // because instantiating one method may need to call private helper methods
                if (genericParams.Count > 0)
                {
                    StoreGenericMethodTemplate(typeName, methodName, genericParams, funcDecl);
                    // Don't create function yet - it will be instantiated when called with concrete types
                    continue;
                }

                // Only import pub methods for non-generic impl blocks
                if (!isPub)
                {
                    continue;
                }

                // For non-generic impl blocks, create the function normally
                var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;

                // Methods are registered with mangled names
                var mangledName = GenerateMethodMangledName(typeName, methodName, isTraitImpl, traitName, traitTypeArgs);
                var function = new IrFunction(mangledName, returnType, Visibility.Private, false);

                // Parse parameters (including self)
                if (funcDecl.parameterList() != null)
                {
                    var paramList = funcDecl.parameterList();

                    // Handle self parameter if present
                    ParseSelfParameter(paramList.selfParameter(), function, typeName);

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
                if (implementingType == null)
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.TypeNotFound,
                        $"Type '{typeName}' not found for trait implementation",
                        errorLocation
                    );
                    return;
                }

                // Construct full trait name with type arguments (e.g., "From<DosError>")
                var fullTraitName = traitName;
                if (traitTypeArgs.Count > 0)
                {
                    fullTraitName = $"{traitName}<{string.Join(", ", traitTypeArgs.Select(t => t.Name))}>";
                }

                // Create IrTraitImpl and add to module
                // For generic impls, this is a template that will be instantiated later
                var traitImpl = new IrTraitImpl(fullTraitName, traitTypeArgs, typeName, implementingType, genericParams);
                _module.TraitImpls.Add(traitImpl);
            }

            // Clear generic params and Self type from scope after impl registration
            _symbols.ClearGenericParameters();
            _currentSelfType = null;
        }

        // If we're importing any impl blocks (generic or not), also import all transitive dependencies
        // that the impl methods might need (e.g., AllocMem from std::exec for Vec methods)
        bool hasImplBlocks = moduleContext.implDeclaration().Length > 0;
        if (hasImplBlocks)
        {
            // First, import any extern functions directly declared in this module
            foreach (var funcDecl in moduleContext.functionDeclaration())
            {
                var funcName = funcDecl.IDENTIFIER().GetText();

                // Check if it's an extern function
                bool isExtern = AstModifierHelper.HasModifier(funcDecl, "extern", 3);

                // Only import extern functions (FFI bindings)
                if (!isExtern) continue;

                // Check if we already have this function
                if (_module.Functions.Any(f => f.Name == funcName)) continue;

                // Parse and import the extern function
                var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;
                var function = new IrFunction(funcName, returnType, Visibility.Private, true);

                // Parse parameters
                ParseFunctionParameters(funcDecl, function);

                _module.AddFunction(function);
            }

            // Second, recursively import symbols from FFI modules that this module imports
            // This handles cases like std::core importing from std::ffi::exec
            // Only import from std::ffi::* modules to avoid conflicts with wrapper functions
            foreach (var importDecl in moduleContext.importDeclaration())
            {
                var importPath = importDecl.modulePath().GetText();

                // Only transitively import from std::ffi::* modules (pure FFI bindings)
                if (!importPath.Contains("::ffi::"))
                {
                    continue;
                }

                var importListCtx = importDecl.importList();

                // Import the specific symbols that this module imports
                // These are extern FFI functions that impl methods need
                if (importListCtx != null)
                {
                    ImportModule(importPath, importAll: false, importList: importListCtx);
                }
            }
        }
    }

    /// <summary>
    /// Instantiate a generic method for a monomorphized struct type
    /// E.g., instantiate Vec<T>::push as Vec<i32>::push
    /// </summary>
    private IrFunction? InstantiateGenericMethod(IrStructType monomorphizedStruct, string methodName)
    {
        var baseTypeName = monomorphizedStruct.StructName;
        var templateKey = $"{baseTypeName}::{methodName}";

        // Check if we have a template for this method
        if (!_genericMethodTemplates.TryGetValue(templateKey, out var template))
        {
            return null; // No template found
        }

        var (genericParams, funcDecl, templateConstants) = template;


        // Save current constants and MERGE template constants with current module constants
        // Current module constants take priority (they may include transitive imports)
        var savedConstants = GetConstantsAsTuples();

        // Start with template constants, then overlay current module constants
        // TODO: This is inefficient - should use child scopes instead
        RestoreConstantsFromTuples(templateConstants);
        RestoreConstantsFromTuples(savedConstants);

        // Build instantiation key (e.g., "Vec<i32>::push")
        var instantiationKey = $"{monomorphizedStruct.CacheKey}::{methodName}";

        // Check if already instantiated
        if (_instantiatedMethods.Contains(instantiationKey))
        {
            // Already generated, look it up
            var mangledName = $"{baseTypeName}_{methodName}";
            return _module.Functions.FirstOrDefault(f => f.Name == mangledName);
        }

        // Build type substitution map from monomorphized struct
        var typeSubstitutions = new Dictionary<string, IrType>();
        var baseStruct = _symbols.LookupStruct(baseTypeName);
        if (baseStruct == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(ErrorCodes.StructNotFound, $"Struct '{baseTypeName}' not found", errorLocation);
            return null;
        }

        // Scan all fields to find which ones use generic types
        // This handles cases where generics aren't in the first N fields
        for (int i = 0; i < baseStruct.Fields.Count && i < monomorphizedStruct.Fields.Count; i++)
        {
            var baseFieldType = baseStruct.Fields[i].Type;
            var monomorphizedFieldType = monomorphizedStruct.Fields[i].Type;

            // Recursively extract generic type mappings from field types
            ExtractGenericTypeMapping(baseFieldType, monomorphizedFieldType, typeSubstitutions);
        }

        // Verify all generic parameters were resolved
        foreach (var genericParam in baseStruct.GenericParameters)
        {
            if (!typeSubstitutions.ContainsKey(genericParam))
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.GenericParameterNotFound,
                    $"Generic parameter '{genericParam}' not found in monomorphized struct {monomorphizedStruct.CacheKey}",
                    errorLocation
                );
                return null;
            }
        }

        // Set up concrete types for substitution during parsing
        var savedGenericParams = new Dictionary<string, IrGenericType>();
        foreach (var paramName in genericParams)
        {
            if (_symbols.HasGenericParameter(paramName))
            {
                var genericParam = _symbols.LookupGenericParameter(paramName);
                if (genericParam != null) savedGenericParams[paramName] = genericParam;
            }
            _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
        }

        // Set active type substitutions for the duration of this instantiation
        var savedSubstitutions = _currentTypeSubstitutions;
        _currentTypeSubstitutions = typeSubstitutions;

        // Set Self type to the monomorphized struct for Self type resolution
        var savedSelfType = _currentSelfType;
        _currentSelfType = monomorphizedStruct;

        // Create the function
        var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;

        // Substitute generic types in return type
        returnType = SubstituteGenericTypes(returnType, typeSubstitutions);

        var mangledMethodName = $"{baseTypeName}_{methodName}";
        var function = new IrFunction(mangledMethodName, returnType, Visibility.Private, false);

        // Parse parameters with substitutions
        if (funcDecl.parameterList() != null)
        {
            var paramList = funcDecl.parameterList();

            // Handle self parameter
            ParseSelfParameter(paramList.selfParameter(), function, monomorphizedStruct);

            // Add regular parameters - need to substitute generic types
            foreach (var paramCtx in paramList.parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                var paramType = ParseType(paramCtx.type());

                // Substitute generic types recursively
                paramType = SubstituteGenericTypes(paramType, typeSubstitutions);

                function.Parameters.Add(new IrParameter(paramName, paramType));
            }

            // Add variadic parameter if present
            if (paramList.variadicParameter() != null)
            {
                var variadicCtx = paramList.variadicParameter();
                var variadicName = variadicCtx.IDENTIFIER().GetText();
                // Variadic parameters have opaque type for now (we'll handle type checking later)
                var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                function.Parameters.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
                function.IsVariadic = true;
            }
        }

        _module.AddFunction(function);

        // Build the function body - save all state to avoid corrupting caller
        var savedFunction = _currentFunction;
        var savedBlock = _currentBlock;
        var savedLocalVars = new Dictionary<string, IrLocalVariable>(_localVariables);

        _currentFunction = function;
        _localVariables.Clear();

        // Add parameters to local variables
        foreach (var param in function.Parameters)
        {
            _localVariables[param.Name] = new IrLocalVariable(param.Name, param.Type, false);
        }

        // Create entry block
        var entryBlock = new IrBasicBlock("entry");
        function.BasicBlocks.Add(entryBlock);
        _currentBlock = entryBlock;

        // Visit the function body with type substitutions active
        if (funcDecl.block() != null)
        {
            Visit(funcDecl.block());
        }

        // Restore all state
        _currentFunction = savedFunction;
        _currentBlock = savedBlock;
        _localVariables.Clear();
        foreach (var kvp in savedLocalVars)
        {
            _localVariables[kvp.Key] = kvp.Value;
        }

        // Restore type substitutions
        _currentTypeSubstitutions = savedSubstitutions;

        // Restore Self type
        _currentSelfType = savedSelfType;

        // Restore constants
        // TODO: Implement proper scope save/restore in SymbolTable
        RestoreConstantsFromTuples(savedConstants);

        // Clear generic params
        foreach (var paramName in typeSubstitutions.Keys)
        {
            _symbols.ClearGenericParameters();
        }

        // Mark as instantiated
        _instantiatedMethods.Add(instantiationKey);

        return function;
    }

    private IrFunction? InstantiateGenericEnumMethod(IrEnumType enumType, string methodName, List<IrValue> arguments)
    {
        var baseTypeName = enumType.EnumName;
        var templateKey = $"{baseTypeName}::{methodName}";


        // Check if we have a template for this method
        if (!_genericMethodTemplates.TryGetValue(templateKey, out var template))
        {
            return null; // No template found
        }

        var (genericParams, funcDecl, templateConstants) = template;

        // Register generic parameters temporarily so ParseType can find them
        var savedGenericParams = new Dictionary<string, IrGenericType>();
        foreach (var paramName in genericParams)
        {
            if (_symbols.HasGenericParameter(paramName))
            {
                var genericParam = _symbols.LookupGenericParameter(paramName);
                if (genericParam != null) savedGenericParams[paramName] = genericParam;
            }
            _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
        }

        // Infer type substitutions from arguments
        // First, parse the template to get parameter types
        var templateParams = new List<IrParameter>();
        if (funcDecl.parameterList() != null)
        {
            var paramList = funcDecl.parameterList();
            foreach (var paramCtx in paramList.parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                var savedSubstitutions = _currentTypeSubstitutions;
                _currentTypeSubstitutions = null; // Parse without substitutions to get generic types
                var paramType = ParseType(paramCtx.type());
                _currentTypeSubstitutions = savedSubstitutions;
                templateParams.Add(new IrParameter(paramName, paramType));
            }

            // Add variadic parameter if present (for template analysis)
            if (paramList.variadicParameter() != null)
            {
                var variadicCtx = paramList.variadicParameter();
                var variadicName = variadicCtx.IDENTIFIER().GetText();
                // Variadic parameters have opaque type for now (we'll handle type checking later)
                var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                templateParams.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
            }
        }

        // Build type substitution map from monomorphized enum (same approach as structs)
        var typeSubstitutions = new Dictionary<string, IrType>();
        var baseEnum = _symbols.LookupEnum(baseTypeName);
        if (baseEnum == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.EnumNotFound,
                $"Enum '{baseTypeName}' not found",
                errorLocation
            );
            return null;
        }

        // Extract type mappings by comparing base enum variants with monomorphized enum variants
        // For example: Option<T> vs Option<i32> should extract T -> i32
        // Use ExtractGenericTypeMapping helper for cleaner, recursive type extraction
        if (enumType.CacheKey != null) // enum is monomorphized (e.g., Option<i32>)
        {
            for (int varIdx = 0; varIdx < baseEnum.Variants.Count && varIdx < enumType.Variants.Count; varIdx++)
            {
                var baseVariant = baseEnum.Variants[varIdx];
                var monoVariant = enumType.Variants[varIdx];

                if (baseVariant.Name == monoVariant.Name &&
                    baseVariant.AssociatedData.Count == monoVariant.AssociatedData.Count)
                {
                    for (int dataIdx = 0; dataIdx < baseVariant.AssociatedData.Count; dataIdx++)
                    {
                        ExtractGenericTypeMapping(baseVariant.AssociatedData[dataIdx], monoVariant.AssociatedData[dataIdx], typeSubstitutions);
                    }
                }

            }
        }

        // Verify all generic parameters were resolved
        foreach (var genericParam in baseEnum.GenericParameters)
        {
            if (!typeSubstitutions.ContainsKey(genericParam))
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.GenericParameterNotFound,
                    $"Generic parameter '{genericParam}' not found in monomorphized enum {enumType.CacheKey ?? enumType.EnumName}",
                    errorLocation
                );
                return null;
            }
        }

        // Use the already-monomorphized enum (no need to monomorphize again)
        // enumType is already monomorphized (e.g., Option<i32>) since it came from the call site

        // Build instantiation key
        var instantiationKey = $"{enumType.CacheKey}::{methodName}";

        // Check if already instantiated
        if (_instantiatedMethods.Contains(instantiationKey))
        {
            // Already generated, look it up
            var cachedTypeArgKeys = genericParams.Select(p => GetTypeCacheKey(typeSubstitutions[p]));
            var cachedMangledName = $"{baseTypeName}::{methodName}_{string.Join("_", cachedTypeArgKeys.Select(k => k.Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace("*", "ptr_")))}";
            return _module.Functions.FirstOrDefault(f => f.Name == cachedMangledName);
        }

        // Save current state
        var savedConstants = GetConstantsAsTuples();
        RestoreConstantsFromTuples(templateConstants);
        RestoreConstantsFromTuples(savedConstants);

        // Generic params already registered from earlier - just set up type substitutions
        var savedTypeSubstitutions = _currentTypeSubstitutions;
        _currentTypeSubstitutions = typeSubstitutions;

        // Set Self type to the monomorphized enum for Self type resolution
        var savedSelfType = _currentSelfType;
        _currentSelfType = enumType;

        // Create the function manually (don't use Visit)
        var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;
        returnType = SubstituteGenericTypes(returnType, typeSubstitutions);

        // Create mangled name from type arguments
        var typeArgKeys = genericParams.Select(p => GetTypeCacheKey(typeSubstitutions[p]));
        var mangledName = $"{baseTypeName}::{methodName}_{string.Join("_", typeArgKeys.Select(k => k.Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace("*", "ptr_")))}";

        var function = new IrFunction(mangledName, returnType, Visibility.Private, false);

        // Parse parameters with substitutions
        if (funcDecl.parameterList() != null)
        {
            var paramList = funcDecl.parameterList();

            // Handle self parameter if present
            if (paramList.selfParameter() != null)
            {
                ParseSelfParameter(paramList.selfParameter(), function, enumType);
            }

            // Add regular parameters
            foreach (var paramCtx in paramList.parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                var paramType = ParseType(paramCtx.type());
                paramType = SubstituteGenericTypes(paramType, typeSubstitutions);
                function.Parameters.Add(new IrParameter(paramName, paramType));
            }

            // Add variadic parameter if present
            if (paramList.variadicParameter() != null)
            {
                var variadicCtx = paramList.variadicParameter();
                var variadicName = variadicCtx.IDENTIFIER().GetText();
                // Variadic parameters have opaque type for now (we'll handle type checking later)
                var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                function.Parameters.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
                function.IsVariadic = true;
            }
        }

        // Check if function already exists in module (could be from import or previous instantiation)
        var existingFunc = _module.Functions.FirstOrDefault(f => f.Name == mangledName);
        if (existingFunc != null)
        {
            // Already exists, return it
            return existingFunc;
        }

        _module.AddFunction(function);

        // Build function body
        var savedFunction = _currentFunction;
        _currentFunction = function;
        var entryBlock = new IrBasicBlock("entry");
        function.BasicBlocks.Add(entryBlock);
        var savedBlock = _currentBlock;
        _currentBlock = entryBlock;

        // Save and add parameters to local variables scope
        var savedLocalVars = new Dictionary<string, IrLocalVariable>(_localVariables);
        foreach (var param in function.Parameters)
        {
            _localVariables[param.Name] = new IrLocalVariable(param.Name, param.Type, false);
        }

        // Visit the function body
        if (funcDecl.block() != null)
        {
            Visit(funcDecl.block());
        }

        // Restore local variables
        _localVariables.Clear();
        foreach (var kvp in savedLocalVars)
        {
            _localVariables[kvp.Key] = kvp.Value;
        }

        // Restore state
        _currentBlock = savedBlock;
        _currentFunction = savedFunction;
        _currentTypeSubstitutions = savedTypeSubstitutions;
        _currentSelfType = savedSelfType;
        RestoreConstantsFromTuples(savedConstants);
        foreach (var paramName in typeSubstitutions.Keys)
        {
            _symbols.ClearGenericParameters();
        }
        foreach (var kvp in savedGenericParams)
        {
            _symbols.RegisterGenericParameter(kvp.Key, kvp.Value);
        }

        _instantiatedMethods.Add(instantiationKey);

        return function;
    }

    private IrEnumType? MonomorphizeEnum(IrEnumType enumType, Dictionary<string, IrType> typeSubstitutions)
    {
        // Build cache key
        var typeArgKeys = enumType.GenericParameters.Select(p =>
        {
            var key = typeSubstitutions.ContainsKey(p) ? GetTypeCacheKey(typeSubstitutions[p]) : p;
            return key;
        });
        var cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";

        // Check cache
        if (_symbols.LookupMonomorphizedEnum(cacheKey) != null)
        {
            return _symbols.LookupMonomorphizedEnum(cacheKey)!;
        }

        // Create monomorphized variants
        var monomorphizedVariants = new List<IrEnumVariant>();
        foreach (var variant in enumType.Variants)
        {
            var monomorphizedData = new List<IrType>();
            foreach (var dataType in variant.AssociatedData)
            {
                monomorphizedData.Add(SubstituteType(dataType, typeSubstitutions));
            }
            monomorphizedVariants.Add(new IrEnumVariant(variant.Name, variant.Tag, monomorphizedData));
        }

        var monomorphizedEnum = new IrEnumType(enumType.EnumName, monomorphizedVariants, null, cacheKey);
        _symbols.RegisterMonomorphizedEnum(cacheKey, monomorphizedEnum);

        return monomorphizedEnum;
    }

    private IrType SubstituteType(IrType type, Dictionary<string, IrType> substitutions)
    {
        if (type is IrGenericType gt && substitutions.ContainsKey(gt.ParameterName))
        {
            return substitutions[gt.ParameterName];
        }

        if (type is IrPointerType ptrType)
        {
            var substitutedPointee = SubstituteType(ptrType.PointeeType, substitutions);
            if (substitutedPointee != ptrType.PointeeType)
            {
                return _typeInterner.GetPointerType(substitutedPointee);
            }
            return ptrType;
        }

        if (type is IrEnumType enumType)
        {
            // If the enum has generic parameters, monomorphize it
            if (enumType.GenericParameters.Count > 0)
            {
                // Check if any of the enum's generic parameters need substitution
                bool needsSubstitution = enumType.GenericParameters.Any(p => substitutions.ContainsKey(p));
                if (needsSubstitution)
                {
                    return MonomorphizeEnum(enumType, substitutions);
                }
            }
            // Already monomorphized or no generic parameters
            return enumType;
        }

        // For other types, return as-is
        return type;
    }

    /// <summary>
    /// Infer generic type arguments for a generic function from call site arguments
    /// </summary>
    private Dictionary<string, IrType>? InferGenericFunctionTypes(List<string> genericParams, List<IrParameter> templateParams, List<IrValue> arguments)
    {
        if (arguments.Count != templateParams.Count)
        {
            return null; // Argument count mismatch
        }

        var typeSubstitutions = new Dictionary<string, IrType>();

        // Match each argument to its parameter and extract type mappings
        for (int i = 0; i < arguments.Count; i++)
        {
            var argType = arguments[i].Type;
            var paramType = templateParams[i].Type;

            // Recursively extract generic type mappings
            ExtractGenericTypeMapping(paramType, argType, typeSubstitutions);
        }

        // Verify all generic parameters were resolved
        foreach (var genericParam in genericParams)
        {
            if (!typeSubstitutions.ContainsKey(genericParam))
            {
                return null; // Could not infer all type parameters
            }
        }

        return typeSubstitutions;
    }

    /// <summary>
    /// Infer generic type arguments for an enum associated function from call site
    /// Handles both argument-based inference and return-type-based inference
    /// Example: Option::FromPointer(ptr: *u8) should infer T=u8
    /// </summary>
    private Dictionary<string, IrType>? InferGenericEnumTypeArguments(
        IrEnumType baseEnum,
        string methodName,
        List<IrValue> arguments,
        IrType? expectedReturnType)
    {
        // Look up the method template to get parameter types
        var templateKey = $"{baseEnum.EnumName}::{methodName}";
        if (!_genericMethodTemplates.TryGetValue(templateKey, out var template))
        {
            return null; // No template found
        }

        var (genericParams, funcDecl, _) = template;

        // Register generic parameters temporarily so we can parse the template
        var savedGenericParams = new Dictionary<string, IrGenericType>();
        foreach (var paramName in genericParams)
        {
            if (_symbols.HasGenericParameter(paramName))
            {
                var genericParam = _symbols.LookupGenericParameter(paramName);
                if (genericParam != null) savedGenericParams[paramName] = genericParam;
            }
            _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
        }

        try
        {
            // Parse template parameters to get their generic types
            var templateParams = new List<IrParameter>();
            if (funcDecl.parameterList() != null)
            {
                var paramList = funcDecl.parameterList();

                // Skip self parameter if present (it doesn't contribute to type inference for enum T)
                var regularParams = paramList.parameter();

                foreach (var paramCtx in regularParams)
                {
                    var paramName = paramCtx.IDENTIFIER().GetText();
                    var savedSubstitutions = _currentTypeSubstitutions;
                    _currentTypeSubstitutions = null; // Parse without substitutions to get generic types
                    var paramType = ParseType(paramCtx.type());
                    _currentTypeSubstitutions = savedSubstitutions;
                    templateParams.Add(new IrParameter(paramName, paramType));
                }
            }

            var typeSubstitutions = new Dictionary<string, IrType>();

            // Step 1: Infer from arguments if available
            if (arguments.Count == templateParams.Count)
            {
                for (int i = 0; i < arguments.Count; i++)
                {
                    var argType = arguments[i].Type;
                    var paramType = templateParams[i].Type;
                    ExtractGenericTypeMapping(paramType, argType, typeSubstitutions);
                }
            }

            // Step 2: Try to infer from expected return type if we still have unresolved generics
            if (expectedReturnType != null && funcDecl.type() != null)
            {
                var savedSubstitutions = _currentTypeSubstitutions;
                _currentTypeSubstitutions = null; // Parse without substitutions
                var templateReturnType = ParseType(funcDecl.type());
                _currentTypeSubstitutions = savedSubstitutions;

                // Extract type mappings from return type
                ExtractGenericTypeMapping(templateReturnType, expectedReturnType, typeSubstitutions);
            }

            // Verify all generic parameters from the enum were resolved
            foreach (var genericParam in baseEnum.GenericParameters)
            {
                if (!typeSubstitutions.ContainsKey(genericParam))
                {
                    return null; // Could not infer all required type parameters
                }
            }

            return typeSubstitutions;
        }
        finally
        {
            // Restore generic parameters
            _symbols.ClearGenericParameters();
            foreach (var kvp in savedGenericParams)
            {
                _symbols.RegisterGenericParameter(kvp.Key, kvp.Value);
            }
        }
    }

    /// <summary>
    /// Build mangled name for instantiated generic function (e.g., "identity_i32")
    /// </summary>
    private string BuildGenericFunctionMangledName(string functionName, Dictionary<string, IrType> typeSubstitutions)
    {
        var mangledName = functionName;
        foreach (var kvp in typeSubstitutions.OrderBy(kv => kv.Key))
        {
            mangledName += "_" + kvp.Value.Name.Replace("*", "ptr").Replace("&", "ref").Replace("[", "arr").Replace("]", "");
        }
        return mangledName;
    }

    /// <summary>
    /// Instantiate a generic function with concrete type arguments
    /// </summary>
    private IrFunction? InstantiateGenericFunction(string functionName, Dictionary<string, IrType> typeSubstitutions)
    {
        // Check if we have a template for this function
        if (!_genericFunctionTemplates.TryGetValue(functionName, out var template))
        {
            return null; // No template found
        }

        var (genericParams, funcDecl, templateConstants) = template;

        // Build instantiation key (e.g., "identity<i32>")
        var instantiationKey = functionName + "<" + string.Join(",", typeSubstitutions.OrderBy(kv => kv.Key).Select(kv => kv.Value.Name)) + ">";

        // Check if already instantiated
        if (_instantiatedGenericFunctions.Contains(instantiationKey))
        {
            // Already generated, look it up
            var existingMangledName = BuildGenericFunctionMangledName(functionName, typeSubstitutions);
            return _module.Functions.FirstOrDefault(f => f.Name == existingMangledName);
        }

        // Save current constants and MERGE template constants with current module constants
        var savedConstants = GetConstantsAsTuples();

        // Start with template constants, then overlay current module constants
        RestoreConstantsFromTuples(templateConstants);
        RestoreConstantsFromTuples(savedConstants);

        // Set up concrete types for substitution during parsing
        var savedGenericParams = new Dictionary<string, IrGenericType>();
        foreach (var paramName in genericParams)
        {
            if (_symbols.HasGenericParameter(paramName))
            {
                var genericParam = _symbols.LookupGenericParameter(paramName);
                if (genericParam != null) savedGenericParams[paramName] = genericParam;
            }
            _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
        }

        // Set active type substitutions for the duration of this instantiation
        var savedSubstitutions = _currentTypeSubstitutions;
        _currentTypeSubstitutions = typeSubstitutions;

        // Create the function with substituted return type
        var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;
        returnType = SubstituteGenericTypes(returnType, typeSubstitutions);

        // Check for pub/internal keywords
        var visibility = Visibility.Private;
        for (int i = 0; i < Math.Min(4, funcDecl.ChildCount); i++)
        {
            var childText = funcDecl.GetChild(i)?.GetText();
            if (childText == "pub") visibility = Visibility.Public;
            if (childText == "internal") visibility = Visibility.Internal;
        }

        var mangledFunctionName = BuildGenericFunctionMangledName(functionName, typeSubstitutions);
        var function = new IrFunction(mangledFunctionName, returnType, visibility, false);

        // Parse parameters with substitutions
        if (funcDecl.parameterList() != null)
        {
            var paramList = funcDecl.parameterList();
            foreach (var paramCtx in paramList.parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                var paramType = ParseType(paramCtx.type());

                // Substitute generic types recursively
                paramType = SubstituteGenericTypes(paramType, typeSubstitutions);

                function.Parameters.Add(new IrParameter(paramName, paramType));
            }

            // Add variadic parameter if present
            if (paramList.variadicParameter() != null)
            {
                var variadicCtx = paramList.variadicParameter();
                var variadicName = variadicCtx.IDENTIFIER().GetText();
                // Variadic parameters have opaque type for now (we'll handle type checking later)
                var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                function.Parameters.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
                function.IsVariadic = true;
            }
        }

        _module.AddFunction(function);

        // Build the function body - save all state to avoid corrupting caller
        var savedFunction = _currentFunction;
        var savedBlock = _currentBlock;
        var savedLocalVars = new Dictionary<string, IrLocalVariable>(_localVariables);

        _currentFunction = function;
        _localVariables.Clear();

        // Add parameters to local variables
        foreach (var param in function.Parameters)
        {
            _localVariables[param.Name] = new IrLocalVariable(param.Name, param.Type, false);
        }

        // Create entry block
        var entryBlock = new IrBasicBlock("entry");
        function.BasicBlocks.Add(entryBlock);
        _currentBlock = entryBlock;

        // Visit the function body with type substitutions active
        if (funcDecl.block() != null)
        {
            Visit(funcDecl.block());
        }

        // Restore all state
        _currentFunction = savedFunction;
        _currentBlock = savedBlock;
        _localVariables.Clear();
        foreach (var kvp in savedLocalVars)
        {
            _localVariables[kvp.Key] = kvp.Value;
        }

        // Restore type substitutions
        _currentTypeSubstitutions = savedSubstitutions;

        // Restore constants
        // TODO: Implement proper scope save/restore in SymbolTable
        RestoreConstantsFromTuples(savedConstants);

        // Restore generic params
        _symbols.ClearGenericParameters();
        foreach (var kvp in savedGenericParams)
        {
            _symbols.RegisterGenericParameter(kvp.Key, kvp.Value);
        }

        // Mark as instantiated
        _instantiatedGenericFunctions.Add(instantiationKey);

        return function;
    }

    /// <summary>
    /// Register ALL enum stubs from a module (not just imported ones).
    /// This is critical because impl block method signatures may reference ANY enum from the module,
    /// even if that enum is not being explicitly imported.
    /// </summary>
    private void RegisterAllEnumStubsForImport(NovusParser.CompilationUnitContext moduleContext)
    {
        foreach (var enumDecl in moduleContext.enumDeclaration())
        {
            var enumName = enumDecl.IDENTIFIER().GetText();

            // Skip if already registered
            if (_symbols.HasEnum(enumName))
            {
                continue;
            }

            // Parse generic parameters for stub so type checking works correctly
            List<string>? genericParams = null;
            if (enumDecl.genericParams() != null)
            {
                genericParams = new List<string>();
                foreach (var paramId in enumDecl.genericParams().IDENTIFIER())
                {
                    genericParams.Add(paramId.GetText());
                }
            }

            // Register the stub enum in symbol table (but NOT in module.Enums yet)
            // The stub will be filled in later only if it's in the import list
            var stubEnum = new IrEnumType(enumName, new List<IrEnumVariant>(), genericParams);
            _symbols.RegisterEnum(enumName, stubEnum);
        }
    }

    private void RegisterEnumStubsForImport(NovusParser.CompilationUnitContext moduleContext, HashSet<string> namesToImport)
    {
        foreach (var enumDecl in moduleContext.enumDeclaration())
        {
            var enumName = enumDecl.IDENTIFIER().GetText();
            if (namesToImport.Contains(enumName))
            {
                if (!_symbols.HasEnum(enumName))
                {
                    // Parse generic parameters for stub so type checking works correctly
                    List<string>? genericParams = null;
                    if (enumDecl.genericParams() != null)
                    {
                        genericParams = new List<string>();
                        foreach (var paramId in enumDecl.genericParams().IDENTIFIER())
                        {
                            genericParams.Add(paramId.GetText());
                        }
                    }
                    var stubEnum = new IrEnumType(enumName, new List<IrEnumVariant>(), genericParams);
                    _symbols.RegisterEnum(enumName, stubEnum);
                }
            }
        }
    }

    private void FillEnumVariantsForImport(NovusParser.CompilationUnitContext moduleContext, HashSet<string> namesToImport)
    {
        foreach (var enumDecl in moduleContext.enumDeclaration())
        {
            var enumName = enumDecl.IDENTIFIER().GetText();
            if (namesToImport.Contains(enumName))
            {
                var existingEnum = _symbols.LookupEnum(enumName);
                if (existingEnum != null && existingEnum.Variants.Count == 0)
                {
                    RegisterEnum(enumDecl);
                }
            }
        }
    }

    private HashSet<string> ExpandStructDependencies(NovusParser.CompilationUnitContext moduleContext, HashSet<string> initialStructNames)
    {
        var expandedStructNames = new HashSet<string>(initialStructNames);
        bool addedNewDependencies;
        do
        {
            addedNewDependencies = false;
            foreach (var structDecl in moduleContext.structDeclaration())
            {
                var structName = structDecl.IDENTIFIER().GetText();
                if (expandedStructNames.Contains(structName))
                {
                    foreach (var fieldCtx in structDecl.structField())
                    {
                        var fieldTypeDeps = ExtractTypeNameDependencies(fieldCtx.type());
                        foreach (var dep in fieldTypeDeps)
                        {
                            if (expandedStructNames.Add(dep))
                            {
                                addedNewDependencies = true;
                            }
                        }
                    }
                }
            }
        } while (addedNewDependencies);
        return expandedStructNames;
    }

    /// <summary>
    /// Register ALL struct placeholders from a module (not just imported ones).
    /// Similar to RegisterAllEnumStubsForImport, this is critical because impl block method signatures
    /// may reference ANY struct from the module, even if that struct is not being explicitly imported.
    /// </summary>
    private void RegisterAllStructPlaceholdersForImport(NovusParser.CompilationUnitContext moduleContext)
    {
        foreach (var structDecl in moduleContext.structDeclaration())
        {
            var structName = structDecl.IDENTIFIER().GetText();

            // Skip if already registered
            if (_symbols.HasStruct(structName))
            {
                continue;
            }

            // Register placeholder struct in symbol table (but NOT in module.Structs yet)
            // The struct will be filled in later only if it's in the import list
            var placeholderStruct = new IrStructType(structName, new List<IrStructField>(), new List<string>(), null, null);
            _symbols.RegisterStruct(structName, placeholderStruct);
        }
    }

    private void RegisterStructPlaceholdersForImport(NovusParser.CompilationUnitContext moduleContext, HashSet<string> expandedStructNames)
    {
        foreach (var structDecl in moduleContext.structDeclaration())
        {
            var structName = structDecl.IDENTIFIER().GetText();
            if (expandedStructNames.Contains(structName))
            {
                if (!_symbols.HasStruct(structName))
                {
                    var placeholderStruct = new IrStructType(structName, new List<IrStructField>(), new List<string>(), null, null);
                    _symbols.RegisterStruct(structName, placeholderStruct);
                }
            }
        }
    }

    private void FillStructFieldsForImport(NovusParser.CompilationUnitContext moduleContext, HashSet<string> expandedStructNames)
    {
        foreach (var structDecl in moduleContext.structDeclaration())
        {
            var structName = structDecl.IDENTIFIER().GetText();
            if (expandedStructNames.Contains(structName))
            {
                var existingStruct = _symbols.LookupStruct(structName);
                if (existingStruct != null && existingStruct.Fields.Count == 0)
                {
                    RegisterStruct(structDecl);
                }
            }
        }
    }

    private void RegisterConstantsForImport(NovusParser.CompilationUnitContext moduleContext, HashSet<string> namesToImport)
    {
        foreach (var constDecl in moduleContext.constDeclaration())
        {
            var constName = constDecl.IDENTIFIER().GetText();
            if (namesToImport.Contains(constName))
            {
                if (!_symbols.HasConstant(constName))
                {
                    RegisterConstant(constDecl);
                }
            }
        }
    }

    private void RegisterTraitsForImport(NovusParser.CompilationUnitContext moduleContext, HashSet<string> namesToImport)
    {
        foreach (var traitDecl in moduleContext.traitDeclaration())
        {
            var traitName = traitDecl.IDENTIFIER().GetText();
            if (namesToImport.Contains(traitName))
            {
                if (!_symbols.HasTrait(traitName))
                {
                    RegisterTrait(traitDecl);
                }
            }
        }
    }

    /// <summary>
    /// Parse function parameters (regular and variadic) and add them to the function.
    /// </summary>
    private void ParseFunctionParameters(NovusParser.FunctionDeclarationContext funcDecl, IrFunction function)
    {
        if (funcDecl.parameterList() == null) return;

        var paramList = funcDecl.parameterList();

        // Add regular parameters
        foreach (var paramCtx in paramList.parameter())
        {
            var paramName = paramCtx.IDENTIFIER().GetText();
            var paramType = ParseType(paramCtx.type());
            function.Parameters.Add(new IrParameter(paramName, paramType));
        }

        // Add variadic parameter if present
        if (paramList.variadicParameter() != null)
        {
            var variadicCtx = paramList.variadicParameter();
            var variadicName = variadicCtx.IDENTIFIER().GetText();
            // Variadic parameters have opaque type for now (we'll handle type checking later)
            var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
            function.Parameters.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
            function.IsVariadic = true;
        }
    }

    /// <summary>
    /// Register functions from a module for import.
    /// </summary>
    private void RegisterFunctionsForImport(NovusParser.CompilationUnitContext moduleContext, HashSet<string> namesToImport, string moduleNamespace)
    {
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
                var errorLocation = SourceLocationHelper.FromContext(funcDecl, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.CannotImportPrivate,
                    $"Cannot import private function '{funcName}' from module '{moduleNamespace}'",
                    errorLocation
                );
                return;
            }

            // Skip if this function has already been imported (transitive dependencies)
            if (_module.Functions.Any(f => f.Name == funcName))
            {
                continue;
            }

            // Parse function signature
            var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;
            // Only mark as extern if it's truly an extern function (FFI)
            // Pub functions from Novus modules are real implementations that need linking
            var function = new IrFunction(funcName, returnType, Visibility.Private, isExtern);

            // Parse parameters
            ParseFunctionParameters(funcDecl, function);

            _module.AddFunction(function);
        }
    }

    /// <summary>
    /// Store a generic method template for later instantiation.
    /// </summary>
    private void StoreGenericMethodTemplate(string typeName, string methodName, List<string> genericParams, NovusParser.FunctionDeclarationContext funcDecl)
    {
        var templateKey = $"{typeName}::{methodName}";
        // Capture current constants dictionary (make a copy so imports don't affect templates)
        var templateConstants = GetConstantsAsTuples();
        _genericMethodTemplates[templateKey] = (genericParams, funcDecl, templateConstants);
    }

    /// <summary>
    /// Generate a mangled name for a method.
    /// Trait impls: Type_Trait_TypeArg1_TypeArg2_method (e.g., Counter_Iterator_i32_next)
    /// Inherent impls: Type::method
    /// </summary>
    private string GenerateMethodMangledName(string typeName, string methodName, bool isTraitImpl, string? traitName, List<IrType> traitTypeArgs)
    {
        if (isTraitImpl && traitName != null)
        {
            var typeArgsSuffix = traitTypeArgs.Count > 0
                ? "_" + string.Join("_", traitTypeArgs.Select(t => t.Name.Replace("::", "_")))
                : "";
            return $"{typeName}_{traitName}{typeArgsSuffix}_{methodName}";
        }
        else
        {
            return $"{typeName}::{methodName}";
        }
    }

    /// <summary>
    /// Parse self parameter and add it to the function.
    /// Looks up the implementing type by name from the symbol table.
    /// </summary>
    private void ParseSelfParameter(NovusParser.SelfParameterContext? selfParam, IrFunction function, string typeName)
    {
        if (selfParam == null) return;

        var isMutable = selfParam.KW_MUT() != null;
        var isBorrowed = selfParam.GetChild(0).GetText() == "&";

        // Determine self type - look up the implementing type (struct, enum, or primitive)
        IrType? implType = null;
        var foundStruct = _symbols.LookupStruct(typeName);
        var foundEnum = _symbols.LookupEnum(typeName);

        if (foundStruct != null)
        {
            implType = foundStruct;
        }
        else if (foundEnum != null)
        {
            implType = foundEnum;
        }
        else
        {
            // Try primitive types
            implType = MapPrimitiveTypeName(typeName);

            if (implType == null)
            {
                var errorLocation = selfParam != null
                    ? SourceLocationHelper.FromContext(selfParam, _inputFilePath, _sourceLines.ToArray())
                    : new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.TypeNotFound,
                    $"Type '{typeName}' not found for impl block",
                    errorLocation
                );
                return;
            }
        }

        IrType selfType = implType;
        if (isBorrowed)
        {
            // Use pointer types for borrowed self parameters (& in Novus produces *T, not &T)
            selfType = _typeInterner.GetPointerType(selfType);
        }

        function.Parameters.Add(new IrParameter("self", selfType));
    }

    /// <summary>
    /// Parse self parameter and add it to the function.
    /// Uses the provided implementing type directly (useful for monomorphized types).
    /// </summary>
    private void ParseSelfParameter(NovusParser.SelfParameterContext? selfParam, IrFunction function, IrType implementingType)
    {
        if (selfParam == null) return;

        var isMutable = selfParam.KW_MUT() != null;
        var isBorrowed = selfParam.GetChild(0).GetText() == "&";

        IrType selfType = implementingType;
        if (isBorrowed)
        {
            // Use pointer types for borrowed self parameters (& in Novus produces *T, not &T)
            selfType = _typeInterner.GetPointerType(selfType);
        }

        function.Parameters.Add(new IrParameter("self", selfType));
    }

    private void RegisterConstant(NovusParser.ConstDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Check for pub/internal keywords
        var (visibility, _, _) = AstModifierHelper.ParseModifiers(context, 3);

        // Evaluate the constant expression using the evaluator
        var valueExpr = context.expression();

        // Convert constants dict to use object values for evaluator
        var constantValues = GetConstantValues();

        var evaluator = new SemanticAnalysis.ConstantExpressionEvaluator(constantValues);
        int? value = evaluator.Visit(valueExpr);

        if (value != null)
        {
            // Handle type - either explicit or inferred
            IrType type;
            if (context.type() != null)
            {
                // Explicit type annotation provided
                type = ParseType(context.type());
            }
            else
            {
                // Infer type from the evaluated value
                // Default to i32 for integer literals
                type = IrIntType.I32;
            }

            _symbols.RegisterConstant(name, type, value);
            // Also store in the IR module for code generator access
            _module.Constants[name] = (visibility, type, value);
        }
    }

    private void RegisterStatic(NovusParser.StaticDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var type = ParseType(context.type());

        // Check for pub/internal/mut keywords
        var (visibility, _, isMutable) = AstModifierHelper.ParseModifiers(context, 5);

        // Evaluate the initial value expression
        var valueExpr = context.expression();

        // For now, we'll create a temporary function context to evaluate the expression
        // In the future, we should allow const expressions only
        _currentFunction = new IrFunction("__static_init", IrVoidType.Instance);
        _currentBlock = _currentFunction.CreateBasicBlock("entry");

        var initialValue = (IrValue?)Visit(valueExpr);

        // Restore state
        _currentFunction = null;
        _currentBlock = null;

        if (initialValue != null)
        {
            var staticVar = new IrStaticVariable(name, type, visibility, isMutable, initialValue);
            _module.StaticVariables.Add(staticVar);
        }
    }

    private void RegisterExternalVariable(NovusParser.GlobalVariableDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var type = ParseType(context.type());

        // Check for optional 'at <address>' clause
        long? address = null;
        if (context.KW_AT() != null && context.expression() != null)
        {
            // Evaluate the address expression (must be a compile-time constant)
            var constantValues = GetConstantValues();

            var evaluator = new SemanticAnalysis.ConstantExpressionEvaluator(constantValues);
            int? addrValue = evaluator.Visit(context.expression());
            if (addrValue.HasValue)
            {
                address = addrValue.Value;
            }
        }

        var externVar = new IrExternalVariable(name, type, address);
        _module.ExternalVariables.Add(externVar);
    }

    private void RegisterEnum(NovusParser.EnumDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Check if this enum is already registered (two-phase registration during imports)
        var existingEnum = _symbols.LookupEnum(name);
        if (existingEnum != null)
        {
            // This is phase 2 - fill in the variants for a placeholder enum
            FillEnumVariants(context, existingEnum);

            // Ensure the enum is in the module (in case it was registered by RegisterEnumStubsForImport)
            if (!_module.Enums.Contains(existingEnum))
            {
                _module.AddEnum(existingEnum);
            }
            return;
        }

        // Phase 1: Register placeholder enum FIRST to allow circular references
        // This is especially important during imports where enums may reference each other

        // Handle generic parameters if present
        var genericParams = new List<string>();
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                var paramName = paramId.GetText();
                genericParams.Add(paramName);
                _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
            }
        }

        // Create placeholder enum with empty variants
        var placeholderEnum = new IrEnumType(name, new List<IrEnumVariant>(), genericParams.Count > 0 ? genericParams : null);
        _symbols.RegisterEnum(name, placeholderEnum);
        _module.AddEnum(placeholderEnum);

        // Phase 2: Now parse and fill in the variants (can now reference other enums including this one)
        FillEnumVariants(context, placeholderEnum);

        // Clear generic parameters after enum registration
        _symbols.ClearGenericParameters();
    }

    private void FillEnumVariants(NovusParser.EnumDeclarationContext context, IrEnumType enumType)
    {
        // If variants are already filled (non-empty), skip
        if (enumType.Variants.Count > 0)
        {
            return;
        }

        var name = context.IDENTIFIER().GetText();

        // Handle generic parameters if present (need them in scope for variant type parsing)
        var genericParams = new List<string>();
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                var paramName = paramId.GetText();
                genericParams.Add(paramName);
                _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
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

        // Parse where clause and update enum
        var whereClause = ParseWhereClause(context.whereClause());
        enumType.WhereClause = whereClause;

        // Fill in the variants
        enumType.Variants.Clear();
        foreach (var variant in variants)
        {
            enumType.Variants.Add(variant);
        }

        // Force size calculation for non-generic enums
        if (genericParams.Count == 0)
        {
            _ = enumType.SizeInBytes;
        }

        // Clear generic parameters after variant parsing
        _symbols.ClearGenericParameters();
    }

    private void RegisterStruct(NovusParser.StructDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Parse attributes (for @library and other struct attributes)
        var attributes = ParseAttributesSimple(context.attribute());

        // Handle generic parameters if present
        var genericParams = new List<string>();
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                var paramName = paramId.GetText();
                genericParams.Add(paramName);

                // Add to generic param scope for field parsing
                _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
            }
        }

        // Register placeholder struct FIRST to allow self-referential types
        var placeholderStruct = new IrStructType(name, new List<IrStructField>(), genericParams, null, attributes);
        _symbols.RegisterStruct(name, placeholderStruct);

        // Now parse struct fields (can now reference the struct being defined)
        var fields = new List<IrStructField>();
        foreach (var fieldCtx in context.structField())
        {
            var fieldName = fieldCtx.IDENTIFIER().GetText();
            var fieldType = ParseType(fieldCtx.type());
            fields.Add(new IrStructField(fieldName, fieldType));
        }

        // Parse where clause
        var whereClause = ParseWhereClause(context.whereClause());

        // Clear generic params from scope after struct registration
        _symbols.ClearGenericParameters();

        // Replace placeholder with complete struct type
        var structType = new IrStructType(name, fields, genericParams, null, attributes, whereClause);

        // Force offset calculation by accessing SizeInBytes (only for non-generic structs)
        // Generic structs will be monomorphized later when instantiated with concrete types
        if (genericParams.Count == 0)
        {
            _ = structType.SizeInBytes;
        }

        // Add all structs to the module (both generic and non-generic)
        _module.Structs.Add(structType);
        _symbols.RegisterStruct(name, structType);
    }

    private void RegisterTrait(NovusParser.TraitDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Parse attributes
        var attributes = ParseAttributesSimple(context.attribute());

        // Handle generic parameters if present
        var genericParams = new List<string>();
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                var paramName = paramId.GetText();
                genericParams.Add(paramName);
                _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
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
                        _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
                    }
                }

                // Parse parameters
                var parameters = new List<IrParameter>();
                if (funcSig.parameterList() != null)
                {
                    var paramList = funcSig.parameterList();

                    // Handle self parameter
                    if (paramList.selfParameter() != null)
                    {
                        var selfParam = paramList.selfParameter();
                        var selfText = selfParam.GetText();

                        // Create placeholder self type (will be replaced during trait impl)
                        IrType selfType;
                        if (selfText.StartsWith("&mut"))
                        {
                            selfType = new IrMutReferenceType(IrVoidType.Instance); // Placeholder
                        }
                        else if (selfText.StartsWith("&"))
                        {
                            selfType = new IrReferenceType(IrVoidType.Instance); // Placeholder
                        }
                        else
                        {
                            selfType = IrVoidType.Instance; // Placeholder
                        }

                        parameters.Add(new IrParameter("self", selfType));
                    }

                    // Regular parameters
                    foreach (var paramCtx in paramList.parameter())
                    {
                        var paramName = paramCtx.IDENTIFIER().GetText();
                        var paramType = ParseType(paramCtx.type());
                        parameters.Add(new IrParameter(paramName, paramType));
                    }

                    // Variadic parameters
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
                // Note: For traits, we don't need to clear individual params, just the whole set
                // TODO: Revisit if we need more granular control
            }
        }

        // Parse visibility
        var visibility = Visibility.Private;
        for (int i = 0; i < Math.Min(3, context.ChildCount); i++)
        {
            var childText = context.GetChild(i)?.GetText();
            if (childText == "pub") visibility = Visibility.Public;
            if (childText == "internal") visibility = Visibility.Internal;
        }

        var trait = new IrTrait(name, methods, genericParams.Count > 0 ? genericParams : null, visibility, attributes);
        _symbols.RegisterTrait(name, trait);
        _module.AddTrait(trait);

        // Clear generic parameters after trait registration
        _symbols.ClearGenericParameters();
    }

    /// <summary>
    /// Simple attribute parser for IrBuilder (doesn't validate - just extracts)
    /// </summary>
    private Novus.SemanticAnalysis.AttributeCollection ParseAttributesSimple(NovusParser.AttributeContext[]? attributeContexts)
    {
        var collection = new Novus.SemanticAnalysis.AttributeCollection();
        if (attributeContexts == null || attributeContexts.Length == 0)
            return collection;

        foreach (var attrCtx in attributeContexts)
        {
            var attrName = attrCtx.IDENTIFIER().GetText();
            // Simple location - just use line/column from token
            var errorLocation = new Novus.Diagnostics.SourceLocation(_inputFilePath, attrCtx.Start.Line, attrCtx.Start.Column, 0, "");
            var attr = new Novus.SemanticAnalysis.AttributeInfo(attrName, errorLocation);

            // Parse attribute arguments if present
            if (attrCtx.attributeArgList() != null)
            {
                foreach (var argCtx in attrCtx.attributeArgList().attributeArg())
                {
                    var expr = argCtx.expression();
                    var exprText = expr.GetText();

                    // Simple value extraction
                    object? value = null;
                    if (int.TryParse(exprText, out var intValue))
                    {
                        value = intValue;
                    }
                    else if (exprText.StartsWith("\"") && exprText.EndsWith("\""))
                    {
                        value = exprText.Trim('"');
                    }
                    else if (exprText == "true")
                    {
                        value = true;
                    }
                    else if (exprText == "false")
                    {
                        value = false;
                    }
                    else
                    {
                        value = exprText;
                    }

                    // Check if it's a named argument
                    if (argCtx.IDENTIFIER() != null)
                    {
                        var argName = argCtx.IDENTIFIER().GetText();
                        attr.NamedArgs[argName] = value;
                    }
                    else
                    {
                        // Positional argument
                        attr.PositionalArgs.Add(value);
                    }
                }
            }

            collection.Add(attr);
        }

        return collection;
    }

    public override object? VisitFunctionDeclaration([NotNull] NovusParser.FunctionDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var returnType = context.type() != null ? ParseType(context.type()) : IrVoidType.Instance;

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
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                function.GenericParameters.Add(paramId.GetText());
            }
        }

        // Parse where clause
        function.WhereClause = ParseWhereClause(context.whereClause());

        // Parse parameters
        if (context.parameterList() != null)
        {
            var paramList = context.parameterList();
            foreach (var paramCtx in paramList.parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                var paramType = ParseType(paramCtx.type());
                function.Parameters.Add(new IrParameter(paramName, paramType));
            }

            // Add variadic parameter if present
            if (paramList.variadicParameter() != null)
            {
                var variadicCtx = paramList.variadicParameter();
                var variadicName = variadicCtx.IDENTIFIER().GetText();
                // Variadic parameters have opaque type for now (we'll handle type checking later)
                var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                function.Parameters.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
                function.IsVariadic = true;
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
        return null;
    }

    public override object? VisitVariableDeclaration([NotNull] NovusParser.VariableDeclarationContext context)
    {
        var isMutable = context.GetChild(0)?.GetText() == "var";

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
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
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
                var errorLocation = SourceLocationHelper.FromContext(fullContext, _inputFilePath, _sourceLines.ToArray());
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
            var errorLocation = SourceLocationHelper.FromContext(fullContext, _inputFilePath, _sourceLines.ToArray());
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
            var errorLocation = SourceLocationHelper.FromContext(fullContext, _inputFilePath, _sourceLines.ToArray());
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
            var errorLocation = SourceLocationHelper.FromContext(fullContext, _inputFilePath, _sourceLines.ToArray());
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
            errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
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
                IrVariable? baseVar = null;
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

                if (baseVar == null)
                {
                    errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
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
                IrVariable? variable = null;
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

                if (variable == null || varType == null)
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
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
            IrVariable? variable = null;
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

            if (variable == null || varType == null)
            {
                errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
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
                IrVariable? variable = null;
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

                if (variable == null || varType == null)
                {
                    errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
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
        // Visit the expression and return its value (for implicit returns)
        return Visit(context.expression());
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

        // Branch based on condition
        _currentBlock!.AddInstruction(new IrConditionalBranch(condition!, thenLabel, falseTarget));
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
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
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
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
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
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
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
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
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

    public override object? VisitForCStyle([NotNull] NovusParser.ForCStyleContext context)
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
    public override object? VisitForInLoop([NotNull] NovusParser.ForInLoopContext context)
    {
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
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
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

        // Push exit label for break statements
        _loopExitLabels.Push(endLabel);

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

        // Increment index: _idx = _idx + 1
        if (!CurrentBlockHasTerminator())
        {
            var incResultName = $"%t{_tempCounter++}";
            var oneLiteral = new IrConstant(1, IrIntType.U32);
            var incOp = new IrBinaryOp(incResultName, IrBinaryOp.OpKind.Add, idxVarRef, oneLiteral, IrIntType.U32);
            _currentBlock!.AddInstruction(incOp);
            var incResult = new IrVariable(incResultName, IrIntType.U32);
            _currentBlock!.AddInstruction(new IrStore(idxVarName, incResult));

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
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "break statement outside of loop",
                errorLocation
            );
            return null;
        }

        var exitLabel = _loopExitLabels.Peek();
        _currentBlock!.AddInstruction(new IrBranch(exitLabel));
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

    public override object? VisitPrimaryExpr([NotNull] NovusParser.PrimaryExprContext context)
    {
        return Visit(context.primaryExpression());
    }

    public override object? VisitCallExpr([NotNull] NovusParser.CallExprContext context)
    {
        // Handle method calls (e.g., v.len())
        // Method calls desugar to: Type::method(receiver, args...)
        if (context.expression() is NovusParser.MemberAccessExprContext memberAccessCtx)
        {
            return HandleMethodCallIr(context, memberAccessCtx);
        }

        // Check if this is a call to a generic function template (before evaluating funcExpr)
        // Generic functions aren't in _module.Functions yet, so we need to check the template dictionary
        string? genericFuncName = null;
        if (context.expression() is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.primaryExpression() is NovusParser.IdentifierExprContext identExpr)
        {
            genericFuncName = identExpr.identifier().GetText();
        }

        var funcExpr = (IrValue?)Visit(context.expression());

        // Parse arguments
        var arguments = new List<IrValue>();
        if (context.argumentList() != null)
        {
            // Check if this is an enum constructor - if so, we can use expected types for arguments
            List<IrType>? expectedArgTypes = null;
            if (funcExpr is IrEnumConstructor tempEnumCtor &&
                _expectedType is IrEnumType expectedEnumType &&
                expectedEnumType.EnumName == (tempEnumCtor.Type as IrEnumType)?.EnumName)
            {
                // Expected type matches the enum we're constructing
                // Extract expected argument types from the corresponding variant
                var expectedVariant = expectedEnumType.GetVariant(tempEnumCtor.VariantName);
                if (expectedVariant != null)
                {
                    expectedArgTypes = expectedVariant.AssociatedData;
                }
            }

            int argIdx = 0;
            foreach (var argCtx in context.argumentList().expression())
            {
                // Set expected type for this argument if available
                var savedExpectedType = _expectedType;
                if (expectedArgTypes != null && argIdx < expectedArgTypes.Count)
                {
                    _expectedType = expectedArgTypes[argIdx];
                }
                else
                {
                    _expectedType = null;
                }

                var argValue = (IrValue?)Visit(argCtx);

                // Restore expected type
                _expectedType = savedExpectedType;

                if (argValue != null)
                {
                    // IMPORTANT: Apply current type substitutions to argument type
                    // This handles the case where a generic function calls another generic function
                    // with generic arguments (e.g., double<T> calling identity(x) where x: T)
                    var argType = argValue.Type;
                    if (_currentTypeSubstitutions != null)
                    {
                        argType = SubstituteGenericTypes(argType, _currentTypeSubstitutions);
                    }

                    // If type was substituted, create a new IrVariable with the substituted type
                    if (argType != argValue.Type && argValue is IrVariable argVar)
                    {
                        arguments.Add(new IrVariable(argVar.Name, argType));
                    }
                    else
                    {
                        arguments.Add(argValue);
                    }
                }
                argIdx++;
            }
        }

        // NOTE: Str → *u8 coercion is now handled later, after function lookup,
        // so we can check the actual parameter types and only coerce when needed

        // If it's a generic function template, infer types and instantiate
        if (genericFuncName != null && _genericFunctionTemplates.ContainsKey(genericFuncName))
        {
            // Get template and parse parameters
            var template = _genericFunctionTemplates[genericFuncName];

            // Save and clear type substitutions so we get the generic template types
            var savedTypeSubstitutions = _currentTypeSubstitutions;
            _currentTypeSubstitutions = null;

            // Set up generic params temporarily
            // TODO: Use child scope instead of save/restore pattern
            _symbols.ClearGenericParameters();
            foreach (var paramName in template.GenericParams)
            {
                _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
            }

            // Parse template parameters
            var templateParams = new List<IrParameter>();
            if (template.Context.parameterList() != null)
            {
                var paramList = template.Context.parameterList();
                foreach (var paramCtx in paramList.parameter())
                {
                    var paramName = paramCtx.IDENTIFIER().GetText();
                    var paramType = ParseType(paramCtx.type());
                    templateParams.Add(new IrParameter(paramName, paramType));
                }

                // Add variadic parameter if present (for template analysis)
                if (paramList.variadicParameter() != null)
                {
                    var variadicCtx = paramList.variadicParameter();
                    var variadicName = variadicCtx.IDENTIFIER().GetText();
                    // Variadic parameters have opaque type for now (we'll handle type checking later)
                    var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
                    templateParams.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
                }
            }

            // Restore generic params
            _symbols.ClearGenericParameters();
            // TODO: Restore saved params when we implement save/restore properly

            // Restore type substitutions
            _currentTypeSubstitutions = savedTypeSubstitutions;

            // Infer types
            var typeSubstitutions = InferGenericFunctionTypes(template.GenericParams, templateParams, arguments);
            if (typeSubstitutions == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Cannot infer type arguments for '{genericFuncName}'",
                    errorLocation
                );
                return null;
            }

            // Instantiate
            var instantiatedFunc = InstantiateGenericFunction(genericFuncName, typeSubstitutions);
            if (instantiatedFunc == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Failed to instantiate '{genericFuncName}'",
                    errorLocation
                );
                return null;
            }

            // Create call
            var genericCallResult = $"%t{_tempCounter++}";
            var genericCall = new IrCall(instantiatedFunc.Name, instantiatedFunc.ReturnType, genericCallResult);
            foreach (var arg in arguments)
            {
                genericCall.Arguments.Add(arg);
            }
            _currentBlock!.AddInstruction(genericCall);
            return new IrVariable(genericCallResult, instantiatedFunc.ReturnType);
        }

        // Handle generic associated function calls (e.g., Vec::new())
        if (funcExpr is IrGenericAssociatedFunction genericAssocFunc)
        {
            // We need to determine the concrete type parameters
            // Priority: 1) Explicit type args (turbo-fish), 2) Expected type, 3) Unresolved

            IrStructType? monomorphizedStruct = null;

            // 1. Check for explicit type arguments (turbo-fish syntax: Vec::<u32>::with_capacity)
            if (genericAssocFunc.ExplicitTypeArgs != null && genericAssocFunc.ExplicitTypeArgs.Count > 0)
            {
                // User provided explicit type arguments
                if (genericAssocFunc.ExplicitTypeArgs.Count != genericAssocFunc.GenericParameters.Count)
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        $"Wrong number of type arguments for '{genericAssocFunc.TypeName}::{genericAssocFunc.MethodName}': expected {genericAssocFunc.GenericParameters.Count}, got {genericAssocFunc.ExplicitTypeArgs.Count}",
                        errorLocation
                    );
                    return null;
                }

                // Build monomorphized struct from explicit type args (same logic as ParseNamedType)
                var baseStruct = _symbols.LookupStruct(genericAssocFunc.TypeName);
                if (baseStruct == null)
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(ErrorCodes.StructNotFound, $"Struct '{genericAssocFunc.TypeName}' not found", errorLocation);
                    return null;
                }
                var typeArgs = genericAssocFunc.ExplicitTypeArgs;

                // Create cache key
                var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
                var cacheKey = $"{baseStruct.StructName}<{string.Join(",", typeArgKeys)}>";

                // Check cache first
                if (_symbols.LookupMonomorphizedStruct(cacheKey) != null)
                {
                    monomorphizedStruct = _symbols.LookupMonomorphizedStruct(cacheKey)!;
                }
                else
                {
                    // Create monomorphized struct with concrete types
                    var typeSubstitutions = new Dictionary<string, IrType>();
                    for (int i = 0; i < baseStruct.GenericParameters.Count; i++)
                    {
                        typeSubstitutions[baseStruct.GenericParameters[i]] = typeArgs[i];
                    }

                    // Create monomorphized fields using recursive substitution
                    var monomorphizedFields = new List<IrStructField>();
                    bool fullyMonomorphized = true;

                    foreach (var origField in baseStruct.Fields)
                    {
                        var fieldType = SubstituteGenericTypes(origField.Type, typeSubstitutions);
                        monomorphizedFields.Add(new IrStructField(origField.Name, fieldType));

                        // Check if field type is still generic
                        if (ContainsGenericTypes(fieldType))
                        {
                            fullyMonomorphized = false;
                        }
                    }

                    // Create new struct type with concrete types (no generic parameters)
                    monomorphizedStruct = new IrStructType(baseStruct.StructName, monomorphizedFields, null, cacheKey);

                    // Force calculation of field offsets only if fully monomorphized
                    if (fullyMonomorphized)
                    {
                        _ = monomorphizedStruct.SizeInBytes;
                    }

                    // Cache it for future use
                    _symbols.RegisterMonomorphizedStruct(cacheKey, monomorphizedStruct);
                }
            }
            // 2. Try to infer from expected type
            else if (_expectedType != null && _expectedType is IrStructType expectedStruct && expectedStruct.GenericParameters.Count == 0)
            {
                // Expected type is a monomorphized struct like Vec<i32>
                monomorphizedStruct = expectedStruct;
            }
            // 3. No explicit type args and no expected type - create unresolved generic
            else if (_expectedType == null)
            {
                // No expected type - create unresolved generic that will be inferred from usage
                // Example: let vec = Vec::new() → vec has type Vec<UnresolvedGeneric>
                // When vec.push(42i32) is called later, we'll resolve it to Vec<i32>

                var unresolvedTypeArgs = new List<IrType>();
                for (int i = 0; i < genericAssocFunc.GenericParameters.Count; i++)
                {
                    unresolvedTypeArgs.Add(new IrUnresolvedGenericType());
                }

                var partiallyResolvedType = new IrPartiallyResolvedGenericType(
                    genericAssocFunc.TypeName,
                    unresolvedTypeArgs
                );

                // Return a placeholder value with the partially resolved type
                // The actual instantiation will happen when we see method calls on this value
                var placeholderResult = $"%t{_tempCounter++}";
                return new IrVariable(placeholderResult, partiallyResolvedType);
            }

            if (monomorphizedStruct == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Could not infer generic type parameters for '{genericAssocFunc.TypeName}::{genericAssocFunc.MethodName}()'. Consider using turbo-fish syntax: {genericAssocFunc.TypeName}::<Type>::{genericAssocFunc.MethodName}",
                    errorLocation
                );
                return null;
            }

            // Instantiate the generic method with the monomorphized struct
            var instantiatedFunc = InstantiateGenericMethod(monomorphizedStruct, genericAssocFunc.MethodName);
            if (instantiatedFunc == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Failed to instantiate generic method '{genericAssocFunc.TypeName}::{genericAssocFunc.MethodName}'",
                    errorLocation
                );
                return null;
            }

            // Generate call to instantiated function
            var callResult = $"%t{_tempCounter++}";
            var callInst = new IrCall(instantiatedFunc.Name, instantiatedFunc.ReturnType, callResult);
            foreach (var arg in arguments)
            {
                callInst.Arguments.Add(arg);
            }
            _currentBlock!.AddInstruction(callInst);

            return new IrVariable(callResult, instantiatedFunc.ReturnType);
        }

        // Handle non-generic function reference calls
        if (funcExpr is IrFunctionRef funcRef)
        {
            // Validate argument count
            var funcRefNonVariadicCount = funcRef.Function.Parameters.Count(p => !p.IsVariadic);
            if (funcRef.Function.IsVariadic)
            {
                if (arguments.Count < funcRefNonVariadicCount)
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        $"Variadic function '{funcRef.Function.Name}' expects at least {funcRefNonVariadicCount} arguments, got {arguments.Count}",
                        errorLocation
                    );
                    return null;
                }
            }
            else
            {
                if (arguments.Count != funcRef.Function.Parameters.Count)
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        $"Function '{funcRef.Function.Name}' expects {funcRef.Function.Parameters.Count} arguments, got {arguments.Count}",
                        errorLocation
                    );
                    return null;
                }
            }

            // Apply automatic Str/String → *u8 coercion only when parameter type is *u8
            for (int i = 0; i < arguments.Count; i++)
            {
                // Skip variadic parameters (they don't have a declared type)
                if (i >= funcRef.Function.Parameters.Count || funcRef.Function.Parameters[i].IsVariadic)
                    continue;

                var paramType = funcRef.Function.Parameters[i].Type;
                var argValue = arguments[i];

                // Only coerce if parameter is *u8 and argument is Str or String
                if (paramType is IrPointerType ptrType &&
                    ptrType.PointeeType.Equals(IrIntType.U8) &&
                    argValue.Type is IrStructType structType &&
                    (structType.StructName == "Str" || structType.StructName == "String"))
                {
                    if (structType.StructName == "Str")
                    {
                        // If argValue is a struct literal, extract the ptr field directly (no instruction needed)
                        if (argValue is IrStructLiteral strLiteral)
                        {
                            if (!strLiteral.FieldValues.TryGetValue("ptr", out var ptrValue))
                            {
                                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                                _diagnostics.ReportError(
                                    ErrorCodes.InvalidExpressionType,
                                    "Str struct literal must have a 'ptr' field",
                                    errorLocation
                                );
                                return null;
                            }
                            arguments[i] = ptrValue;  // Use the ptr value directly
                        }
                        else
                        {
                            // For Str variables (not literals), we need the member access
                            var ptrField = structType.GetField("ptr");
                            if (ptrField == null)
                            {
                                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                                _diagnostics.ReportError(
                                    ErrorCodes.InvalidExpressionType,
                                    "Str struct must have a 'ptr' field",
                                    errorLocation
                                );
                                return null;
                            }

                            var ptrTempName = $"%t{_tempCounter++}";
                            var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                            var ptrFieldAccess = new IrMemberAccess(ptrTempName, argValue, "ptr", u8PtrType, ptrField.Offset);
                            _currentBlock!.AddInstruction(ptrFieldAccess);
                            arguments[i] = new IrVariable(ptrTempName, u8PtrType);
                        }
                    }
                    else if (structType.StructName == "String")
                    {
                        // For String, call the as_ptr() method
                        var asPtrMethodName = "String::as_ptr";
                        var asPtrMethod = _module.Functions.FirstOrDefault(f => f.Name == asPtrMethodName);

                        if (asPtrMethod == null)
                        {
                            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.InvalidExpressionType,
                                "String type must have as_ptr() method for automatic coercion to *u8",
                                errorLocation
                            );
                            return null;
                        }

                        // Call String::as_ptr()
                        var receiverArg = new IrBorrowValue(argValue, asPtrMethod.Parameters[0].Type, false);
                        var resultTempName = $"%t{_tempCounter++}";
                        var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                        var methodCall = new IrCall(asPtrMethodName, u8PtrType, resultTempName);
                        methodCall.Arguments.Add(receiverArg);
                        _currentBlock!.AddInstruction(methodCall);
                        arguments[i] = new IrVariable(resultTempName, u8PtrType);
                    }
                }
            }

            // Generate call instruction
            var callResultName = $"%t{_tempCounter++}";
            var callInstruction = new IrCall(funcRef.Function.Name, funcRef.Function.ReturnType, callResultName);
            foreach (var arg in arguments)
            {
                callInstruction.Arguments.Add(arg);
            }
            _currentBlock!.AddInstruction(callInstruction);

            return new IrVariable(callResultName, funcRef.Function.ReturnType);
        }

        // Check if this is an enum constructor call
        if (funcExpr is IrEnumConstructor enumCtor)
        {
            // Create an enum value with the provided arguments
            var enumType = enumCtor.Type as IrEnumType;
            if (enumType == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    "Enum constructor must have enum type",
                    errorLocation
                );
                return null;
            }

            var variant = enumType.GetVariant(enumCtor.VariantName);
            if (variant == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.EnumNotFound,
                    $"Variant '{enumCtor.VariantName}' not found in enum '{enumType.EnumName}'",
                    errorLocation
                );
                return null;
            }

            // Validate argument count
            if (arguments.Count != variant.AssociatedData.Count)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Variant '{enumCtor.VariantName}' expects {variant.AssociatedData.Count} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }

            // If enum has generic parameters, perform type inference to monomorphize
            IrEnumType finalEnumType = enumType;
            if (enumType.GenericParameters.Count > 0)
            {
                var typeSubstitutions = new Dictionary<string, IrType>();

                // PRIORITY 1: Use expected type for monomorphization if available
                // This allows From<T> trait conversions to work correctly
                if (_expectedType is IrEnumType expectedEnumType &&
                    expectedEnumType.EnumName == enumType.EnumName &&
                    expectedEnumType.GenericParameters.Count == 0) // Expected type is monomorphized
                {
                    // Extract concrete types from expected enum by matching variant structure
                    for (int paramIdx = 0; paramIdx < enumType.GenericParameters.Count; paramIdx++)
                    {
                        var paramName = enumType.GenericParameters[paramIdx];

                        // Find this parameter in a variant and extract the concrete type
                        for (int varIdx = 0; varIdx < enumType.Variants.Count; varIdx++)
                        {
                            var origVariant = enumType.Variants[varIdx];
                            var expectedVar = expectedEnumType.Variants[varIdx];

                            for (int dataIdx = 0; dataIdx < origVariant.AssociatedData.Count; dataIdx++)
                            {
                                var expectedTypeFromVariant = expectedVar.AssociatedData[dataIdx];
                                if (origVariant.AssociatedData[dataIdx] is IrGenericType gt &&
                                    gt.ParameterName == paramName)
                                {
                                    typeSubstitutions[paramName] = expectedTypeFromVariant;
                                    break;
                                }
                            }

                            if (typeSubstitutions.ContainsKey(paramName))
                                break;
                        }
                    }

                    // Use the expected type directly if it matches
                    if (typeSubstitutions.Count == enumType.GenericParameters.Count)
                    {
                        finalEnumType = expectedEnumType;
                    }
                }

                // PRIORITY 2: Fall back to argument types for any missing parameters
                // This handles cases where expected type is not available or incomplete
                for (int i = 0; i < arguments.Count; i++)
                {
                    var argType = arguments[i].Type;
                    var paramType = variant.AssociatedData[i];

                    if (paramType is IrGenericType gt)
                    {
                        if (!typeSubstitutions.ContainsKey(gt.ParameterName))
                        {
                            typeSubstitutions[gt.ParameterName] = argType;
                        }
                    }
                }

                // PRIORITY 3: If not using expected type, create monomorphized enum from type substitutions
                if (finalEnumType == enumType) // Haven't assigned finalEnumType yet
                {
                    // Create cache key using proper type keys
                    var typeArgKeys = enumType.GenericParameters.Select(p =>
                    {
                        var key = typeSubstitutions.ContainsKey(p) ? GetTypeCacheKey(typeSubstitutions[p]) : p;
                        return key;
                    });
                    var cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";

                    // Check cache first
                    if (_symbols.LookupMonomorphizedEnum(cacheKey) != null)
                    {
                        finalEnumType = _symbols.LookupMonomorphizedEnum(cacheKey)!;
                    }
                    else
                    {
                        // Create monomorphized enum type
                        var monomorphizedVariants = new List<IrEnumVariant>();
                        foreach (var origVariant in enumType.Variants)
                        {
                            var monomorphizedData = new List<IrType>();
                            foreach (var dataType in origVariant.AssociatedData)
                            {
                                if (dataType is IrGenericType genType && typeSubstitutions.ContainsKey(genType.ParameterName))
                                {
                                    monomorphizedData.Add(typeSubstitutions[genType.ParameterName]);
                                }
                                else
                                {
                                    monomorphizedData.Add(dataType);
                                }
                            }
                            monomorphizedVariants.Add(new IrEnumVariant(origVariant.Name, origVariant.Tag, monomorphizedData));
                        }

                        // Create new enum type with concrete types
                        finalEnumType = new IrEnumType(enumType.EnumName, monomorphizedVariants, null, cacheKey);

                        // Only cache if fully monomorphized (no generic types in variants)
                        bool isFullyMonomorphized = !monomorphizedVariants.Any(v =>
                            v.AssociatedData.Any(d => d is IrGenericType));

                        if (isFullyMonomorphized)
                        {
                            _symbols.RegisterMonomorphizedEnum(cacheKey, finalEnumType);
                        }
                    }
                }
            }

            // Apply From<T> trait conversions if needed for arguments
            var finalVariant = finalEnumType.GetVariant(enumCtor.VariantName);
            var convertedArguments = new List<IrValue>();

            for (int i = 0; i < arguments.Count; i++)
            {
                var arg = arguments[i];
                var expectedType = finalVariant!.AssociatedData[i];

                // Check if type conversion is needed
                if (!TypesEqual(arg.Type, expectedType))
                {
                    // Try to convert via From<ArgType> trait
                    var convertedArg = TryConvertViaFromTrait(arg, expectedType);
                    if (convertedArg != null)
                    {
                        convertedArguments.Add(convertedArg);
                    }
                    else
                    {
                        // No conversion available, use original (will fail at runtime or be caught elsewhere)
                        convertedArguments.Add(arg);
                    }
                }
                else
                {
                    // No conversion needed
                    convertedArguments.Add(arg);
                }
            }

            // Create the enum value with the monomorphized type
            return new IrEnumValue(finalEnumType, enumCtor.VariantName, finalVariant.Tag, convertedArguments);
        }

        string? resultName;
        IrType returnType;

        // Check if this is an indirect call through a function pointer
        if (funcExpr!.Type is IrFunctionPointerType fpType)
        {
            // Indirect call through function pointer
            if (arguments.Count != fpType.ParameterTypes.Count)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Function pointer expects {fpType.ParameterTypes.Count} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }

            // Apply automatic Str/String → *u8 coercion only when parameter type is *u8
            for (int i = 0; i < arguments.Count; i++)
            {
                var paramType = fpType.ParameterTypes[i];
                var argValue = arguments[i];

                // Only coerce if parameter is *u8 and argument is Str or String
                if (paramType is IrPointerType ptrType &&
                    ptrType.PointeeType.Equals(IrIntType.U8) &&
                    argValue.Type is IrStructType structType &&
                    (structType.StructName == "Str" || structType.StructName == "String"))
                {
                    if (structType.StructName == "Str")
                    {
                        // If argValue is a struct literal, extract the ptr field directly (no instruction needed)
                        if (argValue is IrStructLiteral strLiteral)
                        {
                            if (!strLiteral.FieldValues.TryGetValue("ptr", out var ptrValue))
                            {
                                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                                _diagnostics.ReportError(
                                    ErrorCodes.InvalidExpressionType,
                                    "Str struct literal must have a 'ptr' field",
                                    errorLocation
                                );
                                return null;
                            }
                            arguments[i] = ptrValue;  // Use the ptr value directly
                        }
                        else
                        {
                            // For Str variables (not literals), we need the member access
                            var ptrField = structType.GetField("ptr");
                            if (ptrField == null)
                            {
                                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                                _diagnostics.ReportError(
                                    ErrorCodes.InvalidExpressionType,
                                    "Str struct must have a 'ptr' field",
                                    errorLocation
                                );
                                return null;
                            }

                            var ptrTempName = $"%t{_tempCounter++}";
                            var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                            var ptrFieldAccess = new IrMemberAccess(ptrTempName, argValue, "ptr", u8PtrType, ptrField.Offset);
                            _currentBlock!.AddInstruction(ptrFieldAccess);
                            arguments[i] = new IrVariable(ptrTempName, u8PtrType);
                        }
                    }
                    else if (structType.StructName == "String")
                    {
                        // For String, call the as_ptr() method
                        var asPtrMethodName = "String::as_ptr";
                        var asPtrMethod = _module.Functions.FirstOrDefault(f => f.Name == asPtrMethodName);

                        if (asPtrMethod == null)
                        {
                            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.InvalidExpressionType,
                                "String type must have as_ptr() method for automatic coercion to *u8",
                                errorLocation
                            );
                            return null;
                        }

                        // Call String::as_ptr()
                        var receiverArg = new IrBorrowValue(argValue, asPtrMethod.Parameters[0].Type, false);
                        var resultTempName = $"%t{_tempCounter++}";
                        var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                        var methodCall = new IrCall(asPtrMethodName, u8PtrType, resultTempName);
                        methodCall.Arguments.Add(receiverArg);
                        _currentBlock!.AddInstruction(methodCall);
                        arguments[i] = new IrVariable(resultTempName, u8PtrType);
                    }
                }
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
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Function call target must be an identifier or function pointer",
                errorLocation
            );
            return null;
        }

        var functionName = funcVar.Name;

        // Check if this is an associated function on an enum that needs instantiation (e.g., Option::FromPointer)
        if (functionName.Contains("::"))
        {
            var parts = functionName.Split("::");
            if (parts.Length == 2)
            {
                var typeName = parts[0];
                var methodName = parts[1];

                // Check if it's an enum type
                if (_symbols.HasEnum(typeName))
                {
                    var enumType = _symbols.LookupEnum(typeName)!;
                    if (enumType.GenericParameters.Count > 0)
                    {
                        // Need to infer type arguments and monomorphize the enum before instantiation
                        var typeSubstitutions = InferGenericEnumTypeArguments(enumType, methodName, arguments, _expectedType);

                        if (typeSubstitutions != null)
                        {
                            // Monomorphize the enum with inferred type arguments
                            var monomorphizedEnum = MonomorphizeEnum(enumType, typeSubstitutions);

                            if (monomorphizedEnum != null)
                            {
                                // Now instantiate the method with the monomorphized enum
                                var instantiatedFunc = InstantiateGenericEnumMethod(monomorphizedEnum, methodName, arguments);
                                if (instantiatedFunc != null)
                                {
                                    functionName = instantiatedFunc.Name;
                                }
                                else
                                {
                                    // Try just the method name (impl methods are currently stored without type prefix)
                                    functionName = methodName;
                                }
                            }
                        }
                        else
                        {
                            // Could not infer type arguments
                            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.InvalidExpressionType,
                                $"Cannot infer generic type arguments for {typeName}::{methodName}. Please provide explicit type arguments.",
                                errorLocation
                            );
                            return null;
                        }
                    }
                }
            }
        }

        // Look up the function in the module to get its return type
        var function = _module.Functions.FirstOrDefault(f => f.Name == functionName);
        if (function == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Unknown function: {functionName}",
                errorLocation
            );
            return null;
        }

        // Check argument count matches parameter count
        var nonVariadicCount = function.Parameters.Count(p => !p.IsVariadic);
        if (function.IsVariadic)
        {
            if (arguments.Count < nonVariadicCount)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Variadic function '{functionName}' expects at least {nonVariadicCount} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }
        }
        else
        {
            if (arguments.Count != function.Parameters.Count)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Function {functionName} expects {function.Parameters.Count} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }
        }

        // Apply automatic Str/String → *u8 coercion only when parameter type is *u8
        for (int i = 0; i < arguments.Count; i++)
        {
            // Skip variadic parameters (they don't have a declared type)
            if (i >= function.Parameters.Count || function.Parameters[i].IsVariadic)
                continue;

            var paramType = function.Parameters[i].Type;
            var argValue = arguments[i];

            // Only coerce if parameter is *u8 and argument is Str or String
            if (paramType is IrPointerType ptrType &&
                ptrType.PointeeType.Equals(IrIntType.U8) &&
                argValue.Type is IrStructType structType &&
                (structType.StructName == "Str" || structType.StructName == "String"))
            {
                if (structType.StructName == "Str")
                {
                    // If argValue is a struct literal, extract the ptr field directly (no instruction needed)
                    if (argValue is IrStructLiteral strLiteral)
                    {
                        if (!strLiteral.FieldValues.TryGetValue("ptr", out var ptrValue))
                        {
                            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.InvalidExpressionType,
                                "Str struct literal must have a 'ptr' field",
                                errorLocation
                            );
                            return null;
                        }
                        arguments[i] = ptrValue;  // Use the ptr value directly
                    }
                    else
                    {
                        // For Str variables (not literals), we need the member access
                        var ptrField = structType.GetField("ptr");
                        if (ptrField == null)
                        {
                            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.InvalidExpressionType,
                                "Str struct must have a 'ptr' field",
                                errorLocation
                            );
                            return null;
                        }

                        var ptrTempName = $"%t{_tempCounter++}";
                        var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                        var ptrFieldAccess = new IrMemberAccess(ptrTempName, argValue, "ptr", u8PtrType, ptrField.Offset);
                        _currentBlock!.AddInstruction(ptrFieldAccess);
                        arguments[i] = new IrVariable(ptrTempName, u8PtrType);
                    }
                }
                else if (structType.StructName == "String")
                {
                    // For String, call the as_ptr() method
                    var asPtrMethodName = "String::as_ptr";
                    var asPtrMethod = _module.Functions.FirstOrDefault(f => f.Name == asPtrMethodName);

                    if (asPtrMethod == null)
                    {
                        var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.InvalidExpressionType,
                            "String type must have as_ptr() method for automatic coercion to *u8",
                            errorLocation
                        );
                        return null;
                    }

                    // Call String::as_ptr()
                    var receiverArg = new IrBorrowValue(argValue, asPtrMethod.Parameters[0].Type, false);
                    var resultTempName = $"%t{_tempCounter++}";
                    var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                    var methodCall = new IrCall(asPtrMethodName, u8PtrType, resultTempName);
                    methodCall.Arguments.Add(receiverArg);
                    _currentBlock!.AddInstruction(methodCall);
                    arguments[i] = new IrVariable(resultTempName, u8PtrType);
                }
            }
        }

        // Insert implicit casts for arguments where needed
        // (e.g., u32 -> i32 for same-bit-width conversions)
        // Note: Str -> *u8 coercion is handled above (only when param type is *u8)
        for (int i = 0; i < arguments.Count; i++)
        {
            var argType = arguments[i].Type;

            // For variadic functions, arguments beyond the non-variadic params don't have a corresponding parameter
            IrType? paramType = null;
            if (i < function.Parameters.Count && !function.Parameters[i].IsVariadic)
            {
                paramType = function.Parameters[i].Type;
            }

            // If types don't exactly match but are compatible integer types of same width (non-variadic only)
            if (paramType != null &&
                !argType.Equals(paramType) &&
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

    /// <summary>
    /// Try to resolve generic type parameters from a method call
    /// Example: vec.push(42i32) where vec is Vec<UnresolvedGeneric> → infer T = i32
    /// </summary>
    private IrStructType? TryResolveGenericFromMethodCall(IrPartiallyResolvedGenericType partialType, string methodName, List<IrValue> methodArgs)
    {
        // Look up the method template for this generic type
        var templateKey = $"{partialType.GenericTypeName}::{methodName}";

        if (!_genericMethodTemplates.TryGetValue(templateKey, out var template))
        {
            // Method not found or not generic
            return null;
        }

        var (genericParams, funcDecl, _) = template;

        // Parse the method's parameter types from the template
        // Note: 'self' parameter is handled specially in the grammar and may not be in parameterList
        var paramContexts = funcDecl.parameterList()?.parameter()?.ToList() ?? new List<NovusParser.ParameterContext>();

        if (paramContexts.Count == 0)
        {
            // No non-self parameters to infer from
            return null;
        }

        // Try to infer generic type from the method arguments
        // For Vec::push(&mut self, value: T), paramContexts contains just [value: T]
        // methodArgs contains the user-provided arguments (not including self)
        // So paramContexts[0] corresponds to methodArgs[0]

        var unresolvedTypeToResolvedType = new Dictionary<IrUnresolvedGenericType, IrType>();

        for (int i = 0; i < paramContexts.Count && i < methodArgs.Count; i++)
        {
            var paramCtx = paramContexts[i];
            var paramTypeName = paramCtx.type().GetText();
            var argType = methodArgs[i].Type;

            // Check if parameter type is a generic parameter (e.g., "T")
            if (genericParams.Contains(paramTypeName))
            {
                // This parameter has a generic type - use the argument's type
                var paramIndex = genericParams.IndexOf(paramTypeName);

                if (paramIndex < partialType.TypeArguments.Count &&
                    partialType.TypeArguments[paramIndex] is IrUnresolvedGenericType unresolvedType)
                {
                    unresolvedTypeToResolvedType[unresolvedType] = argType;
                }
            }
        }

        // Resolve all unresolved type parameters
        bool allResolved = true;
        foreach (var typeArg in partialType.TypeArguments)
        {
            if (typeArg is IrUnresolvedGenericType unresolvedType)
            {
                if (unresolvedTypeToResolvedType.TryGetValue(unresolvedType, out var resolvedType))
                {
                    unresolvedType.ResolvedType = resolvedType;
                }
                else
                {
                    allResolved = false;
                }
            }
        }

        if (!allResolved)
        {
            return null; // Couldn't resolve all type parameters
        }

        // Now that all type parameters are resolved, create the monomorphized struct
        var resolvedTypeArgs = partialType.GetResolvedTypeArguments();

        // Find the generic struct template
        var genericStruct = _symbols.GetLocalStructs().Values.FirstOrDefault(s =>
            s.StructName == partialType.GenericTypeName && s.GenericParameters.Count > 0);

        if (genericStruct == null)
        {
            return null;
        }

        // Create monomorphized struct
        var monomorphizedStruct = IrStructType.Monomorphize(genericStruct, resolvedTypeArgs);

        // Cache the monomorphized struct
        var cacheKey = monomorphizedStruct.CacheKey ?? monomorphizedStruct.Name;
        if (_symbols.LookupMonomorphizedStruct(cacheKey) == null)
        {
            _symbols.RegisterMonomorphizedStruct(cacheKey, monomorphizedStruct);
        }

        // Update the partial type to mark it as fully resolved
        partialType.FullyResolvedType = monomorphizedStruct;

        return monomorphizedStruct;
    }

    /// <summary>
    /// Handle method calls (e.g., v.len())
    /// Desugars to: Type::method(receiver, args...)
    /// </summary>
    private object? HandleMethodCallIr(NovusParser.CallExprContext callCtx, NovusParser.MemberAccessExprContext memberAccessCtx)
    {
        // Get the receiver (the thing before the dot)
        var receiverExpr = memberAccessCtx.expression();
        var methodName = memberAccessCtx.IDENTIFIER().GetText();

        // Evaluate the receiver
        var receiver = (IrValue?)Visit(receiverExpr);
        if (receiver == null)
        {
            var errorLocation = SourceLocationHelper.FromContext(callCtx, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Method call receiver is null",
                errorLocation
            );
            return null;
        }

        // Handle unresolved generic types - resolve them from method arguments
        if (receiver.Type is IrPartiallyResolvedGenericType partialType)
        {
            // Parse method arguments to infer the generic type
            var methodArgs = new List<IrValue>();
            if (callCtx.argumentList() != null)
            {
                foreach (var argCtx in callCtx.argumentList().expression())
                {
                    var argValue = (IrValue?)Visit(argCtx);
                    if (argValue != null)
                    {
                        methodArgs.Add(argValue);
                    }
                }
            }

            // Try to resolve the generic type from method arguments
            var resolvedType = TryResolveGenericFromMethodCall(partialType, methodName, methodArgs);
            if (resolvedType != null)
            {
                // Update the receiver's type
                if (receiver is IrVariable irVar)
                {
                    receiver = new IrVariable(irVar.Name, resolvedType);

                    // Also update the local variable's type in the symbol table
                    if (_localVariables.TryGetValue(irVar.Name, out var localVar))
                    {
                        localVar.Type = resolvedType;
                    }
                }
            }
            else
            {
                var errorLocation = SourceLocationHelper.FromContext(callCtx, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Cannot infer generic type parameters for '{partialType.GenericTypeName}' from method call '{methodName}'",
                    errorLocation
                );
                return null;
            }
        }

        // Get the type name for method lookup
        string typeName;
        var receiverType = receiver.Type;

        if (receiverType is IrStructType structType)
        {
            typeName = structType.StructName;  // Use base name, not full generic name
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
                typeName = pointeeStruct.StructName;  // Use base name, not full generic name
            }
            else if (ptrType.PointeeType is IrEnumType pointeeEnum)
            {
                typeName = pointeeEnum.EnumName;
            }
            else
            {
                // Allow methods on primitive types (u64, bool, etc.)
                typeName = ptrType.PointeeType.Name;
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
            else
            {
                // Allow methods on primitive types (u64, bool, etc.)
                typeName = refType.PointeeType.Name;
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
            else
            {
                // Allow methods on primitive types (u64, bool, etc.)
                typeName = mutRefType.PointeeType.Name;
            }
        }
        else
        {
            var errorLocation = SourceLocationHelper.FromContext(callCtx, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.CannotCallMethodOnType,
                $"Cannot call methods on type: {receiverType.Name}",
                errorLocation
            );
            return null;
        }

        // Build the mangled function name: Type::method
        // Note: For monomorphized generic types, we'll try Type::method first,
        // then fall back to instantiation if needed
        var mangledMethodName = $"{typeName}::{methodName}";

        // Look up the method
        var method = _module.Functions.FirstOrDefault(f => f.Name == mangledMethodName);

        // If method not found, try to instantiate it for monomorphized structs or enums
        if (method == null)
        {
            IrStructType? monomorphizedStruct = null;
            IrEnumType? monomorphizedEnum = null;

            // Check if receiver is a monomorphized struct
            if (receiverType is IrStructType receiverStruct && receiverStruct.CacheKey != null)
            {
                monomorphizedStruct = receiverStruct;
            }
            // Check if receiver is a pointer to a monomorphized struct (&Vec<i32>, &mut Vec<i32>)
            else if (receiverType is IrPointerType ptrType && ptrType.PointeeType is IrStructType pointeeStruct && pointeeStruct.CacheKey != null)
            {
                monomorphizedStruct = pointeeStruct;
            }
            // Check if receiver is a reference to a monomorphized struct (&Vec<i32>)
            else if (receiverType is IrReferenceType refType && refType.PointeeType is IrStructType refPointeeStruct && refPointeeStruct.CacheKey != null)
            {
                monomorphizedStruct = refPointeeStruct;
            }
            // Check if receiver is a mutable reference to a monomorphized struct (&mut Vec<i32>)
            else if (receiverType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType mutRefPointeeStruct && mutRefPointeeStruct.CacheKey != null)
            {
                monomorphizedStruct = mutRefPointeeStruct;
            }
            // Check if receiver is a monomorphized enum
            else if (receiverType is IrEnumType receiverEnum && receiverEnum.CacheKey != null)
            {
                monomorphizedEnum = receiverEnum;
            }
            // Check if receiver is a pointer to a monomorphized enum (&Option<i32>, &mut Option<i32>)
            else if (receiverType is IrPointerType ptrType2 && ptrType2.PointeeType is IrEnumType pointeeEnum && pointeeEnum.CacheKey != null)
            {
                monomorphizedEnum = pointeeEnum;
            }
            // Check if receiver is a reference to a monomorphized enum (&Option<i32>)
            else if (receiverType is IrReferenceType refType2 && refType2.PointeeType is IrEnumType refPointeeEnum && refPointeeEnum.CacheKey != null)
            {
                monomorphizedEnum = refPointeeEnum;
            }
            // Check if receiver is a mutable reference to a monomorphized enum (&mut Option<i32>)
            else if (receiverType is IrMutReferenceType mutRefType2 && mutRefType2.PointeeType is IrEnumType mutRefPointeeEnum && mutRefPointeeEnum.CacheKey != null)
            {
                monomorphizedEnum = mutRefPointeeEnum;
            }
            // Check if receiver is a generic enum template (not yet monomorphized)
            // This can happen when a variable has a declared type that includes type arguments,
            // but the receiver's type is still the generic template (e.g., Option<T> instead of Option<i32>)
            else if (receiverType is IrEnumType receiverEnumTemplate &&
                     receiverEnumTemplate.GenericParameters.Count > 0 &&
                     receiverEnumTemplate.CacheKey == null)
            {
                // Try to find the monomorphized version by looking up the variable's declared type
                // This happens when: let opt1: Option<i32> = ... then opt1.is_some()
                // The receiver variable should have the monomorphized type in _localVariables or function parameters
                if (receiver is IrVariable irVar)
                {
                    // Check local variables first
                    if (_localVariables.TryGetValue(irVar.Name, out var localVar))
                    {
                        // The local variable's type should be the monomorphized version
                        if (localVar.Type is IrEnumType localEnumType && localEnumType.CacheKey != null)
                        {
                            monomorphizedEnum = localEnumType;
                            // Update the receiver to use the correct monomorphized type
                            receiver = new IrVariable(irVar.Name, localEnumType);
                        }
                    }
                    // Check function parameters
                    else if (_currentFunction != null)
                    {
                        var param = _currentFunction.Parameters.FirstOrDefault(p => p.Name == irVar.Name);
                        if (param != null && param.Type is IrEnumType paramEnumType && paramEnumType.CacheKey != null)
                        {
                            monomorphizedEnum = paramEnumType;
                            // Update the receiver to use the correct monomorphized type
                            receiver = new IrVariable(irVar.Name, paramEnumType);
                        }
                    }
                }
            }

            if (monomorphizedStruct != null)
            {
                method = InstantiateGenericMethod(monomorphizedStruct, methodName);
            }
            else if (monomorphizedEnum != null)
            {
                // Parse arguments to pass to instantiation (needed for type inference)
                var methodArgs = new List<IrValue>();
                if (callCtx.argumentList() != null)
                {
                    foreach (var argCtx in callCtx.argumentList().expression())
                    {
                        var argValue = (IrValue?)Visit(argCtx);
                        if (argValue != null)
                        {
                            methodArgs.Add(argValue);
                        }
                    }
                }

                method = InstantiateGenericEnumMethod(monomorphizedEnum, methodName, methodArgs);
            }
        }

        // If method not found for structs, try to instantiate it for monomorphized enums
        if (method == null)
        {
            IrEnumType? monomorphizedEnum = null;

            // Check if receiver is a monomorphized enum (e.g., Option<i32>)
            if (receiverType is IrEnumType receiverEnum && receiverEnum.CacheKey != null)
            {
                monomorphizedEnum = receiverEnum;
            }
            // Check if receiver is a pointer to a monomorphized enum
            else if (receiverType is IrPointerType ptrType && ptrType.PointeeType is IrEnumType pointeeEnum && pointeeEnum.CacheKey != null)
            {
                monomorphizedEnum = pointeeEnum;
            }

            if (monomorphizedEnum != null)
            {
                // Build complete arguments list: [receiver, ...user_args]
                // This is needed for type inference to work properly
                var allArgs = new List<IrValue> { receiver };

                // Add user-provided arguments
                if (callCtx.argumentList() != null)
                {
                    foreach (var argCtx in callCtx.argumentList().expression())
                    {
                        var argValue = (IrValue?)Visit(argCtx);
                        if (argValue != null)
                        {
                            allArgs.Add(argValue);
                        }
                    }
                }

                // Instantiate the generic enum method with all arguments (including receiver)
                // The method will infer generic type parameters from the argument types
                method = InstantiateGenericEnumMethod(monomorphizedEnum, methodName, allArgs);
            }
        }

        if (method == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                $"Method '{methodName}' not found for type '{typeName}'",
                errorLocation
            );
            return null;
        }

        // Build arguments list: [receiver, ...user_args]
        // Check if we need to borrow the receiver (for &self or &mut self)
        IrValue receiverArg = receiver;
        if (method.Parameters.Count > 0)
        {
            var firstParamType = method.Parameters[0].Type;

            // If method expects a pointer/reference but we have a value, borrow it
            if ((firstParamType is IrPointerType || firstParamType is IrReferenceType || firstParamType is IrMutReferenceType)
                && receiver.Type is not IrPointerType && receiver.Type is not IrReferenceType && receiver.Type is not IrMutReferenceType)
            {
                // IMPORTANT FIX: If the receiver was loaded from a field access (e.g., self.buffer),
                // we need to borrow the FIELD directly, not the loaded copy.
                // Check if the last instruction is a member access that produced this receiver variable.
                IrValue valueToBoflow = receiver;

                if (receiver is IrVariable receiverVar && _currentBlock != null)
                {
                    // Look for the member access instruction that produced this variable
                    // Search backwards through the block's instructions
                    IrMemberAccess? foundMemberAccess = null;
                    for (int i = _currentBlock.Instructions.Count - 1; i >= 0; i--)
                    {
                        var inst = _currentBlock.Instructions[i];
                        if (inst is IrMemberAccess memberAccess &&
                            memberAccess.ResultName == receiverVar.Name)
                        {
                            foundMemberAccess = memberAccess;
                            _currentBlock.Instructions.RemoveAt(i);
                            break;
                        }
                        // Stop searching if we hit an instruction that might use this variable
                        // (this avoids removing a member access that was already used)
                        if (inst is IrCall || inst is IrStore)
                        {
                            break;
                        }
                    }

                    if (foundMemberAccess != null)
                    {
                        // Found it! Create a field reference instead of using the loaded value
                        valueToBoflow = new IrFieldReference(foundMemberAccess.Struct, foundMemberAccess.FieldName, receiver.Type);
                    }
                }

                // Wrap receiver in IrBorrowValue to take its address
                bool isMutable = firstParamType is IrMutReferenceType || firstParamType is IrPointerType;
                receiverArg = new IrBorrowValue(valueToBoflow, firstParamType, isMutable);
            }
        }

        var arguments = new List<IrValue> { receiverArg };

        // Add user-provided arguments
        if (callCtx.argumentList() != null)
        {
            foreach (var argCtx in callCtx.argumentList().expression())
            {
                var argValue = (IrValue?)Visit(argCtx);
                if (argValue != null)
                {
                    arguments.Add(argValue);
                }
            }
        }

        // Validate argument count
        var nonVariadicCount = method.Parameters.Count(p => !p.IsVariadic);
        if (method.IsVariadic)
        {
            if (arguments.Count < nonVariadicCount)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Variadic method '{methodName}' expects at least {nonVariadicCount} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }
        }
        else
        {
            if (arguments.Count != method.Parameters.Count)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Method {methodName} expects {method.Parameters.Count} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }
        }

        // Note: Str -> *u8 coercion is already handled in VisitCallExpr

        // Create the call instruction
        // Use the actual function name from the method (e.g., "Vec_push" for monomorphized generics)
        // instead of the mangled name (e.g., "Vec::push")
        var returnType = method.ReturnType;
        var resultName = returnType is not IrVoidType ? $"%t{_tempCounter++}" : null;

        var call = new IrCall(method.Name, returnType, resultName);
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
                var fpType = _typeInterner.GetFunctionPointerType(paramTypes, function.ReturnType);
                return new IrFunctionAddress(name, fpType);
            }
        }

        // For variables, struct members, array elements, etc., create a pointer
        // In Novus, & produces pointer types, not reference types
        // Visit the expression to get its value
        var value = (IrValue)Visit(exprContext)!;

        // Create a pointer type (& in Novus produces *T, not &T)
        var ptrType = _typeInterner.GetPointerType(value.Type);

        // For code generation, pointers are addresses
        // We return the value itself - the semantic analyzer will track borrowing
        // At codegen time, we'll take the address of the value

        // Create a "borrow" value that wraps the original value with pointer type
        return new IrBorrowValue(value, ptrType, isMutable);
    }

    public override object? VisitIndexExpr([NotNull] NovusParser.IndexExprContext context)
    {
        var baseExpr = (IrValue)Visit(context.expression(0))!;
        var indexExpr = (IrValue)Visit(context.expression(1))!;

        // Handle array indexing
        if (baseExpr.Type is IrArrayType arrayType)
        {
            // Create an index access instruction for arrays
            var tempName = $"%t{_tempCounter++}";
            var indexAccess = new IrIndexAccess(tempName, baseExpr, indexExpr, arrayType.ElementType);
            _currentBlock!.AddInstruction(indexAccess);

            return new IrVariable(tempName, arrayType.ElementType);
        }

        // Handle pointer indexing: ptr[index] = *(ptr + index * sizeof(T))
        if (baseExpr.Type is IrPointerType ptrType)
        {
            // Create an index access instruction for pointers
            var tempName = $"%t{_tempCounter++}";
            var indexAccess = new IrIndexAccess(tempName, baseExpr, indexExpr, ptrType.PointeeType);
            _currentBlock!.AddInstruction(indexAccess);

            return new IrVariable(tempName, ptrType.PointeeType);
        }

        var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Cannot index into non-array/non-pointer type: {baseExpr.Type.Name}",
            errorLocation
        );
        return null;
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
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Array literals cannot be empty",
                errorLocation
            );
            return null;
        }

        // Infer array type from first element
        var elementType = elements[0].Type;
        var arrayType = _typeInterner.GetArrayType(elementType, elements.Count);

        // Create array literal value
        var arrayLiteral = new IrArrayLiteral(arrayType);
        foreach (var elem in elements)
        {
            // TODO: Check that all elements have compatible types
            arrayLiteral.Elements.Add(elem);
        }

        return arrayLiteral;
    }

    public override object? VisitArrayRepeatLiteral([NotNull] NovusParser.ArrayRepeatLiteralContext context)
    {
        // Array repeat literal: [value; count]
        var expressions = context.expression();
        if (expressions.Length != 2)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Array repeat literal must have exactly 2 expressions",
                errorLocation
            );
            return null;
        }

        // Visit the value expression
        var value = (IrValue)Visit(expressions[0])!;

        // Visit the count expression - must be a constant
        var countExpr = (IrValue)Visit(expressions[1])!;
        if (countExpr is not IrConstant countConstant)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidArrayRepeatCount,
                "Array repeat count must be a compile-time constant",
                errorLocation
            );
            return null;
        }

        // Extract count value (handle all integer types)
        int count = countConstant.Type switch
        {
            IrIntType intType => intType.IsSigned switch
            {
                true => intType.BitWidth switch
                {
                    8 => (sbyte)countConstant.Value,
                    16 => (short)countConstant.Value,
                    32 => (int)countConstant.Value,
                    64 => (int)(long)countConstant.Value,
                    _ => 0  // ERROR: $"Unsupported signed integer bit width: {intType.BitWidth}"
                },
                false => intType.BitWidth switch
                {
                    8 => (byte)countConstant.Value,
                    16 => (ushort)countConstant.Value,
                    32 => (int)(uint)countConstant.Value,
                    64 => (int)(ulong)countConstant.Value,
                    _ => 0  // ERROR: $"Unsupported unsigned integer bit width: {intType.BitWidth}
                }
            },
            _ => 0  // ERROR: "Array repeat count must be an integer"
        };

        if (count < 0)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidArrayRepeatCount,
                "Array repeat count must be non-negative",
                errorLocation
            );
            return null;
        }

        // Create array type
        var arrayType = _typeInterner.GetArrayType(value.Type, count);

        // Create array literal with repeated value
        var arrayLiteral = new IrArrayLiteral(arrayType);
        for (int i = 0; i < count; i++)
        {
            arrayLiteral.Elements.Add(value);
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

        var op = context.GetChild(1).GetText(); // Get the operator: << or >>
        var opKind = op == "<<" ? IrBinaryOp.OpKind.Shl : IrBinaryOp.OpKind.Shr;

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, opKind, left, right, left.Type);
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

        // Create an explicit cast value
        // This preserves the cast operation for the code generator
        // Supports nested casts: (T1)(T2)expr becomes IrCastValue(IrCastValue(expr, T2), T1)
        return new IrCastValue(value, value.Type, targetType);
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
            _ => IrBinaryOp.OpKind.Add  // ERROR: $"Unknown operator: {opText}"
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
            _ => IrBinaryOp.OpKind.Add  // ERROR: $"Unknown comparison operator: {opText}"
        };

        var tempName = $"%t{_tempCounter++}";
        // Comparison result is a boolean
        var binOp = new IrBinaryOp(tempName, op, left, right, IrBoolType.Instance);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, IrBoolType.Instance);
    }

    public override object? VisitUnaryExpr([NotNull] NovusParser.UnaryExprContext context)
    {
        SourceLocation errorLocation;
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
                errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.CannotDereferenceType,
                    $"Cannot dereference non-pointer/reference type: {operand.Type.Name}",
                    errorLocation
                );
                return null;
            }

            // Create a dereference value
            return new IrDereferenceValue(operand, pointeeType);
        }

        // For other unary ops, visit the operand first
        var operandValue = (IrValue)Visit(context.expression())!;

        if (op == "!")
        {
            // Logical NOT: false becomes true, true becomes false
            // Implemented as: result = (operand XOR 1)
            // This flips the boolean bit: 0 XOR 1 = 1, 1 XOR 1 = 0
            var tempName = $"%t{_tempCounter++}";
            var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Xor, operandValue, new IrConstant(1, new IrIntType(32, false)), IrBoolType.Instance);
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

        errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
        _diagnostics.ReportError(
            ErrorCodes.UnknownOperator,
            $"Unknown unary operator: {op}",
            errorLocation
        );
        return null;
    }

    public override object? VisitPostIncrementExpr([NotNull] NovusParser.PostIncrementExprContext context)
    {
        // Post-increment: return old value, but increment the lvalue
        return HandlePostIncrementDecrement(context.expression(), isIncrement: true);
    }

    public override object? VisitPostDecrementExpr([NotNull] NovusParser.PostDecrementExprContext context)
    {
        // Post-decrement: return old value, but decrement the lvalue
        return HandlePostIncrementDecrement(context.expression(), isIncrement: false);
    }

    public override object? VisitTryExpr([NotNull] NovusParser.TryExprContext context)
    {
        // The ? operator for Result propagation with automatic error conversion via From trait
        // expr? desugars to:
        // match expr {
        //     Ok(val) => val,
        //     Err(err) => return Err(TargetError::convert(err))  // Auto-convert if needed
        // }

        var innerExpr = Visit(context.expression()) as IrValue;
        if (innerExpr == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidContext,
                "? operator requires an expression that returns a value",
                errorLocation
            );
            return null;
        }

        // 1. Verify innerExpr type is Result<T, E>
        if (innerExpr.Type is not IrEnumType resultType || resultType.EnumName != "Result")
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidContext,
                $"? operator requires a Result<T, E> type, got {innerExpr.Type}",
                errorLocation
            );
            return null;
        }

        // Extract T and E types from Result<T, E>
        // Result has two variants: Ok(T) and Err(E)
        var okVariant = resultType.Variants.FirstOrDefault(v => v.Name == "Ok");
        if (okVariant == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidType,
                "Result type missing Ok variant",
                errorLocation
            );
            return null;
        }

        var errVariant = resultType.Variants.FirstOrDefault(v => v.Name == "Err");
        if (errVariant == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidType,
                "Result type missing Err variant",
                errorLocation
            );
            return null;
        }

        if (okVariant.AssociatedData.Count == 0)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Result::Ok variant missing associated data",
                errorLocation
            );
            return null;
        }
        if (errVariant.AssociatedData.Count == 0)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Result::Err variant missing associated data",
                errorLocation
            );
            return null;
        }

        var okPayloadType = okVariant.AssociatedData[0];
        var sourceErrorType = errVariant.AssociatedData[0];

        // 2. Get current function's return type Result<T2, E2>
        if (_currentFunction == null)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidContext,
                "? operator can only be used inside a function",
                errorLocation
            );
            return null;
        }

        if (_currentFunction.ReturnType is not IrEnumType funcResultType || funcResultType.EnumName != "Result")
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidContext,
                $"? operator requires current function to return Result<T, E>, got {_currentFunction.ReturnType}",
                errorLocation
            );
            return null;
        }

        var funcErrVariant = funcResultType.Variants.FirstOrDefault(v => v.Name == "Err");
        if (funcErrVariant == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidType,
                "Function return type Result missing Err variant",
                errorLocation
            );
            return null;
        }

        if (funcErrVariant.AssociatedData.Count == 0)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Function return type Result::Err missing associated data",
                errorLocation
            );
            return null;
        }

        var targetErrorType = funcErrVariant.AssociatedData[0];

        // 3. Generate match expression to unwrap Result
        // This is similar to VisitMatchExpr, but we generate the match structure in IR directly

        // Create temporary variable to hold the result expression
        var resultTemp = $"%try_result_{_tempCounter++}";
        var resultLocal = new IrLocalVariable(resultTemp, innerExpr.Type, false);
        _currentFunction.LocalVariables.Add(resultLocal);
        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, innerExpr.Type, false, innerExpr));
        var resultVar = new IrVariable(resultTemp, innerExpr.Type);

        // Create a variable to hold the unwrapped Ok value (declared before branching)
        var okValueTemp = $"%try_ok_val_{_tempCounter++}";
        var okValueLocal = new IrLocalVariable(okValueTemp, okPayloadType, true);
        _currentFunction.LocalVariables.Add(okValueLocal);
        _localVariables[okValueTemp] = okValueLocal;

        // Initialize with a default value (will be overwritten in Ok branch)
        IrValue defaultValue = okPayloadType is IrIntType intType
            ? new IrConstant(0, intType)
            : okPayloadType is IrBoolType
                ? new IrBoolConstant(false)
                : new IrConstant(0, okPayloadType);
        _currentBlock.AddInstruction(new IrLocalDecl(okValueTemp, okPayloadType, true, defaultValue));

        // Create blocks for Ok and Err branches
        var okBlock = _currentFunction.CreateBasicBlock($"try_ok_{_tempCounter}");
        var errBlock = _currentFunction.CreateBasicBlock($"try_err_{_tempCounter}");
        var continueBlock = _currentFunction.CreateBasicBlock($"try_continue_{_tempCounter}");

        // Test which variant we have
        var tagTemp = $"%try_tag_{_tempCounter++}";
        _currentBlock.AddInstruction(new IrExtractTag(tagTemp, resultVar));
        var tagVar = new IrVariable(tagTemp, IrIntType.I32);

        // Branch on tag: compare with Ok tag
        var okTagValue = new IrConstant(okVariant.Tag, IrIntType.I32);
        var isOkTemp = $"%try_isok_{_tempCounter++}";
        _currentBlock.AddInstruction(new IrBinaryOp(
            isOkTemp,
            IrBinaryOp.OpKind.Eq,
            tagVar,
            okTagValue,
            IrBoolType.Instance
        ));
        _currentBlock.AddInstruction(new IrConditionalBranch(
            new IrVariable(isOkTemp, IrBoolType.Instance),
            okBlock.Label,
            errBlock.Label
        ));

        // Ok branch: extract value, store it, and continue
        _currentBlock = okBlock;
        okBlock.AddInstruction(new IrLabel(okBlock.Label));
        var extractedTemp = $"%try_extracted_{_tempCounter++}";
        okBlock.AddInstruction(new IrExtractVariantData(extractedTemp, resultVar, "Ok", 0, okPayloadType));
        okBlock.AddInstruction(new IrStore(okValueTemp, new IrVariable(extractedTemp, okPayloadType)));
        okBlock.AddInstruction(new IrBranch(continueBlock.Label));

        // Err branch: extract error, optionally convert, and return
        _currentBlock = errBlock;
        errBlock.AddInstruction(new IrLabel(errBlock.Label));
        var errValueTemp = $"%try_err_val_{_tempCounter++}";
        errBlock.AddInstruction(new IrExtractVariantData(errValueTemp, resultVar, "Err", 0, sourceErrorType));
        var errVar = new IrVariable(errValueTemp, sourceErrorType);

        // 4. If E != E2, look for From<E> impl for E2 and call convert()
        IrValue finalError;
        if (!TypesEqual(sourceErrorType, targetErrorType))
        {
            // Need to convert error via From<E>::convert()
            // Look for From<sourceErrorType> impl for targetErrorType
            var sourceTypeName = GetTypeName(sourceErrorType);
            var targetTypeName = GetTypeName(targetErrorType);

            // Find the From<sourceType> trait impl for targetType
            var convertMethodName = _module.FindTraitMethod(targetTypeName, "convert");

            if (convertMethodName == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Cannot convert {sourceTypeName} to {targetTypeName}: no From<{sourceTypeName}> implementation found for {targetTypeName}",
                    errorLocation
                );
                return null;
            }

            // Call the convert method
            var convertedTemp = $"%try_converted_{_tempCounter++}";
            var convertCall = new IrCall(convertMethodName, targetErrorType, convertedTemp);
            convertCall.Arguments.Add(errVar);
            errBlock.AddInstruction(convertCall);
            finalError = new IrVariable(convertedTemp, targetErrorType);
        }
        else
        {
            // No conversion needed
            finalError = errVar;
        }

        // Construct Result::Err(finalError) and return it
        var returnErrTemp = $"%try_return_err_{_tempCounter++}";
        var returnErrLocal = new IrLocalVariable(returnErrTemp, funcResultType, false);
        _currentFunction.LocalVariables.Add(returnErrLocal);
        var funcErrTag = funcErrVariant.Tag;
        var returnErrValue = new IrEnumValue(funcResultType, "Err", funcErrTag, new List<IrValue> { finalError });
        errBlock.AddInstruction(new IrLocalDecl(returnErrTemp, funcResultType, false, returnErrValue));
        errBlock.AddInstruction(new IrReturn(new IrVariable(returnErrTemp, funcResultType)));

        // Continue block: the value from Ok is the result of this expression
        _currentBlock = continueBlock;
        continueBlock.AddInstruction(new IrLabel(continueBlock.Label));
        return new IrVariable(okValueTemp, okPayloadType);
    }

    private bool TypesEqual(IrType a, IrType b)
    {
        // Simple type equality check - uses Name property for comparison
        return a.Name == b.Name;
    }

    private string GetTypeName(IrType type)
    {
        return type switch
        {
            IrEnumType enumType => enumType.Name,
            IrStructType structType => structType.Name,
            IrIntType intType => intType.IsSigned ? $"i{intType.BitWidth}" : $"u{intType.BitWidth}",
            IrBoolType => "bool",
            IrPointerType ptrType => $"*{GetTypeName(ptrType.PointeeType)}",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Attempts to convert a value to a target type using the From<SourceType> trait.
    /// Returns the converted value if successful, or null if no conversion is available.
    /// This enables automatic error conversion in Result::Err and similar contexts.
    /// </summary>
    private IrValue? TryConvertViaFromTrait(IrValue sourceValue, IrType targetType)
    {
        var sourceType = sourceValue.Type;
        var sourceTypeName = GetTypeName(sourceType);
        var targetTypeName = GetTypeName(targetType);

        // Look for From<sourceType> trait implementation for targetType
        var convertMethodName = _module.FindTraitMethod(targetTypeName, "convert");

        if (convertMethodName == null)
        {
            // No From trait implementation found
            return null;
        }

        // Generate IR to call the convert method
        // Pattern from ? operator: call From<SourceType>::convert(sourceValue)
        var convertedTemp = $"%from_converted_{_tempCounter++}";
        var convertCall = new IrCall(convertMethodName, targetType, convertedTemp);
        convertCall.Arguments.Add(sourceValue);

        // Add the call instruction to current block
        if (_currentBlock != null)
        {
            _currentBlock.AddInstruction(convertCall);
        }

        return new IrVariable(convertedTemp, targetType);
    }

    private IrValue HandlePostIncrementDecrement(ParserRuleContext exprContext, bool isIncrement)
    {
        // For post-inc/dec, we need to:
        // 1. Load current value and save it
        // 2. Increment/decrement the lvalue
        // 3. Return the saved old value

        // Unwrap parentheses if present (e.g., (*p)++ should work)
        exprContext = UnwrapParentheses(exprContext);

        // First, use the same logic as pre-inc/dec to compute and store the new value
        // But we need to save the old value first

        // Load the current value
        var currentValue = (IrValue)Visit(exprContext)!;

        // Save the old value to return later
        var oldValueTemp = $"%post{(isIncrement ? "inc" : "dec")}_save{_tempCounter++}";
        var oldValueLocal = new IrLocalVariable(oldValueTemp, currentValue.Type, false);
        _currentFunction!.LocalVariables.Add(oldValueLocal);
        _currentBlock!.AddInstruction(new IrStore(oldValueTemp, currentValue));

        // Compute the new value (current +/- 1)
        var newValueTemp = $"%t{_tempCounter++}";
        var op = isIncrement
            ? new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Add, currentValue, new IrConstant(1, currentValue.Type), currentValue.Type)
            : new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Sub, currentValue, new IrConstant(1, currentValue.Type), currentValue.Type);
        _currentBlock.AddInstruction(op);

        var newValue = new IrVariable(newValueTemp, currentValue.Type);

        // Now store the new value back to the lvalue (same logic as pre-inc/dec)
        StoreToLvalue(exprContext, newValue);

        // Return the old value
        return new IrVariable(oldValueTemp, currentValue.Type);
    }

    private void StoreToLvalue(ParserRuleContext exprContext, IrValue value)
    {
        SourceLocation errorLocation;

        // Case 0: Parenthesized expression - unwrap and recurse
        if (exprContext is NovusParser.ParenExprContext parenCtx)
        {
            StoreToLvalue(parenCtx.expression(), value);
            return;
        }

        // Case 1: Simple variable (identifier)
        if (exprContext is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            var varName = identCtx.identifier().GetText();
            _currentBlock!.AddInstruction(new IrStore(varName, value));
            return;
        }

        // Case 2: Member access (obj.field)
        if (exprContext is NovusParser.MemberAccessExprContext memberCtx)
        {
            var baseExpr = (IrValue)Visit(memberCtx.expression())!;
            var memberName = memberCtx.IDENTIFIER().GetText();

            // Auto-dereference pointers and references to structs (like in VisitMemberAccessExpr)
            IrValue actualBase = baseExpr;
            IrType baseType = baseExpr.Type;

            if (baseType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
            {
                // Auto-dereference the pointer - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, ptrType.PointeeType);
                baseType = ptrType.PointeeType;
            }
            else if (baseType is IrReferenceType refType && refType.PointeeType is IrStructType)
            {
                // Auto-dereference the reference - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, refType.PointeeType);
                baseType = refType.PointeeType;
            }
            else if (baseType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType)
            {
                // Auto-dereference the mutable reference - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, mutRefType.PointeeType);
                baseType = mutRefType.PointeeType;
            }

            if (baseType is not IrStructType structType)
            {
                errorLocation = SourceLocationHelper.FromContext(exprContext, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.CannotAccessMember,
                    $"Cannot access member '{memberName}' on non-struct type '{baseType}'",
                    errorLocation
                );
                return;
            }

            var field = structType.Fields.FirstOrDefault(f => f.Name == memberName);
            if (field == null)
            {
                errorLocation = SourceLocationHelper.FromContext(exprContext, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Struct '{structType.Name}' has no field '{memberName}'",
                    errorLocation
                );
                return;
            }

            _currentBlock!.AddInstruction(new IrMemberStore(actualBase, memberName, field.Offset, value));
            return;
        }

        // Case 3: Index access (arr[i])
        if (exprContext is NovusParser.IndexExprContext indexCtx)
        {
            var arrayExpr = (IrValue)Visit(indexCtx.expression(0))!;
            var indexExpr = (IrValue)Visit(indexCtx.expression(1))!;

            _currentBlock!.AddInstruction(new IrIndexStore(arrayExpr, indexExpr, value));
            return;
        }

        // Case 4: Dereference (*ptr)
        if (exprContext is NovusParser.DereferenceExprContext derefCtx)
        {
            var ptrExpr = (IrValue)Visit(derefCtx.expression())!;
            _currentBlock!.AddInstruction(new IrDereferenceStore(ptrExpr, value));
            return;
        }

        // Case 5: If we have a PrimaryExpr that we haven't handled yet, it might be wrapping something
        // This can happen with certain parse tree structures
        if (exprContext is NovusParser.PrimaryExprContext unhandledPrimaryCtx)
        {
            var child = unhandledPrimaryCtx.GetChild(0);

            // Try dereference
            if (child is NovusParser.DereferenceExprContext derefChild)
            {
                var ptrExpr = (IrValue)Visit(derefChild.expression())!;
                _currentBlock!.AddInstruction(new IrDereferenceStore(ptrExpr, value));
                return;
            }
        }

        errorLocation = SourceLocationHelper.FromContext(exprContext, _inputFilePath, _sourceLines.ToArray());
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Cannot store to expression type: {exprContext.GetType().Name}",
            errorLocation
        );
        return;
    }

    public override object? VisitPreIncrementExpr([NotNull] NovusParser.PreIncrementExprContext context)
    {
        // Pre-increment: increment the lvalue and return new value
        return HandlePreIncrementDecrement(context.expression(), isIncrement: true);
    }

    public override object? VisitPreDecrementExpr([NotNull] NovusParser.PreDecrementExprContext context)
    {
        // Pre-decrement: decrement the lvalue and return new value
        return HandlePreIncrementDecrement(context.expression(), isIncrement: false);
    }

    private ParserRuleContext UnwrapParentheses(ParserRuleContext exprContext)
    {
        // Recursively unwrap parenthesized expressions: (((*p))) -> *p
        // Parse tree structure:
        // PrimaryExprContext (expression) -> ParenExprContext (primaryExpression) -> inner expression

        bool changed = true;
        while (changed)
        {
            changed = false;

            // If it's a ParenExpr, unwrap it
            if (exprContext is NovusParser.ParenExprContext parenCtx)
            {
                exprContext = parenCtx.expression();
                changed = true;
            }
            // If it's a PrimaryExpr wrapping a ParenExpr, unwrap the ParenExpr
            else if (exprContext is NovusParser.PrimaryExprContext primaryCtx)
            {
                // The first child of PrimaryExpr is the primaryExpression
                // Check if it's a ParenExpr
                if (primaryCtx.GetChild(0) is NovusParser.ParenExprContext parenChild)
                {
                    // Get the expression inside the parentheses
                    exprContext = parenChild.expression();
                    changed = true;
                }
            }
        }

        return exprContext;
    }

    private IrValue HandlePreIncrementDecrement(ParserRuleContext exprContext, bool isIncrement)
    {
        SourceLocation errorLocation;

        // Unwrap parentheses if present (e.g., ++(*p) should work)
        exprContext = UnwrapParentheses(exprContext);

        // Case 1: Simple variable (identifier)
        if (exprContext is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            var varName = identCtx.identifier().GetText();
            var currentValue = (IrValue)Visit(exprContext)!;

            var newValueTemp = $"%t{_tempCounter++}";
            var op = isIncrement
                ? new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Add, currentValue, new IrConstant(1, currentValue.Type), currentValue.Type)
                : new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Sub, currentValue, new IrConstant(1, currentValue.Type), currentValue.Type);
            _currentBlock!.AddInstruction(op);

            var newValue = new IrVariable(newValueTemp, currentValue.Type);
            _currentBlock.AddInstruction(new IrStore(varName, newValue));
            return newValue;
        }

        // Case 2: Member access (obj.field)
        if (exprContext is NovusParser.MemberAccessExprContext memberCtx)
        {
            var baseExpr = (IrValue)Visit(memberCtx.expression())!;
            var memberName = memberCtx.IDENTIFIER().GetText();

            // Auto-dereference pointers and references to structs (like in VisitMemberAccessExpr)
            IrValue actualBase = baseExpr;
            IrType baseType = baseExpr.Type;

            if (baseType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
            {
                // Auto-dereference the pointer - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, ptrType.PointeeType);
                baseType = ptrType.PointeeType;
            }
            else if (baseType is IrReferenceType refType && refType.PointeeType is IrStructType)
            {
                // Auto-dereference the reference - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, refType.PointeeType);
                baseType = refType.PointeeType;
            }
            else if (baseType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType)
            {
                // Auto-dereference the mutable reference - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, mutRefType.PointeeType);
                baseType = mutRefType.PointeeType;
            }

            // Get the struct type and field info
            if (baseType is not IrStructType structType)
            {
                errorLocation = SourceLocationHelper.FromContext(exprContext, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.CannotAccessMember,
                    $"Cannot access member '{memberName}' on non-struct type '{baseType}'",
                    errorLocation
                );
                return null;
            }

            var field = structType.Fields.FirstOrDefault(f => f.Name == memberName);
            if (field == null)
            {
                errorLocation = SourceLocationHelper.FromContext(exprContext, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Struct '{structType.Name}' has no field '{memberName}'",
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
            var op = isIncrement
                ? new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Add, currentValue, new IrConstant(1, field.Type), field.Type)
                : new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Sub, currentValue, new IrConstant(1, field.Type), field.Type);
            _currentBlock.AddInstruction(op);

            var newValue = new IrVariable(newValueTemp, field.Type);
            _currentBlock.AddInstruction(new IrMemberStore(actualBase, memberName, field.Offset, newValue));
            return newValue;
        }

        // Case 3: Index access (arr[i])
        if (exprContext is NovusParser.IndexExprContext indexCtx)
        {
            var arrayExpr = (IrValue)Visit(indexCtx.expression(0))!;
            var indexExpr = (IrValue)Visit(indexCtx.expression(1))!;

            // Determine element type
            IrType elementType;
            if (arrayExpr.Type is IrPointerType pt)
                elementType = pt.PointeeType;
            else if (arrayExpr.Type is IrArrayType at)
                elementType = at.ElementType;
            else
            {
                errorLocation = SourceLocationHelper.FromContext(exprContext, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.CannotIndexType,
                    $"Cannot index type '{arrayExpr.Type}'",
                    errorLocation
                );
                return null;
            }

            // Load current value
            var loadTemp = $"%index_load_{_tempCounter++}";
            _currentBlock!.AddInstruction(new IrIndexAccess(loadTemp, arrayExpr, indexExpr, elementType));
            var currentValue = new IrVariable(loadTemp, elementType);

            // Increment/decrement
            var newValueTemp = $"%t{_tempCounter++}";
            var op = isIncrement
                ? new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Add, currentValue, new IrConstant(1, elementType), elementType)
                : new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Sub, currentValue, new IrConstant(1, elementType), elementType);
            _currentBlock.AddInstruction(op);

            var newValue = new IrVariable(newValueTemp, elementType);
            _currentBlock.AddInstruction(new IrIndexStore(arrayExpr, indexExpr, newValue));
            return newValue;
        }

        // Case 4: Dereference (*ptr or *ref)
        if (exprContext is NovusParser.DereferenceExprContext derefCtx)
        {
            var ptrExpr = (IrValue)Visit(derefCtx.expression())!;

            // Determine pointee type (handle pointers and references)
            IrType pointeeType;
            if (ptrExpr.Type is IrPointerType ptrType)
            {
                pointeeType = ptrType.PointeeType;
            }
            else if (ptrExpr.Type is IrReferenceType refType)
            {
                pointeeType = refType.PointeeType;
            }
            else if (ptrExpr.Type is IrMutReferenceType mutRefType)
            {
                pointeeType = mutRefType.PointeeType;
            }
            else
            {
                errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.CannotDereferenceType,
                    $"Cannot dereference non-pointer/reference type '{ptrExpr.Type}'",
                    errorLocation
                );
                return null;
            }

            // Load current value (dereference)
            var currentValue = new IrDereferenceValue(ptrExpr, pointeeType);

            // Increment/decrement
            var newValueTemp = $"%t{_tempCounter++}";
            var op = isIncrement
                ? new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Add, currentValue, new IrConstant(1, pointeeType), pointeeType)
                : new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Sub, currentValue, new IrConstant(1, pointeeType), pointeeType);
            _currentBlock!.AddInstruction(op);

            var newValue = new IrVariable(newValueTemp, pointeeType);
            _currentBlock.AddInstruction(new IrDereferenceStore(ptrExpr, newValue));
            return newValue;
        }

        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Pre-{(isIncrement ? "increment" : "decrement")} not supported for expression type: {exprContext.GetType().Name}",
            errorLocation
        );
        return null;
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

        // Add to function's local variables for stack alerrorLocation
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

        // Add to function's local variables for stack alerrorLocation
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

    public override object? VisitTernaryExpr([NotNull] NovusParser.TernaryExprContext context)
    {
        // Ternary operator: condition ? trueExpr : falseExpr
        var condition = (IrValue)Visit(context.expression(0))!;

        var trueLabel = $"ternary_true_{_labelCounter}";
        var falseLabel = $"ternary_false_{_labelCounter}";
        var endLabel = $"ternary_end_{_labelCounter}";
        _labelCounter++;

        var resultTemp = $"%t{_tempCounter++}";

        // Branch based on condition
        _currentBlock!.AddInstruction(new IrConditionalBranch(condition, trueLabel, falseLabel));

        // True branch
        _currentBlock!.AddInstruction(new IrLabel(trueLabel));
        var trueValue = (IrValue)Visit(context.expression(1))!;
        var resultType = trueValue.Type; // Get type from the true branch value

        // Add to function's local variables for stack alerrorLocation
        var localVar = new IrLocalVariable(resultTemp, resultType, false);
        _currentFunction!.LocalVariables.Add(localVar);

        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, resultType, false, trueValue));
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // False branch
        _currentBlock!.AddInstruction(new IrLabel(falseLabel));
        var falseValue = (IrValue)Visit(context.expression(2))!;
        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, resultType, false, falseValue));

        // End
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        return new IrVariable(resultTemp, resultType);
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
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Unexpected float literal type: {type.Name}",
                errorLocation
            );
            return null;
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

        // Create unique label for this string (still null-terminated in data section for C FFI)
        var label = $"_str{_stringCounter++}";
        var stringLiteral = new IrStringLiteral(stringValue, label);
        StringLiterals.Add(stringLiteral);

        // String literals now create Str struct instances: Str { ptr: *u8, len: u32 }
        var strType = _symbols.LookupStruct("Str");
        if (strType == null)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "String literals require Str type from std::strings module",
                errorLocation
            );
            return null;
        }

        // Create struct literal with ptr and len fields
        var fieldValues = new Dictionary<string, IrValue>
        {
            ["ptr"] = stringLiteral,  // IrStringLiteral is still *u8 pointer to data
            ["len"] = new IrConstant(stringValue.Length, IrIntType.U32)  // Length without null terminator
        };

        return new IrStructLiteral(strType, fieldValues);
    }

    public override object? VisitInterpolatedStringLiteral([NotNull] NovusParser.InterpolatedStringLiteralContext context)
    {
        // Auto-import std::fmt_primitives for Display implementations on primitive types
        // This allows integers, bools, etc. to be used in f-strings without explicit imports
        bool isStdLibraryModule = _inputFilePath != null && _inputFilePath.Contains(System.IO.Path.DirectorySeparatorChar + "std" + System.IO.Path.DirectorySeparatorChar);
        if (!isStdLibraryModule && !_processedModules.Contains("std::fmt_primitives"))
        {
            ImportModule("std::fmt_primitives", importAll: true);
        }

        // Get the f-string text and parse it into segments
        var fstring = context.F_STRING_LITERAL().GetText();

        // Strip 'f"' prefix and '"' suffix
        var content = fstring.Substring(2, fstring.Length - 3);

        // Parse the f-string into string and expression segments
        var segments = ParseInterpolatedString(content);

        // Look up the Formatter type and Display trait
        var formatterType = _symbols.LookupStruct("Formatter");
        if (formatterType == null)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Interpolated strings require Formatter type from std::fmt module",
                errorLocation
            );
            return null;
        }

        // Create a temporary variable for the Formatter
        var formatterVarName = $"_formatter{_tempCounter++}";

        // Call Formatter::new() to create the formatter
        // This returns Option<Formatter>, so we need to unwrap it
        var formatterNewMethodName = "Formatter::new";
        var formatterNewMethod = _module.Functions.FirstOrDefault(f => f.Name == formatterNewMethodName);
        if (formatterNewMethod == null)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                "Formatter::new() method not found. Ensure std::fmt is imported.",
                errorLocation
            );
            return null;
        }

        // Call Formatter::new()
        var formatterNewResultName = $"%t{_tempCounter++}";
        var formatterNewCall = new IrCall(formatterNewMethodName, formatterNewMethod.ReturnType, formatterNewResultName);
        _currentBlock!.AddInstruction(formatterNewCall);
        var formatterNewResult = new IrVariable(formatterNewResultName, formatterNewMethod.ReturnType);

        // Unwrap the Option<Formatter> - this should return Formatter or panic
        // For simplicity, we'll use unwrap() which panics on None
        var optionType = formatterNewMethod.ReturnType as IrEnumType;
        if (optionType == null || optionType.EnumName != "Option")
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Formatter::new() must return Option<Formatter>",
                errorLocation
            );
            return null;
        }

        // Extract the Formatter from Option::Some
        // We'll use pattern matching: match on the tag and extract the value
        var someVariant = optionType.GetVariant("Some");
        if (someVariant == null || someVariant.AssociatedData.Count != 1)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Option::Some variant not found or malformed",
                errorLocation
            );
            return null;
        }

        var formatterTypeFromOption = someVariant.AssociatedData[0];

        // Extract tag and check if it's Some
        var tagResultName = $"%t{_tempCounter++}";
        var extractTag = new IrExtractTag(tagResultName, formatterNewResult);
        _currentBlock!.AddInstruction(extractTag);

        var noneVariant = optionType.GetVariant("None");
        if (noneVariant == null)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Option::None variant not found",
                errorLocation
            );
            return null;
        }

        // Compare tag with None variant tag and panic if None
        var tagVar = new IrVariable(tagResultName, IrIntType.I32);
        var noneTagConst = new IrConstant(noneVariant.Tag, IrIntType.I32);
        var isNoneResultName = $"%t{_tempCounter++}";
        var isNoneCheck = new IrBinaryOp(isNoneResultName, IrBinaryOp.OpKind.Eq, tagVar, noneTagConst, IrBoolType.Instance);
        _currentBlock!.AddInstruction(isNoneCheck);

        var isNoneVar = new IrVariable(isNoneResultName, IrBoolType.Instance);
        var panicLabel = $"_panic{_labelCounter++}";
        var continueLabel = $"_continue{_labelCounter++}";

        _currentBlock!.AddInstruction(new IrConditionalBranch(isNoneVar, panicLabel, continueLabel));

        // Panic block
        _currentBlock!.AddInstruction(new IrLabel(panicLabel));
        var panicMessage = "Formatter::new() returned None (out of memory)";
        var panicMessageLabel = $"_str{_stringCounter++}";
        var panicMessageLiteral = new IrStringLiteral(panicMessage, panicMessageLabel);
        StringLiterals.Add(panicMessageLiteral);

        // Create source location for the panic
        var panicLocation = new SourceLocation(
            _inputFilePath ?? "unknown",
            context.Start.Line,
            context.Start.Column,
            context.GetText().Length,
            context.Start.InputStream.ToString() ?? ""
        );
        _currentBlock!.AddInstruction(new IrPanic(panicMessage, panicLocation));

        // Continue block - extract the formatter
        _currentBlock!.AddInstruction(new IrLabel(continueLabel));
        var unwrapResultName = $"%t{_tempCounter++}";
        var unwrapInstr = new IrExtractVariantData(unwrapResultName, formatterNewResult, "Some", 0, formatterTypeFromOption);
        _currentBlock!.AddInstruction(unwrapInstr);

        // Store formatter in a local variable (mutable)
        var formatterVar = new IrLocalVariable(formatterVarName, formatterTypeFromOption, true);
        _currentFunction!.LocalVariables.Add(formatterVar);
        _localVariables[formatterVarName] = formatterVar;
        var unwrappedFormatter = new IrVariable(unwrapResultName, formatterTypeFromOption);
        _currentBlock!.AddInstruction(new IrLocalDecl(formatterVarName, formatterTypeFromOption, true, unwrappedFormatter));

        // Process each segment
        foreach (var segment in segments)
        {
            if (segment.IsStringSegment)
            {
                // Call f.write_str("segment")
                EmitWriteStrLiteral(formatterVarName, formatterTypeFromOption, segment.StringContent);
            }
            else
            {
                // Parse the expression and call expr.fmt(&mut f)
                EmitFormatExpression(formatterVarName, formatterTypeFromOption, segment.Expression);
            }
        }

        // Call f.finish() to get the final String
        var formatterVarRef = new IrVariable(formatterVarName, formatterTypeFromOption);
        var finishMethodName = _module.FindTraitMethod("Formatter", "finish");
        if (finishMethodName == null)
        {
            finishMethodName = "Formatter::finish";
        }

        var finishMethod = _module.Functions.FirstOrDefault(f => f.Name == finishMethodName);
        if (finishMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                "Formatter::finish() method not found",
                errorLocation
            );
            return null;
        }

        // Call finish() - takes self by value
        var finishResultName = $"%t{_tempCounter++}";
        var finishCall = new IrCall(finishMethodName, finishMethod.ReturnType, finishResultName);
        finishCall.Arguments.Add(formatterVarRef);
        _currentBlock!.AddInstruction(finishCall);

        // Return the String result
        return new IrVariable(finishResultName, finishMethod.ReturnType);
    }

    private void EmitWriteStrLiteral(string formatterVarName, IrType formatterType, string stringContent)
    {
        // Create a string literal for the segment
        var label = $"_str{_stringCounter++}";
        var stringLiteral = new IrStringLiteral(stringContent, label);
        StringLiterals.Add(stringLiteral);

        // Create Str struct
        var strType = _symbols.LookupStruct("Str");
        if (strType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TypeNotFound,
                "Str type not found",
                errorLocation
            );
            return;
        }

        var strFieldValues = new Dictionary<string, IrValue>
        {
            ["ptr"] = stringLiteral,
            ["len"] = new IrConstant(stringContent.Length, IrIntType.U32)
        };
        var strValue = new IrStructLiteral(strType, strFieldValues);

        // Call the other EmitWriteStr method with the Str value
        EmitWriteStr(formatterVarName, formatterType, strValue);
    }

    private void EmitFormatExpression(string formatterVarName, IrType formatterType, string expressionText)
    {
        // Parse the expression text into an AST node
        var inputStream = new AntlrInputStream(expressionText);
        var lexer = new NovusLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new NovusParser(tokens);

        // Parse as an expression
        var exprContext = parser.expression();

        // Visit the expression to generate IR
        var exprValue = Visit(exprContext) as IrValue;
        if (exprValue == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Failed to evaluate expression in f-string: {expressionText}",
                errorLocation
            );
            return;
        }

        // Get the type of the expression
        var exprType = exprValue.Type;

        // Handle different types appropriately
        if (exprType is IrStructType st && st.StructName == "Str")
        {
            // For Str types, just write the string directly
            EmitWriteStr(formatterVarName, formatterType, exprValue);
        }
        else if (exprType is IrIntType intType)
        {
            // For integer types, convert to string using built-in functions
            EmitFormatInteger(formatterVarName, formatterType, exprValue, intType);
        }
        else if (exprType is IrBoolType)
        {
            // For bool, write "true" or "false"
            EmitFormatBool(formatterVarName, formatterType, exprValue);
        }
        else
        {
            // For other types, try to find Display::fmt() implementation
            var typeName = exprType is IrStructType structType ? structType.StructName : exprType.Name;
            var fmtMethodName = _module.FindTraitMethod(typeName, "fmt");
            if (fmtMethodName == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Type '{typeName}' does not implement Display trait. All types in f-strings must implement Display.",
                    errorLocation
                );
                return;
            }

            var fmtMethod = _module.Functions.FirstOrDefault(f => f.Name == fmtMethodName);
            if (fmtMethod == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.MethodNotFound,
                    $"Display::fmt() method not found for type '{typeName}'",
                    errorLocation
                );
                return;
            }

            // Call expr.fmt(&mut formatter)
            // First parameter is &self (the expression value)
            var exprBorrow = new IrBorrowValue(exprValue, fmtMethod.Parameters[0].Type, false);

            // Second parameter is &mut Formatter
            var formatterVarRef = new IrVariable(formatterVarName, formatterType);
            var formatterBorrow = new IrBorrowValue(formatterVarRef, fmtMethod.Parameters[1].Type, true);

            var fmtResultName = $"%t{_tempCounter++}";
            var fmtCall = new IrCall(fmtMethodName, fmtMethod.ReturnType, fmtResultName);
            fmtCall.Arguments.Add(exprBorrow);
            fmtCall.Arguments.Add(formatterBorrow);
            _currentBlock!.AddInstruction(fmtCall);
        }
    }

    private void EmitWriteStr(string formatterVarName, IrType formatterType, IrValue strValue)
    {
        // Find write_str method
        var writeStrMethodName = _module.FindTraitMethod("Formatter", "write_str");
        if (writeStrMethodName == null)
        {
            writeStrMethodName = "Formatter::write_str";
        }

        var writeStrMethod = _module.Functions.FirstOrDefault(f => f.Name == writeStrMethodName);
        if (writeStrMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                "Formatter::write_str() method not found",
                errorLocation
            );
            return;
        }

        // Call f.write_str(str_value)
        var formatterVarRef = new IrVariable(formatterVarName, formatterType);
        var formatterBorrow = new IrBorrowValue(formatterVarRef, writeStrMethod.Parameters[0].Type, true);
        var writeStrResultName = $"%t{_tempCounter++}";
        var writeStrCall = new IrCall(writeStrMethodName, writeStrMethod.ReturnType, writeStrResultName);
        writeStrCall.Arguments.Add(formatterBorrow);
        writeStrCall.Arguments.Add(strValue);
        _currentBlock!.AddInstruction(writeStrCall);
    }

    private void EmitFormatInteger(string formatterVarName, IrType formatterType, IrValue intValue, IrIntType intType)
    {
        // Call the Display::fmt() implementation for this integer type
        // Similar to how we handle other types in the else branch above

        var typeName = intType.Name;
        var fmtMethodName = _module.FindTraitMethod(typeName, "fmt");
        if (fmtMethodName == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Type '{typeName}' does not implement Display trait. All types in f-strings must implement Display.",
                errorLocation
            );
            return;
        }

        var fmtMethod = _module.Functions.FirstOrDefault(f => f.Name == fmtMethodName);
        if (fmtMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                $"Display::fmt() method not found for type '{typeName}'",
                errorLocation
            );
            return;
        }

        // Call intValue.fmt(&mut formatter)
        // First parameter is &self (the integer value)
        var intBorrow = new IrBorrowValue(intValue, fmtMethod.Parameters[0].Type, false);

        // Second parameter is &mut Formatter
        var formatterVarRef = new IrVariable(formatterVarName, formatterType);
        var formatterBorrow = new IrBorrowValue(formatterVarRef, fmtMethod.Parameters[1].Type, true);

        var fmtResultName = $"%t{_tempCounter++}";
        var fmtCall = new IrCall(fmtMethodName, fmtMethod.ReturnType, fmtResultName);
        fmtCall.Arguments.Add(intBorrow);
        fmtCall.Arguments.Add(formatterBorrow);
        _currentBlock!.AddInstruction(fmtCall);
    }

    private void EmitFormatBool(string formatterVarName, IrType formatterType, IrValue boolValue)
    {
        // Create string literals for "true" and "false"
        var trueLabel = $"_str{_stringCounter++}";
        var trueLiteral = new IrStringLiteral("true", trueLabel);
        StringLiterals.Add(trueLiteral);

        var falseLabel = $"_str{_stringCounter++}";
        var falseLiteral = new IrStringLiteral("false", falseLabel);
        StringLiterals.Add(falseLiteral);

        // Create Str structs
        var strType = _symbols.LookupStruct("Str");
        if (strType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TypeNotFound,
                "Str type not found",
                errorLocation
            );
            return;
        }

        var trueStr = new IrStructLiteral(strType, new Dictionary<string, IrValue>
        {
            ["ptr"] = trueLiteral,
            ["len"] = new IrConstant(4, IrIntType.U32)
        });

        var falseStr = new IrStructLiteral(strType, new Dictionary<string, IrValue>
        {
            ["ptr"] = falseLiteral,
            ["len"] = new IrConstant(5, IrIntType.U32)
        });

        // Use conditional to select the right string
        var trueLabel2 = $"_true{_labelCounter++}";
        var falseLabel2 = $"_false{_labelCounter++}";
        var endLabel = $"_end{_labelCounter++}";

        _currentBlock!.AddInstruction(new IrConditionalBranch(boolValue, trueLabel2, falseLabel2));

        // True branch
        _currentBlock!.AddInstruction(new IrLabel(trueLabel2));
        EmitWriteStr(formatterVarName, formatterType, trueStr);
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // False branch
        _currentBlock!.AddInstruction(new IrLabel(falseLabel2));
        EmitWriteStr(formatterVarName, formatterType, falseStr);
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // End
        _currentBlock!.AddInstruction(new IrLabel(endLabel));
    }

    private List<InterpolationSegment> ParseInterpolatedString(string content)
    {
        var segments = new List<InterpolationSegment>();
        var i = 0;
        var currentString = new System.Text.StringBuilder();

        while (i < content.Length)
        {
            if (content[i] == '\\' && i + 1 < content.Length)
            {
                // Handle escape sequences
                var nextChar = content[i + 1];
                if (nextChar == '{' || nextChar == '}')
                {
                    // Escaped brace: \{ -> {, \} -> }
                    currentString.Append(nextChar);
                    i += 2;
                }
                else if (nextChar == 'x' && i + 3 < content.Length)
                {
                    // Hex escape: \xNN
                    var hexDigits = content.Substring(i + 2, 2);
                    var byteValue = Convert.ToByte(hexDigits, 16);
                    currentString.Append((char)byteValue);
                    i += 4;
                }
                else
                {
                    // Standard escape sequences
                    var escapeChar = nextChar switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        'b' => '\b',
                        'f' => '\f',
                        '"' => '"',
                        '\'' => '\'',
                        '\\' => '\\',
                        '0' => '\0',
                        _ => nextChar  // Unknown escape sequence - just use the character as-is
                    };
                    currentString.Append(escapeChar);
                    i += 2;
                }
            }
            else if (content[i] == '{')
            {
                // Start of interpolation
                // Save any accumulated string content
                if (currentString.Length > 0)
                {
                    segments.Add(new InterpolationSegment { IsStringSegment = true, StringContent = currentString.ToString() });
                    currentString.Clear();
                }

                // Find the matching closing brace
                var braceDepth = 1;
                var exprStart = i + 1;
                i++;

                while (i < content.Length && braceDepth > 0)
                {
                    if (content[i] == '{')
                    {
                        braceDepth++;
                    }
                    else if (content[i] == '}')
                    {
                        braceDepth--;
                    }

                    if (braceDepth > 0)
                    {
                        i++;
                    }
                }

                if (braceDepth != 0)
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        "Unmatched braces in f-string interpolation",
                        errorLocation
                    );
                    return null;
                }

                // Extract the expression
                var expression = content.Substring(exprStart, i - exprStart);
                segments.Add(new InterpolationSegment { IsStringSegment = false, Expression = expression });
                i++; // Skip the closing brace
            }
            else
            {
                // Regular character
                currentString.Append(content[i]);
                i++;
            }
        }

        // Add any remaining string content
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
    }

    public override object? VisitSizeofExpr([NotNull] NovusParser.SizeofExprContext context)
    {
        // @sizeof(Type) - compile-time intrinsic that returns size in bytes as u32
        var typeCtx = context.type();
        var targetType = ParseType(typeCtx);

        if (targetType == null)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"could not determine type for @sizeof",
                errorLocation
            );
            return null;
        }

        // Return the size as a constant u32 value
        var sizeInBytes = (int)targetType.SizeInBytes;
        return new IrConstant(sizeInBytes, IrIntType.U32);
    }

    private string ProcessEscapeSequences(string input)
    {
        // First handle hex escapes (\xNN) before other replacements
        // This prevents issues with backslashes in the hex escape pattern
        var result = System.Text.RegularExpressions.Regex.Replace(
            input,
            @"\\x([0-9A-Fa-f]{2})",
            m => ((char)Convert.ToByte(m.Groups[1].Value, 16)).ToString()
        );

        // Then handle standard escape sequences
        return result
            .Replace("\\n", "\n")
            .Replace("\\t", "\t")
            .Replace("\\r", "\r")
            .Replace("\\b", "\b")
            .Replace("\\f", "\f")
            .Replace("\\\"", "\"")
            .Replace("\\'", "'")
            .Replace("\\\\", "\\");
    }

    public override object? VisitIdentifierExpr([NotNull] NovusParser.IdentifierExprContext context)
    {
        var name = context.identifier().GetText();

        // Check if this is a qualified enum constructor or associated function (e.g., Result::Ok, Vec::new)
        if (name.Contains("::"))
        {
            var parts = name.Split("::");
            if (parts.Length == 2)
            {
                var typeName = parts[0];
                var memberName = parts[1];

                // Try enum variant first
                if (_symbols.HasEnum(typeName))
                {
                    var enumType = _symbols.LookupEnum(typeName)!;
                    var variant = enumType.GetVariant(memberName);

                    if (variant != null)
                    {
                        // Use expected type if it's a more specific (concrete) version of this enum
                        var concreteEnumType = enumType;
                        if (_expectedType is IrEnumType expectedEnum &&
                            expectedEnum.EnumName == enumType.EnumName &&
                            expectedEnum.CacheKey != null)
                        {
                            // Use the concrete type from context (e.g., Option<MemoryBlock> instead of Option<T>)
                            concreteEnumType = expectedEnum;
                        }
                        else
                        {
                        }

                        // For unit variants (no associated data), create the enum value directly
                        if (variant.AssociatedData.Count == 0)
                        {
                            return new IrEnumValue(concreteEnumType, memberName, variant.Tag, new List<IrValue>());
                        }

                        // For variants with data, return a constructor for use in call expressions
                        return new IrEnumConstructor(concreteEnumType, memberName, variant.Tag);
                    }
                }

                // Try associated function (struct method without self parameter)
                var mangledName = name; // Already has :: format

                // Check if this is a generic type - look in generic method templates
                if (_symbols.HasStruct(typeName))
                {
                    var structType = _symbols.LookupStruct(typeName)!;

                    // If the struct is generic, check generic method templates
                    if (structType.GenericParameters.Count > 0)
                    {
                        var templateKey = mangledName;
                        if (_genericMethodTemplates.ContainsKey(templateKey))
                        {
                            // Return a special marker for generic associated function
                            // This will be instantiated later when we know the concrete types
                            return new IrGenericAssociatedFunction(typeName, memberName, structType.GenericParameters);
                        }
                    }
                }

                // Try to find the function in the module
                var function = _module.Functions.FirstOrDefault(f => f.Name == mangledName);
                if (function != null)
                {
                    // Check if this is an associated function (no self parameter)
                    if (function.Parameters.Count == 0 || function.Parameters[0].Name != "self")
                    {
                        // Return a function reference that can be called
                        return new IrFunctionRef(function);
                    }
                }
            }
        }

        // Check if it's a constant - inline the value
        var constantSymbol = _symbols.LookupConstant(name);
        if (constantSymbol != null)
        {
            return new IrConstant((int)constantSymbol.Value, constantSymbol.Type);
        }
        else
        {
        }

        // Check if it's a static variable
        var staticVar = _module.StaticVariables.FirstOrDefault(sv => sv.Name == name);
        if (staticVar != null)
        {
            return new IrGlobalVariable(name, staticVar.Type);
        }

        // Check if it's an extern variable
        var externVar = _module.ExternalVariables.FirstOrDefault(ev => ev.Name == name);
        if (externVar != null)
        {
            return new IrGlobalVariable(name, externVar.Type);
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

        // Check if it's a known system global variable (CPU, FPU, Chipset)
        // These are declared in system.novus as extern vars
        if (name == "CPU" && _symbols.HasEnum("SystemCPU"))
        {
            return new IrVariable(name, _symbols.LookupEnum("SystemCPU")!);
        }
        if (name == "FPU" && _symbols.HasEnum("SystemFPU"))
        {
            return new IrVariable(name, _symbols.LookupEnum("SystemFPU")!);
        }
        if (name == "Chipset" && _symbols.HasEnum("SystemChipset"))
        {
            return new IrVariable(name, _symbols.LookupEnum("SystemChipset")!);
        }

        // Check if it's a function name (for both calls and function pointers)
        var funcRef = _module.Functions.FirstOrDefault(f => f.Name == name);
        if (funcRef != null)
        {
            // Return a function reference that can be called or used as a function pointer
            return new IrFunctionRef(funcRef);
        }

        // Otherwise, assume it's a temporary variable or function name
        return new IrVariable(name, IrIntType.I32); // Default type for temps
    }

    public override object? VisitSelfExpr([NotNull] NovusParser.SelfExprContext context)
    {
        // Return the 'self' variable (parameter in method)
        if (_localVariables.ContainsKey("self"))
        {
            return new IrVariable("self", _localVariables["self"].Type);
        }
        else if (_currentFunction != null && _currentFunction.Parameters.Any(p => p.Name == "self"))
        {
            var selfParam = _currentFunction.Parameters.First(p => p.Name == "self");
            return new IrVariable("self", selfParam.Type);
        }

        var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            "'self' can only be used inside methods",
            errorLocation
        );
        return null;
    }

    public override object? VisitParenExpr([NotNull] NovusParser.ParenExprContext context)
    {
        return Visit(context.expression());
    }

    public override object? VisitUnitLiteral([NotNull] NovusParser.UnitLiteralContext context)
    {
        // Unit type () - creates a zero-element tuple
        return new IrTupleLiteral(IrTupleType.Unit, new List<IrValue>());
    }

    public override object? VisitTupleLiteral([NotNull] NovusParser.TupleLiteralContext context)
    {
        var expressions = context.expression();
        var elements = new List<IrValue>();

        foreach (var exprCtx in expressions)
        {
            var value = Visit(exprCtx) as IrValue;
            if (value == null)
            {
                var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Invalid expression in tuple literal",
                    errorLocation
                );
                return null;
            }
            elements.Add(value);
        }

        // Get element types and create tuple type
        var elementTypes = elements.Select(e => e.Type).ToList();
        var tupleType = _typeInterner.GetTupleType(elementTypes);

        return new IrTupleLiteral(tupleType, elements);
    }

    public override object? VisitStructLiteral([NotNull] NovusParser.StructLiteralContext context)
    {
        var structName = context.typeName().GetText();

        if (!_symbols.HasStruct(structName))
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Unknown struct type '{structName}'",
                errorLocation
            );
            return null;
        }

        // Get the base struct type
        var baseStructType = _symbols.LookupStruct(structName);
        if (baseStructType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(ErrorCodes.StructNotFound, $"Struct '{structName}' not found", errorLocation);
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
            structType = baseStructType;
        }

        var fieldValues = new Dictionary<string, IrValue>();

        // Process field initializers
        foreach (var fieldInit in context.structFieldInit())
        {
            var fieldName = fieldInit.IDENTIFIER().GetText();
            var fieldValue = (IrValue?)Visit(fieldInit.expression());

            if (fieldValue == null)
            {
                var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Field '{fieldName}' in struct '{structName}' requires a value",
                    errorLocation
                );
                return null;
            }

            fieldValues[fieldName] = fieldValue;
        }

        // If the base struct is generic and we don't have an expected type, infer the concrete type from field values
        if (baseStructType.GenericParameters.Count > 0 && _expectedType == null)
        {
            // Infer generic type parameters from field values
            var typeSubstitutions = new Dictionary<string, IrType>();

            foreach (var field in baseStructType.Fields)
            {
                if (fieldValues.TryGetValue(field.Name, out var fieldValue))
                {
                    // Extract generic type mappings from field type and value type
                    ExtractGenericTypeMapping(field.Type, fieldValue.Type, typeSubstitutions);
                }
            }

            // Check if all generic parameters were inferred
            if (typeSubstitutions.Count == baseStructType.GenericParameters.Count)
            {
                // Check if all type arguments are concrete (not generic)
                var typeArgs = baseStructType.GenericParameters.Select(p => typeSubstitutions[p]).ToList();
                bool allConcrete = typeArgs.All(t => !(t is IrGenericType));

                if (allConcrete)
                {
                    // All type arguments are concrete - create monomorphized struct type
                    var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
                    var cacheKey = $"{baseStructType.StructName}<{string.Join(",", typeArgKeys)}>";

                    // Check cache first
                    if (_symbols.LookupMonomorphizedStruct(cacheKey) != null)
                    {
                        structType = _symbols.LookupMonomorphizedStruct(cacheKey)!;
                    }
                    else
                    {
                        // Create monomorphized fields using recursive substitution
                        var monomorphizedFields = new List<IrStructField>();
                        bool fullyMonomorphized = true;

                        foreach (var origField in baseStructType.Fields)
                        {
                            var fieldType = SubstituteGenericTypes(origField.Type, typeSubstitutions);
                            monomorphizedFields.Add(new IrStructField(origField.Name, fieldType));

                            // Check if field type is still generic
                            if (ContainsGenericTypes(fieldType))
                            {
                                fullyMonomorphized = false;
                            }
                        }

                        // Create new struct type with concrete types
                        structType = new IrStructType(baseStructType.StructName, monomorphizedFields, null, cacheKey);

                        // Force calculation of field offsets only if fully monomorphized
                        if (fullyMonomorphized)
                        {
                            _ = structType.SizeInBytes;
                        }

                        // Cache it for future use
                        _symbols.RegisterMonomorphizedStruct(cacheKey, structType);
                    }
                }
                // else: some type arguments are still generic, use base generic type
            }
        }

        // Validate that all fields are initialized
        foreach (var field in structType.Fields)
        {
            if (!fieldValues.ContainsKey(field.Name))
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Field '{field.Name}' in struct '{structName}' is not initialized",
                    errorLocation
                );
                return null;
            }
        }

        return new IrStructLiteral(structType, fieldValues);
    }

    public override object? VisitStructArrayInit([NotNull] NovusParser.StructArrayInitContext context)
    {
        // Handle Vec { {10, 20, 30} } syntax
        // This is syntactic sugar for collections that can be initialized from an array literal

        var structName = context.typeName().GetText();

        if (!_symbols.HasStruct(structName))
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Unknown struct type '{structName}'",
                errorLocation
            );
            return null;
        }

        var baseStructType = _symbols.LookupStruct(structName);
        if (baseStructType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(ErrorCodes.StructNotFound, $"Struct '{structName}' not found", errorLocation);
            return null;
        }

        // Get the array literal expression
        var arrayExpr = (IrValue?)Visit(context.expression());
        if (arrayExpr == null)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Struct array initializer requires an expression",
                errorLocation
            );
            return null;
        }

        // Verify it's an array literal
        if (arrayExpr is not IrArrayLiteral arrayLiteral)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Struct array initializer for '{structName}' requires an array literal, got {arrayExpr.GetType().Name}",
                errorLocation
            );
            return null;
        }

        // For now, only support this for Vec type
        if (structName != "Vec")
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Struct array initializer syntax is only supported for Vec, not '{structName}'",
                errorLocation
            );
            return null;
        }

        // Extract element type from array
        if (arrayLiteral.Type is not IrArrayType arrayType)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Expected array type, got {arrayLiteral.Type}",
                errorLocation
            );
            return null;
        }

        var elementType = arrayType.ElementType;
        var arrayLength = arrayType.Length;

        // Create a static variable to hold the array data
        var staticVarName = $"_vec_data_{_staticVarCounter++}";
        var staticVar = new IrStaticVariable(staticVarName, arrayType, Visibility.Private, false, arrayLiteral);
        _module.StaticVariables.Add(staticVar);

        // Monomorphize Vec<T> to Vec<elementType> if it's generic
        IrStructType vecType;
        if (baseStructType.GenericParameters.Count > 0)
        {
            // Vec is generic (e.g., Vec<T> from std::collections)
            // Monomorphize to Vec<elementType>
            var typeArgs = new List<IrType> { elementType };
            var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
            var cacheKey = $"{baseStructType.StructName}<{string.Join(",", typeArgKeys)}>";

            // Check cache first
            if (_symbols.LookupMonomorphizedStruct(cacheKey) != null)
            {
                vecType = _symbols.LookupMonomorphizedStruct(cacheKey)!;
            }
            else
            {
                // Create type substitution map
                var typeSubstitutions = new Dictionary<string, IrType>();
                for (int i = 0; i < baseStructType.GenericParameters.Count && i < typeArgs.Count; i++)
                {
                    typeSubstitutions[baseStructType.GenericParameters[i]] = typeArgs[i];
                }

                // Create monomorphized fields
                var monomorphizedFields = new List<IrStructField>();
                foreach (var origField in baseStructType.Fields)
                {
                    var fieldType = SubstituteGenericTypes(origField.Type, typeSubstitutions);
                    monomorphizedFields.Add(new IrStructField(origField.Name, fieldType));
                }

                // Create monomorphized struct type
                vecType = new IrStructType(baseStructType.StructName, monomorphizedFields, null, cacheKey);
                _ = vecType.SizeInBytes; // Force size calculation

                // Cache it
                _symbols.RegisterMonomorphizedStruct(cacheKey, vecType);
            }
        }
        else
        {
            // Vec is not generic (custom Vec with concrete types)
            vecType = baseStructType;
        }

        // Build field values: ptr/data = &static_array, len = array_length, capacity = array_length
        var fieldValues = new Dictionary<string, IrValue>();

        // Determine pointer field name (ptr for std::collections::Vec, data for custom Vec)
        var pointerFieldName = vecType.GetField("ptr") != null ? "ptr" : "data";

        // Pointer field: cast static var to pointer
        var staticVarRef = new IrVariable(staticVarName, arrayType);
        var refType = new IrReferenceType(arrayType);
        var borrowExpr = new IrBorrowValue(staticVarRef, refType, false);
        var pointerType = new IrPointerType(elementType);
        var dataPtr = new IrCastValue(borrowExpr, refType, pointerType);
        fieldValues[pointerFieldName] = dataPtr;

        // len and capacity fields
        // IMPORTANT: capacity must be 0 for static-backed Vecs so Vec_drop won't try to free the static data
        // Static const data cannot be freed on AmigaOS - it would cause a crash (error 81000005)
        fieldValues["len"] = new IrConstant(arrayLength, IrIntType.U32);
        fieldValues["capacity"] = new IrConstant(0, IrIntType.U32);

        return new IrStructLiteral(vecType, fieldValues);
    }

    public override object? VisitMemberAccessExpr([NotNull] NovusParser.MemberAccessExprContext context)
    {
        var baseExpr = (IrValue?)Visit(context.expression());
        if (baseExpr == null)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Member access requires a base expression",
                errorLocation
            );
            return null;
        }

        var memberName = context.IDENTIFIER().GetText();

        // Auto-dereference pointers and references to structs
        IrValue actualBase = baseExpr;
        IrType baseType = baseExpr.Type;

        if (baseType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
        {
            // Auto-dereference the pointer - wrap in IrDereferenceValue
            actualBase = new IrDereferenceValue(actualBase, ptrType.PointeeType);
            baseType = ptrType.PointeeType;
        }
        else if (baseType is IrReferenceType refType && refType.PointeeType is IrStructType)
        {
            // Auto-dereference the reference - wrap in IrDereferenceValue
            actualBase = new IrDereferenceValue(actualBase, refType.PointeeType);
            baseType = refType.PointeeType;
        }
        else if (baseType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType)
        {
            // Auto-dereference the mutable reference - wrap in IrDereferenceValue
            actualBase = new IrDereferenceValue(actualBase, mutRefType.PointeeType);
            baseType = mutRefType.PointeeType;
        }

        // Check if the base expression is a struct type
        if (baseType is not IrStructType structType)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.CannotAccessMember,
                $"Cannot access member '{memberName}' on non-struct type '{baseType.Name}'",
                errorLocation
            );
            return null;
        }

        // Find the field
        var field = structType.GetField(memberName);
        if (field == null)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Struct '{structType.Name}' does not have a field named '{memberName}'",
                errorLocation
            );
            return null;
        }

        // Generate a member access instruction
        var resultName = $"%t{_tempCounter++}";
        var memberAccess = new IrMemberAccess(resultName, actualBase, memberName, field.Type, field.Offset);
        _currentBlock!.AddInstruction(memberAccess);

        return new IrVariable(resultName, field.Type);
    }

    public override object? VisitTurboFishExpr([NotNull] NovusParser.TurboFishExprContext context)
    {
        // Handle turbo-fish syntax: Type::<Args>
        // This creates a parameterized type expression that can then be used with :: to access members
        var baseExpr = context.expression();
        var genericArgsCtx = context.genericTypeArgs();

        // Parse the generic type arguments
        var explicitTypeArgs = new List<IrType>();
        foreach (var typeCtx in genericArgsCtx.typeList().type())
        {
            var irType = ParseType(typeCtx);
            explicitTypeArgs.Add(irType);
        }

        // The base expression should be an identifier for the type
        string? typeName = null;
        if (baseExpr is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            typeName = identCtx.identifier().GetText();
        }

        if (typeName == null)
        {
            var errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Turbo-fish expression must reference a type",
                errorLocation
            );
            return null;
        }

        // Return a marker that stores the type name and explicit type arguments
        // This will be consumed by PathExpr when accessing members
        return new IrTurboFishType(typeName, explicitTypeArgs);
    }

    public override object? VisitPathExpr([NotNull] NovusParser.PathExprContext context)
    {
        SourceLocation errorLocation;

        // Handle path expressions: Type::name
        // This can be:
        // 1. Enum variants: Option::Some, Result::Ok
        // 2. Associated functions (static methods): Vec::new, Vec::with_capacity
        // 3. Members accessed on turbo-fish types: (Vec::<u32>)::with_capacity
        var baseExpr = context.expression();
        var memberName = context.IDENTIFIER().GetText();

        // Check if base expression is a turbo-fish type
        List<IrType>? explicitTypeArgs = null;
        string? typeName = null;

        var baseValue = Visit(baseExpr);
        if (baseValue is IrTurboFishType turboFish)
        {
            typeName = turboFish.TypeName;
            explicitTypeArgs = turboFish.TypeArguments;
        }
        // The base expression should be an identifier for the type
        else if (baseExpr is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            typeName = identCtx.identifier().GetText();
        }

        if (typeName == null)
        {
            var baseExprType = baseExpr?.GetType().Name ?? "null";
            var baseExprText = baseExpr?.GetText() ?? "null";
            errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Path expression must reference a type (got {baseExprType}: '{baseExprText}')",
                errorLocation
            );
            return null;
        }

        // Try enum variant first
        if (_symbols.HasEnum(typeName))
        {
            var enumType = _symbols.LookupEnum(typeName)!;
            var variant = enumType.GetVariant(memberName);

            if (variant == null)
            {
                errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Enum '{typeName}' has no variant '{memberName}'",
                    errorLocation
                );
                return null;
            }

            // Use expected type if it's a more specific (concrete) version of this enum
            var concreteEnumType = enumType;
            if (_expectedType is IrEnumType expectedEnum &&
                expectedEnum.EnumName == enumType.EnumName &&
                expectedEnum.CacheKey != null)
            {
                // Use the concrete type from context (e.g., Option<MemoryBlock> instead of Option<T>)
                concreteEnumType = expectedEnum;
            }

            // For unit variants (no associated data), create the enum value directly
            if (variant.AssociatedData.Count == 0)
            {
                return new IrEnumValue(concreteEnumType, memberName, variant.Tag, new List<IrValue>());
            }

            // Return enum constructor for variants with data
            return new IrEnumConstructor(concreteEnumType, memberName, variant.Tag);
        }

        // Try associated function (struct method without self parameter)
        var mangledName = $"{typeName}::{memberName}";

        // Check if this is a generic type - look in generic method templates
        if (_symbols.HasStruct(typeName))
        {
            var structType = _symbols.LookupStruct(typeName)!;

            // If the struct is generic, check generic method templates
            if (structType.GenericParameters.Count > 0)
            {
                var templateKey = mangledName;
                if (_genericMethodTemplates.ContainsKey(templateKey))
                {
                    // Return a special marker for generic associated function
                    // This will be instantiated later when we know the concrete types
                    return new IrGenericAssociatedFunction(typeName, memberName, structType.GenericParameters, explicitTypeArgs);
                }
            }
        }

        // Try to find the function in the module
        var function = _module.Functions.FirstOrDefault(f => f.Name == mangledName);
        if (function != null)
        {
            // Check if this is an associated function (no self parameter)
            if (function.Parameters.Count == 0 || function.Parameters[0].Name != "self")
            {
                // Return a function reference that can be called
                return new IrFunctionRef(function);
            }
            else
            {
                errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
                _diagnostics.ReportError(
                    ErrorCodes.CannotCallMethodOnType,
                    $"Cannot call method '{memberName}' of type '{typeName}' without an instance (it requires 'self')",
                    errorLocation
                );
                return null;
            }
        }

        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Type '{typeName}' has no associated function or variant '{memberName}'",
            errorLocation
        );
        return null;
    }

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

    public override object? VisitMatchExpr([NotNull] NovusParser.MatchExprContext context)
    {
        SourceLocation errorLocation;
        var matchValue = (IrValue?)Visit(context.expression());
        if (matchValue == null)
        {
            errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
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
                enumTypeForValidation = _symbols.LookupEnum(genericType.ParameterName)!;
            }
        }

        if (!isEnumMatch && !isIntegerMatch)
        {
            errorLocation = SourceLocationHelper.FromContext(context, _inputFilePath, _sourceLines.ToArray());
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Match can only be used with enum or integer types, got '{matchValue.Type.Name}'",
                errorLocation
            );
            return null;
        }

        IrEnumType? enumType = enumTypeForValidation;
        if (enumType != null)
        {
            foreach (var v in enumType.Variants)
            {
            }
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
        bool armsProduceValues = expandedArms.Any(arm => arm.OriginalArm.expression() != null);
        string? matchResultVarName = null;

        // Extract tag from enum value (before declaring match result, so it appears first)
        // Only needed for enum matches
        IrVariable? tagVar = null;
        if (isEnumMatch)
        {
            // If matchValue is a pointer/reference to an enum, we need to dereference it first
            IrValue enumValueForExtract = matchValue;
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
            // Use expected type if available (e.g., from function return type)
            matchResultType = _expectedType ?? _currentFunction?.ReturnType;

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
                break;
            }

            // Handle patterns based on match type
            if (isEnumMatch)
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

                    // Branch: if match, go to arm, otherwise continue to next check
                    var nextLabel = i < checkLabels.Count - 1 ? checkLabels[i + 1] : matchEndLabel;
                    _currentBlock!.AddInstruction(new IrConditionalBranch(cmpVar, armLabels[i], nextLabel));
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

            // Extract associated data for variant patterns (enum matches only)
            if (isEnumMatch && pattern is NovusParser.VariantPatternContext variantPattern)
            {
                // Extract the last identifier from the qualified name (e.g., SimpleResult::Ok -> Ok)
                var identifiers = variantPattern.variantName().IDENTIFIER();
                var variantName = identifiers[identifiers.Length - 1].GetText();
                var variant = enumType!.GetVariant(variantName);

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
                            _currentBlock!.AddInstruction(new IrExtractVariantData(extractName, matchValue, variantName, dataIdx, dataType));

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
            // Integer matches don't have associated data to extract

            // Visit the arm body and capture result if it's an expression
            IrValue? armResult = null;
            if (armCtx.expression() != null)
            {
                // Set expected type so enum constructors get the correct monomorphized type
                // We set this once before visiting all arms and keep it set
                if (matchResultType != null)
                {
                    _expectedType = matchResultType;
                }

                armResult = (IrValue?)Visit(armCtx.expression());

                // Infer match result type from first arm if we didn't have an expected type
                if (i == 0 && armResult != null && matchResultType == null)
                {
                    matchResultType = armResult.Type;
                    matchResultVarName = $"%match_{matchId}_result";

                    // Declare the variable now that we know the type
                    var matchResultVar = new IrLocalVariable(matchResultVarName, matchResultType, true);
                    _currentFunction!.LocalVariables.Add(matchResultVar);
                    _localVariables[matchResultVarName] = matchResultVar;

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

                    // Now that we know the type, set it as expected type for subsequent arms
                    _expectedType = matchResultType;
                }
            }
            else if (armCtx.returnStatement() != null)
            {
                // Handle return statement in match arm
                Visit(armCtx.returnStatement());
                // Return statements terminate the block, so no result to store
            }

            // If we have a result value, result type, and variable name, store it
            if (armResult != null && matchResultType != null && matchResultVarName != null && !CurrentBlockHasTerminator())
            {
                _currentBlock!.AddInstruction(new IrStore(matchResultVarName, armResult));
            }

            // Pop defer scope and emit cleanup BEFORE jumping to match_end
            // This ensures variables declared in this match arm are cleaned up before leaving the scope
            if (!CurrentBlockHasTerminator())
            {
                PopDeferScope();
            }

            // Jump to end (if not already terminated)
            if (!CurrentBlockHasTerminator())
            {
                _currentBlock!.AddInstruction(new IrBranch(matchEndLabel));
                anyArmReachesEnd = true;  // This arm can reach match_end
            }
            else
            {
                // Block already terminated (e.g., return statement)
                // Still need to pop the scope to balance the stack
                if (_scopeDeferStack.Count > 0)
                {
                    _scopeDeferStack.Pop();  // Discard without emitting (unreachable)
                }
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

    /// <summary>
    /// Parse a type from the AST - delegates to TypeParser for unified type parsing logic
    /// </summary>
    private IrType ParseType(NovusParser.TypeContext context)
    {
        return _typeParser.ParseType(context);
    }

    /// <summary>
    /// Map a primitive type name (from grammar keywords) to its IrType representation
    /// </summary>
    private IrType MapPrimitiveTypeName(string primitiveTypeName)
    {
        return primitiveTypeName switch
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
            _ => throw new CompilerBugException(
                $"Unknown primitive type name: {primitiveTypeName}",
                "MapPrimitiveTypeName",
                _inputFilePath,
                null
            )
        };
    }

    /// <summary>
    /// Check if a type contains any generic type parameters
    /// </summary>
    private bool ContainsGenericTypes(IrType type)
    {
        return type switch
        {
            IrGenericType => true,
            IrPointerType ptrType => ContainsGenericTypes(ptrType.PointeeType),
            IrReferenceType refType => ContainsGenericTypes(refType.PointeeType),
            IrMutReferenceType mutRefType => ContainsGenericTypes(mutRefType.PointeeType),
            IrArrayType arrayType => ContainsGenericTypes(arrayType.ElementType),
            IrStructType structType => structType.Fields.Any(f => ContainsGenericTypes(f.Type)),
            IrEnumType enumType => enumType.Variants.Any(v => v.AssociatedData.Any(ContainsGenericTypes)),
            _ => false
        };
    }

    /// <summary>
    /// Check if two types are semantically equal
    /// This is needed because reference equality doesn't work for types that are constructed separately
    /// </summary>
    private bool TypesAreEqual(IrType a, IrType b)
    {
        // Fast path: reference equality
        if (ReferenceEquals(a, b)) return true;

        // Different type classes
        if (a.GetType() != b.GetType()) return false;

        // Generic types: compare parameter names
        if (a is IrGenericType gtA && b is IrGenericType gtB)
        {
            return gtA.ParameterName == gtB.ParameterName;
        }

        // Pointer types: compare pointee types recursively
        if (a is IrPointerType ptrA && b is IrPointerType ptrB)
        {
            return TypesAreEqual(ptrA.PointeeType, ptrB.PointeeType);
        }

        // Reference types: compare pointee types recursively
        if (a is IrReferenceType refA && b is IrReferenceType refB)
        {
            return TypesAreEqual(refA.PointeeType, refB.PointeeType);
        }

        // Mutable reference types: compare pointee types recursively
        if (a is IrMutReferenceType mutRefA && b is IrMutReferenceType mutRefB)
        {
            return TypesAreEqual(mutRefA.PointeeType, mutRefB.PointeeType);
        }

        // Array types: compare element type and length
        if (a is IrArrayType arrA && b is IrArrayType arrB)
        {
            return arrA.Length == arrB.Length && TypesAreEqual(arrA.ElementType, arrB.ElementType);
        }

        // Struct types: compare by name and cache key
        // We use cache key when available because it uniquely identifies monomorphized versions
        if (a is IrStructType structA && b is IrStructType structB)
        {
            if (structA.CacheKey != null && structB.CacheKey != null)
            {
                return structA.CacheKey == structB.CacheKey;
            }
            return structA.StructName == structB.StructName &&
                   structA.GenericParameters.Count == structB.GenericParameters.Count;
        }

        // Enum types: compare by name and cache key
        if (a is IrEnumType enumA && b is IrEnumType enumB)
        {
            if (enumA.CacheKey != null && enumB.CacheKey != null)
            {
                return enumA.CacheKey == enumB.CacheKey;
            }
            return enumA.EnumName == enumB.EnumName &&
                   enumA.GenericParameters.Count == enumB.GenericParameters.Count;
        }

        // For primitive types, reference equality should have caught it
        // but as a fallback, we consider them equal by default
        return false;
    }

    /// <summary>
    /// Recursively substitute generic type parameters with concrete types
    /// </summary>
    /// <summary>
    /// Check if a type contains a specific generic parameter
    /// </summary>
    private bool TypeContainsGeneric(IrType type, string genericParamName)
    {
        if (type is IrGenericType gt)
        {
            return gt.ParameterName == genericParamName;
        }
        if (type is IrPointerType ptrType)
        {
            return TypeContainsGeneric(ptrType.PointeeType, genericParamName);
        }
        if (type is IrReferenceType refType)
        {
            return TypeContainsGeneric(refType.PointeeType, genericParamName);
        }
        if (type is IrMutReferenceType mutRefType)
        {
            return TypeContainsGeneric(mutRefType.PointeeType, genericParamName);
        }
        if (type is IrArrayType arrayType)
        {
            return TypeContainsGeneric(arrayType.ElementType, genericParamName);
        }
        if (type is IrStructType structType)
        {
            return structType.Fields.Any(f => TypeContainsGeneric(f.Type, genericParamName));
        }
        if (type is IrEnumType enumType)
        {
            return enumType.Variants.Any(v => v.AssociatedData.Any(d => TypeContainsGeneric(d, genericParamName)));
        }
        return false;
    }

    private IrType SubstituteGenericTypes(IrType type, Dictionary<string, IrType> substitutions)
    {
        // Handle Self type - resolve to current implementing type
        if (type is IrSelfType)
        {
            if (_currentSelfType == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    "'Self' type encountered outside of impl block context",
                    errorLocation
                );
                return null;
            }
            return _currentSelfType;
        }
        else if (type is IrGenericType gt && substitutions.ContainsKey(gt.ParameterName))
        {
            return substitutions[gt.ParameterName];
        }
        else if (type is IrPointerType ptrType)
        {
            var substitutedPointee = SubstituteGenericTypes(ptrType.PointeeType, substitutions);
            if (substitutedPointee != ptrType.PointeeType)
            {
                return _typeInterner.GetPointerType(substitutedPointee);
            }
        }
        else if (type is IrReferenceType refType)
        {
            var substitutedPointee = SubstituteGenericTypes(refType.PointeeType, substitutions);
            if (substitutedPointee != refType.PointeeType)
            {
                return _typeInterner.GetReferenceType(substitutedPointee);
            }
        }
        else if (type is IrMutReferenceType mutRefType)
        {
            var substitutedPointee = SubstituteGenericTypes(mutRefType.PointeeType, substitutions);
            if (substitutedPointee != mutRefType.PointeeType)
            {
                return _typeInterner.GetMutReferenceType(substitutedPointee);
            }
        }
        else if (type is IrArrayType arrayType)
        {
            var substitutedElement = SubstituteGenericTypes(arrayType.ElementType, substitutions);
            if (substitutedElement != arrayType.ElementType)
            {
                return _typeInterner.GetArrayType(substitutedElement, arrayType.Length);
            }
        }
        else if (type is IrStructType structType)
        {
            // If the struct still has generic parameters and we're in a generic context,
            // we should not create a new struct type - just return the original
            // This prevents creating duplicate generic struct instances
            if (structType.GenericParameters.Count > 0)
            {
                // Check if any of the substitutions actually change generic to concrete
                bool hasConcreteSubstitution = false;
                foreach (var genericParam in structType.GenericParameters)
                {
                    if (substitutions.ContainsKey(genericParam))
                    {
                        var substType = substitutions[genericParam];
                        // Check if it's being replaced with a concrete (non-generic) type
                        if (!(substType is IrGenericType))
                        {
                            hasConcreteSubstitution = true;
                            break;
                        }
                    }
                    else
                    {
                    }
                }

                // If no generic parameters are being replaced with concrete types,
                // return the original struct unchanged
                if (!hasConcreteSubstitution)
                {
                    return structType;
                }
                else
                {
                }
            }

            // Check if any field types contain generics that need substitution
            bool needsSubstitution = false;
            var substitutedFields = new List<IrStructField>();

            foreach (var field in structType.Fields)
            {
                var substitutedFieldType = SubstituteGenericTypes(field.Type, substitutions);
                substitutedFields.Add(new IrStructField(field.Name, substitutedFieldType));

                if (!TypesAreEqual(substitutedFieldType, field.Type))
                {
                    needsSubstitution = true;
                }
            }

            if (needsSubstitution)
            {
                // Create a new struct type with substituted field types
                // Preserve generic parameters from original
                // Clear cache key if struct still has generic parameters (not fully monomorphized)
                string? cacheKey = structType.GenericParameters.Count > 0 ? null : structType.CacheKey;

                var substitutedStruct = new IrStructType(
                    structType.StructName,
                    substitutedFields,
                    structType.GenericParameters,
                    cacheKey,
                    structType.Attributes,
                    structType.WhereClause
                );
                return substitutedStruct;
            }
        }
        else if (type is IrEnumType enumType)
        {
            // If the enum still has generic parameters and we're in a generic context,
            // we should not create a new enum type - just return the original
            // This prevents creating duplicate generic enum instances
            if (enumType.GenericParameters.Count > 0)
            {
                // Check if any of the substitutions actually change generic to concrete
                bool hasConcreteSubstitution = false;
                foreach (var genericParam in enumType.GenericParameters)
                {
                    if (substitutions.ContainsKey(genericParam))
                    {
                        var substType = substitutions[genericParam];
                        // Check if it's being replaced with a concrete (non-generic) type
                        if (!(substType is IrGenericType))
                        {
                            hasConcreteSubstitution = true;
                            break;
                        }
                    }
                }

                // If no generic parameters are being replaced with concrete types,
                // return the original enum unchanged
                if (!hasConcreteSubstitution)
                {
                    return enumType;
                }
            }

            // Check if any variant types contain generics that need substitution
            bool needsSubstitution = false;
            var substitutedVariants = new List<IrEnumVariant>();

            foreach (var variant in enumType.Variants)
            {
                var substitutedData = new List<IrType>();
                foreach (var dataType in variant.AssociatedData)
                {
                    var substitutedDataType = SubstituteGenericTypes(dataType, substitutions);
                    substitutedData.Add(substitutedDataType);

                    if (!TypesAreEqual(substitutedDataType, dataType))
                    {
                        needsSubstitution = true;
                    }
                }

                substitutedVariants.Add(new IrEnumVariant(variant.Name, variant.Tag, substitutedData));
            }

            if (needsSubstitution)
            {
                // Determine which generic parameters remain after substitution
                var remainingGenericParams = new List<string>();
                foreach (var genericParam in enumType.GenericParameters)
                {
                    // If this generic parameter was NOT substituted, keep it
                    if (!substitutions.ContainsKey(genericParam) ||
                        substitutions[genericParam] is IrGenericType)
                    {
                        remainingGenericParams.Add(genericParam);
                    }
                }

                // Generate new cache key for the substituted enum
                string? cacheKey = null;
                if (remainingGenericParams.Count == 0)
                {
                    // Fully monomorphized - generate cache key from the actual variant types
                    // Check if we had any actual substitutions that changed types
                    bool hadSubstitutions = substitutions.Count > 0;

                    if (hadSubstitutions)
                    {
                        // Build cache key from the substituted variant types
                        // For single-type-param enums like Option<T>, use the first variant's first data type
                        // This works for common patterns like Option<*u8> where Some variant holds the type arg
                        var typeArgs = new List<IrType>();

                        // Find the first non-empty variant to extract type args from
                        foreach (var variant in substitutedVariants)
                        {
                            if (variant.AssociatedData.Count > 0)
                            {
                                // For now, assume single-type-param enums (like Option, Result)
                                // Take the first data type as the type argument
                                typeArgs.Add(variant.AssociatedData[0]);
                                break; // Only need one
                            }
                        }

                        if (typeArgs.Count > 0)
                        {
                            var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
                            cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";
                        }
                    }
                }

                var substitutedEnum = new IrEnumType(
                    enumType.EnumName,
                    substitutedVariants,
                    remainingGenericParams,
                    cacheKey
                );
                return substitutedEnum;
            }
        }

        return type;
    }

    private string GetTypeCacheKey(IrType type)
    {
        // Recursively build a cache key for a type, handling nested generics
        if (type is IrEnumType enumType)
        {
            // Check if enum still contains generic types in its variants
            // An enum is only fully monomorphized if it has no generic parameters
            // AND no generic types in its variant data
            bool hasGenericData = enumType.Variants.Any(v =>
                v.AssociatedData.Any(d => d is IrGenericType));

            if (enumType.GenericParameters.Count > 0 || hasGenericData)
            {
                // Still generic - build cache key from generic parameter names found in variant data
                if (hasGenericData)
                {
                    // Extract generic type names from variant data
                    var genericNames = new HashSet<string>();
                    foreach (var variant in enumType.Variants)
                    {
                        foreach (var data in variant.AssociatedData)
                        {
                            if (data is IrGenericType gt)
                            {
                                genericNames.Add(gt.ParameterName);
                            }
                        }
                    }
                    return $"{enumType.EnumName}<{string.Join(",", genericNames.OrderBy(x => x))}>";
                }
                else
                {
                    // Use declared generic parameters
                    return $"{enumType.EnumName}<{string.Join(",", enumType.GenericParameters)}>";
                }
            }
            else
            {
                // Fully monomorphized enum - use stored cache key if available
                if (enumType.CacheKey != null)
                {
                    return enumType.CacheKey;
                }
                // Non-generic enum (like DosError) - just use the name
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

    /// <summary>
    /// Parse a type from its mangled name (e.g., "i32" -> IrIntType.I32, "Vec_i32" -> Vec<i32>)
    /// </summary>
    private IrType ParseTypeFromMangledName(string mangledName)
    {
        // Handle primitive types
        if (mangledName == "i8") return IrIntType.I8;
        if (mangledName == "i16") return IrIntType.I16;
        if (mangledName == "i32") return IrIntType.I32;
        if (mangledName == "i64") return IrIntType.I64;
        if (mangledName == "u8") return IrIntType.U8;
        if (mangledName == "u16") return IrIntType.U16;
        if (mangledName == "u32") return IrIntType.U32;
        if (mangledName == "u64") return IrIntType.U64;
        if (mangledName == "bool") return IrBoolType.Instance;
        if (mangledName == "void") return IrVoidType.Instance;

        // Handle struct types (e.g., "Vec_i32" -> Vec<i32>)
        // For now, this is a simple implementation
        // TODO: Handle nested generics and more complex types
        var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Cannot parse complex mangled type name '{mangledName}' yet",
            errorLocation
        );
        return null;
    }

    /// <summary>
    /// Get the mangled name for a type (e.g., IrIntType.I32 -> "i32", Vec<i32> -> "Vec_i32")
    /// </summary>
    private string GetMangledTypeName(IrType type)
    {
        if (type is IrIntType intType)
        {
            if (intType == IrIntType.I8) return "i8";
            if (intType == IrIntType.I16) return "i16";
            if (intType == IrIntType.I32) return "i32";
            if (intType == IrIntType.I64) return "i64";
            if (intType == IrIntType.U8) return "u8";
            if (intType == IrIntType.U16) return "u16";
            if (intType == IrIntType.U32) return "u32";
            if (intType == IrIntType.U64) return "u64";
        }
        else if (type is IrBoolType)
        {
            return "bool";
        }
        else if (type is IrStructType structType)
        {
            // Use CacheKey if available (for monomorphized types like Vec<i32>)
            if (structType.CacheKey != null)
            {
                return structType.CacheKey;
            }
            // Fall back to struct name for non-generic types
            return structType.StructName;
        }
        else if (type is IrPointerType ptrType)
        {
            return "ptr_" + GetMangledTypeName(ptrType.PointeeType);
        }

        var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Cannot get mangled name for type '{type.Name}'",
            errorLocation
        );
        return null;
    }

    /// <summary>
    /// Ensure that a drop() method is instantiated for this type if it exists as a template.
    /// For generic types like Vec<T>, this will instantiate Vec<T>::drop() if it exists.
    /// Returns true if the type has a drop() method (either already instantiated or newly instantiated).
    /// </summary>
    private bool EnsureDropMethodInstantiated(IrType type)
    {
        // Check if this type implements the Drop trait
        if (!_module.TypeImplementsDrop(type))
        {
            return false;
        }

        // Get the type name for method lookup
        string typeName;
        IrStructType? structType = null;
        IrEnumType? enumType = null;

        if (type is IrStructType st)
        {
            structType = st;
            typeName = st.StructName;  // Use base name for generic types
        }
        else if (type is IrEnumType et)
        {
            enumType = et;
            typeName = et.EnumName;
        }
        else
        {
            // Only structs and enums can have methods
            return false;
        }

        // Look for Type_drop method in the module
        var dropMethod = $"{typeName}_drop";

        // Check if already instantiated
        if (_module.Functions.Any(f => f.Name == dropMethod))
        {
            return true;
        }

        // Check if there's a generic template for the drop() method
        var templateKey = $"{typeName}::drop";

        if (_genericMethodTemplates.ContainsKey(templateKey))
        {
            // Instantiate the generic drop() method
            try
            {
                IrFunction? instantiatedFunc = null;

                if (structType != null)
                {
                    instantiatedFunc = InstantiateGenericMethod(structType, "drop");
                }
                else if (enumType != null)
                {
                    // TODO: Add support for enum methods if needed
                    return false;
                }

                if (instantiatedFunc != null)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                return false;
            }
        }

        // No drop method exists
        return false;
    }

    /// <summary>
    /// Check if a type has a drop() method.
    /// This enables automatic defer cleanup for RAII-style resource management.
    /// </summary>
    private bool TypeHasDropMethod(IrType type)
    {
        // Get the type name for method lookup
        string typeName;
        if (type is IrStructType structType)
        {
            typeName = structType.StructName;  // Use base name for generic types
        }
        else if (type is IrEnumType enumType)
        {
            typeName = enumType.EnumName;
        }
        else
        {
            // Only structs and enums can have methods
            return false;
        }

        // Look for Type_drop method in the module
        var dropMethod = $"{typeName}_drop";
        return _module.Functions.Any(f => f.Name == dropMethod);
    }

    /// <summary>
    /// Push a new defer scope. Variables declared in this scope will have their
    /// defer cleanup emitted when PopDeferScope() is called.
    /// </summary>
    private void PushDeferScope()
    {
        _scopeDeferStack.Push(new List<IrBasicBlock>());
    }

    /// <summary>
    /// Pop the current defer scope and emit cleanup for all defers registered in this scope.
    /// Returns the list of defer blocks that were emitted (in LIFO order).
    /// </summary>
    private List<IrBasicBlock> PopDeferScope()
    {
        if (_scopeDeferStack.Count == 0)
        {
            return new List<IrBasicBlock>();
        }

        var scopeDefers = _scopeDeferStack.Pop();

        // Emit defers in LIFO order (last registered, first executed)
        for (int i = scopeDefers.Count - 1; i >= 0; i--)
        {
            var deferBlock = scopeDefers[i];

            // Emit all instructions in the defer block
            foreach (var instruction in deferBlock.Instructions)
            {
                _currentBlock!.AddInstruction(instruction);
            }

            // Remove from function-level defer list (so it doesn't get emitted again at function exit)
            _currentFunction!.DeferredBlocks.Remove(deferBlock);
        }

        return scopeDefers;
    }

    /// <summary>
    /// Inject an automatic defer block that calls drop() on a variable.
    /// This implements RAII-style cleanup for types with drop() methods.
    /// </summary>
    private void InjectAutomaticDrop(string varName, IrType type)
    {
        // Create a new basic block for the deferred drop() call
        var deferLabel = $"autoclean_{varName}_{_labelCounter++}";
        var deferBlock = new IrBasicBlock(deferLabel);

        // Save current block
        var savedBlock = _currentBlock;
        _currentBlock = deferBlock;

        // Generate call to var.drop()
        // This desugars to: Type_drop(&mut var)
        string typeName;
        if (type is IrStructType structType)
        {
            typeName = structType.StructName;
        }
        else if (type is IrEnumType enumType)
        {
            typeName = enumType.EnumName;
        }
        else
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Cannot generate drop call for type '{type.Name}'",
                errorLocation
            );
            return;
        }

        var dropMethodName = $"{typeName}_drop";
        var dropMethod = _module.Functions.FirstOrDefault(f => f.Name == dropMethodName);
        if (dropMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                $"Drop method '{dropMethodName}' not found",
                errorLocation
            );
            return;
        }

        // Load the variable and borrow it mutably for drop()
        var varRef = new IrVariable(varName, type);
        var mutBorrow = new IrBorrowValue(varRef, new IrMutReferenceType(type), isMutable: true);

        // Create the drop() call (drop() returns void)
        var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
        dropCall.Arguments.Add(mutBorrow);
        deferBlock.AddInstruction(dropCall);

        // Restore current block
        _currentBlock = savedBlock;

        // Add the defer block to the function's deferred blocks list (LIFO)
        _currentFunction!.DeferredBlocks.Add(deferBlock);

        // ALSO add to current scope's defer list if we're in a scope
        if (_scopeDeferStack.Count > 0)
        {
            _scopeDeferStack.Peek().Add(deferBlock);
        }

        // Add defer instruction to current block (marker)
        _currentBlock!.AddInstruction(new IrDefer(deferBlock));
    }

    /// <summary>
    /// Recursively extracts generic type mappings by comparing base and monomorphized types.
    /// Handles nested generics in pointers, arrays, and other type constructors.
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
    private bool IsPrimitiveTypeName(string typeName)
    {
        return typeName switch
        {
            "i8" or "i16" or "i32" or "i64" or
            "u8" or "u16" or "u32" or "u64" or
            "bool" or "void" or "f32" or "f64" or "Self" => true,
            _ => false
        };
    }
}
