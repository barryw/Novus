using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;

namespace Novus.Frontend;

/// <summary>
/// Callback interface for symbol registration during import resolution
/// Allows both IrBuilder and SemanticAnalyzer to handle symbol registration in their own way
/// </summary>
public interface IImportSymbolRegistry
{
    /// <summary>
    /// Register an enum declaration from an imported module
    /// </summary>
    void RegisterEnum(NovusParser.EnumDeclarationContext context);

    /// <summary>
    /// Register a struct declaration from an imported module
    /// </summary>
    void RegisterStruct(NovusParser.StructDeclarationContext context);

    /// <summary>
    /// Register a constant declaration from an imported module
    /// </summary>
    void RegisterConstant(NovusParser.ConstDeclarationContext context);

    /// <summary>
    /// Register a function declaration from an imported module
    /// </summary>
    void RegisterFunction(NovusParser.FunctionDeclarationContext context, string moduleNamespace, string modulePath);

    /// <summary>
    /// Register a global variable declaration from an imported module
    /// </summary>
    void RegisterGlobalVariable(NovusParser.GlobalVariableDeclarationContext context, string moduleNamespace, string modulePath);

    /// <summary>
    /// Register an impl block from an imported module
    /// This is complex and tightly coupled with the host's internal state, so we delegate it entirely
    /// </summary>
    void RegisterImpl(NovusParser.ImplDeclarationContext context);

    /// <summary>
    /// Check if a symbol has already been registered (to avoid duplicates)
    /// </summary>
    bool HasSymbol(string name, SymbolKind kind);

    /// <summary>
    /// Report an error during import resolution
    /// </summary>
    void ReportError(string errorCode, string message, string? helpText = null);

    /// <summary>
    /// Process the module's own imports (for transitive dependencies in generic templates)
    /// </summary>
    void ProcessImport(NovusParser.ImportDeclarationContext context);
}

/// <summary>
/// Type of symbol being checked
/// </summary>
public enum SymbolKind
{
    Enum,
    Struct,
    Constant,
    Function,
    GlobalVariable
}

/// <summary>
/// Result of parsing a module
/// </summary>
public class ParsedModule
{
    public NovusParser.CompilationUnitContext Context { get; }
    public string FilePath { get; }
    public bool HasImplementation { get; }

    public ParsedModule(NovusParser.CompilationUnitContext context, string filePath, bool hasImplementation)
    {
        Context = context;
        FilePath = filePath;
        HasImplementation = hasImplementation;
    }
}

/// <summary>
/// Shared import resolution logic for both IrBuilder and SemanticAnalyzer
/// Handles module path resolution, file parsing, circular import detection, and symbol registration
/// </summary>
public class ImportResolver
{
    private readonly IImportSymbolRegistry _registry;
    private readonly string _stdLibPath;
    private readonly HashSet<string> _processedModules = new();
    private readonly List<string> _importedModulePaths = new();

    public ImportResolver(IImportSymbolRegistry registry, string stdLibPath)
    {
        _registry = registry;
        _stdLibPath = stdLibPath;
    }

    /// <summary>
    /// Get list of imported module file paths (for linking)
    /// Only includes modules with implementations (not pure FFI modules)
    /// </summary>
    public List<string> GetImportedModulePaths()
    {
        return _importedModulePaths;
    }

    /// <summary>
    /// Process an import declaration from the AST
    /// </summary>
    public void ProcessImport(NovusParser.ImportDeclarationContext context)
    {
        // Get the module path (e.g., "std::dos" or "std::ffi::exec")
        var moduleNamespace = context.modulePath().GetText();

        // Get the list of names to import
        var importList = context.importList();
        bool importAll = importList.GetText() == "*";

        ImportModule(moduleNamespace, importAll, importList);
    }

    /// <summary>
    /// Import specific symbols from a module (used for pub use reexports)
    /// </summary>
    public void ImportModuleSpecificSymbols(string moduleNamespace, List<string> symbolNames)
    {
        foreach (var symbolName in symbolNames)
        {
            // Parse the module to get the symbols
            var modulePath = ResolveModulePath(moduleNamespace);

            if (!File.Exists(modulePath))
            {
                _registry.ReportError("E0026", $"Module '{moduleNamespace}' not found at {modulePath}");
                return;
            }

            var parsed = ParseModule(modulePath);
            if (parsed == null) return;

            var moduleContext = parsed.Context;

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
                    _registry.RegisterEnum(enumDecl);
                    return; // Found it
                }
            }

            // Check structs
            foreach (var structDecl in moduleContext.structDeclaration())
            {
                if (structDecl.IDENTIFIER().GetText() == symbolName)
                {
                    _registry.RegisterStruct(structDecl);
                    return; // Found it
                }
            }

