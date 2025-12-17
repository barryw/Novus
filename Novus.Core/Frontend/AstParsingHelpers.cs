using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// Shared AST parsing utilities used by both SemanticAnalyzer and IrBuilder.
/// This eliminates code duplication for common parsing patterns.
/// </summary>
public static class AstParsingHelpers
{
    /// <summary>
    /// Parse generic parameters from context and optionally register them in the symbol table.
    /// This consolidates the repeated pattern found in both SemanticAnalyzer and IrBuilder.
    /// </summary>
    /// <param name="context">The generic parameters context from the parser</param>
    /// <param name="symbols">The symbol table to register parameters in (optional)</param>
    /// <param name="registerInSymbolTable">Whether to register parameters as generic types</param>
    /// <returns>List of generic parameter names</returns>
    public static List<string> ParseGenericParameters(
        NovusParser.GenericParamsContext? context,
        SymbolTable? symbols = null,
        bool registerInSymbolTable = false)
    {
        if (context == null)
            return new List<string>();

        var genericParams = new List<string>();
        foreach (var paramId in context.IDENTIFIER())
        {
            var paramName = paramId.GetText();
            genericParams.Add(paramName);

            if (registerInSymbolTable && symbols != null)
            {
                symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
            }
        }
        return genericParams;
    }

    /// <summary>
    /// Parse generic parameters and register them in a dictionary.
    /// Used by SemanticAnalyzer which maintains its own generic param scope.
    /// </summary>
    /// <param name="context">The generic parameters context from the parser</param>
    /// <param name="genericParamScope">Dictionary to register parameters in</param>
    /// <returns>List of generic parameter names</returns>
    public static List<string> ParseGenericParameters(
        NovusParser.GenericParamsContext? context,
        Dictionary<string, IrGenericType> genericParamScope)
    {
        if (context == null)
            return new List<string>();

        var genericParams = new List<string>();
        foreach (var paramId in context.IDENTIFIER())
        {
            var paramName = paramId.GetText();
            genericParams.Add(paramName);
            genericParamScope[paramName] = new IrGenericType(paramName);
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

        foreach (var paramId in context.IDENTIFIER())
        {
            genericParamScope.Remove(paramId.GetText());
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

            if (namedCtx.genericTypeArgs()?.typeList() != null)
            {
                foreach (var typeArg in namedCtx.genericTypeArgs().typeList().type())
                {
                    ExtractTypeNameDependenciesRecursive(typeArg, dependencies);
                }
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
