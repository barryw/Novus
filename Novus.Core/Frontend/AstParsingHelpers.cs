using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// Extension methods for parser contexts to support const generics.
/// </summary>
public static class ParserContextExtensions
{
    /// <summary>
    /// Get all generic param identifiers (handles both TypeGenericParam and ConstGenericParam).
    /// </summary>
    public static IEnumerable<string> GetAllParamNames(this NovusParser.GenericParamsContext? context)
    {
        if (context == null)
            yield break;

        foreach (var param in context.genericParam())
        {
            if (param is NovusParser.TypeGenericParamContext typeParam)
            {
                yield return typeParam.IDENTIFIER().GetText();
            }
            else if (param is NovusParser.ConstGenericParamContext constParam)
            {
                yield return constParam.IDENTIFIER().GetText();
            }
        }
    }

    /// <summary>
    /// Get type generic param names only.
    /// </summary>
    public static IEnumerable<string> GetTypeParamNames(this NovusParser.GenericParamsContext? context)
    {
        if (context == null)
            yield break;

        foreach (var param in context.genericParam())
        {
            if (param is NovusParser.TypeGenericParamContext typeParam)
            {
                yield return typeParam.IDENTIFIER().GetText();
            }
        }
    }
}

/// <summary>
/// Result of parsing a single generic argument (either type or const)
/// </summary>
public class GenericArgResult
{
    /// <summary>
    /// The type (for type args) or const value type (for const args)
    /// For const args, this will be IrConstGenericValue
    /// </summary>
    public IrType Type { get; set; } = null!;

    /// <summary>
    /// True if this is a const generic argument
    /// </summary>
    public bool IsConst { get; set; }

    /// <summary>
    /// The const value (for const args only)
    /// </summary>
    public object? ConstValue { get; set; }

    /// <summary>
    /// For const identifier references (like SIZE where SIZE is a const)
    /// </summary>
    public string? ConstIdentifier { get; set; }
}

/// <summary>
/// Result of parsing generic parameters, including both type and const parameters.
/// </summary>
public class GenericParametersResult
{
    /// <summary>
    /// Type generic parameter names (e.g., ["T", "E"])
    /// </summary>
    public List<string> TypeParameters { get; } = new();

    /// <summary>
    /// Const generic parameters: name -> type (e.g., {"N" -> u32})
    /// </summary>
    public Dictionary<string, IrType> ConstParameters { get; } = new();

    /// <summary>
    /// All parameter names in declaration order (for cache key generation)
    /// </summary>
    public List<string> AllParameterNames { get; } = new();

    /// <summary>
    /// Whether a parameter is const (by name)
    /// </summary>
    public Dictionary<string, bool> IsConstParameter { get; } = new();

    /// <summary>
    /// Get all type parameter names (for backwards compatibility)
    /// </summary>
    public List<string> GetTypeParameters() => TypeParameters;

    /// <summary>
    /// Check if there are any const parameters
    /// </summary>
    public bool HasConstParameters => ConstParameters.Count > 0;
}

/// <summary>
/// Shared AST parsing utilities used by both SemanticAnalyzer and IrBuilder.
/// This eliminates code duplication for common parsing patterns.
/// </summary>
public static class AstParsingHelpers
{
    /// <summary>
    /// Parse generic parameters from context (both type and const parameters).
    /// This is the new unified method that supports const generics.
    /// </summary>
    /// <param name="context">The generic parameters context from the parser</param>
    /// <param name="typeParser">Type parser for resolving const parameter types (optional)</param>
    /// <returns>GenericParametersResult containing both type and const parameters</returns>
    public static GenericParametersResult ParseGenericParametersEx(
        NovusParser.GenericParamsContext? context,
        Func<NovusParser.TypeContext, IrType>? typeParser = null)
    {
        var result = new GenericParametersResult();

        if (context == null)
            return result;

        foreach (var param in context.genericParam())
        {
            if (param is NovusParser.TypeGenericParamContext typeParam)
            {
                var paramName = typeParam.IDENTIFIER().GetText();
                result.TypeParameters.Add(paramName);
                result.AllParameterNames.Add(paramName);
                result.IsConstParameter[paramName] = false;
            }
            else if (param is NovusParser.ConstGenericParamContext constParam)
            {
                var paramName = constParam.IDENTIFIER().GetText();
                IrType constType = IrIntType.U32; // Default to u32 if no type parser

                if (typeParser != null && constParam.type() != null)
                {
                    constType = typeParser(constParam.type());
                }
                else if (constParam.type() != null)
                {
                    // Try to parse primitive type from text
                    var typeText = constParam.type().GetText();
                    constType = MapPrimitiveTypeName(typeText) ?? IrIntType.U32;
                }

                result.ConstParameters[paramName] = constType;
                result.AllParameterNames.Add(paramName);
                result.IsConstParameter[paramName] = true;
            }
        }

        return result;
    }

