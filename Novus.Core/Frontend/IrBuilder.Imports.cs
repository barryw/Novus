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
        var (moduleContext, syntaxErrors) = ModuleImportHelper.ParseModuleFile(modulePath, _preprocessorConstants);

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
                        var genericParams = AstParsingHelpers.ParseGenericParameters(enumDecl.genericParams());
                        var stubEnum = new IrEnumType(symbolName, new List<IrEnumVariant>(), genericParams.Count > 0 ? genericParams : null);
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
                        var genericParams = AstParsingHelpers.ParseGenericParameters(structDecl.genericParams());
                        var placeholderStruct = new IrStructType(symbolName, new List<IrStructField>(), genericParams.Count > 0 ? genericParams : null, null, null);
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

            // Check global variables (extern var)
            foreach (var globalVarDecl in moduleContext.globalVariableDeclaration())
            {
                if (globalVarDecl.IDENTIFIER().GetText() == symbolName)
                {
                    RegisterExternalVariable(globalVarDecl);
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
        var (moduleContext, syntaxErrors) = ModuleImportHelper.ParseModuleFile(modulePath, _preprocessorConstants);

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
                        var returnType = ParseReturnType(funcDecl.type()) ?? IrVoidType.Instance;

                        // Substitute Self type in return type (e.g., Option<Self> -> Option<Point>)
                        returnType = _typeParser.SubstituteGenericTypes(returnType, new Dictionary<string, IrType>());

                        var function = new IrFunction(mangledName, returnType, Visibility.Private, false);

                        // Parse and store function attributes (for #[chain], @test, @export, etc.)
                        var methodAttributes = ProcessAndFilterModuleAttributes(funcDecl.attribute());
                        function.Attributes = methodAttributes;

                        // Parse parameters
                        if (funcDecl.parameterList() != null)
                        {
                            var paramList = funcDecl.parameterList();
                            ParseSelfParameter(paramList.selfParameter(), function, typeName!);
                            ParseFunctionParameters(funcDecl, function);
                        }

                        // Handle #[chain] attribute - set return type to self's pointer type
                        if (methodAttributes.Has(SemanticAnalysis.KnownAttributes.Chain) && returnType is IrVoidType)
                        {
                            var selfParam = function.Parameters.FirstOrDefault(p => p.Name == "self");
                            if (selfParam != null)
                            {
                                function.ReturnType = selfParam.Type;
                            }
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

                // Step 2.5: Register global variables (extern var)
                RegisterGlobalVariablesForImport(moduleContext, selectiveImports);

                // Step 3: Register structs (with dependency expansion)
                // CRITICAL FIX: Before expanding struct dependencies, we need to scan function signatures
                // for struct dependencies. When importing a function like `file_info` which returns
                // `Result<FileInfo, DosError>`, the FileInfo struct needs to have its fields filled in.
                // First, extract struct dependencies from function signatures
                var structDepsFromFunctions = ExtractStructDependenciesFromFunctions(moduleContext, selectiveImports);

                // Merge function signature dependencies into selective imports
                foreach (var dep in structDepsFromFunctions)
                {
                    selectiveImports.Add(dep);
                }

                // Also extract struct dependencies from impl methods (they may return structs)
                var structDepsFromImpls = ExtractStructDependenciesFromImplMethods(moduleContext);
                foreach (var dep in structDepsFromImpls)
                {
                    selectiveImports.Add(dep);
                }

                // Now expand selective imports to include struct field dependencies
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
                    var baseFuncName = funcDecl.IDENTIFIER().GetText();
                    if (selectiveImports.Contains(baseFuncName))
                    {
                        // Check if this is a generic function - skip for now, they're handled as templates
                        // Generic functions have type parameters like T that can't be parsed without context
                        var genericParams = AstParsingHelpers.ParseGenericParameters(funcDecl.genericParams(), _symbols, registerInSymbolTable: false);
                        if (genericParams.Count > 0)
                        {
                            // For generic functions, just register the template (they'll be instantiated on use)
                            // Note: We can't compute mangled names for generics since param types contain T
                            continue;
                        }

                        // Parse and add the function
                        var returnType = ParseReturnType(funcDecl.type());

                        var (visibility, isExtern, _, isConstFn) = AstModifierHelper.ParseModifiers(funcDecl, 5);

                        // Parse parameters first to compute mangled name for overloaded functions
                        var parameters = new List<IrParameter>();
                        if (funcDecl.parameterList() != null)
                        {
                            ParseRegularParameters(funcDecl.parameterList(), parameters);
                        }

                        // Compute mangled name if this function is overloaded
                        var paramTypes = parameters.Select(p => p.Type).ToList();
                        var mangledName = GetMangledFunctionName(baseFuncName, paramTypes);

                        // Check if not already imported (use mangled name for uniqueness)
                        if (!_module.Functions.Any(f => f.Name == mangledName))
                        {
                            var function = new IrFunction(mangledName, returnType, visibility, isExtern);
                            function.IsConstFn = isConstFn;

                            // Store original name if mangled
                            if (mangledName != baseFuncName)
                            {
                                function.OriginalName = baseFuncName;
                            }

                            // Parse and store function attributes (for @library, etc.)
                            // This is CRITICAL for FFI functions that use @library("bsdsocket.library") etc.
                            var attributes = ProcessAndFilterModuleAttributes(funcDecl.attribute());
                            function.Attributes = attributes;

                            // Add already-parsed parameters
                            function.Parameters.AddRange(parameters);

                            // Add variadic parameter if present
                            if (funcDecl.parameterList()?.variadicParameter() != null)
                            {
                                ParseVariadicParameter(funcDecl.parameterList(), function);
                            }

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
                        var returnType = ParseReturnType(funcDecl.type()) ?? IrVoidType.Instance;

                        // Substitute Self type in return type (e.g., Option<Self> -> Option<Point>)
                        returnType = _typeParser.SubstituteGenericTypes(returnType, new Dictionary<string, IrType>());

                        var function = new IrFunction(mangledName, returnType, Visibility.Private, false);

                        // Parse and store function attributes (for #[chain], @test, @export, etc.)
                        var methodAttributes = ProcessAndFilterModuleAttributes(funcDecl.attribute());
                        function.Attributes = methodAttributes;

                        // Parse parameters
                        if (funcDecl.parameterList() != null)
                        {
                            var paramList = funcDecl.parameterList();
                            ParseSelfParameter(paramList.selfParameter(), function, typeName!);
                            ParseFunctionParameters(funcDecl, function);
                        }

                        // Handle #[chain] attribute - set return type to self's pointer type
                        if (methodAttributes.Has(SemanticAnalysis.KnownAttributes.Chain) && returnType is IrVoidType)
                        {
                            var selfParam = function.Parameters.FirstOrDefault(p => p.Name == "self");
                            if (selfParam != null)
                            {
                                function.ReturnType = selfParam.Type;
                            }
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

        // CRITICAL PHASE 2: Fill in type details

        // Step 2a: Fill in enum variants for ALL enums (not just imported ones)
        // This is needed because imported structs may reference non-imported enums
        FillEnumVariantsForImport(moduleContext, namesToImport);

        // Step 2b: Register imported constants
        RegisterConstantsForImport(moduleContext, namesToImport);

        // Step 2b2: Register imported global variables (extern var)
        RegisterGlobalVariablesForImport(moduleContext, namesToImport);

        // Step 2c: Expand struct import list to include dependencies and fill in fields
        // CRITICAL FIX: Before expanding struct dependencies, scan function signatures for struct dependencies
        var funcStructDeps = ExtractStructDependenciesFromFunctions(moduleContext, namesToImport);
        foreach (var dep in funcStructDeps)
        {
            namesToImport.Add(dep);
        }

        // Also extract struct dependencies from impl methods
        var implStructDeps = ExtractStructDependenciesFromImplMethods(moduleContext);
        foreach (var dep in implStructDeps)
        {
            namesToImport.Add(dep);
        }

        // Now expand struct dependencies to include field dependencies
        // When importing NewScreen, we also need to import TextAttr and BitMap that it references
        var expandedStructNames = ExpandStructDependencies(moduleContext, namesToImport);

        // Fill in struct fields for expanded struct list
        // At this point, all type names (enums + structs) are resolvable for field type parsing
        FillStructFieldsForImport(moduleContext, expandedStructNames);

        // Register imported traits in the module
        RegisterTraitsForImport(moduleContext, namesToImport);

        // Register imported functions in the module
        RegisterFunctionsForImport(moduleContext, namesToImport, moduleNamespace, modulePath);

        // Register imported impl block methods in the module
        foreach (var implDecl in moduleContext.implDeclaration())
        {
            // Handle generic parameters if present (e.g., impl<T> Vec<T>)
            var genericParams = AstParsingHelpers.ParseGenericParameters(implDecl.genericParams(), _symbols, registerInSymbolTable: true);

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
                var returnType = ParseReturnType(funcDecl.type()) ?? IrVoidType.Instance;

                // Substitute Self type in return type (e.g., Option<Self> -> Option<Point>)
                returnType = _typeParser.SubstituteGenericTypes(returnType, new Dictionary<string, IrType>());

                // Methods are registered with mangled names
                var mangledName = GenerateMethodMangledName(typeName!, methodName, isTraitImpl, traitName, traitTypeArgs);
                var function = new IrFunction(mangledName, returnType, Visibility.Private, false);

                // Parse and store function attributes (for #[chain], @test, @export, etc.)
                var methodAttributes = ProcessAndFilterModuleAttributes(funcDecl.attribute());
                function.Attributes = methodAttributes;

                // Parse parameters (including self)
                if (funcDecl.parameterList() != null)
                {
                    var paramList = funcDecl.parameterList();

                    // Handle self parameter if present
                    ParseSelfParameter(paramList.selfParameter(), function, typeName);

                    // Add regular and variadic parameters
                    ParseFunctionParameters(funcDecl, function);
                }

                // Handle #[chain] attribute - set return type to self's pointer type
                if (methodAttributes.Has(SemanticAnalysis.KnownAttributes.Chain) && returnType is IrVoidType)
                {
                    var selfParam = function.Parameters.FirstOrDefault(p => p.Name == "self");
                    if (selfParam != null)
                    {
                        function.ReturnType = selfParam.Type;
                    }
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

                // Create IrTraitImpl and add to module using AddTraitImpl to maintain indices
                // For generic impls, this is a template that will be instantiated later
                var traitImpl = new IrTraitImpl(fullTraitName, traitTypeArgs, typeName!, implementingType, genericParams);
                _module.AddTraitImpl(traitImpl);
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

                // Parse and store function attributes (for @library, etc.)
                // This is CRITICAL for FFI functions that use @library("bsdsocket.library") etc.
                var attributes = ProcessAndFilterModuleAttributes(funcDecl.attribute());
                function.Attributes = attributes;

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


        // Use RAII scope for constants management (exception-safe)
        using var constantsScope = new ConstantsScope(this, templateConstants);

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
            return _module.GetFunction(mangledName);
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

        // Use RAII scopes for state management (exception-safe)
        using var genericParamsScope = new GenericParametersScope(this, genericParams);
        using var typeSubstitutionScope = new TypeSubstitutionScope(this, typeSubstitutions);
        using var selfTypeScope = new SelfTypeScope(this, monomorphizedStruct);

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

        // Parse and store function attributes (for #[chain], @test, @export, etc.)
        var methodAttributes = ProcessAndFilterModuleAttributes(funcDecl.attribute());
        function.Attributes = methodAttributes;

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

        // Handle #[chain] attribute - set return type to self's pointer type
        if (methodAttributes.Has(SemanticAnalysis.KnownAttributes.Chain) && returnType is IrVoidType)
        {
            var selfParam = function.Parameters.FirstOrDefault(p => p.Name == "self");
            if (selfParam != null)
            {
                function.ReturnType = selfParam.Type;
            }
        }

        _module.AddFunction(function);

        // Use RAII scope for function body state (exception-safe)
        using (var functionBodyScope = new FunctionBodyScope(this, function))
        {
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
        } // FunctionBodyScope, SelfTypeScope, TypeSubstitutionScope, GenericParametersScope, ConstantsScope all disposed here

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

        // Use RAII scope for generic parameters (exception-safe)
        using var genericParamsScope = new GenericParametersScope(this, genericParams);

        // Infer type substitutions from arguments
        // First, parse the template to get parameter types
        var templateParams = new List<IrParameter>();
        if (funcDecl.parameterList() != null)
        {
            var paramList = funcDecl.parameterList();
            foreach (var paramCtx in paramList.parameter())
            {
                var paramName = paramCtx.IDENTIFIER().GetText();
                // Use temporary type substitution scope to parse without substitutions
                using (var tempSubScope = new TypeSubstitutionScope(this, null))
                {
                    var paramType = ParseType(paramCtx.type());
                    templateParams.Add(new IrParameter(paramName, paramType));
                }
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
            return _module.GetFunction(cachedMangledName);
        }

        // Use RAII scopes for state management (exception-safe)
        using var constantsScope = new ConstantsScope(this, templateConstants);
        using var typeSubstitutionScope = new TypeSubstitutionScope(this, typeSubstitutions);
        using var selfTypeScope = new SelfTypeScope(this, enumType);

        // Create the function manually (don't use Visit)
        var returnType = ParseReturnType(funcDecl.type());
        returnType = _typeParser.SubstituteGenericTypes(returnType, typeSubstitutions);

        // Create mangled name from type arguments
        var typeArgKeys = genericParams.Select(p => GetTypeCacheKey(typeSubstitutions[p]));
        var mangledName = $"{baseTypeName}::{methodName}_{string.Join("_", typeArgKeys.Select(k => k.Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace("*", "ptr_")))}";

        var function = new IrFunction(mangledName, returnType, Visibility.Private, false);

        // Parse and store function attributes (for #[chain], @test, @export, etc.)
        var methodAttributes = ProcessAndFilterModuleAttributes(funcDecl.attribute());
        function.Attributes = methodAttributes;

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

        // Handle #[chain] attribute - set return type to self's pointer type
        if (methodAttributes.Has(SemanticAnalysis.KnownAttributes.Chain) && returnType is IrVoidType)
        {
            var selfParam = function.Parameters.FirstOrDefault(p => p.Name == "self");
            if (selfParam != null)
            {
                function.ReturnType = selfParam.Type;
            }
        }

        // Check if function already exists in module (could be from import or previous instantiation)
        var existingFunc = _module.GetFunction(mangledName);
        if (existingFunc != null)
        {
            // Already exists, return it
            return existingFunc;
        }

        _module.AddFunction(function);

        // Use RAII scope for function body state (exception-safe)
        using (var functionBodyScope = new FunctionBodyScope(this, function))
        {
            var entryBlock = new IrBasicBlock("entry");
            function.BasicBlocks.Add(entryBlock);
            _currentBlock = entryBlock;

            // Add parameters to local variables scope
            foreach (var param in function.Parameters)
            {
                _localVariables[param.Name] = new IrLocalVariable(param.Name, param.Type, false);
            }

            // Visit the function body
            if (funcDecl.block() != null)
            {
                Visit(funcDecl.block());
            }
        } // All scopes disposed here (FunctionBodyScope, SelfTypeScope, TypeSubstitutionScope, ConstantsScope, GenericParametersScope)

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
        // Delegate to the generic instantiator which has access to the correct template cache
        return _genericInstantiator.InferEnumGenericTypes(baseEnum, methodName, arguments, expectedReturnType);
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
            return _module.GetFunction(existingMangledName);
        }

        // Use RAII scopes for state management (exception-safe)
        using var constantsScope = new ConstantsScope(this, templateConstants);
        using var genericParamsScope = new GenericParametersScope(this, genericParams);
        using var typeSubstitutionScope = new TypeSubstitutionScope(this, typeSubstitutions);

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

        // Use RAII scope for function body state (exception-safe)
        using (var functionBodyScope = new FunctionBodyScope(this, function))
        {
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
        } // All scopes disposed here (FunctionBodyScope, TypeSubstitutionScope, GenericParametersScope, ConstantsScope)

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
            var genericParams = AstParsingHelpers.ParseGenericParameters(enumDecl.genericParams());

            // Register the stub enum in symbol table (but NOT in module.Enums yet)
            // The stub will be filled in later only if it's in the import list
            var stubEnum = new IrEnumType(enumName, new List<IrEnumVariant>(), genericParams.Count > 0 ? genericParams : null);
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
        // IMPORTANT: Fill variants for ALL enums in the module (not just imported ones) because:
        // - Imported structs may have fields of non-imported enum types (e.g., HashMapEntry.state: EntryState)
        // - Match expressions on those fields need access to the enum's variants
        // - If we only fill imported enums, non-imported enum stubs remain with 0 variants
        foreach (var enumDecl in moduleContext.enumDeclaration())
        {
            var enumName = enumDecl.IDENTIFIER().GetText();
            var existingEnum = _symbols.LookupEnum(enumName);
            if (existingEnum != null && existingEnum.Variants is [])
            {
                RegisterEnum(enumDecl);
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
    /// Extract struct dependencies from function signatures (return types and parameters).
    /// This ensures that when importing a function like `file_info() -> Result<FileInfo, DosError>`,
    /// we also import the FileInfo struct and fill in its fields.
    /// </summary>
    private HashSet<string> ExtractStructDependenciesFromFunctions(
        NovusParser.CompilationUnitContext moduleContext,
        HashSet<string> functionsToImport)
    {
        var structDeps = new HashSet<string>();

        foreach (var funcDecl in moduleContext.functionDeclaration())
        {
            var funcName = funcDecl.IDENTIFIER().GetText();

            // Only scan functions that are being imported
            if (!functionsToImport.Contains(funcName))
            {
                continue;
            }

            // Extract dependencies from return type
            if (funcDecl.type() != null)
            {
                var returnTypeDeps = ExtractTypeNameDependencies(funcDecl.type());
                foreach (var dep in returnTypeDeps)
                {
                    structDeps.Add(dep);
                }
            }

            // Extract dependencies from parameters
            if (funcDecl.parameterList() != null)
            {
                foreach (var paramCtx in funcDecl.parameterList().parameter())
                {
                    var paramTypeDeps = ExtractTypeNameDependencies(paramCtx.type());
                    foreach (var dep in paramTypeDeps)
                    {
                        structDeps.Add(dep);
                    }
                }
            }
        }

        return structDeps;
    }

    /// <summary>
    /// Extract struct dependencies from impl method signatures (return types and parameters).
    /// This ensures that when importing a type, all structs referenced by its methods are also imported.
    /// </summary>
    private HashSet<string> ExtractStructDependenciesFromImplMethods(NovusParser.CompilationUnitContext moduleContext)
    {
        var structDeps = new HashSet<string>();

        foreach (var implDecl in moduleContext.implDeclaration())
        {
            foreach (var implItem in implDecl.implItem())
            {
                var funcDecl = implItem.functionDeclaration();
                if (funcDecl == null) continue;

                // Only scan pub methods (we only import pub methods)
                var isPub = AstModifierHelper.HasModifier(funcDecl, "pub", 3);

                // For trait impls, methods are implicitly public
                bool isTraitImpl = implDecl.KW_FOR() != null;
                if (!isPub && !isTraitImpl)
                {
                    continue;
                }

                // Extract dependencies from return type
                if (funcDecl.type() != null)
                {
                    var returnTypeDeps = ExtractTypeNameDependencies(funcDecl.type());
                    foreach (var dep in returnTypeDeps)
                    {
                        structDeps.Add(dep);
                    }
                }

                // Extract dependencies from parameters
                if (funcDecl.parameterList() != null)
                {
                    foreach (var paramCtx in funcDecl.parameterList().parameter())
                    {
                        var paramTypeDeps = ExtractTypeNameDependencies(paramCtx.type());
                        foreach (var dep in paramTypeDeps)
                        {
                            structDeps.Add(dep);
                        }
                    }
                }
            }
        }

        return structDeps;
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
            var genericParams = AstParsingHelpers.ParseGenericParameters(structDecl.genericParams());

            // Register placeholder struct in symbol table (but NOT in module.Structs yet)
            // The struct will be filled in later only if it's in the import list
            var placeholderStruct = new IrStructType(structName, new List<IrStructField>(), genericParams.Count > 0 ? genericParams : null, null, null);
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
                    var genericParams = AstParsingHelpers.ParseGenericParameters(structDecl.genericParams());
                    var placeholderStruct = new IrStructType(structName, new List<IrStructField>(), genericParams.Count > 0 ? genericParams : null, null, null);
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
                if (existingStruct != null && existingStruct.Fields is [])
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
                    // Trait not in symbol table, register it (adds to both _symbols and _module)
                    RegisterTrait(traitDecl);
                }
                else
                {
                    // Trait is already in symbol table but might not be in the module.
                    // This happens when the same trait is imported by multiple modules in the
                    // import chain. We need to ensure the trait is also in the module so that
                    // FindTraitMethod can find it via _module.GetTrait().
                    //
                    // Example: User imports SystemCPU from std::hardware::chipset which imports
                    // Display from std::strings::format and has impl Display for SystemCPU.
                    // When processing the impl, FindTraitMethod needs to find Display trait.
                    var existingTrait = _symbols.LookupTrait(traitName);
                    if (existingTrait != null && _module.GetTrait(traitName) == null)
                    {
                        _module.AddTrait(existingTrait);
                    }
                }
            }
        }
    }

    private void RegisterGlobalVariablesForImport(NovusParser.CompilationUnitContext moduleContext, HashSet<string> namesToImport)
    {
        foreach (var globalVarDecl in moduleContext.globalVariableDeclaration())
        {
            var varName = globalVarDecl.IDENTIFIER().GetText();
            if (namesToImport.Contains(varName))
            {
                // Check if already registered in module's ExternalVariables
                if (!_module.ExternalVariables.Any(ev => ev.Name == varName))
                {
                    RegisterExternalVariable(globalVarDecl);
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
    private void RegisterFunctionsForImport(NovusParser.CompilationUnitContext moduleContext, HashSet<string> namesToImport, string moduleNamespace, string modulePath)
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

            // Check if this is a generic function - must parse generic params BEFORE return type
            // because the return type may reference generic parameters (e.g., fn channel<T>() -> Result<(Sender<T>, Receiver<T>), E>)
            var genericParams = ParseGenericParameters(funcDecl.genericParams(), registerInSymbolTable: true);

            if (genericParams.Count > 0)
            {
                // This is a generic function - register as template for later instantiation
                var templateConstants = GetConstantsAsTuples();
                // Parse where clause for constraint checking during monomorphization
                var whereClause = AstParsingHelpers.ParseWhereClause(funcDecl.whereClause());
                // Store the source module path so dependencies can be resolved during instantiation
                var template = new Generics.GenericTemplate(genericParams, funcDecl, templateConstants, whereClause, MethodGenericParams: null, SourceModulePath: modulePath);
                _genericInstantiator.RegisterFunctionTemplate(funcName, template);

                // Clear generic params from symbol table
                _symbols.ClearGenericParameters();
                continue; // Don't add to _module.Functions yet
            }

            // Parse function signature
            var returnType = ParseReturnType(funcDecl.type());
            // Only mark as extern if it's truly an extern function (FFI)
            // Pub functions from Novus modules are real implementations that need linking
            // CRITICAL: Preserve visibility when importing - pub functions must stay pub!
            var visibility = isPub ? Visibility.Public : Visibility.Private;

            // Parse parameters first to compute mangled name for overloaded functions
            var parameters = new List<IrParameter>();
            if (funcDecl.parameterList() != null)
            {
                ParseRegularParameters(funcDecl.parameterList(), parameters);
            }

            // Compute mangled name if this function is overloaded
            var paramTypes = parameters.Select(p => p.Type).ToList();
            var mangledName = GetMangledFunctionName(funcName, paramTypes);

            // Skip if this function has already been imported (use mangled name for uniqueness)
            if (_module.Functions.Any(f => f.Name == mangledName))
            {
                continue;
            }

            var function = new IrFunction(mangledName, returnType, visibility, isExtern);

            // Store original name if mangled
            if (mangledName != funcName)
            {
                function.OriginalName = funcName;
            }

            // Parse and store function attributes (for @library, @test, @export, etc.)
            // This is CRITICAL for FFI functions that use @library("bsdsocket.library") etc.
            // Without this, the code generator won't know which library the function belongs to.
            var attributes = ProcessAndFilterModuleAttributes(funcDecl.attribute());
            function.Attributes = attributes;

            // Add already-parsed parameters
            function.Parameters.AddRange(parameters);

            // Add variadic parameter if present
            if (funcDecl.parameterList()?.variadicParameter() != null)
            {
                ParseVariadicParameter(funcDecl.parameterList(), function);
            }

            _module.AddFunction(function);
        }
    }

    /// <summary>
    /// Import all public functions and global variables from the specified module.
    /// Used during generic instantiation to ensure dependencies from the source module are available.
    /// This is called when a generic function template references functions/statics from its source module.
    /// </summary>
    internal void ImportModuleDependencies(string modulePath)
    {
        // Skip if already imported or is the current file
        if (modulePath == _inputFilePath)
        {
            return;
        }

        // Parse the source module
        var (moduleContext, syntaxErrors) = ModuleImportHelper.ParseModuleFile(modulePath, _preprocessorConstants);
        if (moduleContext == null || syntaxErrors > 0)
        {
            // Module not found or has errors - can't import dependencies
            return;
        }

        // Import all pub functions from the module (including private ones for internal dependencies)
        foreach (var funcDecl in moduleContext.functionDeclaration())
        {
            var funcName = funcDecl.IDENTIFIER().GetText();

            // Skip generic functions - they have their own templates
            var genericParams = AstParsingHelpers.ParseGenericParameters(funcDecl.genericParams(), _symbols, registerInSymbolTable: false);
            if (genericParams.Count > 0)
            {
                continue;
            }

            // Skip if already in module
            if (_module.GetFunction(funcName) != null)
            {
                continue;
            }

            // Try to parse function signature - skip if it fails (e.g., references unresolved generic types)
            try
            {
                var returnType = ParseReturnType(funcDecl.type());
                var (isPub, isExtern) = ModuleImportHelper.GetFunctionVisibility(funcDecl);

                // Import both pub and private functions - private ones are needed for internal dependencies
                var visibility = isPub ? Visibility.Public : Visibility.Private;
                var function = new IrFunction(funcName, returnType, visibility, isExtern);

                // Parse parameters
                if (funcDecl.parameterList() != null)
                {
                    var parameters = new List<IrParameter>();
                    ParseRegularParameters(funcDecl.parameterList(), parameters);
                    function.Parameters.AddRange(parameters);

                    if (funcDecl.parameterList().variadicParameter() != null)
                    {
                        ParseVariadicParameter(funcDecl.parameterList(), function);
                    }
                }

                // Parse attributes
                var attributes = ProcessAndFilterModuleAttributes(funcDecl.attribute());
                function.Attributes = attributes;

                _module.AddFunction(function);
            }
            catch
            {
                // Skip functions with unresolvable types (e.g., generic parameters from impl blocks)
                continue;
            }
        }

        // Import global variables (static vars)
        foreach (var globalVarDecl in moduleContext.globalVariableDeclaration())
        {
            var varName = globalVarDecl.IDENTIFIER().GetText();

            // Skip if already registered
            if (_module.ExternalVariables.Any(ev => ev.Name == varName))
            {
                continue;
            }

            // Register the external variable
            RegisterExternalVariable(globalVarDecl);
        }
    }
}
