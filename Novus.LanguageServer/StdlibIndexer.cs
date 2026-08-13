using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Parser;

namespace Novus.LanguageServer;

/// <summary>
/// Dynamically indexes the standard library to discover what symbols each module exports.
/// This replaces hardcoded module export lists with actual parsing of stdlib files.
/// </summary>
public class StdlibIndexer
{
    private readonly string _stdLibPath;
    private readonly Dictionary<string, HashSet<string>> _moduleExports = new();
    private bool _indexed = false;

    public StdlibIndexer(string stdLibPath)
    {
        _stdLibPath = stdLibPath;
    }

    /// <summary>
    /// Indexes all stdlib modules to discover their exports.
    /// This is called once at language server startup.
    /// </summary>
    public void IndexStdlib()
    {
        if (_indexed) return;

        Console.Error.WriteLine($"[LSP] Indexing stdlib at: {_stdLibPath}");

        try
        {
            IndexTree(_stdLibPath, "std", skipDirectory: "amiga");
            IndexTree(Path.Combine(_stdLibPath, "amiga"), "amiga");

            _indexed = true;
            Console.Error.WriteLine($"[LSP] Indexed {_moduleExports.Count} stdlib modules");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error indexing stdlib: {ex.Message}");
        }
    }

    /// <summary>
    /// Indexes all .novus files in a directory
    /// </summary>
    private void IndexTree(string directoryPath, string namespacePrefix, string? skipDirectory = null)
    {
        if (!Directory.Exists(directoryPath))
        {
            Console.Error.WriteLine($"[LSP] Directory not found: {directoryPath}");
            return;
        }

        foreach (var file in Directory.GetFiles(directoryPath, "*.novus"))
        {
            var moduleName = Path.GetFileNameWithoutExtension(file);
            var fullModuleName = $"{namespacePrefix}::{moduleName}";

            IndexModule(file, fullModuleName);
        }

        foreach (var child in Directory.GetDirectories(directoryPath))
        {
            var name = Path.GetFileName(child);
            if (name.StartsWith('.') || name is "bin" or "obj" || name == skipDirectory)
                continue;
            IndexTree(child, $"{namespacePrefix}::{name}");
        }
    }

    /// <summary>
    /// Parses a single module file and extracts its public exports
    /// </summary>
    private void IndexModule(string filePath, string moduleName)
    {
        try
        {
            var sourceCode = File.ReadAllText(filePath);
            var diagnostics = new DiagnosticBag();

            // Parse the module
            var parser = NovusParserFactory.CreateParser(
                sourceCode,
                diagnostics,
                filePath,
                NovusParserFactory.ParseMode.LanguageServer
            );

            var tree = parser.compilationUnit();

            // Extract public symbols
            var exports = new HashSet<string>();

            // Public functions
            foreach (var funcDecl in tree.functionDeclaration())
            {
                if (IsPublic(funcDecl))
                {
                    var funcName = funcDecl.IDENTIFIER().GetText();
                    exports.Add(funcName);
                }
            }

            // Public constants
            foreach (var constDecl in tree.constDeclaration())
            {
                if (IsPublic(constDecl))
                {
                    var constName = constDecl.IDENTIFIER().GetText();
                    exports.Add(constName);
                }
            }

            foreach (var aliasDecl in tree.typeAliasDeclaration())
            {
                if (aliasDecl.KW_PUB() != null)
                    exports.Add(aliasDecl.IDENTIFIER().GetText());
            }

            // Public enums
            foreach (var enumDecl in tree.enumDeclaration())
            {
                if (IsPublic(enumDecl))
                {
                    var enumName = enumDecl.IDENTIFIER().GetText();
                    exports.Add(enumName);

                    // Also add enum variants (Option::Some, Result::Ok, etc.)
                    foreach (var variant in enumDecl.enumVariant())
                    {
                        var variantName = variant.IDENTIFIER().GetText();
                        exports.Add(variantName);
                    }
                }
            }

            // Public structs
            foreach (var structDecl in tree.structDeclaration())
            {
                if (IsPublic(structDecl))
                {
                    var structName = structDecl.IDENTIFIER().GetText();
                    exports.Add(structName);
                }
            }

            // Public traits
            foreach (var traitDecl in tree.traitDeclaration())
            {
                if (IsPublic(traitDecl))
                {
                    var traitName = traitDecl.IDENTIFIER().GetText();
                    exports.Add(traitName);
                }
            }

            // Public statics
            foreach (var staticDecl in tree.staticDeclaration())
            {
                if (IsPublic(staticDecl))
                {
                    var staticName = staticDecl.IDENTIFIER().GetText();
                    exports.Add(staticName);
                }
            }

            if (exports.Count > 0)
            {
                _moduleExports[moduleName] = exports;
                Console.Error.WriteLine($"[LSP] Indexed {moduleName}: {exports.Count} exports");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error indexing module {moduleName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a function declaration has the 'pub' or 'extern pub' keyword
    /// </summary>
    private bool IsPublic(NovusParser.FunctionDeclarationContext context)
    {
        // Check first few children for 'pub' or 'extern' keyword
        for (int i = 0; i < Math.Min(3, context.ChildCount); i++)
        {
            var child = context.GetChild(i);
            if (child?.GetText() == "pub")
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a const declaration has the 'pub' keyword
    /// </summary>
    private bool IsPublic(NovusParser.ConstDeclarationContext context)
    {
        return context.GetChild(0)?.GetText() == "pub";
    }

    /// <summary>
    /// Checks if an enum declaration has the 'pub' keyword
    /// </summary>
    private bool IsPublic(NovusParser.EnumDeclarationContext context)
    {
        return context.GetChild(0)?.GetText() == "pub";
    }

    /// <summary>
    /// Checks if a struct declaration has the 'pub' keyword
    /// </summary>
    private bool IsPublic(NovusParser.StructDeclarationContext context)
    {
        return context.GetChild(0)?.GetText() == "pub";
    }

    /// <summary>
    /// Checks if a trait declaration has the 'pub' keyword
    /// </summary>
    private bool IsPublic(NovusParser.TraitDeclarationContext context)
    {
        return context.GetChild(0)?.GetText() == "pub";
    }

    /// <summary>
    /// Checks if a static declaration has the 'pub' keyword
    /// </summary>
    private bool IsPublic(NovusParser.StaticDeclarationContext context)
    {
        return context.GetChild(0)?.GetText() == "pub";
    }

    /// <summary>
    /// Finds all modules that export the given symbol
    /// </summary>
    public List<string> FindModulesExportingSymbol(string symbolName)
    {
        if (!_indexed)
        {
            IndexStdlib();
        }

        var modules = new List<string>();

        foreach (var (moduleName, exports) in _moduleExports)
        {
            if (exports.Contains(symbolName))
            {
                modules.Add(moduleName);
            }
        }

        return modules;
    }

    /// <summary>
    /// Gets all exports for a given module
    /// </summary>
    public HashSet<string>? GetModuleExports(string moduleName)
    {
        if (!_indexed)
        {
            IndexStdlib();
        }

        return _moduleExports.TryGetValue(moduleName, out var exports) ? exports : null;
    }

    /// <summary>
    /// Gets all indexed modules
    /// </summary>
    public IEnumerable<string> GetAllModules()
    {
        if (!_indexed)
        {
            IndexStdlib();
        }

        return _moduleExports.Keys;
    }
}
