using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// IrBuilder partial class containing import and module processing methods.
/// This file contains methods for importing symbols from other modules.
/// </summary>
public partial class IrBuilder
{
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
        // Parse the module ONCE (not per-symbol)
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
        // This ensures that types used by the symbols we're importing are available
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

        // CRITICAL: Register struct/enum placeholders for the REQUESTED symbols ONLY
        // But do it BEFORE processing any symbols, so that cross-references work correctly.
        // For example, if importing struct A which has a field of type B, and B is in the same module,
        // we need to register placeholder for A first, then when parsing A's fields, the RegisterStruct
        // call will handle creating placeholder for B if needed.
        foreach (var symbolName in symbolNames)
        {
            // Register placeholder for enums
            foreach (var enumDecl in moduleContext.enumDeclaration())
            {
                if (enumDecl.IDENTIFIER().GetText() == symbolName)
                {
                    // RegisterEnum will handle placeholder registration
                    if (!_symbols.HasEnum(symbolName))
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
                        var stubEnum = new IrEnumType(symbolName, new List<IrEnumVariant>(), genericParams);
                        _symbols.RegisterEnum(symbolName, stubEnum);
                    }
                }
            }

            // Register placeholder for structs
            foreach (var structDecl in moduleContext.structDeclaration())
            {
                if (structDecl.IDENTIFIER().GetText() == symbolName)
                {
                    // RegisterStruct will handle placeholder registration and self-referential types
                    if (!_symbols.HasStruct(symbolName))
                    {
                        List<string> genericParams = new List<string>();
                        if (structDecl.genericParams() != null)
                        {
                            foreach (var paramId in structDecl.genericParams().IDENTIFIER())
                            {
                                genericParams.Add(paramId.GetText());
                            }
                        }
                        var placeholderStruct = new IrStructType(symbolName, new List<IrStructField>(), genericParams, null, null);
                        _symbols.RegisterStruct(symbolName, placeholderStruct);
                    }
                }
            }
        }

        // Now process each specific symbol requested
        foreach (var symbolName in symbolNames)
        {
            // Find and register the specific symbol
            // Check enums
            foreach (var enumDecl in moduleContext.enumDeclaration())
            {
                if (enumDecl.IDENTIFIER().GetText() == symbolName)
                {
                    RegisterEnum(enumDecl);
                    goto nextSymbol; // Found it, move to next symbol
                }
            }
            // Check structs
            foreach (var structDecl in moduleContext.structDeclaration())
            {
                if (structDecl.IDENTIFIER().GetText() == symbolName)
                {
                    RegisterStruct(structDecl);
                    goto nextSymbol; // Found it, move to next symbol
                }
            }
            // Check traits
            foreach (var traitDecl in moduleContext.traitDeclaration())
            {
                if (traitDecl.IDENTIFIER().GetText() == symbolName)
                {
                    RegisterTrait(traitDecl);
                    goto nextSymbol; // Found it, move to next symbol
                }
            }
            // Check constants
            foreach (var constDecl in moduleContext.constDeclaration())
            {
                if (constDecl.IDENTIFIER().GetText() == symbolName)
                {
                    RegisterConstant(constDecl);
                    goto nextSymbol; // Found it, move to next symbol
                }
            }

            nextSymbol:
                ; // Continue to next symbol
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
                ? GetLocation(importList)
                : new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.ModuleNotFound,
                $"Module '{moduleNamespace}' not found at {modulePath} or has syntax errors",
                errorLocation
            );
            return;
        }

        // Check if module has already been fully processed (optimization to avoid redundant work)
        bool alreadyProcessed = _processedModules.Contains(moduleNamespace);


        if (alreadyProcessed)
        {
            // Even if module is already processed, we still need to handle imports
            // This allows: from std::ffi::dos import SystemTagList
            //         AND: from std::ffi::dos import IoErr
            // Both imports from the same module

            // CRITICAL: ALWAYS register type stubs first (even for importAll)
            // This is needed for auto-imports from f-strings which use importAll=true
            RegisterAllEnumStubsForImport(moduleContext);
            RegisterAllStructPlaceholdersForImport(moduleContext);

            // For importAll (used by f-string auto-imports), we need to also register impl methods
            // This ensures that StackFormatter::new() and other methods are available
            if (importAll)
            {
                // Process all impl blocks to register their methods
                // This is a simplified version of the impl processing in the main path below
                foreach (var implDecl in moduleContext.implDeclaration())
                {
                    // Handle generic parameters if present (e.g., impl<T> Vec<T>)
                    var genericParams = ParseGenericParameters(implDecl.genericParams(), registerInSymbolTable: true);

                    // Determine if this is a trait impl or inherent impl
                    bool isTraitImpl = implDecl.KW_FOR() != null;
                    string? traitName = null;
                    List<IrType> traitTypeArgs = new();

                    // Extract implementing type name
                    string? typeName;
                    IrType? implementingType;

                    if (isTraitImpl)
                    {
                        traitName = implDecl.traitTypeName.IDENTIFIER(0).GetText();

                        // Parse trait type arguments if present
                        traitTypeArgs = ParseTypeArguments(implDecl.traitTypeArgs);

                        // Parse the impl target type (primitive or named)
                        (typeName, implementingType) = ParseImplTargetType(implDecl.implTargetType(), null, implDecl);
                        if (implementingType == null || typeName == null)
                        {
                            _symbols.ClearGenericParameters();
                            continue;
                        }
                    }
                    else
                    {
                        // Parse the impl target type (inherent impl)
                        (typeName, implementingType) = ParseImplTargetType(null, implDecl.targetTypeName, implDecl);
                        if (implementingType == null || typeName == null)
                        {
                            _symbols.ClearGenericParameters();
                            continue;
                        }
                    }

                    _currentSelfType = implementingType;

                    // Register all pub methods in this impl block
                    foreach (var implItem in implDecl.implItem())
                    {
                        var funcDecl = implItem.functionDeclaration();
                        if (funcDecl == null) continue;

                        var methodName = funcDecl.IDENTIFIER().GetText();
                        var isPub = AstModifierHelper.HasModifier(funcDecl, "pub", 3);

                        // For trait impls, methods are implicitly public
                        if (isTraitImpl)
                        {
                            isPub = true;
                        }

                        // For generic impl blocks, store as templates
                        if (genericParams.Count > 0)
                        {
                            StoreGenericMethodTemplate(typeName!, methodName, genericParams, funcDecl);
                            continue;
                        }

                        // Only import pub methods for non-generic impl blocks
                        if (!isPub)
                        {
                            continue;
                        }

                        // Generate mangled name
                        var mangledName = GenerateMethodMangledName(typeName!, methodName, isTraitImpl, traitName, traitTypeArgs);

                        // Skip if already registered
                        if (_module.Functions.Any(f => f.Name == mangledName))
                        {
                            continue;
                        }

                        // Create function
                        var returnType = ParseReturnType(funcDecl.type());
                        var function = new IrFunction(mangledName, returnType, Visibility.Private, false);

                        // Parse parameters
                        if (funcDecl.parameterList() != null)
                        {
                            var paramList = funcDecl.parameterList();
                            ParseSelfParameter(paramList.selfParameter(), function, typeName!);
                            ParseFunctionParameters(funcDecl, function);
                        }

                        _module.AddFunction(function);
                    }

                    // Clear generic params and Self type
                    _symbols.ClearGenericParameters();
                    _currentSelfType = null;
                }
            }
            else if (importList != null)
            {
                // Build the list of names to import for this specific import statement
                var selectiveImports = ModuleImportHelper.BuildImportNameSet(moduleContext, importAll, importList);

                // CRITICAL: Follow the same order as the "not already processed" path!
                // Type registrations MUST happen before parsing function signatures.

                // Step 1: Fill in enum variants for selective imports
                FillEnumVariantsForImport(moduleContext, selectiveImports);

                // Step 2: Register constants
                RegisterConstantsForImport(moduleContext, selectiveImports);

                // Step 3: Register structs (with dependency expansion)
                // First, expand selective imports to include struct dependencies
                var expandedStructImports = ExpandStructDependencies(moduleContext, selectiveImports);

                // Step 4: Register placeholder structs
                RegisterStructPlaceholdersForImport(moduleContext, expandedStructImports);

                // Step 5: Fill in struct fields
                // At this point, enum stubs are registered so struct fields can reference enums
                FillStructFieldsForImport(moduleContext, expandedStructImports);

                // Step 6: Register traits
                RegisterTraitsForImport(moduleContext, selectiveImports);

                // Step 7: NOW register functions - all type stubs are registered
                // so function signatures can reference any type
                foreach (var funcDecl in moduleContext.functionDeclaration())
                {
                    var funcName = funcDecl.IDENTIFIER().GetText();
                    if (selectiveImports.Contains(funcName))
                    {
                        // Check if not already imported
                        if (!_module.Functions.Any(f => f.Name == funcName))
                        {
                            // Parse and add the function
                            var returnType = ParseReturnType(funcDecl.type());

                            var (visibility, isExtern, _) = AstModifierHelper.ParseModifiers(funcDecl, 4);

                            var function = new IrFunction(funcName, returnType, visibility, isExtern);

                            // Parse parameters
                            ParseFunctionParameters(funcDecl, function);

                            _module.AddFunction(function);
                        }
                    }
                }

                // Register impl blocks for ALL types (not just selective imports)
                // This is critical: when you import a type, you also need its methods!
                // For example: "from std::graphics::menus import GadToolsMenuBuilder"
                // should also import methods on MenuHandle, MenuItemHandle, etc.
                foreach (var implDecl in moduleContext.implDeclaration())
                {
                    // Handle generic parameters if present (e.g., impl<T> Vec<T>)
                    var genericParams = ParseGenericParameters(implDecl.genericParams(), registerInSymbolTable: true);

                    // Determine if this is a trait impl or inherent impl
                    bool isTraitImpl = implDecl.KW_FOR() != null;
                    string? traitName = null;
                    List<IrType> traitTypeArgs = new();

                    // Extract implementing type name
                    string? typeName;
                    IrType? implementingType;

                    if (isTraitImpl)
                    {
                        traitName = implDecl.traitTypeName.IDENTIFIER(0).GetText();

                        // Parse trait type arguments if present
                        traitTypeArgs = ParseTypeArguments(implDecl.traitTypeArgs);

                        // Parse the impl target type (primitive or named)
                        (typeName, implementingType) = ParseImplTargetType(implDecl.implTargetType(), null, implDecl);
                        if (implementingType == null || typeName == null)
                        {
                            _symbols.ClearGenericParameters();
                            continue;
                        }
                    }
                    else
                    {
                        // Parse the impl target type (inherent impl)
                        (typeName, implementingType) = ParseImplTargetType(null, implDecl.targetTypeName, implDecl);
                        if (implementingType == null || typeName == null)
                        {
                            _symbols.ClearGenericParameters();
                            continue;
                        }
                    }

                    _currentSelfType = implementingType;

                    // Register all pub methods in this impl block
                    foreach (var implItem in implDecl.implItem())
                    {
                        var funcDecl = implItem.functionDeclaration();
                        if (funcDecl == null) continue;

                        var methodName = funcDecl.IDENTIFIER().GetText();
                        var isPub = AstModifierHelper.HasModifier(funcDecl, "pub", 3);

                        // For trait impls, methods are implicitly public
                        if (isTraitImpl)
                        {
                            isPub = true;
                        }

                        // For generic impl blocks, store as templates
                        if (genericParams.Count > 0)
                        {
                            StoreGenericMethodTemplate(typeName!, methodName, genericParams, funcDecl);
                            continue;
                        }

                        // Only import pub methods for non-generic impl blocks
                        if (!isPub)
                        {
                            continue;
                        }

                        // Generate mangled name
                        var mangledName = GenerateMethodMangledName(typeName!, methodName, isTraitImpl, traitName, traitTypeArgs);

                        // Skip if already registered
                        if (_module.Functions.Any(f => f.Name == mangledName))
                        {
                            continue;
                        }

                        // Create function
                        var returnType = ParseReturnType(funcDecl.type());
                        var function = new IrFunction(mangledName, returnType, Visibility.Private, false);

                        // Parse parameters
                        if (funcDecl.parameterList() != null)
                        {
                            var paramList = funcDecl.parameterList();
                            ParseSelfParameter(paramList.selfParameter(), function, typeName!);
                            ParseFunctionParameters(funcDecl, function);
                        }

                        _module.AddFunction(function);
                    }

                    // Clear generic params and Self type
                    _symbols.ClearGenericParameters();
                    _currentSelfType = null;
                }
            }

            return; // Don't reprocess the entire module
        }

        // Check for circular imports before processing a new module
        // Use the absolute file path for reliable cycle detection
        if (!_circularImportDetector.EnterModule(modulePath))
        {
            // Circular dependency detected - error already reported by detector
            return;
        }

        // Mark this module as being processed (for efficiency - avoid redundant re-processing)
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

        // Process the module's imports to make constants available for generic templates
        // CircularImportDetector tracks the import chain and will catch any cycles
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
            string? typeName;
            IrType? implementingType;

            if (isTraitImpl)
            {
                // Format: impl [<GenericParams>] TraitName<TraitArgs> for TargetType
                // traitTypeName is the trait being implemented
                traitName = implDecl.traitTypeName.IDENTIFIER(0).GetText();

                // Parse trait type arguments if present (e.g., From<DosError>)
                traitTypeArgs = ParseTypeArguments(implDecl.traitTypeArgs);

                // Parse the impl target type (primitive or named)
                (typeName, implementingType) = ParseImplTargetType(implDecl.implTargetType(), null, implDecl);
            }
            else
            {
                // Format: impl [<GenericParams>] TargetType
                // Parse the impl target type (inherent impl)
                (typeName, implementingType) = ParseImplTargetType(null, implDecl.targetTypeName, implDecl);
            }

            // IMPORTANT: We do NOT skip impl blocks based on whether the type is in namesToImport
            // Even if a type isn't explicitly imported, it may still be used (returned from methods,
            // passed as parameters, etc.). For example:
            //   from std::graphics::menus import GadToolsMenuBuilder
            //   let menu = builder.add_menu("File")  // returns MenuHandle (not explicitly imported)
            //   menu.add_item("New")  // need MenuHandle::add_item method!
            // So we import ALL pub methods from ALL impl blocks in the module.

            // Skip if implementing type not found (type not imported or not registered yet)
            if (implementingType == null || typeName == null)
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
                    StoreGenericMethodTemplate(typeName!, methodName, genericParams, funcDecl);
                    // Don't create function yet - it will be instantiated when called with concrete types
                    continue;
                }

                // Only import pub methods for non-generic impl blocks
                if (!isPub)
                {
                    continue;
                }

                // For non-generic impl blocks, create the function normally
                var returnType = ParseReturnType(funcDecl.type());

                // Methods are registered with mangled names
                var mangledName = GenerateMethodMangledName(typeName!, methodName, isTraitImpl, traitName, traitTypeArgs);
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
                var traitImpl = new IrTraitImpl(fullTraitName, traitTypeArgs, typeName!, implementingType, genericParams);
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
                var returnType = ParseReturnType(funcDecl.type());
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

        // Exit the module from the import chain now that processing is complete
        _circularImportDetector.ExitModule();
    }

    /// <summary>
    /// Instantiate a generic method for a monomorphized struct type
    /// E.g., instantiate Vec<T>::push as Vec<i32>::push
    /// For trait impls, pass isTraitImpl=true and traitName (e.g., "Drop")
    /// </summary>
    private IrFunction? InstantiateGenericMethod(IrStructType monomorphizedStruct, string methodName, bool isTraitImpl = false, string? traitName = null, List<IrType>? traitTypeArgs = null)
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

        // Build instantiation key (e.g., "Vec<i32>::push" or "Vec<i32>::Drop::drop" for trait impls)
        var instantiationKey = isTraitImpl && traitName != null
            ? $"{monomorphizedStruct.CacheKey}::{traitName}::{methodName}"
            : $"{monomorphizedStruct.CacheKey}::{methodName}";

        // For mangling, use monomorphized type name (e.g., "Vec<i32>")
        var mangledTypeName = monomorphizedStruct.CacheKey ?? baseTypeName;

        // Check if already instantiated
        if (_instantiatedMethods.Contains(instantiationKey))
        {
            // Already generated, look it up using correct mangling convention
            var mangledName = GenerateMethodMangledName(
                mangledTypeName,
                methodName,
                isTraitImpl,
                traitName,
                traitTypeArgs ?? new List<IrType>()
            );
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

        // First, try to build substitutions directly from TypeArguments if available
        // This is the most reliable method when the monomorphized struct has its type arguments set correctly
        if (monomorphizedStruct.TypeArguments != null &&
            monomorphizedStruct.TypeArguments.Count == baseStruct.GenericParameters.Count)
        {
            for (int i = 0; i < baseStruct.GenericParameters.Count; i++)
            {
                typeSubstitutions[baseStruct.GenericParameters[i]] = monomorphizedStruct.TypeArguments[i];
            }
        }
        // Second, try to parse type arguments from the cache key (e.g., "HashMap<u32,u32>")
        else if (monomorphizedStruct.CacheKey != null && monomorphizedStruct.CacheKey.Contains("<"))
        {
            // Parse the cache key to extract type argument names
            // Format: TypeName<Type1,Type2,...>
            var openBracket = monomorphizedStruct.CacheKey.IndexOf('<');
            var closeBracket = monomorphizedStruct.CacheKey.LastIndexOf('>');
            if (openBracket >= 0 && closeBracket > openBracket)
            {
                var typeArgsStr = monomorphizedStruct.CacheKey.Substring(openBracket + 1, closeBracket - openBracket - 1);
                var typeArgNames = typeArgsStr.Split(',');

                if (typeArgNames.Length == baseStruct.GenericParameters.Count)
                {
                    for (int i = 0; i < baseStruct.GenericParameters.Count; i++)
                    {
                        var typeArgName = typeArgNames[i].Trim();
                        // Look up the type by name - use simple name-based lookup for primitive types
                        IrType? concreteType = typeArgName switch
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
                            "void" => IrVoidType.Instance,
                            _ => null
                        };

                        if (concreteType != null)
                        {
                            typeSubstitutions[baseStruct.GenericParameters[i]] = concreteType;
                        }
                    }
                }
            }
        }

        // Fallback: scan fields to extract generic type mappings if we still don't have all substitutions
        if (typeSubstitutions.Count < baseStruct.GenericParameters.Count)
        {
            for (int i = 0; i < baseStruct.Fields.Count && i < monomorphizedStruct.Fields.Count; i++)
            {
                var baseFieldType = baseStruct.Fields[i].Type;
                var monomorphizedFieldType = monomorphizedStruct.Fields[i].Type;

                // Recursively extract generic type mappings from field types
                ExtractGenericTypeMapping(baseFieldType, monomorphizedFieldType, typeSubstitutions);
            }
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
        var returnType = ParseReturnType(funcDecl.type());

        // Substitute generic types in return type
        returnType = _typeParser.SubstituteGenericTypes(returnType!, typeSubstitutions);

        // Generate correct mangled name for trait impls vs inherent methods (using mangledTypeName from above)
        var mangledMethodName = GenerateMethodMangledName(
            mangledTypeName,
            methodName,
            isTraitImpl,
            traitName,
            traitTypeArgs ?? new List<IrType>()
        );
        var function = new IrFunction(mangledMethodName, returnType!, Visibility.Private, false);

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
                paramType = _typeParser.SubstituteGenericTypes(paramType, typeSubstitutions);

                function.Parameters.Add(new IrParameter(paramName, paramType));
            }

            // Add variadic parameter if present
            ParseVariadicParameter(paramList, function);
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
            ParseVariadicParameter(paramList, templateParams);
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
        var returnType = ParseReturnType(funcDecl.type());
        returnType = _typeParser.SubstituteGenericTypes(returnType, typeSubstitutions);

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
                paramType = _typeParser.SubstituteGenericTypes(paramType, typeSubstitutions);
                function.Parameters.Add(new IrParameter(paramName, paramType));
            }

            // Add variadic parameter if present
            ParseVariadicParameter(paramList, function);
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

    // NOTE: MonomorphizeEnum and SubstituteType have been removed and consolidated into TypeParser.
    // Use _typeParser.SubstituteGenericTypes() instead, which provides:
    // - Full recursive type substitution for all type kinds (pointers, references, arrays, structs, enums)
    // - Proper monomorphization with cache registration and finalization
    // - Cycle detection for self-referential types

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
        var returnType = ParseReturnType(funcDecl.type());
        returnType = _typeParser.SubstituteGenericTypes(returnType, typeSubstitutions);

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
                paramType = _typeParser.SubstituteGenericTypes(paramType, typeSubstitutions);

                function.Parameters.Add(new IrParameter(paramName, paramType));
            }

            // Add variadic parameter if present
            ParseVariadicParameter(paramList, function);
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
                    var genericParams = ParseGenericParameters(enumDecl.genericParams());
                    List<string>? genericParamsOrNull = genericParams.Count > 0 ? genericParams : null;
                    var stubEnum = new IrEnumType(enumName, new List<IrEnumVariant>(), genericParamsOrNull);
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

            // Parse generic parameters for placeholder so type checking works correctly
            List<string> genericParams = new List<string>();
            if (structDecl.genericParams() != null)
            {
                foreach (var paramId in structDecl.genericParams().IDENTIFIER())
                {
                    genericParams.Add(paramId.GetText());
                }
            }

            // Register placeholder struct in symbol table (but NOT in module.Structs yet)
            // The struct will be filled in later only if it's in the import list
            var placeholderStruct = new IrStructType(structName, new List<IrStructField>(), genericParams, null, null);
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
                    // Parse generic parameters for placeholder so type checking works correctly
                    List<string> genericParams = new List<string>();
                    if (structDecl.genericParams() != null)
                    {
                        foreach (var paramId in structDecl.genericParams().IDENTIFIER())
                        {
                            genericParams.Add(paramId.GetText());
                        }
                    }

                    var placeholderStruct = new IrStructType(structName, new List<IrStructField>(), genericParams, null, null);
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
        ParseVariadicParameter(paramList, function);
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
                var errorLocation = GetLocation(funcDecl);
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
            var returnType = ParseReturnType(funcDecl.type());
            // Only mark as extern if it's truly an extern function (FFI)
            // Pub functions from Novus modules are real implementations that need linking
            // CRITICAL: Preserve visibility when importing - pub functions must stay pub!
            var visibility = isPub ? Visibility.Public : Visibility.Private;
            var function = new IrFunction(funcName, returnType, visibility, isExtern);

            // Parse parameters
            ParseFunctionParameters(funcDecl, function);

            _module.AddFunction(function);
        }
    }
}
