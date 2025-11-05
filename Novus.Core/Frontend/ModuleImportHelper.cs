using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Novus.Diagnostics;
using Novus.Parser;

namespace Novus.Frontend;

/// <summary>
/// Helper utilities for module import operations shared between IrBuilder and SemanticAnalyzer
/// Provides common functionality without requiring major refactoring of existing code
/// </summary>
public static class ModuleImportHelper
{
    /// <summary>
    /// Resolve a module namespace to a file path
    /// std::dos → std/dos.novus
    /// std::ffi::exec → std/ffi/exec.novus
    /// </summary>
    public static string ResolveModulePath(string moduleNamespace, string stdLibPath)
    {
        var pathParts = moduleNamespace.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);

        if (pathParts.Length == 0)
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
        else
        {
            // User module (future: will use package resolution)
            var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts);
            return relativePath + ".novus";
        }
    }

    /// <summary>
    /// Parse a module file and return the compilation unit context
    /// </summary>
    public static (NovusParser.CompilationUnitContext? Context, int SyntaxErrors) ParseModuleFile(string modulePath)
    {
        if (!File.Exists(modulePath))
        {
            return (null, 0);
        }

        var moduleSource = File.ReadAllText(modulePath);
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(
            moduleSource,
            diagnostics,
            modulePath,
            NovusParserFactory.ParseMode.Compilation
        );
        var moduleContext = parser.compilationUnit();

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
            foreach (var implItem in implDecl.implItem())
            {
                var funcDecl = implItem.functionDeclaration();
                if (funcDecl != null && IsPub(funcDecl))
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

        return namesToImport;
    }
}