            // Check constants
            foreach (var constDecl in moduleContext.constDeclaration())
            {
                if (constDecl.IDENTIFIER().GetText() == symbolName)
                {
                    _registry.RegisterConstant(constDecl);
                    return; // Found it
                }
            }
        }
    }

    /// <summary>
    /// Import a module by namespace (e.g., "std::core")
    /// </summary>
    public void ImportModule(string moduleNamespace, bool importAll, NovusParser.ImportListContext? importList = null)
    {
        // Skip if this module has already been processed (prevent circular imports)
        if (_processedModules.Contains(moduleNamespace))
        {
            return;
        }

        // Mark this module as being processed
        _processedModules.Add(moduleNamespace);

        // Convert namespace path to file path
        var modulePath = ResolveModulePath(moduleNamespace);

        if (!File.Exists(modulePath))
        {
            _registry.ReportError("E0026", $"Module '{moduleNamespace}' not found at {modulePath}");
            return;
        }

        // Load and parse the module
        var parsed = ParseModule(modulePath);
        if (parsed == null) return;

        var moduleContext = parsed.Context;

        // Track this module for compilation only if it has real implementations
        if (parsed.HasImplementation && !_importedModulePaths.Contains(modulePath))
        {
            _importedModulePaths.Add(modulePath);
        }

        // Process the module's imports to make constants available for generic templates
        // This is safe because _processedModules prevents circular dependencies
        foreach (var importDecl in moduleContext.importDeclaration())
        {
            _registry.ProcessImport(importDecl);
        }

        // CRITICAL: Process pub use reexports before parsing any function signatures
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
                    ImportModuleSpecificSymbols(reexportPath, symbolNames);
                }
            }
        }

        // Build the list of names to import
        var namesToImport = BuildImportList(moduleContext, importAll, importList);

        // Register imported symbols
        RegisterImportedSymbols(moduleContext, namesToImport, moduleNamespace, modulePath);

        // Handle impl blocks with FFI dependencies
        HandleImplBlockDependencies(moduleContext, namesToImport);
    }

    /// <summary>
    /// Resolve a module namespace to a file path
    /// std::dos → std/dos.novus
    /// std::ffi::exec → std/ffi/exec.novus
    /// </summary>
    private string ResolveModulePath(string moduleNamespace)
    {
        var pathParts = moduleNamespace.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);

        if (pathParts.Length == 0)
        {
            _registry.ReportError("E0026", $"Invalid module namespace: {moduleNamespace}");
            return "";
        }

        // Build file path
        if (pathParts[0] == "std")
        {
            // std library module - relative to std lib path
            var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Skip(1));
            return Path.Combine(_stdLibPath, relativePath + ".novus");
        }
        else
        {
            // User module (future: will use package resolution)
            var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts);
            return relativePath + ".novus";
        }
    }

    /// <summary>
    /// Parse a module file and check if it has implementation
    /// </summary>
    private ParsedModule? ParseModule(string modulePath)
    {
        var moduleSource = File.ReadAllText(modulePath);
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(
            moduleSource,
            diagnostics,
            modulePath,
            NovusParserFactory.ParseMode.Compilation
        );
        var moduleContext = parser.compilationUnit();

        if (diagnostics.HasErrors)
        {
            _registry.ReportError("E0027", $"Module has syntax errors at {modulePath}");
            return null;
        }

        // Check if this module has any pub (non-extern) functions that need compilation
        // FFI modules (only extern functions) don't need to be compiled separately
        bool hasImplementation = CheckHasImplementation(moduleContext);

        return new ParsedModule(moduleContext, modulePath, hasImplementation);
    }

    /// <summary>
    /// Check if a module has any implementation (non-extern pub functions or pub methods)
    /// </summary>
    private bool CheckHasImplementation(NovusParser.CompilationUnitContext context)
    {
        // Check for top-level functions
        foreach (var funcDecl in context.functionDeclaration())
        {
            var (isPub, isExtern) = AstModifierHelper.GetFunctionVisibility(funcDecl);

            // Module has implementation if it has pub functions that aren't extern
            if (isPub && !isExtern)
            {
                return true;
            }
        }

        // Check for impl declarations with public methods
        foreach (var implDecl in context.implDeclaration())
        {
            foreach (var implItem in implDecl.implItem())
            {
                var funcDecl = implItem.functionDeclaration();
                if (funcDecl != null && AstModifierHelper.IsPublic(funcDecl))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Build the list of symbol names to import based on import mode (all vs specific)
    /// </summary>
    private HashSet<string> BuildImportList(
        NovusParser.CompilationUnitContext context,
        bool importAll,
        NovusParser.ImportListContext? importList)
    {
        var namesToImport = new HashSet<string>();

        if (importAll)
        {
            // Import all pub enums
            foreach (var enumDecl in context.enumDeclaration())
            {
                if (IsPub(enumDecl))
                {
                    namesToImport.Add(enumDecl.IDENTIFIER().GetText());
                }
            }

            // Import all pub constants
            foreach (var constDecl in context.constDeclaration())
            {
                if (IsPub(constDecl))
                {
                    namesToImport.Add(constDecl.IDENTIFIER().GetText());
                }
            }

            // Import all pub structs
            foreach (var structDecl in context.structDeclaration())
            {
                if (IsPub(structDecl))
                {
                    namesToImport.Add(structDecl.IDENTIFIER().GetText());
                }
            }

            // Import all pub/extern functions
            foreach (var funcDecl in context.functionDeclaration())
            {
                var (isPub, isExtern) = AstModifierHelper.GetFunctionVisibility(funcDecl);

                if (isPub || isExtern)
                {
                    namesToImport.Add(funcDecl.IDENTIFIER().GetText());
                }
            }

            // Import all extern global variables
            foreach (var globalVarDecl in context.globalVariableDeclaration())
            {
                namesToImport.Add(globalVarDecl.IDENTIFIER().GetText());
            }
        }
        else if (importList != null)
        {
            // Import specific names
            foreach (var importNameCtx in importList.importName())
            {
                namesToImport.Add(importNameCtx.IDENTIFIER(0).GetText());
            }
        }

        return namesToImport;
    }

    /// <summary>
    /// Register imported symbols (enums, structs, constants, functions) from a module
    /// </summary>
    private void RegisterImportedSymbols(
        NovusParser.CompilationUnitContext context,
        HashSet<string> namesToImport,
        string moduleNamespace,
        string modulePath)
    {
        // Register imported enums
        foreach (var enumDecl in context.enumDeclaration())
        {
            var enumName = enumDecl.IDENTIFIER().GetText();
            if (namesToImport.Contains(enumName) && !_registry.HasSymbol(enumName, SymbolKind.Enum))
            {
                _registry.RegisterEnum(enumDecl);
            }
        }

        // Register imported constants
        foreach (var constDecl in context.constDeclaration())
        {
            var constName = constDecl.IDENTIFIER().GetText();
            if (namesToImport.Contains(constName) && !_registry.HasSymbol(constName, SymbolKind.Constant))
            {
                _registry.RegisterConstant(constDecl);
            }
        }

        // Register imported structs
        foreach (var structDecl in context.structDeclaration())
        {
            var structName = structDecl.IDENTIFIER().GetText();
            if (namesToImport.Contains(structName) && !_registry.HasSymbol(structName, SymbolKind.Struct))
            {
                _registry.RegisterStruct(structDecl);
            }
        }

        // Register imported functions
        foreach (var funcDecl in context.functionDeclaration())
        {
            var funcName = funcDecl.IDENTIFIER().GetText();
            if (namesToImport.Contains(funcName))
            {
                // Check if function is pub or extern
                var (isPub, isExtern) = AstModifierHelper.GetFunctionVisibility(funcDecl);

                if (!isPub && !isExtern)
                {
                    _registry.ReportError("E0028",
                        $"Cannot import private function '{funcName}' from module '{moduleNamespace}'",
                        "Only pub or extern functions can be imported");
                    continue;
                }

                if (!_registry.HasSymbol(funcName, SymbolKind.Function))
                {
                    _registry.RegisterFunction(funcDecl, moduleNamespace, modulePath);
                }
            }
        }

        // Register imported global variables
        foreach (var globalVarDecl in context.globalVariableDeclaration())
        {
            var varName = globalVarDecl.IDENTIFIER().GetText();
            if (namesToImport.Contains(varName) && !_registry.HasSymbol(varName, SymbolKind.GlobalVariable))
            {
                _registry.RegisterGlobalVariable(globalVarDecl, moduleNamespace, modulePath);
            }
        }

        // Register all impl blocks (methods are always imported with their types)
        foreach (var implDecl in context.implDeclaration())
        {
            _registry.RegisterImpl(implDecl);
        }
    }

    /// <summary>
    /// Handle transitive dependencies for impl blocks (import FFI functions they need)
    /// </summary>
    private void HandleImplBlockDependencies(NovusParser.CompilationUnitContext context, HashSet<string> namesToImport)
    {
        bool hasImplBlocks = context.implDeclaration().Length > 0;
        if (!hasImplBlocks) return;

        // Import extern functions directly declared in this module
        foreach (var funcDecl in context.functionDeclaration())
        {
            var funcName = funcDecl.IDENTIFIER().GetText();

            // Check if it's an extern function
            var (_, isExtern) = AstModifierHelper.GetFunctionVisibility(funcDecl);

            // Only import extern functions (FFI bindings)
            if (isExtern && !_registry.HasSymbol(funcName, SymbolKind.Function))
            {
                _registry.RegisterFunction(funcDecl, "", "");
            }
        }

        // Recursively import symbols from FFI modules that this module imports
        // Only import from std::ffi::* modules to avoid conflicts with wrapper functions
        foreach (var importDecl in context.importDeclaration())
        {
            var importPath = importDecl.modulePath().GetText();

            // Only transitively import from std::ffi::* modules (pure FFI bindings)
            if (!importPath.Contains("::ffi::"))
            {
                continue;
            }

            var importListCtx = importDecl.importList();

            // Import the specific symbols that this module imports
            if (importListCtx != null)
            {
                ImportModule(importPath, importAll: false, importList: importListCtx);
            }
        }
    }

    /// <summary>
    /// Check if a declaration has the 'pub' visibility modifier
    /// </summary>
    private bool IsPub(IParseTree context)
    {
        return AstModifierHelper.HasModifier(context, "pub", 3);
    }
}
