using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Novus.Diagnostics;
using Novus.Parser;
using Novus.Preprocessing;

namespace Novus.Frontend;

/// <summary>
/// Helper utilities for module import operations shared between IrBuilder and SemanticAnalyzer
/// Provides common functionality without requiring major refactoring of existing code
/// </summary>
public static class ModuleImportHelper
{
    private sealed record CachedModule(string ContentHash, NovusParser.CompilationUnitContext Context, int SyntaxErrors);
    private static readonly ConcurrentDictionary<string, CachedModule> ParseCache = new();

    /// <summary>
    /// Returns the stable prefix used for generated files and private Novus link symbols.
    /// Standard-library paths remain reproducible across installations; user modules include
    /// a short path hash so same-named files in different directories cannot collide.
    /// </summary>
    public static string GetGeneratedModulePrefix(string modulePath, string? fallback = null)
    {
        var normalized = modulePath.Replace('\\', '/');
        var stdMarker = normalized.LastIndexOf("/std/", StringComparison.Ordinal);
        if (stdMarker >= 0)
        {
            var relative = Path.ChangeExtension(normalized[(stdMarker + 5)..], null)!;
            return relative.Replace('/', '_');
        }

        fallback ??= Path.GetFileNameWithoutExtension(modulePath);
        var hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(Path.GetFullPath(modulePath))))[..8].ToLowerInvariant();
        return $"{fallback}_{hash}";
    }

    /// <summary>Returns the C-link symbol for a non-exported Novus function.</summary>
    public static string GetFunctionLinkName(string modulePath, string functionName) =>
        $"novus_mod_{GetGeneratedModulePrefix(modulePath)}_{functionName}";

    /// <summary>
    /// Resolve a module namespace to a file path
    /// amiga::dos → std/amiga/dos.novus
    /// amiga::raw::exec → std/amiga/raw/exec.novus
    /// </summary>
    public static string ResolveModulePath(
        string moduleNamespace, string stdLibPath, string? userModuleBasePath = null)
    {
        var pathParts = moduleNamespace.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);

        if (pathParts is [])
        {
            throw new ArgumentException($"Invalid module namespace: {moduleNamespace}");
        }

        // Build file path
        if (pathParts[0] == "std")
        {
            // std library module - relative to std lib path
            var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Skip(1));
            return Path.Combine(stdLibPath, relativePath + ".novus");
        }
        else if (pathParts[0] == "amiga")
        {
            if (pathParts.Length < 2)
                throw new ArgumentException($"Invalid Amiga module namespace: {moduleNamespace}");

            var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Skip(1));
            return Path.Combine(stdLibPath, "amiga", relativePath + ".novus");
        }
        else
        {
            // User module (future: will use package resolution)
            var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts);
            return Path.Combine(userModuleBasePath ?? "", relativePath + ".novus");
        }
    }

    /// <summary>
    /// Parse a module file and return the compilation unit context.
    /// If preprocessor constants are provided, the source is preprocessed before parsing.
    /// </summary>
    /// <param name="modulePath">Path to the module file</param>
    /// <param name="preprocessorConstants">Optional preprocessor constants for conditional compilation</param>
    public static (NovusParser.CompilationUnitContext? Context, int SyntaxErrors) ParseModuleFile(
        string modulePath,
        Dictionary<string, bool>? preprocessorConstants = null)
    {
        if (!File.Exists(modulePath))
        {
            return (null, 0);
        }

        var moduleSource = File.ReadAllText(modulePath);
        var diagnostics = new DiagnosticBag();

        // Run preprocessor if constants are provided
        if (preprocessorConstants != null)
        {
            var preprocessor = new Preprocessor(preprocessorConstants, diagnostics, modulePath);
            moduleSource = preprocessor.Process(moduleSource);

            // If preprocessor had errors, return early
            if (diagnostics.ErrorCount > 0)
            {
                return (null, diagnostics.ErrorCount);
            }
        }

        var fullPath = Path.GetFullPath(modulePath);
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(moduleSource)));
        if (ParseCache.TryGetValue(fullPath, out var cached) && cached.ContentHash == contentHash)
        {
            return (cached.Context, cached.SyntaxErrors);
        }

        var parser = NovusParserFactory.CreateParser(
            moduleSource,
            diagnostics,
            modulePath,
            NovusParserFactory.ParseMode.Compilation
        );
        var moduleContext = parser.compilationUnit();
        ParseCache[fullPath] = new CachedModule(contentHash, moduleContext, diagnostics.ErrorCount);

        return (moduleContext, diagnostics.ErrorCount);
    }

    /// <summary>
    /// Check if a declaration node has the 'pub' visibility modifier
    /// </summary>
    public static bool IsPub(IParseTree context)
    {
        return AstModifierHelper.HasModifier(context, "pub", 3);
    }

    /// <summary>
    /// Check if a function declaration has the 'extern' modifier
    /// </summary>
    public static bool IsExtern(NovusParser.FunctionDeclarationContext context)
    {
        return AstModifierHelper.IsExtern(context);
    }

    /// <summary>
    /// Check if a function declaration is pub or extern (importable)
    /// </summary>
    public static (bool IsPub, bool IsExtern) GetFunctionVisibility(NovusParser.FunctionDeclarationContext context)
    {
        return AstModifierHelper.GetFunctionVisibility(context);
    }

    /// <summary>
    /// Check if a module has any implementation (non-extern pub functions or pub methods)
    /// FFI modules (only extern functions) don't need to be compiled separately
    /// </summary>
    public static bool CheckHasImplementation(NovusParser.CompilationUnitContext context)
    {
        // Check for top-level functions
        foreach (var funcDecl in context.functionDeclaration())
        {
            var (isPub, isExtern) = GetFunctionVisibility(funcDecl);

            // Module has implementation if it has pub functions that aren't extern
            if (isPub && !isExtern)
            {
                return true;
            }
        }

        // Check for impl declarations with public methods
        foreach (var implDecl in context.implDeclaration())
        {
            // Trait implementations have implicitly public methods
            bool isTraitImpl = implDecl.KW_FOR() != null;

            foreach (var implItem in implDecl.implItem())
            {
                var funcDecl = implItem.functionDeclaration();
                if (funcDecl != null && (isTraitImpl || IsPub(funcDecl)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Build a set of symbol names to import based on import mode (all vs specific)
    /// </summary>
    public static HashSet<string> BuildImportNameSet(
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

            foreach (var aliasDecl in context.typeAliasDeclaration())
            {
                if (IsPub(aliasDecl))
                    namesToImport.Add(aliasDecl.IDENTIFIER().GetText());
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

            // Import all pub traits
            foreach (var traitDecl in context.traitDeclaration())
            {
                if (IsPub(traitDecl))
                {
                    namesToImport.Add(traitDecl.IDENTIFIER().GetText());
                }
            }

            // Import all pub/extern functions
            foreach (var funcDecl in context.functionDeclaration())
            {
                var (isPub, isExtern) = GetFunctionVisibility(funcDecl);
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
            // Import specific names (including wildcard patterns)
            foreach (var importNameCtx in importList.importName())
            {
                var wildcardCtx = importNameCtx.importWildcard();
                if (wildcardCtx != null)
                {
                    // Handle wildcard pattern
                    var identifierNode = wildcardCtx.IDENTIFIER();
                    if (wildcardCtx.GetChild(0).GetText() == "*")
                    {
                        // Suffix wildcard: *Mem
                        var suffix = identifierNode.GetText();

                        // Match all pub enums with this suffix
                        foreach (var enumDecl in context.enumDeclaration())
                        {
                            if (IsPub(enumDecl))
                            {
                                var name = enumDecl.IDENTIFIER().GetText();
                                if (name.EndsWith(suffix))
                                {
                                    namesToImport.Add(name);
                                }
                            }
                        }

                        foreach (var aliasDecl in context.typeAliasDeclaration())
                        {
                            var name = aliasDecl.IDENTIFIER().GetText();
                            if (IsPub(aliasDecl) && name.EndsWith(suffix))
                                namesToImport.Add(name);
                        }

                        // Match all pub constants with this suffix
                        foreach (var constDecl in context.constDeclaration())
                        {
                            if (IsPub(constDecl))
                            {
                                var name = constDecl.IDENTIFIER().GetText();
                                if (name.EndsWith(suffix))
                                {
                                    namesToImport.Add(name);
                                }
                            }
                        }

                        // Match all pub structs with this suffix
                        foreach (var structDecl in context.structDeclaration())
                        {
                            if (IsPub(structDecl))
                            {
                                var name = structDecl.IDENTIFIER().GetText();
                                if (name.EndsWith(suffix))
                                {
                                    namesToImport.Add(name);
                                }
                            }
                        }

                        // Match all pub traits with this suffix
                        foreach (var traitDecl in context.traitDeclaration())
                        {
                            if (IsPub(traitDecl))
                            {
                                var name = traitDecl.IDENTIFIER().GetText();
                                if (name.EndsWith(suffix))
                                {
                                    namesToImport.Add(name);
                                }
                            }
                        }

                        // Match all pub/extern functions with this suffix
                        foreach (var funcDecl in context.functionDeclaration())
                        {
                            var (isPub, isExtern) = GetFunctionVisibility(funcDecl);
                            if (isPub || isExtern)
                            {
                                var name = funcDecl.IDENTIFIER().GetText();
                                if (name.EndsWith(suffix))
                                {
                                    namesToImport.Add(name);
                                }
                            }
                        }

                        // Match all extern global variables with this suffix
                        foreach (var globalVarDecl in context.globalVariableDeclaration())
                        {
                            var name = globalVarDecl.IDENTIFIER().GetText();
                            if (name.EndsWith(suffix))
                            {
                                namesToImport.Add(name);
                            }
                        }
                    }
                    else
                    {
                        // Prefix wildcard: MEMF_*
                        var prefix = identifierNode.GetText();

                        // Match all pub enums with this prefix
                        foreach (var enumDecl in context.enumDeclaration())
                        {
                            if (IsPub(enumDecl))
                            {
                                var name = enumDecl.IDENTIFIER().GetText();
                                if (name.StartsWith(prefix))
                                {
                                    namesToImport.Add(name);
                                }
                            }
                        }

                        foreach (var aliasDecl in context.typeAliasDeclaration())
                        {
                            var name = aliasDecl.IDENTIFIER().GetText();
                            if (IsPub(aliasDecl) && name.StartsWith(prefix))
                                namesToImport.Add(name);
                        }

                        // Match all pub constants with this prefix
                        foreach (var constDecl in context.constDeclaration())
                        {
                            if (IsPub(constDecl))
                            {
                                var name = constDecl.IDENTIFIER().GetText();
                                if (name.StartsWith(prefix))
                                {
                                    namesToImport.Add(name);
                                }
                            }
                        }

                        // Match all pub structs with this prefix
                        foreach (var structDecl in context.structDeclaration())
                        {
                            if (IsPub(structDecl))
                            {
                                var name = structDecl.IDENTIFIER().GetText();
                                if (name.StartsWith(prefix))
                                {
                                    namesToImport.Add(name);
                                }
                            }
                        }

                        // Match all pub traits with this prefix
                        foreach (var traitDecl in context.traitDeclaration())
                        {
                            if (IsPub(traitDecl))
                            {
                                var name = traitDecl.IDENTIFIER().GetText();
                                if (name.StartsWith(prefix))
                                {
                                    namesToImport.Add(name);
                                }
                            }
                        }

                        // Match all pub/extern functions with this prefix
                        foreach (var funcDecl in context.functionDeclaration())
                        {
                            var (isPub, isExtern) = GetFunctionVisibility(funcDecl);
                            if (isPub || isExtern)
                            {
                                var name = funcDecl.IDENTIFIER().GetText();
                                if (name.StartsWith(prefix))
                                {
                                    namesToImport.Add(name);
                                }
                            }
                        }

                        // Match all extern global variables with this prefix
                        foreach (var globalVarDecl in context.globalVariableDeclaration())
                        {
                            var name = globalVarDecl.IDENTIFIER().GetText();
                            if (name.StartsWith(prefix))
                            {
                                namesToImport.Add(name);
                            }
                        }
                    }
                }
                else
                {
                    // Regular identifier import
                    namesToImport.Add(importNameCtx.IDENTIFIER(0).GetText());
                }
            }
        }

        AddConstantDependencies(context, namesToImport);
        return namesToImport;
    }

    private static void AddConstantDependencies(
        NovusParser.CompilationUnitContext module,
        HashSet<string> names)
    {
        var constants = module.constDeclaration()
            .ToDictionary(declaration => declaration.IDENTIFIER().GetText());
        var pending = new Queue<string>(names.Where(constants.ContainsKey));

        while (pending.TryDequeue(out var name))
        {
            var identifiers = new HashSet<string>();
            CollectIdentifiers(constants[name].expression(), identifiers);
            foreach (var dependency in identifiers.Where(constants.ContainsKey))
            {
                if (names.Add(dependency))
                    pending.Enqueue(dependency);
            }
        }
    }

    private static void CollectIdentifiers(IParseTree tree, HashSet<string> identifiers)
    {
        if (tree is ITerminalNode { Symbol.Type: NovusParser.IDENTIFIER } terminal)
        {
            identifiers.Add(terminal.GetText());
            return;
        }

        for (var index = 0; index < tree.ChildCount; index++)
            CollectIdentifiers(tree.GetChild(index), identifiers);
    }

    internal static bool CanImportWithoutDependencies(
        NovusParser.CompilationUnitContext module,
        HashSet<string> namesToImport,
        bool importAll)
    {
        if (importAll || namesToImport.Count == 0 ||
            module.structDeclaration().Length > 0 ||
            module.enumDeclaration().Length > 0 ||
            module.typeAliasDeclaration().Length > 0 ||
            module.implDeclaration().Length > 0 ||
            module.reexportDeclaration().Length > 0)
        {
            return false;
        }

        var matchedNames = new HashSet<string>();
        foreach (var function in module.functionDeclaration())
        {
            var name = function.IDENTIFIER().GetText();
            if (!namesToImport.Contains(name))
                continue;

            matchedNames.Add(name);
            if (function.attribute().Length > 0 ||
                function.genericParams() != null || function.whereClause() != null ||
                ContainsIdentifier(function.type()) ||
                function.parameterList()?.parameter().Any(parameter =>
                    parameter.type() == null || ContainsIdentifier(parameter.type())) == true)
            {
                return false;
            }
        }

        return matchedNames.SetEquals(namesToImport);
    }

    private static bool ContainsIdentifier(IParseTree? tree)
    {
        if (tree is ITerminalNode terminal)
            return terminal.Symbol.Type == NovusParser.IDENTIFIER;

        if (tree == null)
            return false;

        for (var index = 0; index < tree.ChildCount; index++)
        {
            if (ContainsIdentifier(tree.GetChild(index)))
                return true;
        }

        return false;
    }
}
