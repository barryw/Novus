using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
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
    private int _staticVarCounter = 0;  // Counter for auto-generated static variables
    private readonly Stack<string> _loopExitLabels = new(); // Track loop exit labels for break
    private readonly Dictionary<string, IrLocalVariable> _localVariables = new(); // Track local variables in current function
    private readonly Dictionary<string, IrStructType> _structs = new(); // Track struct types
    private readonly Dictionary<string, IrEnumType> _enums = new(); // Track enum types
    private readonly Dictionary<string, IrTrait> _traits = new(); // Track trait types
    private readonly Dictionary<string, IrGenericType> _genericParams = new(); // Track generic type parameters
    private readonly Dictionary<string, (IrType Type, object Value)> _constants = new(); // Track constant values
    private readonly Dictionary<string, IrEnumType> _monomorphizedEnums = new(); // Cache for monomorphized generic enums
    private readonly Dictionary<string, IrStructType> _monomorphizedStructs = new(); // Cache for monomorphized generic structs

    // Store generic method templates for later instantiation
    // Key: "TypeName::methodName", Value: (genericParams, context, constants)
    // The constants dictionary captures the constants visible when the template was created
    private readonly Dictionary<string, (List<string> GenericParams, NovusParser.FunctionDeclarationContext Context, Dictionary<string, (IrType Type, object Value)> Constants)> _genericMethodTemplates = new();

    // Track which monomorphized methods have been generated
    // Key: "TypeName<ConcreteType>::methodName" (e.g., "Vec<i32>::push")
    private readonly HashSet<string> _instantiatedMethods = new();

    // Store generic function templates for later instantiation (standalone functions, not methods)
    // Key: function name (e.g., "identity"), Value: (genericParams, context, constants)
    private readonly Dictionary<string, (List<string> GenericParams, NovusParser.FunctionDeclarationContext Context, Dictionary<string, (IrType Type, object Value)> Constants)> _genericFunctionTemplates = new();

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

    /// <summary>
    /// Constructor for IrBuilder
    /// </summary>
    public IrBuilder(bool skipAutoImports = false)
    {
        _skipAutoImports = skipAutoImports;
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
                var templateConstants = new Dictionary<string, (IrType Type, object Value)>(_constants);
                _genericFunctionTemplates[name] = (genericParams, funcContext, templateConstants);
                continue; // Don't add to _module.Functions yet
            }

            // Non-generic function: register normally
            var returnType = funcContext.type() != null ? ParseType(funcContext.type()) : IrVoidType.Instance;

            // Check for extern, pub, and internal keywords
            var isExtern = false;
            var visibility = Visibility.Private;
            for (int i = 0; i < Math.Min(4, funcContext.ChildCount); i++)
            {
                var childText = funcContext.GetChild(i)?.GetText();
                if (childText == "extern") isExtern = true;
                if (childText == "pub") visibility = Visibility.Public;
                if (childText == "internal") visibility = Visibility.Internal;
            }

            var function = new IrFunction(name, returnType, visibility, isExtern);

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
            // Get the type name this impl is for
            // Get only the base type name (Vec, not Vec<T>)
            // typeName() returns an array: [Type] for "impl Type" or [Trait, Type] for "impl Trait for Type"
            var typeNames = implContext.typeName();
            var typeName = typeNames[typeNames.Length - 1].IDENTIFIER(0).GetText();

            // IMPORTANT: Extract generic parameters FIRST before parsing trait type args
            // This ensures that 'T' is in scope when parsing 'Iterable<T>'
            var genericParams = new List<string>();
            if (implContext.genericParams() != null)
            {
                foreach (var paramId in implContext.genericParams().IDENTIFIER())
                {
                    var paramName = paramId.GetText();
                    genericParams.Add(paramName);
                    _genericParams[paramName] = new IrGenericType(paramName);
                }
            }

            // Check if this is a trait implementation
            bool isTraitImpl = implContext.KW_FOR() != null;
            string? traitName = null;
            List<IrType> traitTypeArgs = new();

            if (isTraitImpl)
            {
                traitName = typeNames[0].IDENTIFIER(0).GetText();

                // Parse trait type arguments if present (e.g., Iterator<i32>)
                // Generic params are now in scope, so 'T' in 'Iterable<T>' can be resolved
                var traitGenericArgs = implContext.genericTypeArgs().Length > 0 ? implContext.genericTypeArgs(0) : null;
                if (traitGenericArgs != null)
                {
                    var typeList = traitGenericArgs.typeList();
                    foreach (var typeCtx in typeList.type())
                    {
                        traitTypeArgs.Add(ParseType(typeCtx));
                    }
                }
            }

            // Process each method in the impl block
            foreach (var implItem in implContext.implItem())
            {
                var funcDecl = implItem.functionDeclaration();
                if (funcDecl == null) continue;

                var methodName = funcDecl.IDENTIFIER().GetText();

                // For generic impl blocks, store methods as templates for later instantiation
                if (genericParams.Count > 0)
                {
                    var templateKey = $"{typeName}::{methodName}";
                    // Capture current constants dictionary (make a copy so imports don't affect templates)
                    var templateConstants = new Dictionary<string, (IrType Type, object Value)>(_constants);
                    _genericMethodTemplates[templateKey] = (genericParams, funcDecl, templateConstants);
                    // Don't create function yet - it will be instantiated when called with concrete types
                    continue;
                }

                // Non-generic impl blocks: create function signatures now
                var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;

                // Check for extern, pub, and internal keywords
                var isExtern = false;
                var visibility = Visibility.Private;
                for (int i = 0; i < Math.Min(4, funcDecl.ChildCount); i++)
                {
                    var childText = funcDecl.GetChild(i)?.GetText();
                    if (childText == "extern") isExtern = true;
                    if (childText == "pub") visibility = Visibility.Public;
                    if (childText == "internal") visibility = Visibility.Internal;
                }

                // Methods are registered with mangled names
                // Trait impls: Type_Trait_TypeArg1_TypeArg2_method (e.g., Counter_Iterator_i32_next)
                // Inherent impls: Type::method
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

                var function = new IrFunction(mangledName, returnType, visibility, isExtern);

                // Parse parameters (including self)
                if (funcDecl.parameterList() != null)
                {
                    var paramList = funcDecl.parameterList();

                    // Handle self parameter if present
                    if (paramList.selfParameter() != null)
                    {
                        var selfParam = paramList.selfParameter();
                        var isMutable = selfParam.KW_MUT() != null;
                        var isBorrowed = selfParam.GetChild(0).GetText() == "&";

                        // Determine self type - look up the struct type
                        if (!_structs.TryGetValue(typeName, out var structType))
                        {
                            throw new Exception($"Type '{typeName}' not found for impl block");
                        }

                        IrType selfType = structType;
                        if (isBorrowed)
                        {
                            selfType = _typeInterner.GetPointerType(selfType);
                        }

                        function.Parameters.Add(new IrParameter("self", selfType));
                    }

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

                _module.AddFunction(function);
            }

            // Register trait implementation if this is a trait impl
            if (isTraitImpl && traitName != null && genericParams.Count == 0)
            {
                // Look up the implementing type
                if (!_structs.TryGetValue(typeName, out var implementingType))
                {
                    throw new Exception($"Type '{typeName}' not found for trait implementation");
                }

                // Create IrTraitImpl and add to module
                var traitImpl = new IrTraitImpl(traitName, traitTypeArgs, typeName, implementingType);
                _module.TraitImpls.Add(traitImpl);
            }

            // Clear generic parameters after processing impl block
            foreach (var paramName in genericParams)
            {
                _genericParams.Remove(paramName);
            }
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
                throw new Exception($"Function '{funcName}' not found in module. This indicates a compiler bug in an earlier pass.");
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

            // Get only the base type name (Vec, not Vec<T>)
            // typeName() returns an array: [Type] for "impl Type" or [Trait, Type] for "impl Trait for Type"
            var typeNames = implContext.typeName();
            var typeName = typeNames[typeNames.Length - 1].IDENTIFIER(0).GetText();

            // Check if this is a trait implementation
            bool isTraitImpl = implContext.KW_FOR() != null;
            string? traitName = null;
            List<IrType> traitTypeArgs = new();

            if (isTraitImpl)
            {
                traitName = typeNames[0].IDENTIFIER(0).GetText();

                // Parse trait type arguments if present (e.g., Iterator<i32>)
                var traitGenericArgs = implContext.genericTypeArgs().Length > 0 ? implContext.genericTypeArgs(0) : null;
                if (traitGenericArgs != null)
                {
                    var typeList = traitGenericArgs.typeList();
                    foreach (var typeCtx in typeList.type())
                    {
                        traitTypeArgs.Add(ParseType(typeCtx));
                    }
                }
            }

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
                    throw new Exception($"Method '{mangledName}' not found in module. This indicates a compiler bug in an earlier pass.");
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
                throw new Exception($"Module '{moduleNamespace}' not found or has syntax errors");
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
        // Skip if this module has already been processed (prevent circular imports)
        if (_processedModules.Contains(moduleNamespace))
        {
            return;
        }

        // Mark this module as being processed
        _processedModules.Add(moduleNamespace);

        // Convert namespace path to file path
        string modulePath = ModuleImportHelper.ResolveModulePath(moduleNamespace, _stdLibPath);

        // Load and parse the module first to check if it needs compilation
        var (moduleContext, syntaxErrors) = ModuleImportHelper.ParseModuleFile(modulePath);

        if (moduleContext == null || syntaxErrors > 0)
        {
            throw new Exception($"Module '{moduleNamespace}' not found at {modulePath} or has syntax errors");
        }

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

        // Register imported enums in the module
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

            // Skip if this constant has already been imported (transitive dependencies)
            if (_constants.ContainsKey(constName))
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

            // Skip if this struct has already been imported (transitive dependencies)
            if (_structs.ContainsKey(structName))
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
            var (isPub, isExtern) = ModuleImportHelper.GetFunctionVisibility(funcDecl);

            if (!isPub && !isExtern)
            {
                throw new Exception($"Cannot import private function '{funcName}' from module '{moduleNamespace}'");
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
            if (funcDecl.parameterList() != null)
            {
                var paramList = funcDecl.parameterList();
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
                    _genericParams[paramName] = new IrGenericType(paramName);
                }
            }

            // Get only the base type name (Vec, not Vec<T>)
            // typeName() returns an array: [Type] for "impl Type" or [Trait, Type] for "impl Trait for Type"
            var typeNames = implDecl.typeName();
            var typeName = typeNames[typeNames.Length - 1].IDENTIFIER(0).GetText();

            // Skip if the type this impl is for is not in the import list
            // This prevents importing methods for types we don't have access to
            if (!namesToImport.Contains(typeName))
            {
                continue;
            }

            foreach (var implItem in implDecl.implItem())
            {
                var funcDecl = implItem.functionDeclaration();
                if (funcDecl == null) continue;

                var methodName = funcDecl.IDENTIFIER().GetText();

                // Check if method is pub
                var isPub = false;
                for (int i = 0; i < Math.Min(3, funcDecl.ChildCount); i++)
                {
                    if (funcDecl.GetChild(i)?.GetText() == "pub")
                    {
                        isPub = true;
                        break;
                    }
                }

                // For generic impl blocks, store ALL methods as templates (pub and private)
                // because instantiating one method may need to call private helper methods
                if (genericParams.Count > 0)
                {
                    var templateKey = $"{typeName}::{methodName}";
                    // Capture current constants dictionary (make a copy so imports don't affect templates)
                    var templateConstants = new Dictionary<string, (IrType Type, object Value)>(_constants);
                    _genericMethodTemplates[templateKey] = (genericParams, funcDecl, templateConstants);
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

                // Methods are registered with mangled names: Type::method
                var mangledName = $"{typeName}::{methodName}";
                var function = new IrFunction(mangledName, returnType, Visibility.Private, false);

                // Parse parameters (including self)
                if (funcDecl.parameterList() != null)
                {
                    var paramList = funcDecl.parameterList();

                    // Handle self parameter if present
                    if (paramList.selfParameter() != null)
                    {
                        var selfParam = paramList.selfParameter();
                        var isMutable = selfParam.KW_MUT() != null;
                        var isBorrowed = selfParam.GetChild(0).GetText() == "&";

                        // Determine self type - look up the struct type
                        if (!_structs.TryGetValue(typeName, out var structType))
                        {
                            throw new Exception($"Type '{typeName}' not found for impl block");
                        }

                        IrType selfType = structType;
                        if (isBorrowed)
                        {
                            selfType = _typeInterner.GetPointerType(selfType);
                        }

                        function.Parameters.Add(new IrParameter("self", selfType));
                    }

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

                _module.AddFunction(function);
            }

            // Clear generic params from scope after impl registration
            foreach (var paramName in genericParams)
            {
                _genericParams.Remove(paramName);
            }
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
                bool isExtern = false;
                for (int i = 0; i < Math.Min(3, funcDecl.ChildCount); i++)
                {
                    if (funcDecl.GetChild(i)?.GetText() == "extern")
                    {
                        isExtern = true;
                        break;
                    }
                }

                // Only import extern functions (FFI bindings)
                if (!isExtern) continue;

                // Check if we already have this function
                if (_module.Functions.Any(f => f.Name == funcName)) continue;

                // Parse and import the extern function
                var returnType = funcDecl.type() != null ? ParseType(funcDecl.type()) : IrVoidType.Instance;
                var function = new IrFunction(funcName, returnType, Visibility.Private, true);

                // Parse parameters
                if (funcDecl.parameterList() != null)
                {
                    var paramList = funcDecl.parameterList();
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
        var savedConstants = new Dictionary<string, (IrType Type, object Value)>(_constants);

        // Start with template constants
        _constants.Clear();
        foreach (var kvp in templateConstants)
        {
            _constants[kvp.Key] = kvp.Value;
        }

        // Overlay current module constants (allows transitive imports to work)
        foreach (var kvp in savedConstants)
        {
            _constants[kvp.Key] = kvp.Value;
            if (!templateConstants.ContainsKey(kvp.Key))
            {
            }
        }

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
        var baseStruct = _structs[baseTypeName];

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
                throw new Exception($"Generic parameter '{genericParam}' not found in monomorphized struct {monomorphizedStruct.CacheKey}");
            }
        }

        // Set up concrete types for substitution during parsing
        var savedGenericParams = new Dictionary<string, IrGenericType>();
        foreach (var paramName in genericParams)
        {
            if (_genericParams.ContainsKey(paramName))
            {
                savedGenericParams[paramName] = _genericParams[paramName];
            }
            _genericParams[paramName] = new IrGenericType(paramName);
        }

        // Set active type substitutions for the duration of this instantiation
        var savedSubstitutions = _currentTypeSubstitutions;
        _currentTypeSubstitutions = typeSubstitutions;

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
            if (paramList.selfParameter() != null)
            {
                var selfParam = paramList.selfParameter();
                var isBorrowed = selfParam.GetChild(0).GetText() == "&";

                IrType selfType = monomorphizedStruct;
                if (isBorrowed)
                {
                    selfType = _typeInterner.GetPointerType(selfType);
                }

                function.Parameters.Add(new IrParameter("self", selfType));
            }

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

        // Restore constants
        _constants.Clear();
        foreach (var kvp in savedConstants)
        {
            _constants[kvp.Key] = kvp.Value;
        }

        // Clear generic params
        foreach (var paramName in typeSubstitutions.Keys)
        {
            _genericParams.Remove(paramName);
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
            if (_genericParams.ContainsKey(paramName))
            {
                savedGenericParams[paramName] = _genericParams[paramName];
            }
            _genericParams[paramName] = new IrGenericType(paramName);
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

        // Keep generic params registered for later use during function instantiation
        // They will be restored at the end

        // Infer generic types from arguments
        var typeSubstitutions = InferGenericFunctionTypes(genericParams, templateParams, arguments);
        if (typeSubstitutions == null)
        {
            return null; // Type inference failed
        }


        // Build monomorphized enum with inferred types
        var monomorphizedEnum = MonomorphizeEnum(enumType, typeSubstitutions);
        if (monomorphizedEnum == null)
        {
            return null;
        }

        // Build instantiation key
        var instantiationKey = $"{monomorphizedEnum.CacheKey}::{methodName}";

        // Check if already instantiated
        if (_instantiatedMethods.Contains(instantiationKey))
        {
            // Already generated, look it up
            var cachedTypeArgKeys = genericParams.Select(p => GetTypeCacheKey(typeSubstitutions[p]));
            var cachedMangledName = $"{baseTypeName}::{methodName}_{string.Join("_", cachedTypeArgKeys.Select(k => k.Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace("*", "ptr_")))}";
            return _module.Functions.FirstOrDefault(f => f.Name == cachedMangledName);
        }

        // Save current state
        var savedConstants = new Dictionary<string, (IrType Type, object Value)>(_constants);
        _constants.Clear();
        foreach (var kvp in templateConstants)
        {
            _constants[kvp.Key] = kvp.Value;
        }
        foreach (var kvp in savedConstants)
        {
            _constants[kvp.Key] = kvp.Value;
        }

        // Generic params already registered from earlier - just set up type substitutions
        var savedTypeSubstitutions = _currentTypeSubstitutions;
        _currentTypeSubstitutions = typeSubstitutions;

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

        // Visit the function body
        if (funcDecl.block() != null)
        {
            Visit(funcDecl.block());
        }

        // Restore state
        _currentBlock = savedBlock;
        _currentFunction = savedFunction;
        _currentTypeSubstitutions = savedTypeSubstitutions;
        _constants.Clear();
        foreach (var kvp in savedConstants)
        {
            _constants[kvp.Key] = kvp.Value;
        }
        foreach (var paramName in typeSubstitutions.Keys)
        {
            _genericParams.Remove(paramName);
        }
        foreach (var kvp in savedGenericParams)
        {
            _genericParams[kvp.Key] = kvp.Value;
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
        if (_monomorphizedEnums.ContainsKey(cacheKey))
        {
            return _monomorphizedEnums[cacheKey];
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
        _monomorphizedEnums[cacheKey] = monomorphizedEnum;

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
        var savedConstants = new Dictionary<string, (IrType Type, object Value)>(_constants);

        // Start with template constants
        _constants.Clear();
        foreach (var kvp in templateConstants)
        {
            _constants[kvp.Key] = kvp.Value;
        }

        // Overlay current module constants (allows transitive imports to work)
        foreach (var kvp in savedConstants)
        {
            _constants[kvp.Key] = kvp.Value;
        }

        // Set up concrete types for substitution during parsing
        var savedGenericParams = new Dictionary<string, IrGenericType>();
        foreach (var paramName in genericParams)
        {
            if (_genericParams.ContainsKey(paramName))
            {
                savedGenericParams[paramName] = _genericParams[paramName];
            }
            _genericParams[paramName] = new IrGenericType(paramName);
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
        _constants.Clear();
        foreach (var kvp in savedConstants)
        {
            _constants[kvp.Key] = kvp.Value;
        }

        // Restore generic params
        _genericParams.Clear();
        foreach (var kvp in savedGenericParams)
        {
            _genericParams[kvp.Key] = kvp.Value;
        }

        // Mark as instantiated
        _instantiatedGenericFunctions.Add(instantiationKey);

        return function;
    }

    private void RegisterConstant(NovusParser.ConstDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Check for pub/internal keywords
        var visibility = Visibility.Private;
        for (int i = 0; i < Math.Min(3, context.ChildCount); i++)
        {
            var childText = context.GetChild(i)?.GetText();
            if (childText == "pub") visibility = Visibility.Public;
            if (childText == "internal") visibility = Visibility.Internal;
        }

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

            _constants[name] = (type, value);
            // Also store in the IR module for code generator access
            _module.Constants[name] = (visibility, type, value);
        }
    }

    private void RegisterStatic(NovusParser.StaticDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var type = ParseType(context.type());

        // Check for pub/internal/mut keywords
        var visibility = Visibility.Private;
        var isMutable = false;
        for (int i = 0; i < Math.Min(5, context.ChildCount); i++)
        {
            var childText = context.GetChild(i)?.GetText();
            if (childText == "pub") visibility = Visibility.Public;
            if (childText == "internal") visibility = Visibility.Internal;
            if (childText == "mut") isMutable = true;
        }

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
            var constantValues = _constants.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Value
            );

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
                _genericParams[paramName] = new IrGenericType(paramName);
            }
        }

        // Register placeholder struct FIRST to allow self-referential types
        var placeholderStruct = new IrStructType(name, new List<IrStructField>(), genericParams, null, attributes);
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
        var structType = new IrStructType(name, fields, genericParams, null, attributes);

        // Force offset calculation by accessing SizeInBytes (only for non-generic structs)
        // Generic structs will be monomorphized later when instantiated with concrete types
        if (genericParams.Count == 0)
        {
            _ = structType.SizeInBytes;

            // Add non-generic structs to the module (for library generation, etc.)
            _module.Structs.Add(structType);
        }

        _structs[name] = structType;
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
                foreach (var param in methodGenericParams)
                {
                    _genericParams.Remove(param);
                }
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
        _traits[name] = trait;
        _module.AddTrait(trait);

        // Clear generic parameters after trait registration
        _genericParams.Clear();
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
            var location = new Novus.Diagnostics.SourceLocation(_inputFilePath, attrCtx.Start.Line, attrCtx.Start.Column, 0, "");
            var attr = new Novus.SemanticAnalysis.AttributeInfo(attrName, location);

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

        // Check if function is extern by looking for 'extern' keyword in children
        var isExtern = false;
        for (int i = 0; i < Math.Min(3, context.ChildCount); i++)
        {
            var childText = context.GetChild(i)?.GetText();
            if (childText == "extern") isExtern = true;
        }

        // Parse visibility
        var visibility = Visibility.Private;
        for (int i = 0; i < Math.Min(4, context.ChildCount); i++)
        {
            var childText = context.GetChild(i)?.GetText();
            if (childText == "pub") visibility = Visibility.Public;
            if (childText == "internal") visibility = Visibility.Internal;
        }

        var function = new IrFunction(name, returnType, visibility, isExtern);
        _module.AddFunction(function);
        _currentFunction = function;

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
        // Check if this is a throwaway binding (_)
        var identifierNode = context.IDENTIFIER();
        var name = identifierNode?.GetText() ?? "_";
        var isThrowaway = name == "_";
        var isMutable = context.GetChild(0)?.GetText() == "var";

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
            throw new Exception($"Variable must have an initial value");
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

    public override object? VisitAssignmentStatement([NotNull] NovusParser.AssignmentStatementContext context)
    {
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
                derefCount++;
            else if (context.GetChild(i) is ITerminalNode terminal && terminal.Symbol.Type == NovusLexer.IDENTIFIER)
                break;
        }

        var lvalueSuffixes = context.lvalueSuffix();

        // Handle post-increment/decrement statements (no expression)
        if (isPostIncDec)
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
                throw new Exception($"Variable {name} not found");
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

        // Check if this is a member or index assignment (has lvalueSuffix elements)
        if (lvalueSuffixes.Length > 0)
        {
            // Handle member/index assignments: obj.field = value, arr[index] = value
            var value = (IrValue?)Visit(context.expression());
            if (value == null)
            {
                throw new Exception($"Assignment requires a value");
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
                throw new Exception($"Undefined variable: {name}");
            }

            // For single member access (e.g., self.value = expr)
            if (lvalueSuffixes.Length == 1 && lvalueSuffixes[0].GetChild(0).GetText() == ".")
            {
                var memberName = lvalueSuffixes[0].IDENTIFIER().GetText();

                // Auto-dereference pointers to structs (like in VisitMemberAccessExpr)
                IrValue actualBase = baseVar;
                var structType = baseVar.Type;
                if (structType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
                {
                    // Wrap in IrDereferenceValue for auto-dereference
                    actualBase = new IrDereferenceValue(baseVar, ptrType.PointeeType);
                    structType = ptrType.PointeeType;
                }

                if (structType is not IrStructType irStructType)
                {
                    throw new Exception($"Cannot access member '{memberName}' on non-struct type");
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
                    throw new Exception($"Field '{memberName}' not found in struct '{irStructType.Name}'");
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
                    throw new Exception("Index expression is required");
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

                    // Auto-dereference pointers to structs
                    IrValue actualBase = currentLValue;
                    var structType = currentLValue.Type;
                    if (structType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
                    {
                        actualBase = new IrDereferenceValue(currentLValue, ptrType.PointeeType);
                        structType = ptrType.PointeeType;
                    }

                    if (structType is not IrStructType irStructType)
                    {
                        throw new Exception($"Cannot access member '{memberName}' on non-struct type");
                    }

                    // Find the field
                    var field = irStructType.Fields.FirstOrDefault(f => f.Name == memberName);
                    if (field == null)
                    {
                        throw new Exception($"Field '{memberName}' not found in struct '{irStructType.Name}'");
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
                        throw new Exception("Index expression is required");
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
                            throw new Exception($"Cannot index type '{currentLValue.Type}' - must be pointer or array");
                        }

                        var tempName = $"_indexed_{_tempCounter++}";
                        var loadIndex = new IrIndexAccess(tempName, currentLValue, indexExpr, elementType);
                        _currentBlock!.AddInstruction(loadIndex);
                        currentLValue = new IrVariable(tempName, elementType);
                    }
                }
                else
                {
                    throw new Exception($"Unexpected lvalue suffix: {suffix.GetText()}");
                }
            }

            // If we get here, something went wrong
            throw new Exception("Failed to process lvalue chain");
        }

        if (derefCount > 0)
        {
            // Dereference assignment: *ptr = value or **ptr = value, etc.
            var value = (IrValue?)Visit(context.expression());

            if (value == null)
            {
                throw new Exception($"Assignment to dereferenced variable requires a value");
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
                throw new Exception($"Variable {name} not found");
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
                    throw new Exception($"Cannot dereference non-pointer type");

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
                throw new Exception($"Assignment to {name} requires a value");
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
                    throw new Exception($"Variable {name} not found");
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
                    _ => throw new Exception($"Unknown compound operator: {op}")
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
            throw new Exception($"if let expression returned null");

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
            throw new Exception($"if let only works with pointers or integers, got {expression.Type.Name}");
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
            throw new Exception($"if var expression returned null");

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
            throw new Exception($"if var only works with pointers or integers, got {expression.Type.Name}");
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
            throw new Exception($"Type '{typeName}' does not implement Iterable trait (missing len() method). For-in loops require types to implement Iterable<T>.");
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
            throw new Exception($"Type '{typeName}' does not implement Iterable trait (missing get() method). For-in loops require types to implement Iterable<T>.");
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
            throw new Exception($"Iterator::get must return Option<T>, but returned {getMethod.ReturnType.Name}");
        }

        // Find the Some variant to get the inner type
        var someVariant = optionType.Variants.FirstOrDefault(v => v.Name == "Some");
        if (someVariant == null || someVariant.AssociatedData.Count == 0)
        {
            throw new Exception("Option::Some variant not found or has no associated data");
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
            throw new Exception("Option::None variant not found");
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
            throw new Exception("break statement outside of loop");
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

        // If it's a generic function template, infer types and instantiate
        if (genericFuncName != null && _genericFunctionTemplates.ContainsKey(genericFuncName))
        {
            // Get template and parse parameters
            var template = _genericFunctionTemplates[genericFuncName];

            // Save and clear type substitutions so we get the generic template types
            var savedTypeSubstitutions = _currentTypeSubstitutions;
            _currentTypeSubstitutions = null;

            // Set up generic params temporarily
            var savedGenericParams = new Dictionary<string, IrGenericType>(_genericParams);
            _genericParams.Clear();
            foreach (var paramName in template.GenericParams)
            {
                _genericParams[paramName] = new IrGenericType(paramName);
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
            _genericParams.Clear();
            foreach (var kvp in savedGenericParams)
            {
                _genericParams[kvp.Key] = kvp.Value;
            }

            // Restore type substitutions
            _currentTypeSubstitutions = savedTypeSubstitutions;

            // Infer types
            var typeSubstitutions = InferGenericFunctionTypes(template.GenericParams, templateParams, arguments);
            if (typeSubstitutions == null)
            {
                throw new Exception($"Cannot infer type arguments for '{genericFuncName}'");
            }

            // Instantiate
            var instantiatedFunc = InstantiateGenericFunction(genericFuncName, typeSubstitutions);
            if (instantiatedFunc == null)
            {
                throw new Exception($"Failed to instantiate '{genericFuncName}'");
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
            // Try to infer from expected type
            if (_expectedType == null)
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

            // Extract concrete type parameters from expected type
            IrStructType? monomorphizedStruct = null;

            if (_expectedType is IrStructType expectedStruct && expectedStruct.GenericParameters.Count == 0)
            {
                // Expected type is a monomorphized struct like Vec<i32>
                monomorphizedStruct = expectedStruct;
            }

            if (monomorphizedStruct == null)
            {
                throw new Exception($"Cannot determine concrete type parameters for '{genericAssocFunc.TypeName}::{genericAssocFunc.MethodName}()' from expected type '{_expectedType.Name}'");
            }

            // Instantiate the generic method with the monomorphized struct
            var instantiatedFunc = InstantiateGenericMethod(monomorphizedStruct, genericAssocFunc.MethodName);
            if (instantiatedFunc == null)
            {
                throw new Exception($"Failed to instantiate generic method '{genericAssocFunc.TypeName}::{genericAssocFunc.MethodName}'");
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
                    throw new Exception($"Variadic function '{funcRef.Function.Name}' expects at least {funcRefNonVariadicCount} arguments, got {arguments.Count}");
            }
            else
            {
                if (arguments.Count != funcRef.Function.Parameters.Count)
                    throw new Exception($"Function '{funcRef.Function.Name}' expects {funcRef.Function.Parameters.Count} arguments, got {arguments.Count}");
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

            // If enum has generic parameters, perform type inference to monomorphize
            IrEnumType finalEnumType = enumType;
            if (enumType.GenericParameters.Count > 0)
            {
                // Build type substitutions from argument types
                var typeSubstitutions = new Dictionary<string, IrType>();
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

                // Special case: If this is a unit variant (no arguments) and we have an expected type,
                // use the expected type directly for monomorphization
                if (arguments.Count == 0 &&
                    _expectedType is IrEnumType expectedEnumType &&
                    expectedEnumType.EnumName == enumType.EnumName &&
                    expectedEnumType.GenericParameters.Count == 0)
                {
                    // The expected type is already fully monomorphized, use it directly
                    finalEnumType = expectedEnumType;
                }
                // Bidirectional type checking: use expected type to fill in missing parameters
                else if (_expectedType is IrEnumType expectedEnumType2 &&
                    expectedEnumType2.EnumName == enumType.EnumName &&
                    expectedEnumType2.GenericParameters.Count == 0) // Expected type is monomorphized
                {
                    // Extract concrete types from expected enum by matching variant structure
                    for (int paramIdx = 0; paramIdx < enumType.GenericParameters.Count; paramIdx++)
                    {
                        var paramName = enumType.GenericParameters[paramIdx];

                        // Check if we need to refine or replace the existing substitution
                        bool needsRefinement = false;
                        if (typeSubstitutions.ContainsKey(paramName))
                        {
                            var existing = typeSubstitutions[paramName];
                            // Check if the existing substitution is still generic (contains IrGenericType)
                            if (existing is IrEnumType existingEnum)
                            {
                                bool hasGenericData = existingEnum.Variants.Any(v =>
                                    v.AssociatedData.Any(d => d is IrGenericType));
                                if (hasGenericData || existingEnum.GenericParameters.Count > 0)
                                {
                                    needsRefinement = true;
                                }
                            }
                        }

                        if (!typeSubstitutions.ContainsKey(paramName) || needsRefinement)
                        {
                            // Find this parameter in a variant and extract the concrete type
                            for (int varIdx = 0; varIdx < enumType.Variants.Count; varIdx++)
                            {
                                var origVariant = enumType.Variants[varIdx];
                                var expectedVar = expectedEnumType2.Variants[varIdx];

                                for (int dataIdx = 0; dataIdx < origVariant.AssociatedData.Count; dataIdx++)
                                {
                                    var expectedType = expectedVar.AssociatedData[dataIdx];
                                    if (origVariant.AssociatedData[dataIdx] is IrGenericType gt &&
                                        gt.ParameterName == paramName)
                                    {
                                        typeSubstitutions[paramName] = expectedType;
                                        break;
                                    }
                                }

                                if (typeSubstitutions.ContainsKey(paramName) && !needsRefinement)
                                    break;
                            }
                        }
                    }

                    // Create cache key using proper type keys
                    var typeArgKeys = enumType.GenericParameters.Select(p =>
                    {
                        var key = typeSubstitutions.ContainsKey(p) ? GetTypeCacheKey(typeSubstitutions[p]) : p;
                        return key;
                    });
                    var cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";

                    // Check cache first
                    if (_monomorphizedEnums.ContainsKey(cacheKey))
                    {
                        finalEnumType = _monomorphizedEnums[cacheKey];
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
                            _monomorphizedEnums[cacheKey] = finalEnumType;
                        }
                    }
                }
            }

            // Create the enum value with the monomorphized type
            var finalVariant = finalEnumType.GetVariant(enumCtor.VariantName);
            return new IrEnumValue(finalEnumType, enumCtor.VariantName, finalVariant!.Tag, arguments);
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

        // Check if this is an associated function on an enum that needs instantiation (e.g., Option::FromPointer)
        if (functionName.Contains("::"))
        {
            var parts = functionName.Split("::");
            if (parts.Length == 2)
            {
                var typeName = parts[0];
                var methodName = parts[1];

                // Check if it's an enum type
                if (_enums.ContainsKey(typeName))
                {
                    var enumType = _enums[typeName];
                    if (enumType.GenericParameters.Count > 0)
                    {
                        // Try to instantiate the generic method for this enum
                        var instantiatedFunc = InstantiateGenericEnumMethod(enumType, methodName, arguments);
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
            }
        }

        // Look up the function in the module to get its return type
        var function = _module.Functions.FirstOrDefault(f => f.Name == functionName);
        if (function == null)
        {
            throw new Exception($"Unknown function: {functionName}");
        }

        // Check argument count matches parameter count
        var nonVariadicCount = function.Parameters.Count(p => !p.IsVariadic);
        if (function.IsVariadic)
        {
            if (arguments.Count < nonVariadicCount)
                throw new Exception($"Variadic function '{functionName}' expects at least {nonVariadicCount} arguments, got {arguments.Count}");
        }
        else
        {
            if (arguments.Count != function.Parameters.Count)
                throw new Exception($"Function {functionName} expects {function.Parameters.Count} arguments, got {arguments.Count}");
        }

        // Insert implicit casts for arguments where needed
        // (e.g., u32 -> i32 for same-bit-width conversions, String -> i32 for FFI)
        for (int i = 0; i < arguments.Count; i++)
        {
            var argType = arguments[i].Type;
            var paramType = function.Parameters[i].Type;

            // Handle String to i32 conversion for FFI
            if (argType is IrStringType && paramType is IrIntType)
            {
                // String literal - need to create temp String variable first, then extract .ptr
                if (arguments[i] is IrStringLiteral stringLit)
                {
                    // Create a temporary String variable to hold the {ptr, len} struct
                    var stringTempName = $"_str_temp_{_tempCounter++}";
                    var stringVar = new IrLocalVariable(stringTempName, IrStringType.Instance, false);
                    _currentFunction!.LocalVariables.Add(stringVar);

                    var stringDecl = new IrLocalDecl(stringTempName, IrStringType.Instance, false, stringLit);
                    _currentBlock!.AddInstruction(stringDecl);

                    // Now extract the .ptr field
                    var ptrTempName = $"%t{_tempCounter++}";
                    var ptrAccess = new IrMemberAccess(
                        ptrTempName,
                        new IrVariable(stringTempName, IrStringType.Instance),
                        "ptr",
                        _typeInterner.GetPointerType(IrIntType.U8),
                        0  // ptr is at offset 0
                    );
                    _currentBlock!.AddInstruction(ptrAccess);

                    // Replace argument with the extracted pointer (cast to i32 for compatibility)
                    arguments[i] = new IrVariable(ptrTempName, paramType);
                }
                else
                {
                    // String variable - extract .ptr field
                    var ptrTempName = $"%t{_tempCounter++}";
                    var ptrAccess = new IrMemberAccess(
                        ptrTempName,
                        arguments[i],
                        "ptr",
                        _typeInterner.GetPointerType(IrIntType.U8),
                        0  // ptr is at offset 0
                    );
                    _currentBlock!.AddInstruction(ptrAccess);
                    arguments[i] = new IrVariable(ptrTempName, paramType);
                }
            }
            // If types don't exactly match but are compatible integer types of same width
            else if (!argType.Equals(paramType) &&
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
        var genericStruct = _structs.Values.FirstOrDefault(s =>
            s.StructName == partialType.GenericTypeName && s.GenericParameters.Count > 0);

        if (genericStruct == null)
        {
            return null;
        }

        // Create monomorphized struct
        var monomorphizedStruct = IrStructType.Monomorphize(genericStruct, resolvedTypeArgs);

        // Cache the monomorphized struct
        var cacheKey = monomorphizedStruct.CacheKey ?? monomorphizedStruct.Name;
        if (!_monomorphizedStructs.ContainsKey(cacheKey))
        {
            _monomorphizedStructs[cacheKey] = monomorphizedStruct;
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
            throw new Exception("Method call receiver is null");
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
                throw new Exception($"Cannot infer generic type parameters for '{partialType.GenericTypeName}' from method call '{methodName}'");
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
                throw new Exception($"Cannot call methods on pointer to non-struct/enum type: {receiverType.Name}");
            }
        }
        else
        {
            throw new Exception($"Cannot call methods on type: {receiverType.Name}");
        }

        // Build the mangled function name: Type::method
        // Note: For monomorphized generic types, we'll try Type::method first,
        // then fall back to instantiation if needed
        var mangledMethodName = $"{typeName}::{methodName}";

        // Look up the method
        var method = _module.Functions.FirstOrDefault(f => f.Name == mangledMethodName);

        // If method not found, try to instantiate it for monomorphized structs
        if (method == null)
        {
            IrStructType? monomorphizedStruct = null;

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

            if (monomorphizedStruct != null)
            {
                method = InstantiateGenericMethod(monomorphizedStruct, methodName);
            }
        }

        if (method == null)
        {
            throw new Exception($"Method '{methodName}' not found for type '{typeName}'");
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
                // Wrap receiver in IrBorrowValue to take its address
                bool isMutable = firstParamType is IrMutReferenceType || firstParamType is IrPointerType;
                receiverArg = new IrBorrowValue(receiver, firstParamType, isMutable);
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
                throw new Exception($"Variadic method '{methodName}' expects at least {nonVariadicCount} arguments, got {arguments.Count}");
        }
        else
        {
            if (arguments.Count != method.Parameters.Count)
                throw new Exception($"Method {methodName} expects {method.Parameters.Count} arguments, got {arguments.Count}");
        }

        // Create the call instruction
        var returnType = method.ReturnType;
        var resultName = returnType is not IrVoidType ? $"%t{_tempCounter++}" : null;

        var call = new IrCall(mangledMethodName, returnType, resultName);
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

        // For variables, struct members, array elements, etc., create a reference
        // Visit the expression to get its value
        var value = (IrValue)Visit(exprContext)!;

        // Create the appropriate reference type
        var refType = isMutable
            ? (IrType)_typeInterner.GetMutReferenceType(value.Type)
            : _typeInterner.GetReferenceType(value.Type);

        // For code generation, references are just pointers (addresses)
        // We return the value itself - the semantic analyzer will track that it's a reference
        // At codegen time, we'll take the address of the value

        // Create a "borrow" value that wraps the original value with reference type
        return new IrBorrowValue(value, refType, isMutable);
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

        throw new Exception($"Cannot index into non-array/non-pointer type: {baseExpr.Type.Name}");
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

        // Handle String to integer cast - extract the .ptr field
        // This allows FFI interop: extern fn foo(s: i32) can accept String arguments
        if (value.Type is IrStringType && targetType is IrIntType)
        {
            // Create a member access to extract the .ptr field
            var ptrTempName = $"_str_ptr_{_tempCounter++}";
            var ptrAccess = new IrMemberAccess(
                ptrTempName,
                value,
                "ptr",
                new IrPointerType(IrIntType.U8),
                0  // ptr is at offset 0
            );
            _currentBlock!.AddInstruction(ptrAccess);
            return new IrVariable(ptrTempName, _typeInterner.GetPointerType(IrIntType.U8));
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

        throw new Exception($"Unknown unary operator: {op}");
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

            if (baseExpr.Type is not IrStructType structType)
            {
                throw new Exception($"Cannot access member '{memberName}' on non-struct type '{baseExpr.Type}'");
            }

            var field = structType.Fields.FirstOrDefault(f => f.Name == memberName);
            if (field == null)
            {
                throw new Exception($"Struct '{structType.Name}' has no field '{memberName}'");
            }

            _currentBlock!.AddInstruction(new IrMemberStore(baseExpr, memberName, field.Offset, value));
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

        throw new Exception($"Cannot store to expression type: {exprContext.GetType().Name}");
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

            // Get the struct type and field info
            if (baseExpr.Type is not IrStructType structType)
            {
                throw new Exception($"Cannot access member '{memberName}' on non-struct type '{baseExpr.Type}'");
            }

            var field = structType.Fields.FirstOrDefault(f => f.Name == memberName);
            if (field == null)
            {
                throw new Exception($"Struct '{structType.Name}' has no field '{memberName}'");
            }

            // Load current value
            var loadTemp = $"%member_load_{_tempCounter++}";
            _currentBlock!.AddInstruction(new IrMemberAccess(loadTemp, baseExpr, memberName, field.Type, field.Offset));
            var currentValue = new IrVariable(loadTemp, field.Type);

            // Increment/decrement
            var newValueTemp = $"%t{_tempCounter++}";
            var op = isIncrement
                ? new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Add, currentValue, new IrConstant(1, field.Type), field.Type)
                : new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Sub, currentValue, new IrConstant(1, field.Type), field.Type);
            _currentBlock.AddInstruction(op);

            var newValue = new IrVariable(newValueTemp, field.Type);
            _currentBlock.AddInstruction(new IrMemberStore(baseExpr, memberName, field.Offset, newValue));
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
                throw new Exception($"Cannot index type '{arrayExpr.Type}'");

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
                throw new Exception($"Cannot dereference non-pointer/reference type '{ptrExpr.Type}'");
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

        throw new Exception($"Pre-{(isIncrement ? "increment" : "decrement")} not supported for expression type: {exprContext.GetType().Name}");
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

        // Add to function's local variables for stack allocation
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

    public override object? VisitSizeofExpr([NotNull] NovusParser.SizeofExprContext context)
    {
        // @sizeof(Type) - compile-time intrinsic that returns size in bytes as u32
        var typeCtx = context.type();
        var targetType = ParseType(typeCtx);

        if (targetType == null)
        {
            throw new Exception($"could not determine type for @sizeof");
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
                if (_enums.ContainsKey(typeName))
                {
                    var enumType = _enums[typeName];
                    var variant = enumType.GetVariant(memberName);

                    if (variant != null)
                    {
                        // For unit variants (no associated data), create the enum value directly
                        if (variant.AssociatedData.Count == 0)
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

                            return new IrEnumValue(concreteEnumType, memberName, variant.Tag, new List<IrValue>());
                        }

                        // For variants with data, return a constructor for use in call expressions
                        return new IrEnumConstructor(enumType, memberName, variant.Tag);
                    }
                }

                // Try associated function (struct method without self parameter)
                var mangledName = name; // Already has :: format

                // Check if this is a generic type - look in generic method templates
                if (_structs.ContainsKey(typeName))
                {
                    var structType = _structs[typeName];

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
        if (_constants.ContainsKey(name))
        {
            var (type, value) = _constants[name];
            return new IrConstant((int)value, type);
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
        if (name == "CPU" && _enums.ContainsKey("SystemCPU"))
        {
            return new IrVariable(name, _enums["SystemCPU"]);
        }
        if (name == "FPU" && _enums.ContainsKey("SystemFPU"))
        {
            return new IrVariable(name, _enums["SystemFPU"]);
        }
        if (name == "Chipset" && _enums.ContainsKey("SystemChipset"))
        {
            return new IrVariable(name, _enums["SystemChipset"]);
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

        throw new Exception("'self' can only be used inside methods");
    }

    public override object? VisitParenExpr([NotNull] NovusParser.ParenExprContext context)
    {
        return Visit(context.expression());
    }

    public override object? VisitStructLiteral([NotNull] NovusParser.StructLiteralContext context)
    {
        var structName = context.typeName().GetText();

        if (!_structs.ContainsKey(structName))
        {
            throw new Exception($"Unknown struct type '{structName}'");
        }

        // Get the base struct type
        var baseStructType = _structs[structName];

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
                throw new Exception($"Field '{fieldName}' in struct '{structName}' requires a value");
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
                // Create monomorphized struct type
                var typeArgs = baseStructType.GenericParameters.Select(p => typeSubstitutions[p]).ToList();
                var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
                var cacheKey = $"{baseStructType.StructName}<{string.Join(",", typeArgKeys)}>";

                // Check cache first
                if (_monomorphizedStructs.ContainsKey(cacheKey))
                {
                    structType = _monomorphizedStructs[cacheKey];
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
                    _monomorphizedStructs[cacheKey] = structType;
                }
            }
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

    public override object? VisitStructArrayInit([NotNull] NovusParser.StructArrayInitContext context)
    {
        // Handle Vec { {10, 20, 30} } syntax
        // This is syntactic sugar for collections that can be initialized from an array literal

        var structName = context.typeName().GetText();

        if (!_structs.ContainsKey(structName))
        {
            throw new Exception($"Unknown struct type '{structName}'");
        }

        var baseStructType = _structs[structName];

        // Get the array literal expression
        var arrayExpr = (IrValue?)Visit(context.expression());
        if (arrayExpr == null)
        {
            throw new Exception("Struct array initializer requires an expression");
        }

        // Verify it's an array literal
        if (arrayExpr is not IrArrayLiteral arrayLiteral)
        {
            throw new Exception($"Struct array initializer for '{structName}' requires an array literal, got {arrayExpr.GetType().Name}");
        }

        // For now, only support this for Vec type
        if (structName != "Vec")
        {
            throw new Exception($"Struct array initializer syntax is only supported for Vec, not '{structName}'");
        }

        // Extract element type from array
        if (arrayLiteral.Type is not IrArrayType arrayType)
        {
            throw new Exception($"Expected array type, got {arrayLiteral.Type}");
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
            if (_monomorphizedStructs.ContainsKey(cacheKey))
            {
                vecType = _monomorphizedStructs[cacheKey];
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
                _monomorphizedStructs[cacheKey] = vecType;
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
            throw new Exception("Member access requires a base expression");
        }

        var memberName = context.IDENTIFIER().GetText();

        // Handle String type member access
        if (baseExpr.Type is IrStringType)
        {
            IrType fieldType;
            int fieldOffset;

            if (memberName == "ptr")
            {
                fieldType = _typeInterner.GetPointerType(IrIntType.U8);
                fieldOffset = 0;  // ptr is at offset 0
            }
            else if (memberName == "len")
            {
                fieldType = IrIntType.I32;
                fieldOffset = 4;  // len is at offset 4 (after the 4-byte ptr)
            }
            else
            {
                throw new Exception($"String type does not have a field named '{memberName}'. Available fields: ptr, len");
            }

            // Generate a member access instruction for String
            var strResultName = $"%t{_tempCounter++}";
            var strMemberAccess = new IrMemberAccess(strResultName, baseExpr, memberName, fieldType, fieldOffset);
            _currentBlock!.AddInstruction(strMemberAccess);

            return new IrVariable(strResultName, fieldType);
        }

        // Auto-dereference pointers to structs
        IrValue actualBase = baseExpr;
        IrType baseType = baseExpr.Type;

        if (baseType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
        {
            // Auto-dereference the pointer - wrap in IrDereferenceValue
            actualBase = new IrDereferenceValue(actualBase, ptrType.PointeeType);
            baseType = ptrType.PointeeType;
        }

        // Check if the base expression is a struct type
        if (baseType is not IrStructType structType)
        {
            throw new Exception($"Cannot access member '{memberName}' on non-struct type '{baseType.Name}'");
        }

        // Find the field
        var field = structType.GetField(memberName);
        if (field == null)
        {
            throw new Exception($"Struct '{structType.Name}' does not have a field named '{memberName}'");
        }

        // Generate a member access instruction
        var resultName = $"%t{_tempCounter++}";
        var memberAccess = new IrMemberAccess(resultName, actualBase, memberName, field.Type, field.Offset);
        _currentBlock!.AddInstruction(memberAccess);

        return new IrVariable(resultName, field.Type);
    }

    public override object? VisitPathExpr([NotNull] NovusParser.PathExprContext context)
    {
        // Handle path expressions: Type::name
        // This can be:
        // 1. Enum variants: Option::Some, Result::Ok
        // 2. Associated functions (static methods): Vec::new, Vec::with_capacity
        var baseExpr = context.expression();
        var memberName = context.IDENTIFIER().GetText();

        // The base expression should be an identifier for the type
        string? typeName = null;
        if (baseExpr is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            typeName = identCtx.identifier().GetText();
        }

        if (typeName == null)
        {
            throw new Exception($"Path expression must reference a type");
        }

        // Try enum variant first
        if (_enums.ContainsKey(typeName))
        {
            var enumType = _enums[typeName];
            var variant = enumType.GetVariant(memberName);

            if (variant == null)
            {
                throw new Exception($"Enum '{typeName}' has no variant '{memberName}'");
            }

            // For unit variants (no associated data), create the enum value directly
            if (variant.AssociatedData.Count == 0)
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

                return new IrEnumValue(concreteEnumType, memberName, variant.Tag, new List<IrValue>());
            }

            // Return enum constructor for variants with data
            return new IrEnumConstructor(enumType, memberName, variant.Tag);
        }

        // Try associated function (struct method without self parameter)
        var mangledName = $"{typeName}::{memberName}";

        // Check if this is a generic type - look in generic method templates
        if (_structs.ContainsKey(typeName))
        {
            var structType = _structs[typeName];

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
            else
            {
                throw new Exception($"Cannot call method '{memberName}' of type '{typeName}' without an instance (it requires 'self')");
            }
        }

        throw new Exception($"Type '{typeName}' has no associated function or variant '{memberName}'");
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
        var checkLabels = new List<string>();

        for (int i = 0; i < context.matchArm().Length; i++)
        {
            armLabels.Add($"match_arm_{_labelCounter}_{i}");
            checkLabels.Add($"match_check_{_labelCounter}_{i}");
        }
        var matchId = _labelCounter;
        _labelCounter++;

        // Determine if arms produce values and their type
        IrType? matchResultType = null;
        bool armsProduceValues = context.matchArm().Any(arm => arm.expression() != null);
        string? matchResultVarName = null;

        // Extract tag from enum value (before declaring match result, so it appears first)
        var tagName = $"%t{_tempCounter++}";
        _currentBlock!.AddInstruction(new IrExtractTag(tagName, matchValue));
        var tagVar = new IrVariable(tagName, IrIntType.I32);

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
        for (int i = 0; i < context.matchArm().Length; i++)
        {
            var armCtx = context.matchArm()[i];
            var pattern = armCtx.pattern();

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

                // Branch: if match, go to arm, otherwise continue to next check
                var nextLabel = i < checkLabels.Count - 1 ? checkLabels[i + 1] : matchEndLabel;
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
                // Extract the last identifier from the qualified name (e.g., SimpleResult::Ok -> Ok)
                var identifiers = variantPattern.variantName().IDENTIFIER();
                var variantName = identifiers[identifiers.Length - 1].GetText();
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

            // Visit the arm body and capture result if it's an expression
            IrValue? armResult = null;
            if (armCtx.expression() != null)
            {
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
                }
            }
            else if (armCtx.block() != null)
            {
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
                }
            }

            // If we have a result value, result type, and variable name, store it
            if (armResult != null && matchResultType != null && matchResultVarName != null && !CurrentBlockHasTerminator())
            {
                _currentBlock!.AddInstruction(new IrStore(matchResultVarName, armResult));
            }

            // Jump to end (if not already terminated)
            if (!CurrentBlockHasTerminator())
            {
                _currentBlock!.AddInstruction(new IrBranch(matchEndLabel));
                anyArmReachesEnd = true;  // This arm can reach match_end
            }
        }

        // End label
        _currentBlock!.AddInstruction(new IrLabel(matchEndLabel));

        // If no arms can reach match_end (all terminated), emit panic for invalid enum tags
        // This is unreachable in correct programs but provides safety against corrupted memory
        if (!anyArmReachesEnd)
        {
            // Call panic with an error message
            // For now, mark the block as terminated with a return
            // If function returns a value, emit a dummy return to avoid C warnings
            // TODO: Once panic() is implemented, use that instead
            if (_currentFunction?.ReturnType is not null and not IrVoidType)
            {
                // Non-void function: return zero as unreachable fallback
                var returnType = _currentFunction.ReturnType;
                IrValue defaultValue;
                if (returnType is IrIntType intType)
                {
                    defaultValue = new IrConstant(0, intType);
                }
                else
                {
                    // For other types, use zero constant
                    defaultValue = new IrConstant(0, returnType);
                }
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
            // If we're inside a generic method instantiation and have a concrete type, use it
            if (_currentTypeSubstitutions != null && _currentTypeSubstitutions.ContainsKey(typeName))
            {
                return _currentTypeSubstitutions[typeName];
            }
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

                // Create cache key
                var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
                var cacheKey = $"{structType.StructName}<{string.Join(",", typeArgKeys)}>";

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

                // Create monomorphized fields using recursive substitution
                var monomorphizedFields = new List<IrStructField>();
                bool fullyMonomorphized = true;

                foreach (var origField in structType.Fields)
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
                var monomorphizedStruct = new IrStructType(structType.StructName, monomorphizedFields, null, cacheKey);

                // Force calculation of field offsets only if fully monomorphized
                // If still contains generic types, offset calculation will happen later
                if (fullyMonomorphized)
                {
                    _ = monomorphizedStruct.SizeInBytes;
                }

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

                // Create cache key using proper type keys that handle nested generics
                var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
                var cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";

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
                            var substituted = typeSubstitutions[gt.ParameterName];
                            monomorphizedData.Add(substituted);
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

        throw new Exception($"Unknown type '{typeName}'");
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
    /// Recursively substitute generic type parameters with concrete types
    /// </summary>
    private IrType SubstituteGenericTypes(IrType type, Dictionary<string, IrType> substitutions)
    {
        if (type is IrGenericType gt && substitutions.ContainsKey(gt.ParameterName))
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
            // Check if any field types contain generics that need substitution
            bool needsSubstitution = false;
            var substitutedFields = new List<IrStructField>();

            foreach (var field in structType.Fields)
            {
                var substitutedFieldType = SubstituteGenericTypes(field.Type, substitutions);
                substitutedFields.Add(new IrStructField(field.Name, substitutedFieldType));

                if (substitutedFieldType != field.Type)
                {
                    needsSubstitution = true;
                }
            }

            if (needsSubstitution)
            {
                // Create a new struct type with substituted field types
                var substitutedStruct = new IrStructType(structType.StructName, substitutedFields);
                return substitutedStruct;
            }
        }
        else if (type is IrEnumType enumType)
        {
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

                    if (substitutedDataType != dataType)
                    {
                        needsSubstitution = true;
                    }
                }

                substitutedVariants.Add(new IrEnumVariant(variant.Name, variant.Tag, substitutedData));
            }

            if (needsSubstitution)
            {
                // Create a new enum type with substituted variant types
                var substitutedEnum = new IrEnumType(enumType.EnumName, substitutedVariants);
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

    private IrType ParseArrayType(NovusParser.ArrayTypeContext context)
    {
        // Evaluate the size expression as a compile-time constant
        var sizeExpr = context.expression();
        var evaluator = new ConstantExpressionEvaluator(
            _constants.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value),
            errorMsg => {
                // Error handling - will be caught by semantic analyzer
            }
        );

        var sizeValue = evaluator.Visit(sizeExpr);
        if (!sizeValue.HasValue)
        {
            sizeValue = 0; // fallback - error will be reported by semantic analyzer
        }

        var elementType = ParseType(context.type());
        return _typeInterner.GetArrayType(elementType, sizeValue.Value);
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
        throw new Exception($"Cannot parse complex mangled type name '{mangledName}' yet");
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

        throw new Exception($"Cannot get mangled name for type '{type.Name}'");
    }

    /// <summary>
    /// Ensure that a drop() method is instantiated for this type if it exists as a template.
    /// For generic types like Vec<T>, this will instantiate Vec<T>::drop() if it exists.
    /// Returns true if the type has a drop() method (either already instantiated or newly instantiated).
    /// </summary>
    private bool EnsureDropMethodInstantiated(IrType type)
    {
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
            catch (Exception)
            {
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
            throw new Exception($"Cannot generate drop call for type '{type.Name}'");
        }

        var dropMethodName = $"{typeName}_drop";
        var dropMethod = _module.Functions.FirstOrDefault(f => f.Name == dropMethodName);
        if (dropMethod == null)
        {
            throw new Exception($"Drop method '{dropMethodName}' not found");
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
}
