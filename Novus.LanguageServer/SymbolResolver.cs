using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.LanguageServer;

/// <summary>
/// Resolves symbols at specific positions in source code.
/// Used for language server features like go-to-definition, hover, find-references, etc.
/// </summary>
public class SymbolResolver
{
    private readonly SemanticAnalyzer _analyzer;
    private readonly IParseTree _parseTree;
    private readonly string _sourceText;

    public SymbolResolver(SemanticAnalyzer analyzer, IParseTree parseTree, string sourceText)
    {
        _analyzer = analyzer;
        _parseTree = parseTree;
        _sourceText = sourceText;
    }

    /// <summary>
    /// Finds the symbol at the given position (line and column are 0-based).
    /// </summary>
    /// <returns>A SymbolInfo if a symbol is found, or null otherwise.</returns>
    public SymbolInfo? FindSymbolAtPosition(int line, int column)
    {
        // Convert 0-based to 1-based for ANTLR (ANTLR uses 1-based line numbers)
        var antlrLine = line + 1;
        var antlrColumn = column;

        // Find the token at this position
        var token = FindTokenAtPosition(_parseTree, antlrLine, antlrColumn);
        if (token == null)
        {
            return null;
        }

        var tokenText = token.Text;

        // Try to resolve the symbol in order of precedence
        // 1. Check if it's a function call or function reference
        if (_analyzer.Functions.TryGetValue(tokenText, out var function))
        {
            return new SymbolInfo(
                SymbolKind.Function,
                tokenText,
                function.Location,
                function.ReturnType,
                function
            );
        }

        // 2. Check if it's a local variable
        if (_analyzer.Variables.TryGetValue(tokenText, out var variable))
        {
            return new SymbolInfo(
                SymbolKind.Variable,
                tokenText,
                variable.Location,
                variable.Type,
                variable
            );
        }

        // 3. Check if it's a global variable
        if (_analyzer.GlobalVariables.TryGetValue(tokenText, out var globalVar))
        {
            return new SymbolInfo(
                SymbolKind.GlobalVariable,
                tokenText,
                globalVar.Location,
                globalVar.Type,
                globalVar
            );
        }

        // 4. Check if it's a constant
        if (_analyzer.Constants.TryGetValue(tokenText, out var constant))
        {
            return new SymbolInfo(
                SymbolKind.Constant,
                tokenText,
                constant.Location,
                constant.Type,
                constant
            );
        }

        // 5. Check if it's a struct type
        if (_analyzer.Structs.TryGetValue(tokenText, out var structType))
        {
            // Get location from location dictionary
            if (_analyzer.StructLocations.TryGetValue(tokenText, out var structLocation))
            {
                return new SymbolInfo(
                    SymbolKind.Struct,
                    tokenText,
                    structLocation,
                    structType,
                    structType
                );
            }
        }

        // 6. Check if it's an enum type
        if (_analyzer.Enums.TryGetValue(tokenText, out var enumType))
        {
            // Get location from location dictionary
            if (_analyzer.EnumLocations.TryGetValue(tokenText, out var enumLocation))
            {
                return new SymbolInfo(
                    SymbolKind.Enum,
                    tokenText,
                    enumLocation,
                    enumType,
                    enumType
                );
            }
        }

        // 7. Check if it's a trait
        if (_analyzer.Traits.TryGetValue(tokenText, out var trait))
        {
            // Get location from location dictionary
            if (_analyzer.TraitLocations.TryGetValue(tokenText, out var traitLocation))
            {
                return new SymbolInfo(
                    SymbolKind.Trait,
                    tokenText,
                    traitLocation,
                    null,
                    trait
                );
            }
        }

        // Symbol not found
        return null;
    }

    /// <summary>
    /// Finds the ANTLR token at the given position (1-based line, 0-based column).
    /// </summary>
    private IToken? FindTokenAtPosition(IParseTree tree, int line, int column)
    {
        if (tree is TerminalNodeImpl terminal)
        {
            var token = terminal.Symbol;
            var tokenLine = token.Line;
            var tokenColumn = token.Column;
            var tokenLength = token.Text.Length;

            // Check if the position is within this token
            if (tokenLine == line && column >= tokenColumn && column < tokenColumn + tokenLength)
            {
                return token;
            }
        }

        // Recursively search children
        for (int i = 0; i < tree.ChildCount; i++)
        {
            var child = tree.GetChild(i);
            var result = FindTokenAtPosition(child, line, column);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}

/// <summary>
/// Represents information about a symbol found at a position.
/// </summary>
public record SymbolInfo(
    SymbolKind Kind,
    string Name,
    SourceLocation Location,
    IrType? Type,
    object Symbol
);

/// <summary>
/// The kind of symbol.
/// </summary>
public enum SymbolKind
{
    Function,
    Variable,
    GlobalVariable,
    Constant,
    Struct,
    Enum,
    Trait,
    Method,
    Field,
    EnumVariant,
    Parameter
}