    /// <summary>
    /// Parse generic parameters from context and optionally register them in the symbol table.
    /// This consolidates the repeated pattern found in both SemanticAnalyzer and IrBuilder.
    /// </summary>
    /// <param name="context">The generic parameters context from the parser</param>
    /// <param name="symbols">The symbol table to register parameters in (optional)</param>
    /// <param name="registerInSymbolTable">Whether to register parameters as generic types</param>
    /// <returns>List of type generic parameter names (for backwards compatibility)</returns>
    public static List<string> ParseGenericParameters(
        NovusParser.GenericParamsContext? context,
        SymbolTable? symbols = null,
        bool registerInSymbolTable = false)
    {
        if (context == null)
            return new List<string>();

        var genericParams = new List<string>();

        foreach (var param in context.genericParam())
        {
            if (param is NovusParser.TypeGenericParamContext typeParam)
            {
                var paramName = typeParam.IDENTIFIER().GetText();
                genericParams.Add(paramName);

                if (registerInSymbolTable && symbols != null)
                {
                    symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
                }
            }
            else if (param is NovusParser.ConstGenericParamContext constParam)
            {
                // For now, we also add const parameters to the list for backwards compatibility
                // The name will be used for substitution mapping
                var paramName = constParam.IDENTIFIER().GetText();
                genericParams.Add(paramName);

                if (registerInSymbolTable && symbols != null)
                {
                    // Parse the const type from the context
                    var typeText = constParam.type().GetText();
                    var constType = MapPrimitiveTypeName(typeText) ?? IrIntType.U32;
                    symbols.RegisterConstGenericParameter(paramName, new IrConstGenericParam(paramName, constType));
                }
            }
        }

        return genericParams;
    }

    /// <summary>
    /// Parse generic parameters and register them in a dictionary.
    /// Used by SemanticAnalyzer which maintains its own generic param scope.
    /// </summary>
    /// <param name="context">The generic parameters context from the parser</param>
    /// <param name="genericParamScope">Dictionary to register type parameters in</param>
    /// <returns>List of type generic parameter names</returns>
    public static List<string> ParseGenericParameters(
        NovusParser.GenericParamsContext? context,
        Dictionary<string, IrGenericType> genericParamScope)
    {
        if (context == null)
            return new List<string>();

        var genericParams = new List<string>();

        foreach (var param in context.genericParam())
        {
            if (param is NovusParser.TypeGenericParamContext typeParam)
            {
                var paramName = typeParam.IDENTIFIER().GetText();
                genericParams.Add(paramName);
                genericParamScope[paramName] = new IrGenericType(paramName);
            }
            else if (param is NovusParser.ConstGenericParamContext constParam)
            {
                // Const generic parameters are handled separately
                // For backwards compatibility, we still add the name to the list
                var paramName = constParam.IDENTIFIER().GetText();
                genericParams.Add(paramName);
                // Note: We don't add to genericParamScope since it's a const, not a type
            }
        }

        return genericParams;
    }

    /// <summary>
    /// Clear generic parameters from a dictionary scope.
    /// </summary>
    public static void ClearGenericParameters(
        NovusParser.GenericParamsContext? context,
        Dictionary<string, IrGenericType> genericParamScope)
    {
        if (context == null)
            return;

        foreach (var param in context.genericParam())
        {
            if (param is NovusParser.TypeGenericParamContext typeParam)
            {
                genericParamScope.Remove(typeParam.IDENTIFIER().GetText());
            }
            // Note: Const params aren't in genericParamScope
        }
    }

    /// <summary>
    /// Parse generic parameters and register them, returning both the list and any where clause.
    /// </summary>
    public static (List<string> Params, IrWhereClause? WhereClause) ParseGenericParametersWithConstraints(
        NovusParser.GenericParamsContext? genericParams,
        NovusParser.WhereClauseContext? whereClause,
        SymbolTable? symbols = null,
        bool registerInSymbolTable = false)
    {
        var paramList = ParseGenericParameters(genericParams, symbols, registerInSymbolTable);
        var clause = whereClause != null ? ParseWhereClause(whereClause) : null;
        return (paramList, clause);
    }

    /// <summary>
    /// Parse a where clause from the AST into an IrWhereClause.
    /// </summary>
    public static IrWhereClause? ParseWhereClause(NovusParser.WhereClauseContext? context)
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
    /// Parse a trait bound (potentially with multiple traits separated by +).
    /// </summary>
    public static List<IrTraitBound> ParseTraitBound(NovusParser.TraitBoundContext context)
    {
        var bounds = new List<IrTraitBound>();

        if (context is NovusParser.SingleTraitBoundContext singleBound)
        {
            var traitName = singleBound.typeName().GetText();
            var typeArgs = new List<IrType>();

            // Type arguments would need to be resolved by the caller since they need type resolution context
            // For now, we just capture the trait name
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
    /// Extract modifier information (visibility, extern, mutable) from a parser context.
    /// Works with function declarations, struct declarations, etc.
    /// </summary>
    /// <param name="context">The parser context to examine</param>
    /// <param name="maxChildrenToCheck">Maximum number of children to check for modifiers</param>
    /// <returns>Tuple of (visibility, isExtern, isMutable, isConst)</returns>
    public static (Visibility Visibility, bool IsExtern, bool IsMutable, bool IsConst) ParseModifiers(
        Antlr4.Runtime.ParserRuleContext context,
        int maxChildrenToCheck = 4)
    {
        return AstModifierHelper.ParseModifiers(context, maxChildrenToCheck);
    }

    /// <summary>
    /// Parse an integer literal string into value and type (for const generics)
    /// </summary>
    public static (object Value, IrType Type) ParseIntegerLiteral(string text)
    {
        // Remove suffix if present
        var suffix = "";
        var numPart = text;

        if (text.EndsWith("u8")) { suffix = "u8"; numPart = text[..^2]; }
        else if (text.EndsWith("u16")) { suffix = "u16"; numPart = text[..^3]; }
        else if (text.EndsWith("u32")) { suffix = "u32"; numPart = text[..^3]; }
        else if (text.EndsWith("u64")) { suffix = "u64"; numPart = text[..^3]; }
        else if (text.EndsWith("i8")) { suffix = "i8"; numPart = text[..^2]; }
        else if (text.EndsWith("i16")) { suffix = "i16"; numPart = text[..^3]; }
        else if (text.EndsWith("i32")) { suffix = "i32"; numPart = text[..^3]; }
        else if (text.EndsWith("i64")) { suffix = "i64"; numPart = text[..^3]; }

        var value = long.Parse(numPart);
        IrType type = suffix switch
        {
            "u8" => IrIntType.U8,
            "u16" => IrIntType.U16,
            "u32" => IrIntType.U32,
            "u64" => IrIntType.U64,
            "i8" => IrIntType.I8,
            "i16" => IrIntType.I16,
            "i32" => IrIntType.I32,
            "i64" => IrIntType.I64,
            _ => IrIntType.U32 // Default to u32 for const generics
        };

        return (value, type);
    }

    /// <summary>
    /// Parse a hex literal string into value and type (for const generics)
    /// </summary>
    public static (object Value, IrType Type) ParseHexLiteral(string text)
    {
        // Remove 0x prefix
        var numPart = text.StartsWith("0x") || text.StartsWith("0X") ? text[2..] : text;

        // Remove suffix if present
        var suffix = "";
        if (numPart.EndsWith("u8")) { suffix = "u8"; numPart = numPart[..^2]; }
        else if (numPart.EndsWith("u16")) { suffix = "u16"; numPart = numPart[..^3]; }
        else if (numPart.EndsWith("u32")) { suffix = "u32"; numPart = numPart[..^3]; }
        else if (numPart.EndsWith("u64")) { suffix = "u64"; numPart = numPart[..^3]; }
        else if (numPart.EndsWith("i8")) { suffix = "i8"; numPart = numPart[..^2]; }
        else if (numPart.EndsWith("i16")) { suffix = "i16"; numPart = numPart[..^3]; }
        else if (numPart.EndsWith("i32")) { suffix = "i32"; numPart = numPart[..^3]; }
        else if (numPart.EndsWith("i64")) { suffix = "i64"; numPart = numPart[..^3]; }

        var value = Convert.ToInt64(numPart, 16);
        IrType type = suffix switch
        {
            "u8" => IrIntType.U8,
            "u16" => IrIntType.U16,
            "u32" => IrIntType.U32,
            "u64" => IrIntType.U64,
            "i8" => IrIntType.I8,
            "i16" => IrIntType.I16,
            "i32" => IrIntType.I32,
            "i64" => IrIntType.I64,
            _ => IrIntType.U32 // Default to u32 for const generics
        };

        return (value, type);
    }

    /// <summary>
    /// Check if a type name is a primitive type (not a struct/enum).
    /// </summary>
    public static bool IsPrimitiveTypeName(string typeName)
    {
        return typeName switch
        {
            "i8" or "i16" or "i32" or "i64" => true,
            "u8" or "u16" or "u32" or "u64" => true,
            "f32" or "f64" => true,
            "bool" or "void" or "char" => true,
            "isize" or "usize" => true,
            _ => false
        };
    }

    /// <summary>
    /// Map a primitive type name to its IrType representation.
    /// Returns null if the name is not a primitive type.
    /// </summary>
    public static IrType? MapPrimitiveTypeName(string typeName)
    {
        return typeName switch
        {
            "i8" => IrIntType.I8,
            "i16" => IrIntType.I16,
            "i32" => IrIntType.I32,
            "i64" => IrIntType.I64,
            "u8" => IrIntType.U8,
            "u16" => IrIntType.U16,
            "u32" => IrIntType.U32,
            "u64" => IrIntType.U64,
            "f32" => IrFloatType.F32,
            "f64" => IrFloatType.F64,
            "bool" => IrBoolType.Instance,
            "void" => IrVoidType.Instance,
            "char" => IrIntType.U8, // char is an alias for u8
            "isize" => IrIntType.I32, // 32-bit on 68k
            "usize" => IrIntType.U32, // 32-bit on 68k
            _ => null
        };
    }

    /// <summary>
    /// Extract type names from a type context (for dependency analysis).
    /// Returns all struct/enum type names referenced in the type expression.
    /// </summary>
    public static HashSet<string> ExtractTypeNameDependencies(NovusParser.TypeContext typeContext)
    {
        var dependencies = new HashSet<string>();
        ExtractTypeNameDependenciesRecursive(typeContext, dependencies);
        return dependencies;
    }

    private static void ExtractTypeNameDependenciesRecursive(
        NovusParser.TypeContext typeContext,
        HashSet<string> dependencies)
    {
        if (typeContext is NovusParser.PointerTypeContext ptrCtx)
        {
            ExtractTypeNameDependenciesRecursive(ptrCtx.type(), dependencies);
        }
        else if (typeContext is NovusParser.ReferenceTypeContext refCtx)
        {
            ExtractTypeNameDependenciesRecursive(refCtx.type(), dependencies);
        }
        else if (typeContext is NovusParser.ArrayTypeWithSizeContext arrayCtx)
        {
            ExtractTypeNameDependenciesRecursive(arrayCtx.type(), dependencies);
        }
        else if (typeContext is NovusParser.ArrayTypeInferredContext arrayInferredCtx)
        {
            ExtractTypeNameDependenciesRecursive(arrayInferredCtx.type(), dependencies);
        }
        else if (typeContext is NovusParser.NamedTypeContext namedCtx)
        {
            var typeName = namedCtx.typeName().GetText();
            if (!IsPrimitiveTypeName(typeName))
            {
                dependencies.Add(typeName);
            }

            // Get type contexts from generic args
            var typeArgs = namedCtx.genericTypeArgs()?.typeList()?.type() ?? [];
            foreach (var typeArg in typeArgs)
            {
                ExtractTypeNameDependenciesRecursive(typeArg, dependencies);
            }
        }
        else if (typeContext is NovusParser.FunctionPointerTypeContext fpCtx)
        {
            if (fpCtx.typeList() != null)
            {
                foreach (var paramType in fpCtx.typeList().type())
                {
                    ExtractTypeNameDependenciesRecursive(paramType, dependencies);
                }
            }
            if (fpCtx.type() != null)
            {
                ExtractTypeNameDependenciesRecursive(fpCtx.type(), dependencies);
            }
        }
    }
}
